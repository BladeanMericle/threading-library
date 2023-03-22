namespace Mericle.Threading;

using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// コールバックを順番に処理し続けるワーカーを表します。
/// </summary>
public class CallbackWorker : ICallbackWorker
{
    /// <summary>
    /// コールバックのキュー。
    /// </summary>
    private readonly BlockingCollection<WorkItem> _callbackQueue = new ();

    /// <summary>
    /// キャンセルトークンを作成するオブジェクト。
    /// </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new ();

    /// <summary>
    /// 処理の完了を待機するためのオブジェクト。
    /// </summary>
    private readonly ManualResetEventSlim _completedWaitHandle = new (true);

    /// <summary>
    /// 最大再帰カウント。
    /// </summary>
    private readonly int _maxRecursionCount;

    /// <summary>
    /// 即座に停止するかどうか。
    /// </summary>
    private readonly bool _isStopImmediately;

    /// <summary>
    /// スレッド ID。
    /// </summary>
    private long _managedThreadId;

    /// <summary>
    /// 再帰カウント。
    /// </summary>
    private int _recursionCount;

    /// <summary>
    /// リソースが解放されたかどうか。
    /// </summary>
    private long _isDisposed;

    /// <summary>
    /// 標準の構成で新しいインスタンスを初期化します。
    /// </summary>
    public CallbackWorker()
        : this(new CallbackWorkerConfig())
    {
    }

    /// <summary>
    /// 指定した構成で新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">構成。</param>
    public CallbackWorker(ICallbackWorkerConfig config)
    {
        config ??= new CallbackWorkerConfig();

        _maxRecursionCount = config.MaxRecursionCount;
        _isStopImmediately = config.IsStopImmediately;
    }

    /// <summary>
    /// 処理の開始を通知します。
    /// </summary>
    public event EventHandler Started = (sender, e) => { };

    /// <summary>
    /// 処理の終了を通知します。
    /// </summary>
    public event EventHandler Ended = (sender, e) => { };

    /// <summary>
    /// 処理が実行中かどうかを取得します。
    /// </summary>
    /// <value>処理が実行中の場合は <see langword="true"/>、それ以外の場合は <see langword="false"/>。</value>
    public bool IsRunning
    {
        get
        {
            return Interlocked.Read(ref _managedThreadId) != 0;
        }
    }

    /// <summary>
    /// スレッド ID を取得します。
    /// </summary>
    /// <value>スレッド ID。</value>
    protected int ManagedThreadId
    {
        get
        {
            return (int)Interlocked.Read(ref _managedThreadId);
        }
    }

    /// <summary>
    /// キャンセルトークンを取得します。
    /// </summary>
    /// <value>キャンセルトークン。</value>
    protected CancellationToken Token
    {
        get
        {
            return _cancellationTokenSource.Token;
        }
    }

    /// <summary>
    /// コールバックメソッドを同期的に実行します。
    /// </summary>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    /// <exception cref="InvalidOperationException">まだ処理が開始されていません。</exception>
    /// <exception cref="StackOverflowException">コールバックメソッドを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックメソッドで例外が発生しました。</exception>
    public virtual void Invoke(WorkCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        long currentThreadId = ManagedThreadId;
        if (currentThreadId == 0)
        {
            throw new InvalidOperationException("Worker is not running yet.");
        }

        if (currentThreadId == Environment.CurrentManagedThreadId)
        {
            InvokeFromSameThread(callback, state);
        }
        else
        {
            InvokeFromDifferentThread(callback, state);
        }
    }

    /// <summary>
    /// コールバックメソッドを非同期に実行します。
    /// </summary>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/>が<see langword="null"/>です。</exception>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    public virtual void InvokeAsync(WorkCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        // タイミング悪くすり抜けてきても、ObjectDisposedException または処理が実行されずすり抜けるだけなはず。
        try
        {
            _callbackQueue.Add(new WorkItem(callback, state));
        }
        catch (InvalidOperationException ex)
        {
            throw new ObjectDisposedException("Worker is already disposed.", ex);
        }
    }

    /// <summary>
    /// 処理を開始し、コールバックメソッドを実行し続けます。
    /// このメソッドはコールバックメソッドで例外が発生するか、インスタンスが破棄されるまで処理を継続します。
    /// </summary>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    /// <exception cref="InvalidOperationException">すでに開始されています。</exception>
    /// <exception cref="Exception">コールバックの実行中に例外が発生しました。</exception>
    public virtual void Run()
    {
        ThrowIfDisposed();

        // Run が 1 度だけしか実行されないようにしています。
        if (Interlocked.CompareExchange(ref _managedThreadId, Environment.CurrentManagedThreadId, 0) != 0)
        {
            throw new InvalidOperationException("Worker is already running.");
        }

        try
        {
            _completedWaitHandle.Reset();

            // 処理の開始を通知します。
            Started(this, EventArgs.Empty);

            // キューの処理を実行し続けます。
            // 例外はこのメソッドの呼び出し元へそのまま投げられます。
            CancellationToken token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                WorkItem callbackQueueItem;
                try
                {
                    callbackQueueItem = _callbackQueue.Take(token);
                }
                catch (OperationCanceledException)
                {
                    // キャンセルは正常な動作なので無視します。
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // タイミングによっては Dispose の後に処理されることがあるので無視します。
                    break;
                }

                callbackQueueItem.Callback(callbackQueueItem.State);
            }

            // 残りの処理を実行します。
            InvokeRemainCallback();

            // 処理の終了を通知します。
            Ended(this, EventArgs.Empty);
        }
        finally
        {
            _completedWaitHandle.Set();
        }
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    public void Dispose()
    {
        DisposeCore(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// リソースを非同期に解放します。
    /// </summary>
    /// <returns>非同期の解放操作を表すタスク。</returns>
    public ValueTask DisposeAsync()
    {
        return new ValueTask(DisposeAsyncCore().ContinueWith(_ =>
        {
            DisposeCore(true);
            GC.SuppressFinalize(this);
        }));
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    /// <param name="disposing">
    /// マネージドリソースとアンマネージドリソースを解放する場合は<see langword="true"/>、
    /// アンマネージドリソースのみ解放する場合は<see langword="false"/>。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>
    /// リソースを非同期に解放する主要の処理です。
    /// </summary>
    /// <returns>非同期の解放操作を表すタスク。</returns>
    protected virtual Task DisposeAsyncCore()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 現在のインスタンスが既に破棄されているなら例外を投げます。
    /// </summary>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    protected void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _isDisposed) != 0)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    /// <summary>
    /// 同じスレッドでコールバックメソッドを同期的に実行します。
    /// </summary>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="StackOverflowException">コールバックメソッドを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    private void InvokeFromSameThread(WorkCallback callback, object? state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(Interlocked.Read(ref _managedThreadId) == Environment.CurrentManagedThreadId, "Interlocked.Read(ref _managedThreadId) == Environment.CurrentManagedThreadId");

        if (_recursionCount >= _maxRecursionCount)
        {
            throw new StackOverflowException("Exceeded max recursion count.");
        }

        try
        {
            ++_recursionCount;
            callback(state);
        }
        finally
        {
            --_recursionCount;
        }
    }

    /// <summary>
    /// 異なるスレッドでコールバックメソッドを同期的に実行します。
    /// </summary>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    private void InvokeFromDifferentThread(WorkCallback callback, object? state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(Interlocked.Read(ref _managedThreadId) != Environment.CurrentManagedThreadId, "Interlocked.Read(ref _managedThreadId) != Environment.CurrentManagedThreadId");

        using (ManualResetEventSlim invokeWaitHandle = new (false))
        using (_cancellationTokenSource.Token.Register(invokeWaitHandle.Set))
        {
            try
            {
                _callbackQueue.Add(new WorkItem(
                    s =>
                    {
                        try
                        {
                            callback(s);
                        }
                        finally
                        {
                            invokeWaitHandle.Set();
                        }
                    },
                    state));
                invokeWaitHandle.Wait();
            }
            catch (InvalidOperationException ex)
            {
                throw new ObjectDisposedException("Worker is already disposed.", ex);
            }
        }
    }

    /// <summary>
    /// 残りのコールバックを実行します。
    /// </summary>
    /// <exception cref="Exception">コールバックの実行中に例外が発生しました。</exception>
    private void InvokeRemainCallback()
    {
        if (_isStopImmediately)
        {
            return;
        }

        foreach (WorkItem remainingItem in _callbackQueue)
        {
            Debug.Assert(remainingItem != null, "remainingItem != null");

            remainingItem.Callback(remainingItem.State);
        }
    }

    /// <summary>
    /// リソースを解放する主要な処理です。
    /// </summary>
    /// <param name="disposing">
    /// マネージドリソースとアンマネージドリソースを解放する場合は<see langword="true"/>、
    /// アンマネージドリソースのみ解放する場合は<see langword="false"/>。
    /// </param>
    private void DisposeCore(bool disposing)
    {
        // Dispose が 1 度だけしか実行されないようにしています。
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            Dispose(disposing);
        }
        finally
        {
            DisposeInternal(disposing);
        }
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    /// <param name="disposing">
    /// マネージドリソースとアンマネージドリソースを解放する場合は <see langword="true"/>、
    /// アンマネージドリソースのみ解放する場合は <see langword="false"/>。
    /// </param>
    /// <exception cref="Exception">コールバックの実行中に例外が発生しました。</exception>
    private void DisposeInternal(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        try
        {
            _callbackQueue.CompleteAdding();

            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (AggregateException)
            {
                // こちらの例外については、Dispose からは例外を投げない原則に基づき無視します。
            }

            if (ManagedThreadId == Environment.CurrentManagedThreadId)
            {
                // Dispose は例外を投げないという原則に違反してしまいますが、実行スレッドで Dispose が呼ばれた場合は例外を投げる可能性があります。
                // 途中で例外がキャッチされていなければ、最終的に Run の例外として投げられるので自然な処理となるからです。
                InvokeRemainCallback();
            }
            else
            {
                // Run の終了を待機します。
                _completedWaitHandle.Wait();
            }
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _callbackQueue.Dispose();
            _completedWaitHandle.Dispose();
        }
    }
}

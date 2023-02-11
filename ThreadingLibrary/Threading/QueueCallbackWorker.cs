namespace Mericle.Threading;

using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// コールバックをキューに入れ、順番に処理し続ける機能を表します。
/// </summary>
public class QueueCallbackWorker : IQueueCallbackWorker
{
    /// <summary>
    /// コールバックのキュー。
    /// </summary>
    private readonly BlockingCollection<QueueCallbackWorkerItem> _callbackQueue = new ();

    /// <summary>
    /// キャンセルトークンを作成するオブジェクト。
    /// </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new ();

    /// <summary>
    /// 処理の完了を待機するためのオブジェクト。
    /// </summary>
    private readonly ManualResetEventSlim _completedWaitHandle = new (false);

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
    public QueueCallbackWorker()
        : this(new QueueCallbackWorkerConfig())
    {
    }

    /// <summary>
    /// 指定した構成で新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">構成。</param>
    public QueueCallbackWorker(IQueueCallbackWorkerConfig config)
    {
        config ??= new QueueCallbackWorkerConfig();

        _maxRecursionCount = config.MaxRecursionCount;
        _isStopImmediately = config.IsStopImmediately;
    }

    /// <summary>
    /// 処理の開始を通知します。
    /// </summary>
    public event EventHandler Started = (sender, e) => { };

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
    /// コールバックメソッドを同期的に実行します。
    /// </summary>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    /// <exception cref="InvalidOperationException">まだ処理が開始されていません。</exception>
    /// <exception cref="StackOverflowException">コールバックメソッドを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックメソッドで例外が発生しました。</exception>
    public void Invoke(QueueCallback callback, object state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        long currentThreadId = Interlocked.Read(ref _managedThreadId);
        if (currentThreadId == 0)
        {
            throw new InvalidOperationException("The instance is not running yet.");
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
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> が <see langword="null"/> です。</exception>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    public void InvokeAsync(QueueCallback callback, object state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        // タイミング悪くすり抜けてきても、ObjectDisposedException または処理が実行されずすり抜けるだけなはず。
        try
        {
            _callbackQueue.Add(new QueueCallbackWorkerItem(callback, state));
        }
        catch (InvalidOperationException ex)
        {
            throw new ObjectDisposedException("Cannot add callback queue, because the instance is already disposed.", ex);
        }
    }

    /// <summary>
    /// 処理を開始し、コールバックメソッドを実行し続けます。
    /// このメソッドはコールバックメソッドで例外が発生するか、インスタンスが破棄されるまで処理を継続します。
    /// </summary>
    /// <exception cref="ObjectDisposedException">現在のインスタンスは既に破棄されています。</exception>
    /// <exception cref="InvalidOperationException">すでに開始されています。</exception>
    /// <exception cref="Exception">処理中に例外が発生しました。</exception>
    public void Run()
    {
        ThrowIfDisposed();

        // Run が 1 度だけしか実行されないようにしています。
        if (Interlocked.CompareExchange(ref _managedThreadId, Environment.CurrentManagedThreadId, 0) != 0)
        {
            throw new InvalidOperationException("The instance is already running.");
        }

        try
        {
            _completedWaitHandle.Reset();

            // 処理の開始を通知します。
            Started(this, EventArgs.Empty);

            // キューの処理を実行し続けます。
            // 例外はこのメソッドの呼び出し元へそのまま投げられます。
            while (true)
            {
                QueueCallbackWorkerItem callbackQueueItem;
                try
                {
                    callbackQueueItem = _callbackQueue.Take(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // 即座に停止しない場合は、キューの残りを処理します。
                    if (!_isStopImmediately)
                    {
                        foreach (QueueCallbackWorkerItem remainingItem in _callbackQueue)
                        {
                            remainingItem.Callback(remainingItem.State);
                        }
                    }

                    // キャンセルは正常な動作なので無視します。
                    return;
                }
                catch (ObjectDisposedException)
                {
                    // タイミングによっては Dispose の後に処理されることがあるので無視します。
                    return;
                }

                callbackQueueItem.Callback(callbackQueueItem.State);
            }
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// リソースを非同期に解放します。
    /// </summary>
    /// <returns>非同期の解放操作を表すタスク。</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    /// <param name="disposing">
    /// マネージドリソースとアンマネージドリソースを解放する場合は <see langword="true"/>、
    /// アンマネージドリソースのみ解放する場合は <see langword="false"/>。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        DisposeCore();
    }

    /// <summary>
    /// リソースを非同期に解放する主要の処理です。
    /// </summary>
    /// <returns>非同期の解放操作を表すタスク。</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        await Task.Yield();
        DisposeCore();
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
    private void InvokeFromSameThread(QueueCallback callback, object state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(_managedThreadId == Environment.CurrentManagedThreadId, "managedThreadId == Environment.CurrentManagedThreadId");

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
    private void InvokeFromDifferentThread(QueueCallback callback, object state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(_managedThreadId != Environment.CurrentManagedThreadId, "managedThreadId != Environment.CurrentManagedThreadId");

        using WaitHandle tokenWaitHandle = _cancellationTokenSource.Token.WaitHandle;
        using ManualResetEventSlim invokeWaitHandle = new (false);
        try
        {
            _callbackQueue.Add(new QueueCallbackWorkerItem(
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
            WaitHandle.WaitAny(new[] { tokenWaitHandle, invokeWaitHandle.WaitHandle });
        }
        catch (InvalidOperationException ex)
        {
            throw new ObjectDisposedException("Cannot add callback queue, because the instance is already disposed.", ex);
        }
    }

    /// <summary>
    /// リソースを解放する主要な処理です。
    /// </summary>
    private void DisposeCore()
    {
        // Dispose が 1 度だけしか実行されないようにしています。
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _callbackQueue.CompleteAdding();
        _cancellationTokenSource.Cancel();
        _completedWaitHandle.Wait(); // Cancel が通知されるのを待機しています。

        _cancellationTokenSource.Dispose();
        _callbackQueue.Dispose();
        _completedWaitHandle.Dispose();
    }
}

namespace Mericle.Threading;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

/// <summary>
/// コールバックを順番に呼び出し続けるワーカーを表します。
/// </summary>
public class InvokableWorker : IInvokable, IWorkable
{
    /// <summary>
    /// 最大再帰カウントの初期値。
    /// </summary>
    public static readonly int DefaultMaxRecursionCount = 64;

    /// <summary>
    /// 停止中を表す数値。
    /// </summary>
    private const long Stopping = 1;

    /// <summary>
    /// 中断中を表す数値。
    /// </summary>
    private const long Aborting = 2;

    /// <summary>
    /// コールバックのキュー。
    /// </summary>
    private readonly BlockingCollection<Action> _callbackQueue = new ();

    /// <summary>
    /// キャンセルトークンを作成するオブジェクト。
    /// </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new ();

    /// <summary>
    /// 最大再帰カウント。
    /// </summary>
    private readonly int _maxRecursionCount;

    /// <summary>
    /// スレッド ID。
    /// </summary>
    private long _managedThreadId;

    /// <summary>
    /// 停止モード。
    /// </summary>
    private long _stopMode;

    /// <summary>
    /// 再帰カウント。
    /// </summary>
    private int _recursionCount;

    /// <summary>
    /// 標準の構成で新しいインスタンスを初期化します。
    /// </summary>
    public InvokableWorker()
        : this(DefaultMaxRecursionCount)
    {
    }

    /// <summary>
    /// 指定した構成で新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="maxRecursionCount">最大再帰カウント。</param>
    public InvokableWorker(int maxRecursionCount)
    {
        _maxRecursionCount = maxRecursionCount;
    }

    /// <summary>
    /// 実行する直前に発生するイベントです。
    /// </summary>
    public event EventHandler Running = (sender, e) => { };

    /// <summary>
    /// 実行した直後に発生するイベントです。
    /// </summary>
    public event EventHandler Ran = (sender, e) => { };

    /// <summary>
    /// 実行中かどうかを取得します。
    /// </summary>
    /// <value>実行中の場合は <see langword="true"/>、それ以外は <see langword="false"/>。</value>
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
    /// 実行します。
    /// </summary>
    /// <exception cref="InvalidOperationException">すでに開始されています。</exception>
    /// <exception cref="Exception">コールバックの実行中に例外が発生しました。</exception>
    public virtual void Run()
    {
        // Run が 1 度だけしか実行されないようにしています。
        if (Interlocked.CompareExchange(ref _managedThreadId, Environment.CurrentManagedThreadId, 0) != 0)
        {
            throw new InvalidOperationException("Worker is already running.");
        }

        Exception? error = null;
        try
        {
            Running(this, EventArgs.Empty);

            CancellationToken token;
            try
            {
                token = _cancellationTokenSource.Token;
            }
            catch (ObjectDisposedException)
            {
                // トークンが取得できなかったら、すでに停止されているので何もせず終了します。
                return;
            }

            // キューの処理を実行し続けます。
            // 例外はこのメソッドの呼び出し元へそのまま投げられます。
            while (!token.IsCancellationRequested)
            {
                Action callback;
                try
                {
                    callback = _callbackQueue.Take(token);

                    Debug.Assert(callback != null, "callback != null");
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

                callback();
            }

            // 残りの処理を実行します。
            if (Interlocked.Read(ref _stopMode) == Stopping)
            {
                foreach (Action callback in _callbackQueue)
                {
                    Debug.Assert(callback != null, "callback != null");

                    callback();
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            InvokeRan(error);
        }
    }

    /// <summary>
    /// 停止します。
    /// </summary>
    /// <exception cref="InvalidOperationException">停止できる状態ではありません。</exception>
    /// <exception cref="AggregateException">停止中にエラーが発生しました。</exception>
    public virtual void Stop()
    {
        ThrowIfNotRunning(out _);

        // StopとAbortが1度だけしか実行されないようにしています。
        ThrowIfStoppingOrAborting(Interlocked.CompareExchange(ref _stopMode, Stopping, 0));
        Dispose();
    }

    /// <summary>
    /// 中断します。
    /// </summary>
    /// <exception cref="InvalidOperationException">中断できる状態ではありません。</exception>
    /// <exception cref="AggregateException">中断中にエラーが発生しました。</exception>
    public virtual void Abort()
    {
        ThrowIfNotRunning(out _);

        // StopとAbortが1度だけしか実行されないようにしています。
        ThrowIfStoppingOrAborting(Interlocked.CompareExchange(ref _stopMode, Aborting, 0));
        Dispose();
    }

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    public virtual void Invoke([NotNull] Action callback)
    {
        Invoke(
            new Func<object?, object?>(
                state =>
                {
                    callback();
                    return null;
                }),
            null);
    }

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    public virtual void Invoke<TState>([NotNull] Action<TState?> callback, TState? state)
    {
        Invoke(
            new Func<TState?, object?>(
                s =>
                {
                    callback(s);
                    return null;
                }),
            state);
    }

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <returns>コールバックの戻り値。</returns>
    public virtual TResult? Invoke<TResult>([NotNull] Func<TResult?> callback)
    {
        return Invoke(new Func<object?, TResult?>(state => callback()), null);
    }

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <returns>コールバックの戻り値。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> が <see langword="null"/> です。</exception>
    /// <exception cref="InvalidOperationException">まだ処理が開始されていません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    public virtual TResult? Invoke<TState, TResult>([NotNull] Func<TState?, TResult?> callback, TState? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        ThrowIfNotRunning(out int currentThreadId);

        if (currentThreadId == Environment.CurrentManagedThreadId)
        {
            return InvokeFromSameThread(callback, state);
        }
        else
        {
            return InvokeFromDifferentThread(callback, state);
        }
    }

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/>が<see langword="null"/>です。</exception>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    public virtual void InvokeAsync([NotNull] Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        ThrowIfNotRunning(out _);
        try
        {
            _callbackQueue.Add(callback);
        }
        catch (ObjectDisposedException ex)
        {
            ThrowStoppingOrAborting(ex);
        }
    }

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/>が<see langword="null"/>です。</exception>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    public virtual void InvokeAsync<TState>([NotNull] Action<TState?> callback, TState? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        ThrowIfNotRunning(out _);
        try
        {
            _callbackQueue.Add(() => callback(state));
        }
        catch (ObjectDisposedException ex)
        {
            ThrowStoppingOrAborting(ex);
        }
    }

    /// <summary>
    /// 停止中か中断中の場合は例外を投げます。
    /// </summary>
    /// <param name="stopMode">停止モード。</param>
    /// <exception cref="InvalidOperationException">停止中または中断中です。</exception>
    private static void ThrowIfStoppingOrAborting(long stopMode)
    {
        if (stopMode == 0)
        {
            return;
        }

        ThrowStoppingOrAborting(null);
    }

    /// <summary>
    /// 停止中か中断中を表す例外を投げます。
    /// </summary>
    /// <param name="ex">内部エラー。</param>
    /// <exception cref="InvalidOperationException">停止中または中断中です。</exception>
    [DoesNotReturn]
    private static void ThrowStoppingOrAborting(Exception? ex)
    {
        throw new InvalidOperationException("Worker is already stopping or aborting.", ex);
    }

    /// <summary>
    /// 同じスレッドでコールバックメソッドを同期的に実行します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="StackOverflowException">コールバックメソッドを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    private TResult? InvokeFromSameThread<TState, TResult>([NotNull] Func<TState?, TResult?> callback, TState? state)
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
            return callback(state);
        }
        finally
        {
            --_recursionCount;
        }
    }

    /// <summary>
    /// 異なるスレッドでコールバックメソッドを同期的に実行します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    /// <exception cref="InvalidOperationException">停止中または中断中です。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    private TResult? InvokeFromDifferentThread<TState, TResult>([NotNull] Func<TState?, TResult?> callback, TState? state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(Interlocked.Read(ref _managedThreadId) != Environment.CurrentManagedThreadId, "Interlocked.Read(ref _managedThreadId) != Environment.CurrentManagedThreadId");

        TResult? result = default;
        Exception? error = null;
        try
        {
            CancellationToken token = _cancellationTokenSource.Token;
            using (ManualResetEventSlim invokeWaitHandle = new ())
            using (token.Register(invokeWaitHandle.Set))
            {
                _callbackQueue.Add(
                    () =>
                    {
                        try
                        {
                            result = callback(state);
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                        }
                        finally
                        {
                            invokeWaitHandle.Set();
                        }
                    });
                invokeWaitHandle.Wait(token);
            }
        }
        catch (ObjectDisposedException ex)
        {
            ThrowStoppingOrAborting(ex);
        }
        catch (InvalidOperationException ex)
        {
            ThrowStoppingOrAborting(ex);
        }

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result;
    }

    /// <summary>
    /// 実行中でなければ例外を投げます。
    /// </summary>
    /// <param name="currentThreadId">スレッドID。</param>
    /// <exception cref="InvalidOperationException">まだ実行されていません。</exception>
    private void ThrowIfNotRunning(out int currentThreadId)
    {
        currentThreadId = ManagedThreadId;
        if (currentThreadId != 0)
        {
            return;
        }

        throw new InvalidOperationException("Worker is not running yet.");
    }

    /// <summary>
    /// 停止中か中断中の場合は例外を投げます。
    /// </summary>
    /// <exception cref="InvalidOperationException">停止中または中断中です。</exception>
    private void ThrowIfStoppingOrAborting()
    {
        ThrowIfStoppingOrAborting(Interlocked.Read(ref _stopMode));
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    /// <exception cref="AggregateException">停止中にエラーが発生しました。</exception>
    private void Dispose()
    {
        try
        {
            _callbackQueue.CompleteAdding();
            _cancellationTokenSource.Cancel();
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _callbackQueue.Dispose();
        }
    }

    /// <summary>
    /// 実行した直後に発生するイベントを呼び出します。
    /// </summary>
    /// <param name="error">実行した処理の例外。</param>
    /// <exception cref="Exception">イベント中にエラーが発生しました。</exception>
    private void InvokeRan(Exception? error)
    {
        try
        {
            Ran(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // 実行した処理に例外が無ければ、そのまま例外を投げます。
            if (error == null)
            {
                throw;
            }

            // 実行した処理に例外があれば、両方の例外を投げます。
            throw new AggregateException(new[] { error, ex });
        }
    }
}

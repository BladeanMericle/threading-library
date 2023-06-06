namespace Mericle.Threading;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

/// <summary>
/// コールバックを順番に呼び出し続けるワーカーを表します。
/// </summary>
public class InvokableWorker : RepeatWorkerBase, IInvokableWorker
{
    /// <summary>
    /// 最大再帰カウントの初期値。
    /// </summary>
    public const int DefaultMaxRecursionCount = 64;

    /// <summary>
    /// 最大再帰カウント。
    /// </summary>
    private readonly int _maxRecursionCount;

    /// <summary>
    /// ワーカーを実行しているスレッドのスレッドID。
    /// </summary>
    private long _workerThreadId;

    /// <summary>
    /// コールバックのキュー。
    /// </summary>
    private volatile BlockingCollection<Action>? _callbackQueue;

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
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    public virtual void Invoke(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Invoke(
            (object? _) =>
            {
                callback();
                return (object?)null;
            },
            null);
    }

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> が <see langword="null"/> です。</exception>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    public virtual void Invoke<TState>(Action<TState> callback, TState state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Invoke(
            s =>
            {
                callback(s);
                return (object?)null;
            },
            state);
    }

    /// <summary>
    /// コールバックを同期的に呼び出します。
    /// </summary>
    /// <typeparam name="TResult">コールバックの戻り値の型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <returns>コールバックの戻り値。</returns>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    [return: MaybeNull]
    public virtual TResult Invoke<TResult>(Func<TResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return Invoke((object? s) => callback(), null);
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
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    /// <exception cref="StackOverflowException">コールバックを実行しているスレッドで一定回数以上再帰的に呼び出されました。</exception>
    /// <exception cref="Exception">コールバックで例外が発生しました。</exception>
    [return: MaybeNull]
    public virtual TResult Invoke<TState, TResult>(Func<TState, TResult> callback, TState state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (GetWorkerThreadId() == Environment.CurrentManagedThreadId)
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
    public virtual void InvokeAsync(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        InvokeAsync((object? _) => callback(), null);
    }

    /// <summary>
    /// コールバックを非同期に呼び出します。
    /// </summary>
    /// <typeparam name="TState">コールバックに渡すオブジェクトの型。</typeparam>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/>が<see langword="null"/>です。</exception>
    /// <exception cref="InvalidOperationException">コールバックを呼び出せる状態ではありません。</exception>
    public virtual void InvokeAsync<TState>(Action<TState> callback, TState state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        BlockingCollection<Action> callbackQueue = _callbackQueue
            ?? throw new InvalidOperationException(GetWorkerIsNotRunningMessage());
        try
        {
            callbackQueue.Add(() => callback(state));
        }
        catch (ObjectDisposedException ex)
        {
            throw new InvalidOperationException(GetWorkerIsNotRunningMessage(), ex);
        }
    }

    /// <summary>
    /// ワーカーを実行しているスレッドのスレッドIDを取得します。
    /// </summary>
    /// <returns>ワーカーを実行しているスレッドのスレッドID。</returns>
    /// <exception cref="InvalidOperationException">ワーカーが実行中ではありません。</exception>
    protected virtual int GetWorkerThreadId()
    {
        long workerThreadId = Interlocked.Read(ref _workerThreadId);
        if (workerThreadId <= 0)
        {
            throw new InvalidOperationException(GetWorkerIsNotRunningMessage());
        }

        return (int)workerThreadId;
    }

    /// <summary>
    /// 処理の実行のメイン部分です。
    /// </summary>
    protected override void RunMain()
    {
        try
        {
            using (_callbackQueue = new ())
            {
                Interlocked.Exchange(ref _workerThreadId, Environment.CurrentManagedThreadId);
                base.RunMain();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _workerThreadId, 0);
            _callbackQueue = null;
        }
    }

    /// <summary>
    /// 処理を実行します。
    /// </summary>
    /// <param name="token">キャンセルトークン。</param>
    protected override void Work(in CancellationToken token)
    {
        Action callback;
        try
        {
            Debug.Assert(_callbackQueue is not null, "_callbackQueue is not null");

            callback = _callbackQueue!.Take(token);
        }
        catch (OperationCanceledException)
        {
            // キャンセルは正常な動作なので無視します。
            return;
        }

        // コールバックが OperationCanceledException を投げる可能性もあるので、外に出しています。
        callback();
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
    [return: MaybeNull]
    private TResult InvokeFromSameThread<TState, TResult>(Func<TState, TResult> callback, TState state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(Interlocked.Read(ref _workerThreadId) == Environment.CurrentManagedThreadId, "Interlocked.Read(ref _workerThreadId) == Environment.CurrentManagedThreadId");

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
    [return: MaybeNull]
    private TResult InvokeFromDifferentThread<TState, TResult>(Func<TState, TResult> callback, TState state)
    {
        Debug.Assert(callback != null, "callback != null");
        Debug.Assert(Interlocked.Read(ref _workerThreadId) != Environment.CurrentManagedThreadId, "Interlocked.Read(ref _workerThreadId) != Environment.CurrentManagedThreadId");

        BlockingCollection<Action> callbackQueue = _callbackQueue
            ?? throw new InvalidOperationException(GetWorkerIsNotRunningMessage());
        CancellationToken token = GetCancellationToken();
        TResult? result = default;
        Exception? error = null;
        try
        {
            using ManualResetEventSlim invokeWaitHandle = new ();
            callbackQueue.Add(
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
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(GetWorkerIsNotRunningMessage(), ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException($"{GetType().Name} is canceled.", ex);
        }

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result;
    }
}

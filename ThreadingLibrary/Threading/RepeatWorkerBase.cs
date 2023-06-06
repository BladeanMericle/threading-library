namespace Mericle.Threading;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

/// <summary>
/// 繰り返し処理を実行し続けるワーカーの基本機能を表します。
/// </summary>
public abstract class RepeatWorkerBase : IWorkable
{
    /// <summary>
    /// ワーカーが未実行であることを表す定数。
    /// </summary>
    private const long Suspended = 0;

    /// <summary>
    /// ワーカーが準備中であることを表す定数。
    /// </summary>
    private const long Preparing = 1;

    /// <summary>
    /// ワーカーが実行中であることを表す定数。
    /// </summary>
    private const long Running = 2;

    /// <summary>
    /// ワーカーが停止中であることを表す定数。
    /// </summary>
    private const long Stopping = 3;

    /// <summary>
    /// ワーカーが中断中であることを表す定数。
    /// </summary>
    private const long Aborting = 4;

    /// <summary>
    /// ワーカーの状態。
    /// </summary>
    private long _workerState = Suspended;

    /// <summary>
    /// キャンセルトークンを作成するオブジェクト。
    /// </summary>
    private volatile CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// 新しいインスタンスを初期化します。
    /// </summary>
    public RepeatWorkerBase()
    {
    }

    /// <summary>
    /// 実行する直前に発生するイベントです。
    /// </summary>
    public event EventHandler? Working;

    /// <summary>
    /// 実行した直後に発生するイベントです。
    /// </summary>
    public event EventHandler? Worked;

    /// <summary>
    /// 実行します。
    /// </summary>
    /// <exception cref="InvalidOperationException">停止状態ではありません。</exception>
    /// <exception cref="Exception">実行中に例外が発生しました。</exception>
    public void Run()
    {
        UpdateWorkerState(Suspended, Preparing);
        RunMain();
    }

    /// <summary>
    /// 停止します。
    /// </summary>
    /// <exception cref="InvalidOperationException">実行中ではありません。</exception>
    public void Stop()
    {
        UpdateWorkerState(Running, Stopping);
        OnStopping();
        Cancel();
    }

    /// <summary>
    /// 中断します。
    /// </summary>
    /// <exception cref="InvalidOperationException">実行中ではありません。</exception>
    public void Abort()
    {
        UpdateWorkerState(Running, Aborting);
        OnAborting();
        Cancel();
    }

    /// <summary>
    /// ワーカーが実行中ではないというメッセージを取得します。
    /// </summary>
    /// <returns>ワーカーが実行中ではないというメッセージ。</returns>
    protected string GetWorkerIsNotRunningMessage()
    {
        return $"{GetType().Name} is not running.";
    }

    /// <summary>
    /// キャンセルトークンを取得します。
    /// </summary>
    /// <returns>キャンセルトークン。</returns>
    /// <exception cref="InvalidOperationException">キャンセルトークンを取得できる状態ではありません。</exception>
    protected virtual CancellationToken GetCancellationToken()
    {
        CancellationTokenSource cancellationTokenSource = _cancellationTokenSource
            ?? throw new InvalidOperationException(GetWorkerIsNotRunningMessage());
        try
        {
            return cancellationTokenSource.Token;
        }
        catch (ObjectDisposedException ex)
        {
            throw new InvalidOperationException(GetWorkerIsNotRunningMessage(), ex);
        }
    }

    /// <summary>
    /// 処理の実行のメイン部分です。
    /// </summary>
    protected virtual void RunMain()
    {
        try
        {
            using (_cancellationTokenSource = new ())
            {
                Interlocked.Exchange(ref _workerState, Running);
                OnRunning();
                Exception? error = null;
                try
                {
                    RepeatWork(_cancellationTokenSource.Token);
                    OnAfterWork();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                OnRan(error);
                if (error is not null)
                {
                    ExceptionDispatchInfo.Capture(error).Throw();
                }
            }
        }
        finally
        {
            _cancellationTokenSource = null;
            Interlocked.Exchange(ref _workerState, Suspended);
        }
    }

    /// <summary>
    /// 処理を実行します。
    /// </summary>
    /// <param name="token">キャンセルトークン。</param>
    protected abstract void Work(in CancellationToken token);

    /// <summary>
    /// 実行する直前に呼び出されます。
    /// </summary>
    protected virtual void OnRunning()
    {
        Working?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 実行後に呼び出されます。
    /// </summary>
    /// <param name="workError">処理のエラー。</param>
    /// <exception cref="Exception">実行後に例外が発生しました。</exception>
    protected virtual void OnRan(Exception? workError)
    {
        try
        {
            Worked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (workError is not null)
            {
                throw new AggregateException(workError, ex);
            }

            throw;
        }
    }

    /// <summary>
    /// 停止中に呼び出されます。
    /// </summary>
    protected virtual void OnStopping()
    {
    }

    /// <summary>
    /// 停止後に呼び出されます。
    /// </summary>
    protected virtual void OnStopped()
    {
    }

    /// <summary>
    /// 中断中に呼び出されます。
    /// </summary>
    protected virtual void OnAborting()
    {
    }

    /// <summary>
    /// 中断後に呼び出されます。
    /// </summary>
    protected virtual void OnAborted()
    {
    }

    /// <summary>
    /// ワーカーの状態を更新します。
    /// </summary>
    /// <param name="expectedState">想定しているワーカーの状態。</param>
    /// <param name="newState">新しいワーカーの状態。</param>
    /// <exception cref="InvalidOperationException">状態の更新に失敗しました。</exception>
    private void UpdateWorkerState(long expectedState, long newState)
    {
        long actualState = Interlocked.CompareExchange(ref _workerState, newState, expectedState);
        if (actualState == expectedState)
        {
            return;
        }

        throw actualState switch
        {
            Suspended => new InvalidOperationException($"{GetType().Name} is suspended."),
            Running => new InvalidOperationException($"{GetType().Name} is running."),
            Stopping => new InvalidOperationException($"{GetType().Name} is stopping."),
            Aborting => new InvalidOperationException($"{GetType().Name} is aborting."),
            _ => new InvalidOperationException($"{GetType().Name} is unknown state."),
        };
    }

    /// <summary>
    /// 処理を繰り返します。
    /// </summary>
    /// <param name="token">キャンセルトークン。</param>
    private void RepeatWork(in CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Work(token);
        }
    }

    /// <summary>
    /// 処理をキャンセルします。
    /// </summary>
    /// <exception cref="InvalidOperationException">キャンセル中に例外が発生しました。</exception>
    private void Cancel()
    {
        try
        {
            Debug.Assert(_cancellationTokenSource is not null, "_cancellationTokenSource is not null");

            _cancellationTokenSource!.Cancel();
        }
        catch (AggregateException ex)
        {
            throw new InvalidOperationException("Failed to cancel.", ex);
        }
    }

    /// <summary>
    /// 処理後に呼び出されます。
    /// </summary>
    private void OnAfterWork()
    {
        Interlocked.Read(ref _workerState);
        switch (_workerState)
        {
            case Stopping:
                OnStopped();
                break;
            case Aborting:
                OnAborted();
                break;
        }
    }
}
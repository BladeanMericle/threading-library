namespace Mericle.Threading;

/// <summary>
/// <see cref="ICallbackWorker"/>を使用した<see cref="SynchronizationContext"/>を表します。
/// </summary>
public class CallbackWorkerSynchronizationContext : SynchronizationContext
{
    /// <summary>
    /// ワーカー。
    /// </summary>
    private readonly ICallbackWorker _callbackWorker;

    /// <summary>
    /// 指定したワーカーで新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="callbackWorker">ワーカー。</param>
/// <exception cref="ArgumentNullException"><paramref name="callbackWorker"/>が<see langword="null"/>です。</exception>
    public CallbackWorkerSynchronizationContext(ICallbackWorker callbackWorker)
    {
        ArgumentNullException.ThrowIfNull(callbackWorker);

        _callbackWorker = callbackWorker;
    }

    /// <summary>
    /// コールバックメソッドを同期的に実行します。
    /// </summary>
    /// <param name="d">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    public override void Send(SendOrPostCallback d, object? state)
    {
        _callbackWorker.Invoke(new WorkCallback(d), state);
    }

    /// <summary>
    /// コールバックメソッドを非同期に実行します。
    /// </summary>
    /// <param name="d">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    public override void Post(SendOrPostCallback d, object? state)
    {
        _callbackWorker.InvokeAsync(new WorkCallback(d), state);
    }
}

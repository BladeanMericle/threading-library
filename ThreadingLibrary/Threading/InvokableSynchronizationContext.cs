namespace Mericle.Threading;

/// <summary>
/// <see cref="IInvokable"/>を使用した<see cref="SynchronizationContext"/>を表します。
/// </summary>
public class InvokableSynchronizationContext : SynchronizationContext
{
    /// <summary>
    /// コールバックの呼び出しが可能なオブジェクト。
    /// </summary>
    private readonly IInvokable _invokable;

    /// <summary>
    /// 指定したワーカーで新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="invokable">コールバックの呼び出しが可能なオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="invokable"/>が<see langword="null"/>です。</exception>
    public InvokableSynchronizationContext(IInvokable invokable)
    {
        ArgumentNullException.ThrowIfNull(invokable);

        _invokable = invokable;
    }

    /// <summary>
    /// コールバックメソッドを同期的に実行します。
    /// </summary>
    /// <param name="d">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    public override void Send(SendOrPostCallback d, object? state)
    {
        Action<object?> callback = new (d);
        _invokable.Invoke(callback, state);
    }

    /// <summary>
    /// コールバックメソッドを非同期に実行します。
    /// </summary>
    /// <param name="d">コールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    public override void Post(SendOrPostCallback d, object? state)
    {
        Action<object?> callback = new (d);
        _invokable.InvokeAsync(callback, state);
    }
}

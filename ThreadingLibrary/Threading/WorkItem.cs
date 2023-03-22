namespace Mericle.Threading;

/// <summary>
/// <see cref="CallbackWorker"/> の処理要素を表します。
/// </summary>
public class WorkItem
{
    /// <summary>
    /// インスタンスを初期化します。
    /// </summary>
    /// <param name="callback"><see cref="CallbackWorker"/>で実行するコールバックメソッド。</param>
    /// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
    public WorkItem(WorkCallback callback, object? state)
    {
        Callback = callback;
        State = state;
    }

    /// <summary>
    /// コールバックメソッドを取得します。
    /// </summary>
    /// <value>コールバックメソッド。</value>
    public WorkCallback Callback { get; }

    /// <summary>
    /// コールバックメソッドが使用する情報を格納したオブジェクトを取得します。
    /// </summary>
    /// <value>コールバックメソッドが使用する情報を格納したオブジェクト。</value>
    public object? State { get; }
}

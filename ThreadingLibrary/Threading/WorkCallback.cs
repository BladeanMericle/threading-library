namespace Mericle.Threading;

/// <summary>
/// <see cref="CallbackWorker"/>で実行するコールバックメソッドを表します。
/// </summary>
/// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
public delegate void WorkCallback(object? state);

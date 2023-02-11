namespace Mericle.Threading;

/// <summary>
/// <see cref="QueueCallbackWorker"/>で実行するコールバックメソッドを表します。
/// </summary>
/// <param name="state">コールバックメソッドが使用する情報を格納したオブジェクト。</param>
public delegate void QueueCallback(object? state);

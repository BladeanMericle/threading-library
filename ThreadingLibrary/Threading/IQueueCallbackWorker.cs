namespace Mericle.Threading;

/// <summary>
/// コールバックをキューに入れ、順番に処理し続ける機能を表すインタフェースです。
/// </summary>
public interface IQueueCallbackWorker : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 処理の開始を通知します。
    /// </summary>
    event EventHandler Started;

    /// <summary>
    /// 処理の終了を通知します。
    /// </summary>
    event EventHandler Ended;

    /// <summary>
    /// 処理が実行中かどうかを取得します。
    /// </summary>
    /// <value>処理が実行中の場合は <see langword="true"/>、それ以外は <see langword="false"/>。</value>
    bool IsRunning { get; }

    /// <summary>
    /// コールバックを同期的に実行します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    void Invoke(QueueCallback callback, object state);

    /// <summary>
    /// コールバックを非同期に実行します。
    /// </summary>
    /// <param name="callback">コールバック。</param>
    /// <param name="state">コールバックに渡すオブジェクト。</param>
    void InvokeAsync(QueueCallback callback, object state);

    /// <summary>
    /// 処理を開始し、コールバックを実行し続けます。このメソッドはインスタンスが破棄されるまで処理が継続します。
    /// </summary>
    void Run();
}

namespace Mericle.Threading;

/// <summary>
/// <see cref="IQueueCallbackWorker"/> の設定を表します。
/// </summary>
public class QueueCallbackWorkerConfig : IQueueCallbackWorkerConfig
{
    /// <summary>
    /// 最大再帰カウントの初期値。
    /// </summary>
    public static readonly int DefaultMaxRecursionCount = 64;

    /// <summary>
    /// 即座に停止するかどうかの初期値。
    /// </summary>
    public static readonly bool DefaultIsStopImmediately = true;

    /// <summary>
    /// 新しいインスタンスを初期化します。
    /// </summary>
    public QueueCallbackWorkerConfig()
    {
    }

    /// <summary>
    /// 最大再帰カウントを取得または設定します。
    /// </summary>
    /// <value>最大再帰カウント。</value>
    public int MaxRecursionCount { get; set; } = DefaultMaxRecursionCount;

    /// <summary>
    /// 即座に停止するかどうかを取得または設定します。
    /// </summary>
    /// <value>即座に停止する場合は <see langword="true"/>、それ以外の場合は <see langword="false"/>。</value>
    public bool IsStopImmediately { get; set; } = DefaultIsStopImmediately;
}

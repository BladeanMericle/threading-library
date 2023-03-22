namespace Mericle.Threading;

/// <summary>
/// <see cref="ICallbackWorker"/> の設定を表すインタフェースです。
/// </summary>
public interface ICallbackWorkerConfig
{
    /// <summary>
    /// 最大再帰カウントを取得します。
    /// </summary>
    /// <value>最大再帰カウント。</value>
    int MaxRecursionCount { get; }

    /// <summary>
    /// 即座に停止するかどうかを取得します。
    /// </summary>
    /// <value>即座に停止する場合は <see langword="true"/>、それ以外の場合は <see langword="false"/>。</value>
    bool IsStopImmediately { get; }
}

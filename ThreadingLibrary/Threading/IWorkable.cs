namespace Mericle.Threading;

/// <summary>
/// 実行可能なオブジェクトを表すインタフェースです。
/// </summary>
public interface IWorkable
{
    /// <summary>
    /// 実行する直前に発生するイベントです。
    /// </summary>
    event EventHandler Running;

    /// <summary>
    /// 実行した直後に発生するイベントです。
    /// </summary>
    event EventHandler Ran;

    /// <summary>
    /// 実行中かどうかを取得します。
    /// </summary>
    /// <value>実行中の場合は <see langword="true"/>、それ以外は <see langword="false"/>。</value>
    bool IsRunning { get; }

    /// <summary>
    /// 実行します。
    /// </summary>
    void Run();

    /// <summary>
    /// 停止します。
    /// </summary>
    void Stop();

    /// <summary>
    /// 中断します。
    /// </summary>
    void Abort();
}

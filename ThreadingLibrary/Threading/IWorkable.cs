namespace Mericle.Threading;

/// <summary>
/// 実行可能なオブジェクトを表すインタフェースです。
/// </summary>
public interface IWorkable
{
    /// <summary>
    /// 実行する直前に発生するイベントです。
    /// </summary>
    event EventHandler Working;

    /// <summary>
    /// 実行した直後に発生するイベントです。
    /// </summary>
    event EventHandler Worked;

    /// <summary>
    /// 実行します。
    /// </summary>
    /// <exception cref="InvalidOperationException">実行中、または停止・中断中です。</exception>
    /// <exception cref="Exception">実行中に例外が発生しました。</exception>
    void Run();

    /// <summary>
    /// 停止します。
    /// </summary>
    /// <exception cref="InvalidOperationException">実行中ではないか、すでに停止・中断中です。</exception>
    void Stop();

    /// <summary>
    /// 中断します。
    /// </summary>
    /// <exception cref="InvalidOperationException">実行中ではないか、すでに停止・中断中です。</exception>
    void Abort();
}

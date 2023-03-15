namespace Mericle.Threading;

using System.Globalization;

/// <summary>
/// スレッドの設定を表すインタフェースです。
/// </summary>
public interface IThreadConfig
{
    /// <summary>
    /// スレッドのカルチャを取得します。
    /// </summary>
    /// <value>スレッドのカルチャ。</value>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// スレッドのUIカルチャを取得します。
    /// </summary>
    /// <value>スレッドのUIカルチャ。</value>
    CultureInfo CurrentUICulture { get; }

    /// <summary>
    /// バックグラウンドスレッドかどうかを取得します。
    /// </summary>
    /// <value>バックグラウンドスレッドの場合は <see langword="true"/>、それ以外は <see langword="false"/>。</value>
    bool IsBackground { get; }

    /// <summary>
    /// スレッドのスケジューリング優先順位を取得します。
    /// </summary>
    /// <value>スレッドのスケジューリング優先順位。</value>
    ThreadPriority Priority { get; }
}
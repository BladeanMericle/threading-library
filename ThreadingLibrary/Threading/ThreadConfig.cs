namespace Mericle.Threading;

using System.Globalization;

/// <summary>
/// スレッドの構成を表します。
/// </summary>
public class ThreadConfig : IThreadConfig
{
    /// <summary>
    /// スレッドのカルチャの初期値。
    /// </summary>
    public static readonly CultureInfo DefaultCurrentCulture = CultureInfo.CurrentCulture;

    /// <summary>
    /// スレッドのUIカルチャの初期値。
    /// </summary>
    public static readonly CultureInfo DefaultCurrentUICulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// バックグラウンドスレッドかどうかの初期値。
    /// </summary>
    public static readonly bool DefaultIsBackground = false;

    /// <summary>
    /// スレッドのスケジューリング優先順位の初期値。
    /// </summary>
    public static readonly ThreadPriority DefaultPriority = ThreadPriority.Normal;

    /// <summary>
    /// 新しいインスタンスを初期化します。
    /// </summary>
    public ThreadConfig()
    {
    }

    /// <summary>
    /// スレッドのカルチャを取得または設定します。
    /// </summary>
    /// <value>スレッドのカルチャ。</value>
    public CultureInfo CurrentCulture { get; set; } = DefaultCurrentCulture;

    /// <summary>
    /// スレッドのUIカルチャを取得または設定します。
    /// </summary>
    /// <value>スレッドのUIカルチャ。</value>
    public CultureInfo CurrentUICulture { get; set; } = DefaultCurrentUICulture;

    /// <summary>
    /// バックグラウンドスレッドかどうかを取得または設定します。
    /// </summary>
    /// <value>バックグラウンドスレッドの場合は <see langword="true"/>、それ以外は <see langword="false"/>。</value>
    public bool IsBackground { get; set; } = DefaultIsBackground;

    /// <summary>
    /// スレッドのスケジューリング優先順位を取得または設定します。
    /// </summary>
    /// <value>スレッドのスケジューリング優先順位。</value>
    public ThreadPriority Priority { get; set; } = DefaultPriority;
}
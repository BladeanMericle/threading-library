namespace Mericle.Threading;

/// <summary>
/// スレッドプールの要素を作成するファクトリーを表します。
/// </summary>
public interface IUserThreadPoolItemFactory
{
    /// <summary>
    /// スレッドプールの要素を作成します。
    /// </summary>
    /// <param name="threadName">スレッド名。</param>
    /// <returns>スレッドプールの要素。</returns>
    IUserThreadPoolItem Create(string threadName);
}
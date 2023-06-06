namespace Mericle.Threading
{
    /// <summary>
    /// 実行可能かつコールバックの呼び出しが可能なオブジェクトを表すインタフェースです。
    /// </summary>
    public interface IInvokableWorker : IInvokable, IWorkable
    {
    }
}
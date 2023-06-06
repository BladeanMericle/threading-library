using System.Threading;
namespace Mericle.Threading;

/// <summary>
/// <see cref="InvokableWorker"/>をテストします。
/// </summary>
public class InvokableWorkerTest
{
    /// <summary>
    /// <see cref="InvokableWorker.Running"/>と<see cref="InvokableWorker.Ran"/>の実行をテストします。
    /// </summary>
    [Fact]
    public void TestRunningAndRan()
    {
        using ManualResetEventSlim runningWaitHandle = new ();
        using ManualResetEventSlim ranWaitHandle = new ();
        InvokableWorker worker = new ();
        worker.Working += (sender, e) => runningWaitHandle.Set();
        worker.Worked += (sender, e) => ranWaitHandle.Set();
        Task.Run(worker.Run);
        Assert.True(runningWaitHandle.Wait(TimeSpan.FromMinutes(1)));
        worker.Stop();
        Assert.True(ranWaitHandle.Wait(TimeSpan.FromMinutes(1)));
    }
}
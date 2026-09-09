using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TableTennis
{
    // Owns one background evaluation; UI polls completion on its own thread.
    public sealed class ReceiveStudyRunner : IDisposable
    {
        Task<ReceiveReport> receiveTask;
        CancellationTokenSource receiveCancellation;
        int studyDone, studyTotal;
        bool disposed;
        readonly bool cooperative;
        IEnumerator<ReceiveReport> work;
        TaskCompletionSource<ReceiveReport> completion;
        public ReceiveStudyRunner(bool cooperative = false) { this.cooperative = cooperative; }

        // Browser builds cannot use managed worker threads. Yield between trials to keep input and rendering live.
        public void Pump(double milliseconds = 3)
        {
            if (work == null) return;
            var watch = Stopwatch.StartNew();
            try
            {
                do
                {
                    if (!work.MoveNext()) throw new InvalidOperationException("评估未返回报告。");
                    if (work.Current != null) { completion.SetResult(work.Current); break; }
                } while (watch.Elapsed.TotalMilliseconds < milliseconds);
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception e) { completion.TrySetException(e); }
            if (completion.Task.IsCompleted) { work.Dispose(); work = null; completion = null; }
        }
        public bool IsRunning => receiveTask != null;
        public bool TestingCurrent { get; private set; }
        public int Done => Volatile.Read(ref studyDone);
        public int Total => Volatile.Read(ref studyTotal);

        public void Start(BallState input, PhysicsSettings physics, ReceiveStudySettings settings, ReceiveAction baseline,
            double bounce, double friction, double tangent, bool currentOnly)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ReceiveStudyRunner));
            if (IsRunning) throw new InvalidOperationException("接发评估已在运行。");
            var p = physics.Copy(); var study = settings.Copy();
            receiveCancellation = new CancellationTokenSource(); var token = receiveCancellation.Token;
            TestingCurrent = currentOnly; studyDone = 0; studyTotal = currentOnly ? 1 : study.Candidates + 85;
            if (cooperative)
            {
                completion = new TaskCompletionSource<ReceiveReport>(); receiveTask = completion.Task;
                work = ReceiveStudy.RunSteps(input, p, study, baseline, bounce, friction, tangent, currentOnly, token,
                    (done, total) => { studyDone = done; studyTotal = total; }).GetEnumerator();
                return;
            }
            receiveTask = Task.Run(() => currentOnly
                ? ReceiveStudy.TestCurrent(input, p, study, baseline, bounce, friction, tangent, token)
                : ReceiveStudy.Search(input, p, study, baseline, bounce, friction, tangent, token,
                    (done, total) => { Volatile.Write(ref studyDone, done); Volatile.Write(ref studyTotal, total); }), token);
        }

        public void Cancel() => receiveCancellation?.Cancel();

        public bool TryComplete(out ReceiveReport report, out Exception error, out bool cancelled)
        {
            report = null; error = null; cancelled = false;
            if (receiveTask == null || !receiveTask.IsCompleted) return false;
            var task = receiveTask; receiveTask = null;
            cancelled = receiveCancellation.IsCancellationRequested || task.IsCanceled;
            receiveCancellation.Dispose(); receiveCancellation = null;
            // Observe a fault even if cancellation wins the completion race.
            error = task.IsFaulted ? task.Exception.GetBaseException() : null;
            if (!cancelled && error == null) report = task.Result;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Cancel();
            var cancellation = receiveCancellation; var task = receiveTask;
            receiveCancellation = null; receiveTask = null;
            if (cooperative)
            {
                work?.Dispose(); work = null; completion?.TrySetCanceled(); completion = null;
                var observed = task?.Exception; cancellation?.Dispose(); return;
            }
            if (task == null) cancellation?.Dispose();
            else task.ContinueWith(completed => { var observed = completed.Exception; cancellation.Dispose(); }, TaskScheduler.Default);
        }
    }
}

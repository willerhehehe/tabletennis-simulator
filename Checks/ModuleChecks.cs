using System;
using System.Globalization;
using System.IO;
using System.Threading;
using TableTennis;

static class ModuleChecks
{
    public static void Run()
    {
        void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        var physics = new PhysicsSettings();
        var serve = BallPhysics.Simulate(new BallState(new V3(0, 1, -1.65),
            new V3(0, -6 * Math.Sin(Math.PI / 12), 6 * Math.Cos(Math.PI / 12)), new V3(-40 * BallPhysics.Rps, 0, 0)), physics);
        var incoming = Reception.FromServe(serve, physics, 0.1).Incoming;
        var settings = new ReceiveStudySettings { Trials = 16, Candidates = 16 };
        var action = new ReceiveAction(10, 0, 0.5, 4, 0);
        var expected = ReceiveStudy.TestCurrent(incoming, physics, settings, action, .8, .8, .15);
        using var runner = new ReceiveStudyRunner();
        (ReceiveReport report, Exception error, bool cancelled) Finish()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do {
                if (runner.TryComplete(out var report, out var error, out bool cancelled)) return (report, error, cancelled);
                Thread.Sleep(1);
            } while (DateTime.UtcNow < deadline);
            throw new Exception("后台评估超时");
        }
        runner.Start(incoming, physics, settings, action, .8, .8, .15, true);
        physics.Step = double.NaN; settings.Trials = 1024; // In-flight work owns a snapshot.
        var first = Finish();
        Require(first.error == null && !first.cancelled && first.report.Baseline.Attempts == 16 &&
            first.report.Baseline.QualifiedSuccesses == expected.Baseline.QualifiedSuccesses, "任务隔离或计算结果改变");
        physics.Step = .001; settings.Trials = 16;
        runner.Start(incoming, physics, settings, action, .8, .8, .15, false); runner.Cancel();
        var cancelledResult = Finish();
        Require(cancelledResult.cancelled && cancelledResult.report == null && !runner.IsRunning, "取消任务发布了结果");
        runner.Start(incoming, physics, settings, action, double.NaN, .8, .15, true);
        var failed = Finish(); Require(failed.error != null && failed.report == null, "后台异常未返回调用方");
        runner.Dispose(); bool rejected = false;
        try { runner.Start(incoming, physics, settings, action, .8, .8, .15, true); } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "已释放任务仍可启动");
        Console.WriteLine("PASS: task snapshots, cancellation, errors and disposal preserve the application boundary.");

        string folder = Path.Combine(Path.GetTempPath(), "tabletennis-module-" + Guid.NewGuid());
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var files = new ExperimentFiles(folder);
            string stroke = files.WriteStroke(serve.Samples, "{\"version\":1}");
            Require(File.ReadAllLines(stroke + ".csv").Length == serve.Samples.Count + 1 &&
                File.ReadAllLines(stroke + ".csv")[1].Split(',').Length == 10, "轨迹 CSV 格式改变");
            string report = files.WriteReceive(expected, "{\"version\":1}");
            Require(File.ReadAllLines(report + "-trials.csv").Length == 17 &&
                File.ReadAllText(report + "-settings.json") == "{\"version\":1}", "复验导出遗漏数据");
            string flight = files.WriteFlight(serve, null, new BallState(), new BallState(), "{}");
            Require(File.ReadAllLines(flight + "-events.csv").Length == serve.Events.Count + 1, "碰撞事件导出改变");
        }
        finally { CultureInfo.CurrentCulture = culture; if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("PASS: independent exports preserve trajectories, events, trial rows and invariant numeric format.");
    }
}

using System;
using System.Globalization;
using System.IO;
using TableTennis;

static class RubberCalibrationChecks
{
    public static void Run()
    {
        double Number(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        var golden = File.ReadAllLines("Calibration/rubber/test-predictions.csv");
        int total = 0, goldenRow = 1;
        foreach (var p in RubberCalibrations.All)
        {
            var rows = File.ReadAllLines($"Calibration/rubber/source/data/test/racket_{p.Id:00}.csv");
            double ev = 0, ew = 0, baseV = 0, baseW = 0;
            for (int i = 1; i < rows.Length; i++)
            {
                var values = Array.ConvertAll(rows[i].Split(','), Number);
                var s = new BallState(new V3(), new V3(values[8], values[9], values[10]), new V3(values[2], values[3], values[4]));
                var fitted = BallPhysics.Contact(s, new V3(0,0,1), new V3(), p.Restitution, p.Friction, p.Tangent);
                var baseline = BallPhysics.Contact(s, new V3(0,0,1), new V3(), .8, .8, .15);
                var expected = Array.ConvertAll(golden[goldenRow++].Split(','), Number);
                if ((int)expected[0]!=p.Id || (int)expected[1]!=i-1 || Math.Abs(fitted.Velocity.X-expected[2])>1e-9 ||
                    Math.Abs(fitted.Velocity.Z-expected[3])>1e-9 || Math.Abs(fitted.Spin.Y-expected[4])>1e-8)
                    throw new Exception("Python/C# calibrated contact mismatch");
                if (BallPhysics.Energy(fitted)>BallPhysics.Energy(s)+1e-9) throw new Exception("Calibrated stationary rubber created energy");
                ev += Math.Sqrt(Math.Pow(fitted.Velocity.X-values[11],2)+Math.Pow(fitted.Velocity.Z-values[13],2));
                ew += Math.Abs(fitted.Spin.Y-values[6])/BallPhysics.Rps;
                baseV += Math.Sqrt(Math.Pow(baseline.Velocity.X-values[11],2)+Math.Pow(baseline.Velocity.Z-values[13],2));
                baseW += Math.Abs(baseline.Spin.Y-values[6])/BallPhysics.Rps;
                total++;
            }
            int n=rows.Length-1;
            if(n!=p.TestCount || Math.Abs(ev/n-p.VelocityError)>1e-9 || Math.Abs(ew/n-p.SpinErrorRps)>1e-9 || ev>=baseV || ew>=baseW)
                throw new Exception("Held-out calibration report mismatch: R"+p.Id);
            if(!p.Matches((float)p.Restitution,(float)p.Friction,(float)p.Tangent))throw new Exception("UI float conversion loses profile identity");
        }
        Console.WriteLine($"PASS: {total} held-out contacts replay identically in C#; all 10 profiles improve both measured metrics and preserve passive energy.");
    }
}

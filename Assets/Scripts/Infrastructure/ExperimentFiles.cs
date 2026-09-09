using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TableTennis
{
    // File format and paths live here; callers supply data, not UI objects.
    public sealed class ExperimentFiles
    {
        readonly string directory;
        public ExperimentFiles(string directory) { this.directory = Path.GetFullPath(directory); }
        string NewStem(string prefix)
        {
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, prefix + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
        public string WriteStroke(IEnumerable<BallState> samples, string settingsJson)
        {
            string stem = NewStem("stroke-");
            File.WriteAllText(stem + ".json", settingsJson);
            WriteTrajectory(stem + ".csv", samples);
            return stem;
        }
        public string WriteFlight(FlightResult flight, IEnumerable<BallState> approach, BallState incoming, BallState launch, string settingsJson)
        {
            string stem = NewStem("flight-");
            WriteTrajectory(stem + ".csv", flight.Samples);
            if (approach != null) WriteTrajectory(stem + "-incoming.csv", approach);
            var events = new StringBuilder("kind,time_s,v_before_m_s,v_after_m_s,spin_before_rps,spin_after_rps\n");
            if (approach != null) events.AppendLine(FormattableString.Invariant($"球拍,{incoming.Time:R},{incoming.Velocity.Length:R},{launch.Velocity.Length:R},{incoming.Spin.Length / BallPhysics.Rps:R},{launch.Spin.Length / BallPhysics.Rps:R}"));
            foreach (var e in flight.Events) events.AppendLine(FormattableString.Invariant($"{e.Kind},{e.Before.Time:R},{e.Before.Velocity.Length:R},{e.After.Velocity.Length:R},{e.Before.Spin.Length / BallPhysics.Rps:R},{e.After.Spin.Length / BallPhysics.Rps:R}"));
            File.WriteAllText(stem + "-events.csv", events.ToString(), new UTF8Encoding(false));
            File.WriteAllText(stem + "-settings.json", settingsJson);
            return stem;
        }
        public string WriteReceive(ReceiveReport report, string settingsJson)
        {
            string stem = NewStem("receive-study-");
                var csv = new StringBuilder("rank,manual_baseline,pitch_deg,yaw_deg,forward_m_s,up_m_s,side_m_s,successes,trials,rate,margin_p10_m,mean_return_speed_m_s,qualified_successes,qualified_rate,max_net_clearance_m\n");
                var points = new StringBuilder("rank,trial,success,reason,x_m,z_m,height_qualified,net_clearance_m\n");
                for (int i = 0; i < report.Ranked.Count; i++)
                {
                    var s = report.Ranked[i]; var a = s.Action;
                    csv.AppendLine(FormattableString.Invariant($"{i+1},{a.Equals(report.Baseline.Action)},{a.Pitch:R},{a.Yaw:R},{a.Forward:R},{a.Up:R},{a.Side:R},{s.Successes},{s.Attempts},{s.Rate:R},{s.Margin10:R},{s.AverageSpeed:R},{s.QualifiedSuccesses},{s.QualifiedRate:R},{report.Settings.MaxNetClearance:R}"));
                    for (int j = 0; j < s.Trials.Count; j++)
                    {
                        var trial = s.Trials[j];
                        points.AppendLine(FormattableString.Invariant($"{i+1},{j+1},{trial.Success},{trial.Reason},{trial.Landing?.X:R},{trial.Landing?.Z:R},{trial.HeightQualified},{trial.NetClearance:R}"));
                    }
                }
                File.WriteAllText(stem + ".csv", csv.ToString(), new UTF8Encoding(false));
                File.WriteAllText(stem + "-trials.csv", points.ToString(), new UTF8Encoding(false));
                File.WriteAllText(stem + "-settings.json", settingsJson);
            return stem;
        }

        public static void WriteTrajectory(string path, IEnumerable<BallState> samples)
        {
            var csv = new StringBuilder("time_s,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,wx_rad_s,wy_rad_s,wz_rad_s\n");
            foreach (var s in samples)
                csv.AppendLine(string.Join(",", Array.ConvertAll(new[] { s.Time, s.Position.X, s.Position.Y, s.Position.Z, s.Velocity.X, s.Velocity.Y, s.Velocity.Z, s.Spin.X, s.Spin.Y, s.Spin.Z }, n => n.ToString("R", CultureInfo.InvariantCulture))));
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        }

    }
}

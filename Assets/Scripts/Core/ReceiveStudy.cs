using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TableTennis
{
    public static class RacketMotionLimits
    {
        public const float Component = 20, Total = 40;
    }

    public readonly struct ReceiveAction
    {
        public readonly double Pitch, Yaw, Forward, Up, Side;
        public ReceiveAction(double pitch, double yaw, double forward, double up, double side)
        { Pitch = (float)pitch; Yaw = (float)yaw; Forward = (float)forward; Up = (float)up; Side = (float)side; }
        public V3 Normal
        {
            get
            {
                double p = Pitch * Math.PI / 180, y = Yaw * Math.PI / 180;
                return new V3(Math.Sin(y) * Math.Cos(p), Math.Sin(p), -Math.Cos(y) * Math.Cos(p));
            }
        }
        public V3 Velocity => new V3(Side, Up, -Forward);
    }

    public static class RacketGeometry
    {
        public const double ContactWindow = 0.01, FaceHalfWidth = 0.076, FaceHalfHeight = 0.086;
        public const double HandleOffset = 0.12, HandleHalfLength = 0.055, HandleHalfWidth = 0.0125;
        public static void Axes(V3 normal, double rollDegrees, out V3 right, out V3 up)
        {
            V3 referenceUp = Math.Abs(normal.Y) > 0.999 ? new V3(0, 0, 1) : new V3(0, 1, 0);
            V3 r = V3.Cross(referenceUp, normal).Unit, u = V3.Cross(normal, r);
            double roll = rollDegrees * Math.PI / 180;
            right = r * Math.Cos(roll) + u * Math.Sin(roll);
            up = u * Math.Cos(roll) - r * Math.Sin(roll);
        }
        static V3 Abs(V3 v) => new V3(Math.Abs(v.X), Math.Abs(v.Y), Math.Abs(v.Z));
        static double Rim(double a, double b) => Math.Sqrt(a * a + b * b);
        static bool OverlapsTable(V3 center, V3 extent) =>
            center.Y - extent.Y <= 0.76 && center.Y + extent.Y >= 0.68 &&
            Math.Abs(center.X) <= BallPhysics.HalfWidth + extent.X && Math.Abs(center.Z) <= BallPhysics.HalfLength + extent.Z;

        public static bool MayTouchTable(V3 ballCenter, V3 normal, V3 velocity, double rollDegrees)
        {
            Axes(normal, rollDegrees, out V3 right, out V3 up);
            V3 center = ballCenter - normal * (BallPhysics.Radius + 0.004), sweep = Abs(velocity) * ContactWindow;
            V3 a = right * FaceHalfWidth, b = up * FaceHalfHeight;
            V3 faceExtent = new V3(Rim(a.X, b.X), Rim(a.Y, b.Y), Rim(a.Z, b.Z)) + Abs(normal) * 0.0045 + sweep;
            V3 handleExtent = Abs(right) * HandleHalfWidth + Abs(up) * HandleHalfLength + Abs(normal) * 0.009 + sweep;
            // ponytail: swept world AABBs conservatively reject near-edge poses; use convex collision if this loses useful strokes.
            return OverlapsTable(center, faceExtent) || OverlapsTable(center - up * HandleOffset, handleExtent);
        }
    }

    public enum SpinReceiveDirection { Any, WithSpin, AgainstSpin }

    [Serializable]
    public sealed class ReceiveStudySettings
    {
        public double BallVelocityError = 0.3, SpinErrorRps = 8, FaceErrorDegrees = 1, RacketVelocityError = 0.25;
        public double MaxRacketSpeed = 8;
        public double MaxNetClearance = 0.15; // Ball bottom above net top, metres.
        public double RacketRollDegrees = -70;
        public SpinReceiveDirection SpinDirection;
        public int RubberResponseId;
        public int DirectionConstraint; // 0 all, 1 forward/up, 2 retreat, 3 downward
        public int Candidates = 256, Trials = 256, Seed = 42;
        public double MinForward => DirectionConstraint == 1 ? 0 : -Math.Min(RacketMotionLimits.Component, MaxRacketSpeed);
        public double MaxForward => DirectionConstraint == 2 ? 0 : Math.Min(RacketMotionLimits.Component, MaxRacketSpeed);
        public double MinUp => DirectionConstraint == 1 ? 0 : -Math.Min(RacketMotionLimits.Component, MaxRacketSpeed);
        public double MaxUp => DirectionConstraint == 3 ? 0 : Math.Min(RacketMotionLimits.Component, MaxRacketSpeed);
        public bool Allows(ReceiveAction action) => action.Forward >= MinForward && action.Forward <= MaxForward && action.Up >= MinUp && action.Up <= MaxUp && Math.Abs(action.Side) <= RacketMotionLimits.Component;
        // Compare the racket's tangential motion with rotation at this candidate's actual contact point.
        public static double SpinAlignment(ReceiveAction action, BallState incoming)
        {
            V3 normal = action.Normal;
            V3 rotation = V3.Cross(incoming.Spin, normal * -BallPhysics.Radius);
            V3 tangent = action.Velocity - normal * V3.Dot(action.Velocity, normal);
            if (rotation.Length < 1e-6 || tangent.Length < 1e-6) return 0;
            return V3.Dot(rotation.Unit, tangent.Unit);
        }
        public bool Allows(ReceiveAction action, BallState incoming)
        {
            if (!Allows(action)) return false;
            if (SpinDirection == SpinReceiveDirection.Any) return true;
            double alignment = SpinAlignment(action, incoming);
            return SpinDirection == SpinReceiveDirection.WithSpin ? alignment > 1e-6 : alignment < -1e-6;
        }
        public ReceiveStudySettings Copy() => (ReceiveStudySettings)MemberwiseClone();
        public void Validate()
        {
            if (RubberResponseId != 0) InvertedRubberResponses.Get(RubberResponseId);
            PhysicsSettings.Range(BallVelocityError, 0, 2, nameof(BallVelocityError));
            PhysicsSettings.Range(SpinErrorRps, 0, 50, nameof(SpinErrorRps));
            PhysicsSettings.Range(FaceErrorDegrees, 0, 10, nameof(FaceErrorDegrees));
            PhysicsSettings.Range(RacketVelocityError, 0, 2, nameof(RacketVelocityError));
            PhysicsSettings.Range(MaxNetClearance, 0.01, 1, nameof(MaxNetClearance));
            PhysicsSettings.Range(MaxRacketSpeed, 1, RacketMotionLimits.Total, nameof(MaxRacketSpeed));
            PhysicsSettings.Range(RacketRollDegrees, -180, 180, nameof(RacketRollDegrees));
            PhysicsSettings.Range((int)SpinDirection, 0, 2, nameof(SpinDirection));
            PhysicsSettings.Range(DirectionConstraint, 0, 3, nameof(DirectionConstraint));
            PhysicsSettings.Range(Candidates, 16, 1024, nameof(Candidates));
            PhysicsSettings.Range(Trials, 16, 1024, nameof(Trials));
        }
    }

    public sealed class Reception
    {
        public BallState Incoming;
        public double ContactTime;
        public readonly List<BallState> Approach = new List<BallState>();
        public readonly List<ContactEvent> Bounces = new List<ContactEvent>();

        public static Reception FromServe(FlightResult serve, PhysicsSettings p, double afterBounce)
        {
            PhysicsSettings.Range(afterBounce, 0.01, 0.5, nameof(afterBounce));
            if (!p.NetEnabled) throw new ArgumentException("接发研究需要开启球网检测。");
            if (serve.Events.Count < 2 || serve.Events[0].Kind != "球台" || serve.Events[1].Kind != "球台" ||
                serve.Events[0].Before.Position.Z >= 0 || serve.Events[1].Before.Position.Z <= 0)
                throw new ArgumentException("发球需先落左半台，再落右半台，途中不能触网。可先恢复默认发球。");
            double contactTime = serve.Events[1].After.Time + afterBounce;
            double nextEvent = serve.Events.Count > 2 ? serve.Events[2].Before.Time : serve.Samples[serve.Samples.Count - 1].Time;
            if (contactTime >= nextEvent)
                throw new ArgumentException("接球时刻已晚于下一次碰撞，请减小“弹起后接球”。");
            var result = new Reception { ContactTime = contactTime };
            BallState previous = serve.Events[1].After;
            foreach (var sample in serve.Samples)
            {
                if (sample.Time > contactTime) break;
                previous = sample;
                BallState shifted = sample; shifted.Time -= contactTime; result.Approach.Add(shifted);
            }
            result.Incoming = BallPhysics.Advance(previous, contactTime - previous.Time, p);
            result.Incoming.Time = 0;
            if (result.Incoming.Position.Z <= 0) throw new ArgumentException("请选择右半台一侧的接球点。");
            if (result.Approach[result.Approach.Count - 1].Time < -1e-10) result.Approach.Add(result.Incoming);
            for (int i = 0; i < 2; i++)
            {
                BallState before = serve.Events[i].Before, after = serve.Events[i].After;
                before.Time -= contactTime; after.Time -= contactTime;
                result.Bounces.Add(new ContactEvent("发球 " + (i + 1), before, after));
            }
            return result;
        }
    }

    public sealed class ReceiveTrial
    {
        public bool Success, HeightQualified;
        public double? NetClearance;
        public string Reason;
        public V3? Landing;
    }

    public sealed class ReceiveScore
    {
        public ReceiveAction Action;
        public int Successes, QualifiedSuccesses, Attempts;
        public double QualifiedRate => (double)QualifiedSuccesses / Attempts;
        public double Margin10, AverageSpeed;
        public double Rate => (double)Successes / Attempts;
        public readonly List<ReceiveTrial> Trials = new List<ReceiveTrial>();
        public readonly Dictionary<string, int> Failures = new Dictionary<string, int>();
    }

    public sealed class ReceiveReport
    {
        public ReceiveStudySettings Settings;
        public BallState Incoming;
        public ReceiveScore Baseline;
        public List<ReceiveScore> Ranked;
        public int EvaluatedCandidates;
    }

    public static class ReceiveStudy
    {
        readonly struct Noise
        {
            public readonly V3 Ball, Spin, Racket;
            public readonly double Pitch, Yaw;
            public Noise(Random r, ReceiveStudySettings s)
            {
                Ball = Jitter(r, s.BallVelocityError); Spin = Jitter(r, s.SpinErrorRps * BallPhysics.Rps);
                Racket = Jitter(r, s.RacketVelocityError); Pitch = Symmetric(r, s.FaceErrorDegrees); Yaw = Symmetric(r, s.FaceErrorDegrees);
            }
        }
        static double Symmetric(Random r, double size) => (r.NextDouble() * 2 - 1) * size;
        static double Clamp(double value, double low, double high) => Math.Max(low, Math.Min(high, value));
        static V3 Jitter(Random r, double size) => new V3(Symmetric(r, size), Symmetric(r, size), Symmetric(r, size));
        static Noise[] NoiseBank(int count, int seed, ReceiveStudySettings settings)
        {
            var r = new Random(seed); var bank = new Noise[count];
            for (int i = 0; i < count; i++) bank[i] = new Noise(r, settings);
            return bank;
        }

        public static ReceiveScore Evaluate(BallState incoming, PhysicsSettings physics, ReceiveStudySettings settings,
            ReceiveAction action, double restitution, double friction, double tangent, CancellationToken cancellation = default)
        {
            Validate(incoming, physics, settings, action, restitution, friction, tangent);
            return EvaluateBank(incoming, physics, settings, action, restitution, friction, tangent,
                NoiseBank(settings.Trials, unchecked(settings.Seed + 1000003), settings), true, cancellation);
        }

        public static ReceiveReport TestCurrent(BallState incoming, PhysicsSettings physics, ReceiveStudySettings settings,
            ReceiveAction action, double restitution, double friction, double tangent, CancellationToken cancellation = default)
        {
            var score = Evaluate(incoming, physics, settings, action, restitution, friction, tangent, cancellation);
            return new ReceiveReport { Settings = settings.Copy(), Incoming = incoming, Baseline = score,
                Ranked = new List<ReceiveScore> { score }, EvaluatedCandidates = 1 };
        }

        static void Validate(BallState incoming, PhysicsSettings physics, ReceiveStudySettings settings, ReceiveAction action,
            double restitution, double friction, double tangent)
        {
            settings.Validate(); physics.Validate(); BallPhysics.ValidateState(incoming);
            PhysicsSettings.Range(restitution, 0, 1, nameof(restitution)); PhysicsSettings.Range(friction, 0, 2, nameof(friction));
            PhysicsSettings.Range(tangent, -1, 1, nameof(tangent));
            if (settings.SpinDirection != SpinReceiveDirection.Any && incoming.Spin.Length < 1e-6)
                throw new ArgumentException("当前接球点无旋转，无法区分顺／逆旋转，请选择不限制。");
            if (!action.Normal.Finite || !action.Velocity.Finite || action.Velocity.Length > 100)
                throw new ArgumentException("接球动作包含无效数值。");
        }

        static ReceiveScore EvaluateBank(BallState incoming, PhysicsSettings p, ReceiveStudySettings settings,
            ReceiveAction action, double restitution, double friction, double tangent, Noise[] bank, bool keepTrials, CancellationToken cancellation)
        {
            if (!p.NetEnabled) throw new ArgumentException("稳定性评估必须开启球网检测。");
            var score = new ReceiveScore { Action = action, Attempts = bank.Length };
            var margins = new List<double>();
            foreach (var noise in bank)
            {
                cancellation.ThrowIfCancellationRequested();
                var trial = new ReceiveTrial();
                BallState perturbed = incoming; perturbed.Velocity += noise.Ball; perturbed.Spin += noise.Spin;
                var rotated = new ReceiveAction(action.Pitch + noise.Pitch, action.Yaw + noise.Yaw, action.Forward, action.Up, action.Side);
                V3 velocity = action.Velocity + noise.Racket;
                if (!settings.Allows(action, incoming)) trial.Reason = "动作超出所选接旋转思路或方向范围";
                else if (velocity.Length > settings.MaxRacketSpeed) trial.Reason = "挥拍超限";
                else if (RacketGeometry.MayTouchTable(incoming.Position, rotated.Normal, velocity, settings.RacketRollDegrees))
                    trial.Reason = "球拍可能碰台";
                else
                {
                    try
                    {
                        var outgoing = BallPhysics.Strike(perturbed, rotated.Normal, velocity, restitution, friction, tangent, settings.RubberResponseId);
                        var result = BallPhysics.Simulate(outgoing, p, -1, true);
                        trial.Reason = result.Verdict;
                        trial.Success = result.Events.Count > 0 && result.Events[0].Kind == "球台" && result.Events[0].Before.Position.Z < 0;
                        trial.NetClearance = result.NetClearance;
                        trial.HeightQualified = trial.Success && result.NetClearance.HasValue && result.NetClearance.Value >= 0 && result.NetClearance.Value <= settings.MaxNetClearance;
                        if (trial.HeightQualified) score.QualifiedSuccesses++;
                        trial.Landing = result.FirstTableHeightCrossing?.Position;
                        if (trial.Success)
                        {
                            score.Successes++; score.AverageSpeed += outgoing.Velocity.Length;
                            V3 hit = result.Events[0].Before.Position;
                            double edge = Math.Min(BallPhysics.HalfWidth - Math.Abs(hit.X), Math.Min(BallPhysics.HalfLength - Math.Abs(hit.Z), Math.Abs(hit.Z)));
                            if (trial.HeightQualified) margins.Add(Math.Min(edge, Math.Min(result.NetClearance.Value, settings.MaxNetClearance - result.NetClearance.Value)));
                        }
                    }
                    catch (ArgumentException) { trial.Reason = "未形成有效击球"; }
                }
                if (!trial.Success)
                {
                    score.Failures.TryGetValue(trial.Reason, out int count); score.Failures[trial.Reason] = count + 1;
                }
                if (keepTrials) score.Trials.Add(trial);
            }
            if (score.Successes > 0)
            {
                score.AverageSpeed /= score.Successes; margins.Sort();
                if (margins.Count > 0) score.Margin10 = margins[(int)((margins.Count - 1) * 0.1)];
            }
            return score;
        }

        static int Compare(ReceiveScore a, ReceiveScore b)
        {
            int byQuality = b.QualifiedRate.CompareTo(a.QualifiedRate);
            if (byQuality != 0) return byQuality;
            int byRate = b.Rate.CompareTo(a.Rate);
            if (byRate != 0) return byRate;
            int byMargin = b.Margin10.CompareTo(a.Margin10);
            return byMargin != 0 ? byMargin : a.Action.Velocity.Length.CompareTo(b.Action.Velocity.Length);
        }

        public static ReceiveReport Search(BallState incoming, PhysicsSettings p, ReceiveStudySettings settings, ReceiveAction baseline,
            double restitution, double friction, double tangent, CancellationToken cancellation = default, Action<int, int> progress = null)
        {
            Validate(incoming, p, settings, baseline, restitution, friction, tangent);
            // Common random numbers compare actions fairly; the separate validation bank is never used by the candidate search.
            Noise[] train = NoiseBank(32, settings.Seed, settings);
            var actions = new List<ReceiveAction> { baseline };
            foreach (double pitch in new[] { 10.0, -10, 30 })
            foreach (double yaw in new[] { 0.0, -15, 15 })
            foreach (double forward in new[] { 0.5, -1.0, 2.5 })
            foreach (double up in new[] { 1.0, 4, -2 })
            {
                var action = new ReceiveAction(pitch, yaw, forward, up, 0);
                if (actions.Count < settings.Candidates && settings.Allows(action, incoming) && action.Velocity.Length <= settings.MaxRacketSpeed) actions.Add(action);
            }
            var random = new Random(unchecked(settings.Seed + 17));
            int attempts = 0;
            while (actions.Count < settings.Candidates && attempts++ < settings.Candidates * 200)
            {
                cancellation.ThrowIfCancellationRequested();
                var action = new ReceiveAction(-20 + random.NextDouble() * 75, Symmetric(random, 35),
                    settings.MinForward + random.NextDouble() * (settings.MaxForward - settings.MinForward),
                    settings.MinUp + random.NextDouble() * (settings.MaxUp - settings.MinUp), Symmetric(random, Math.Min(RacketMotionLimits.Component, settings.MaxRacketSpeed)));
                if (settings.Allows(action, incoming) && action.Velocity.Length <= settings.MaxRacketSpeed) actions.Add(action);
            }
            if (!actions.Any(a => settings.Allows(a, incoming)))
                throw new ArgumentException("所选旋转思路与动作范围中未找到可采样方向，请放宽方向范围。");
            Comparison<ReceiveScore> compare = (a, b) => {
                int allowed = settings.Allows(b.Action, incoming).CompareTo(settings.Allows(a.Action, incoming));
                return allowed != 0 ? allowed : Compare(a, b);
            };
            var coarse = new List<ReceiveScore>();
            for (int i = 0; i < actions.Count; i++)
            {
                coarse.Add(EvaluateBank(incoming, p, settings, actions[i], restitution, friction, tangent, train, false, cancellation));
                progress?.Invoke(i + 1, actions.Count + 85);
            }
            coarse.Sort(compare);
            var leaders = coarse.Where(s => settings.Allows(s.Action, incoming)).Take(6).Select(s => s.Action).ToArray();
            foreach (var leader in leaders)
            for (int i = 0; i < 12; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var action = new ReceiveAction(Clamp(leader.Pitch + Symmetric(random, 7.5), -20, 55),
                    Clamp(leader.Yaw + Symmetric(random, 7.5), -35, 35), Clamp(leader.Forward + Symmetric(random, 0.75), settings.MinForward, settings.MaxForward),
                    Clamp(leader.Up + Symmetric(random, 0.75), settings.MinUp, settings.MaxUp), Clamp(leader.Side + Symmetric(random, 0.75), -RacketMotionLimits.Component, RacketMotionLimits.Component));
                if (!settings.Allows(action, incoming) || action.Velocity.Length > settings.MaxRacketSpeed) continue;
                coarse.Add(EvaluateBank(incoming, p, settings, action, restitution, friction, tangent, train, false, cancellation));
                progress?.Invoke(coarse.Count, actions.Count + 85);
            }
            coarse.Sort(compare);
            var finalists = coarse.Take(6).Select(s => s.Action).ToList();
            foreach (var action in leaders) if (!finalists.Contains(action)) finalists.Add(action);
            if (!finalists.Contains(baseline)) finalists.Add(baseline);
            var scores = new List<ReceiveScore>();
            Noise[] validation = NoiseBank(settings.Trials, unchecked(settings.Seed + 1000003), settings);
            foreach (var action in finalists)
            {
                scores.Add(EvaluateBank(incoming, p, settings, action, restitution, friction, tangent, validation, true, cancellation));
                progress?.Invoke(coarse.Count + scores.Count, coarse.Count + finalists.Count);
            }
            scores.Sort(compare);
            return new ReceiveReport { Settings = settings.Copy(), Incoming = incoming, Baseline = scores.First(s => s.Action.Equals(baseline)),
                Ranked = scores, EvaluatedCandidates = coarse.Count };
        }
    }
}

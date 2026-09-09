using System;
using System.Collections.Generic;
using System.Globalization;

namespace TableTennis
{
    // SI units; right-handed coordinates: X right, Y up, Z toward the opponent.
    public readonly struct V3
    {
        public readonly double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Length => Math.Sqrt(Dot(this, this));
        public bool Finite => Valid(X) && Valid(Y) && Valid(Z);
        static bool Valid(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        public V3 Unit => Length > 1e-12 ? this / Length : new V3();
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator -(V3 a) => a * -1;
        public static V3 operator *(V3 a, double b) => new V3(a.X * b, a.Y * b, a.Z * b);
        public static V3 operator /(V3 a, double b) => a * (1 / b);
        public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    public struct BallState
    {
        public V3 Position, Velocity, Spin; // Spin is rad/s, not revolutions/s.
        public double Time;
        public BallState(V3 position, V3 velocity, V3 spin, double time = 0)
        { Position = position; Velocity = velocity; Spin = spin; Time = time; }
    }

    public sealed class PhysicsSettings
    {
        public double Gravity = 9.81, AirDensity = 1.225, Drag = 0.47;
        public double LiftSlope = 0.6, LiftCap = 0.35, SpinDecay = 0.08;
        public double TableRestitution = 0.88, TableFriction = 0.20;
        public double Step = 0.001, Duration = 4;
        public bool NetEnabled = true;
        public PhysicsSettings Copy() => (PhysicsSettings)MemberwiseClone();
        public void Validate()
        {
            Range(Gravity, 0, 30, nameof(Gravity)); Range(AirDensity, 0, 3, nameof(AirDensity));
            Range(Drag, 0, 2, nameof(Drag)); Range(LiftSlope, 0, 3, nameof(LiftSlope));
            Range(LiftCap, 0, 2, nameof(LiftCap)); Range(SpinDecay, 0, 5, nameof(SpinDecay));
            Range(TableRestitution, 0, 1, nameof(TableRestitution)); Range(TableFriction, 0, 2, nameof(TableFriction));
            Range(Step, 0.0001, 0.01, nameof(Step)); Range(Duration, 0.01, 10, nameof(Duration));
        }
        public static void Range(double value, double low, double high, string name)
        {
            if (double.IsNaN(value) || value < low || value > high)
                throw new ArgumentOutOfRangeException(name, $"Expected {low}..{high}");
        }
        public static bool TryNumber(string text, double low, double high, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !double.IsNaN(value) && value >= low && value <= high;
    }

    public sealed class ContactEvent
    {
        public string Kind;
        public BallState Before, After;
        public ContactEvent(string kind, BallState before, BallState after)
        { Kind = kind; Before = before; After = after; }
    }

    public sealed class FlightResult
    {
        public readonly List<BallState> Samples = new List<BallState>();
        public readonly List<ContactEvent> Events = new List<ContactEvent>();
        public string EndReason = "时长上限";
        public double? NetClearance;
        public BallState? FirstTableHeightCrossing;
        public int TargetSide = 1;
        public string Verdict
        {
            get
            {
                if (Events.Count > 0 && Events[0].Kind == "触网") return "触网";
                if (Events.Count > 0 && Events[0].Kind == "球台")
                    return Events[0].Before.Position.Z * TargetSide > 0 ? "落入对方半台" : "先落本方半台";
                if (FirstTableHeightCrossing.HasValue)
                {
                    V3 p = FirstTableHeightCrossing.Value.Position;
                    if (Math.Abs(p.X) > BallPhysics.HalfWidth) return "侧出界";
                    if (p.Z * TargetSide > BallPhysics.HalfLength) return "长出台";
                    return "未到对方半台";
                }
                return Events.Count > 0 ? "未落台" : "模拟时长内未落台";
            }
        }
    }

    public static class BallPhysics
    {
        public const double Radius = 0.02, Mass = 0.0027;
        public const double TableHeight = 0.76, HalfWidth = 0.7625, HalfLength = 1.37, NetHeight = 0.1525;
        // Thin hollow shell, not a solid sphere.
        public const double Inertia = (2.0 / 3) * Mass * Radius * Radius;
        public const double Rps = 2 * Math.PI;

        public static void ValidateState(BallState s)
        {
            if (!s.Position.Finite || !s.Velocity.Finite || !s.Spin.Finite ||
                double.IsNaN(s.Time) || double.IsInfinity(s.Time) || s.Position.Length > 100 ||
                s.Velocity.Length > 100 || s.Spin.Length > 500 * Rps)
                throw new ArgumentException("Ball state is non-finite or outside simulator limits (100 m, 100 m/s, 500 rps).");
        }

        public static V3 Acceleration(V3 velocity, V3 spin, PhysicsSettings p)
        {
            double speed = velocity.Length;
            V3 force = new V3(0, -p.Gravity, 0);
            if (speed < 1e-10) return force;
            double scale = 0.5 * p.AirDensity * Math.PI * Radius * Radius / Mass;
            force -= velocity * (scale * p.Drag * speed);
            V3 cross = V3.Cross(spin, velocity);
            double spinRatio = Radius * cross.Length / (speed * speed);
            // ponytail: empirical saturation and constant Cd; fit measured flight data before claiming calibrated aerodynamics.
            double cl = p.LiftCap == 0 ? 0 : p.LiftCap * (1 - Math.Exp(-p.LiftSlope * spinRatio / p.LiftCap));
            return force + cross.Unit * (scale * cl * speed * speed);
        }

        // RK4 free flight; spin decay is integrated analytically at the RK stages.
        public static BallState Advance(BallState s, double dt, PhysicsSettings p)
        {
            V3 halfSpin = s.Spin * Math.Exp(-p.SpinDecay * dt * 0.5);
            V3 endSpin = s.Spin * Math.Exp(-p.SpinDecay * dt);
            V3 a1 = Acceleration(s.Velocity, s.Spin, p);
            V3 v2 = s.Velocity + a1 * (dt * 0.5);
            V3 a2 = Acceleration(v2, halfSpin, p);
            V3 v3 = s.Velocity + a2 * (dt * 0.5);
            V3 a3 = Acceleration(v3, halfSpin, p);
            V3 v4 = s.Velocity + a3 * dt;
            V3 a4 = Acceleration(v4, endSpin, p);
            return new BallState(s.Position + (s.Velocity + v2 * 2 + v3 * 2 + v4) * (dt / 6),
                s.Velocity + (a1 + a2 * 2 + a3 * 2 + a4) * (dt / 6), endSpin, s.Time + dt);
        }

        // Prescribed infinite-mass surface. Surface velocity must be the velocity at the contact point.
        public static BallState Contact(BallState s, V3 normal, V3 surfaceVelocity,
            double restitution, double friction, double tangentRestitution = 0)
        {
            ValidateState(s);
            PhysicsSettings.Range(restitution, 0, 1, nameof(restitution));
            PhysicsSettings.Range(friction, 0, 2, nameof(friction));
            PhysicsSettings.Range(tangentRestitution, -1, 1, nameof(tangentRestitution));
            if (!normal.Finite || normal.Length < 1e-10 || !surfaceVelocity.Finite || surfaceVelocity.Length > 100)
                throw new ArgumentException("Invalid contact normal or surface velocity.");
            normal = normal.Unit;
            V3 arm = normal * -Radius;
            V3 relative = s.Velocity + V3.Cross(s.Spin, arm) - surfaceVelocity;
            double vn = V3.Dot(relative, normal);
            if (vn >= 0) return s;
            double jn = -(1 + restitution) * Mass * vn;
            V3 tangent = relative - normal * vn;
            // beta=-1 preserves tangential slip (zero tangential impulse); beta>0 allows elastic reversal.
            double jt = Math.Min((1 + tangentRestitution) * tangent.Length / (1 / Mass + Radius * Radius / Inertia), friction * jn);
            V3 impulse = normal * jn - tangent.Unit * jt;
            s.Velocity += impulse / Mass;
            s.Spin += V3.Cross(arm, impulse) / Inertia;
            return s;
        }

        public static BallState Strike(BallState incoming, V3 normal, V3 racketVelocity,
            double restitution, double friction, double tangentRestitution, int responseId = 0)
        {
            if (responseId != 0)
            {
                var c = InvertedRubberResponses.Get(responseId).At(incoming, normal, racketVelocity, out _);
                restitution = c.Restitution; friction = c.Friction; tangentRestitution = c.Tangent;
            }
            BallState outgoing = Contact(incoming, normal, racketVelocity, restitution, friction, tangentRestitution);
            if ((outgoing.Velocity - incoming.Velocity).Length < 1e-10)
                throw new ArgumentException("球与拍面没有相向接近，请调整来球、挥拍或拍面方向。");
            return outgoing;
        }

        static double PlaneTime(BallState s, double dt, double height, PhysicsSettings p)
        {
            double low = 0, high = 1;
            for (int i = 0; i < 28; i++)
            {
                double middle = (low + high) * 0.5;
                if (Advance(s, middle * dt, p).Position.Y > height) low = middle; else high = middle;
            }
            return high * dt;
        }

        public static List<BallState> PreviewIncoming(BallState contact, PhysicsSettings p, double duration = 0.12)
        {
            p.Validate(); ValidateState(contact); PhysicsSettings.Range(duration, 0, 0.25, nameof(duration));
            var samples = new List<BallState> { contact };
            BallState s = contact;
            double start = contact.Time - duration;
            // ponytail: reconstruct only the last free-flight segment; earlier contacts require measured incoming trajectory data.
            while (s.Time > start + 1e-10)
            {
                double dt = -Math.Min(p.Step, s.Time - start);
                BallState next = Advance(s, dt, p);
                if (!next.Position.Finite || !next.Velocity.Finite || !next.Spin.Finite || next.Position.Length > 100 ||
                    next.Velocity.Length > 100 || next.Spin.Length > 500 * Rps) break;
                if (next.Position.Y <= Radius || (p.NetEnabled && NetFraction(s.Position, next.Position) <= 1)) break;
                if (s.Position.Y >= TableHeight + Radius && next.Position.Y <= TableHeight + Radius &&
                    OnTable(Advance(s, PlaneTime(s, dt, TableHeight + Radius, p), p).Position)) break;
                samples.Add(next); s = next;
            }
            samples.Reverse(); return samples;
        }

        // Swept sphere against an expanded net box; touching the net ends this flight.
        static double NetFraction(V3 a, V3 b)
        {
            double low = 0, high = 1;
            if (!Slab(a.X, b.X - a.X, -HalfWidth - NetHeight - Radius, HalfWidth + NetHeight + Radius, ref low, ref high) ||
                !Slab(a.Y, b.Y - a.Y, TableHeight - Radius, TableHeight + NetHeight + Radius, ref low, ref high) ||
                !Slab(a.Z, b.Z - a.Z, -Radius - 0.001, Radius + 0.001, ref low, ref high)) return double.PositiveInfinity;
            return low;
        }

        static bool Slab(double start, double delta, double min, double max, ref double low, ref double high)
        {
            if (Math.Abs(delta) < 1e-12) return start >= min && start <= max;
            double a = (min - start) / delta, b = (max - start) / delta;
            low = Math.Max(low, Math.Min(a, b)); high = Math.Min(high, Math.Max(a, b));
            return low <= high;
        }

        public static FlightResult Simulate(BallState initial, PhysicsSettings p, int targetSide = 1, bool firstContactOnly = false)
        {
            p.Validate(); ValidateState(initial);
            if (targetSide != 1 && targetSide != -1) throw new ArgumentException("Target side must be +1 or -1.");
            if (initial.Position.Y < Radius || (OnTable(initial.Position) && initial.Position.Y < TableHeight + Radius - 1e-8))
                throw new ArgumentException("初始球心不能位于地面或球台内部。");
            var result = new FlightResult { TargetSide = targetSide };
            BallState s = initial;
            result.Samples.Add(s);
            double end = s.Time + p.Duration;
            int bounces = 0;
            while (s.Time < end - 1e-10)
            {
                double dt = Math.Min(p.Step, end - s.Time);
                BallState next = Advance(s, dt, p);
                double hitTime = double.PositiveInfinity;
                double planeTime = double.PositiveInfinity;
                BallState? planeCrossing = null;
                string kind = null;
                if (s.Position.Y >= TableHeight + Radius - 1e-8 && next.Position.Y <= TableHeight + Radius && next.Velocity.Y < 0)
                {
                    double t = PlaneTime(s, dt, TableHeight + Radius, p);
                    planeCrossing = Advance(s, t, p); planeTime = t;
                    if (OnTable(planeCrossing.Value.Position)) { hitTime = t; kind = "球台"; }
                }
                if (next.Position.Y <= Radius)
                {
                    double t = PlaneTime(s, dt, Radius, p);
                    if (t < hitTime) { hitTime = t; kind = "地面"; }
                }
                if (p.NetEnabled)
                {
                    // ponytail: linear sweep over <= 10 ms and rounded-corner overestimate; refine to deformable net only for net-play work.
                    double t = NetFraction(s.Position, next.Position) * dt;
                    if (t < hitTime) { hitTime = t; kind = "触网"; }
                }
                BallState before = kind == null ? next : Advance(s, hitTime, p);
                if (!result.FirstTableHeightCrossing.HasValue && planeTime <= hitTime && planeCrossing.HasValue)
                    result.FirstTableHeightCrossing = planeCrossing;
                if (!result.NetClearance.HasValue && s.Position.Z * before.Position.Z <= 0 && Math.Abs(before.Position.Z - s.Position.Z) > 1e-12 && Math.Abs(s.Position.Z) > 1e-12)
                {
                    double t = -s.Position.Z / (before.Position.Z - s.Position.Z);
                    result.NetClearance = s.Position.Y + (before.Position.Y - s.Position.Y) * t - TableHeight - NetHeight - Radius;
                }
                if (kind == null) { s = next; if (!firstContactOnly) result.Samples.Add(s); continue; }
                result.Samples.Add(before);
                if (kind != "球台")
                {
                    result.Events.Add(new ContactEvent(kind, before, before));
                    result.EndReason = kind; return result;
                }
                before.Position = new V3(before.Position.X, TableHeight + Radius, before.Position.Z);
                s = Contact(before, new V3(0, 1, 0), new V3(), p.TableRestitution, p.TableFriction);
                result.Events.Add(new ContactEvent(kind, before, s));
                result.Samples.Add(s);
                if (firstContactOnly) { result.EndReason = "首次落台"; return result; }
                // Restart a full step from the exact impact time; no time or travel is discarded.
                if (++bounces >= 4 || s.Velocity.Y < 0.03)
                { result.EndReason = "台面停留 / 反弹上限"; return result; }
            }
            return result;
        }

        public static bool OnTable(V3 p) => Math.Abs(p.X) <= HalfWidth && Math.Abs(p.Z) <= HalfLength;
        public static double Energy(BallState s) => 0.5 * Mass * V3.Dot(s.Velocity, s.Velocity) + 0.5 * Inertia * V3.Dot(s.Spin, s.Spin);
    }
}

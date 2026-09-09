using System;
using System.Collections.Generic;

namespace TableTennis
{
    public sealed class ServeStrokeResult
    {
        public readonly List<BallState> Approach = new List<BallState>();
        public ServeContact Contact;
        public FlightResult Flight;
        public bool FrontFace, MayTouchTable;
        public string MissReason = "挥拍没有碰到球";
        public bool TwoBounces => Flight != null && Flight.Events.Count >= 2 &&
            Flight.Events[0].Kind == "球台" && Flight.Events[0].Before.Position.Z < 0 &&
            Flight.Events[1].Kind == "球台" && Flight.Events[1].Before.Position.Z > 0;
    }

    // Prescribed fixed-angle, straight stroke. Time zero is the apex of a zero-spin toss.
    public sealed class ServeStroke
    {
        public const double FaceOffset = 0.004, FaceWidth = 0.075, FaceHeight = 0.085;
        public V3 Apex = new V3(0, 1.55, -1.65);
        public V3 Start = new V3(0, 1.0264, -2.1591), End = new V3(0, 0.8464, -1.2591);
        public double StartTime = 0.25, Speed = Math.Sqrt(26), TableAngle = 10, Yaw, Roll = -70;
        public double Restitution = 0.8, Friction = 0.8, Tangent = 0.15;
        public bool BackFace;
        public int ResponseId;
        public ServeStroke Copy() => (ServeStroke)MemberwiseClone();
        public V3 Normal
        {
            get
            {
                double a = TableAngle * Math.PI / 180, y = Yaw * Math.PI / 180;
                return new V3(Math.Sin(a) * Math.Sin(y), Math.Cos(a), Math.Sin(a) * Math.Cos(y)) * (BackFace ? -1 : 1);
            }
        }
        public double PathLength => (End - Start).Length;
        public double Duration => Speed > 0 ? PathLength / Speed : 0;
        public V3 Velocity => (End - Start).Unit * Speed;
        public V3 CenterAt(double t) => Duration > 0 ? Start + (End - Start) * Math.Max(0, Math.Min(1, (t - StartTime) / Duration)) : (Start + End) * 0.5;
        public V3 VelocityAt(double t) => t >= StartTime && t < StartTime + Duration ? Velocity : new V3();

        // Aim a prescribed straight stroke through the falling ball; the collision solver still decides the hit.
        public BallState AlignContact(PhysicsSettings p, double height)
        {
            Validate(); p.Validate(); PhysicsSettings.Range(height, 0.8, 3, nameof(height));
            if (height >= Apex.Y) throw new ArgumentException("击球高度应低于抛球顶点。");
            var s = new BallState(Apex, new V3(), new V3());
            while (s.Time < p.Duration)
            {
                double dt = Math.Min(p.Step, p.Duration - s.Time);
                var next = BallPhysics.Advance(s, dt, p);
                if (next.Position.Y <= height)
                {
                    double low = 0, high = dt;
                    for (int i = 0; i < 30; i++)
                    { double mid = (low + high) * 0.5; if (BallPhysics.Advance(s, mid, p).Position.Y > height) low = mid; else high = mid; }
                    var hit = BallPhysics.Advance(s, high, p);
                    V3 normal = Normal * (BackFace ? -1 : 1);
                    V3 center = hit.Position - normal * (BallPhysics.Radius + FaceOffset);
                    // Keep the chosen path and its midpoint contact; slower strokes can begin before the toss apex.
                    V3 shift = center - (Start + End) * 0.5;
                    Start += shift; End += shift;
                    StartTime = hit.Time - Duration * 0.5;
                    return hit;
                }
                s = next;
            }
            throw new ArgumentException("模拟时长内球未落到指定击球高度。");
        }

        public void Validate()
        {
            foreach (V3 p in new[] { Apex, Start, End })
                if (!p.Finite || Math.Abs(p.X) > 3 || p.Y < 0.1 || p.Y > 4 || Math.Abs(p.Z) > 4)
                    throw new ArgumentException("球和球拍位置超出实验范围。");
            PhysicsSettings.Range(StartTime, -60, 2, nameof(StartTime));
            PhysicsSettings.Range(Speed, 0, 25, nameof(Speed));
            PhysicsSettings.Range(Duration, 0, 60, nameof(Duration));
            if (Speed > 0 && PathLength < 1e-6) throw new ArgumentException("请拉开挥拍起终点，或将速度设为 0。");
            PhysicsSettings.Range(TableAngle, 0, 180, nameof(TableAngle));
            PhysicsSettings.Range(Yaw, -180, 180, nameof(Yaw)); PhysicsSettings.Range(Roll, -180, 180, nameof(Roll));
            PhysicsSettings.Range(Restitution, 0, 1, nameof(Restitution)); PhysicsSettings.Range(Friction, 0, 2, nameof(Friction));
            PhysicsSettings.Range(Tangent, -1, 1, nameof(Tangent));
            if (ResponseId != 0) InvertedRubberResponses.Get(ResponseId);
        }

        bool OnFace(V3 offset)
        {
            RacketGeometry.Axes(Normal, Roll, out V3 right, out V3 up);
            double x = V3.Dot(offset, right) / FaceWidth, y = V3.Dot(offset, up) / FaceHeight;
            // ponytail: finite rubber faces only; rim/handle impacts need separate collision shapes.
            return x * x + y * y <= 1;
        }

        public ServeStrokeResult Simulate(PhysicsSettings p)
        {
            Validate(); p.Validate();
            var initial = new BallState(Apex, new V3(), new V3());
            var fall = BallPhysics.Simulate(initial, p, 1, true);
            double end = fall.Events.Count > 0 ? fall.Events[0].Before.Time : p.Duration;
            var result = new ServeStrokeResult();
            V3 n = Normal;
            double radius = BallPhysics.Radius + FaceOffset;
            V3 initialOffset = Apex - CenterAt(0);
            if (Math.Abs(V3.Dot(initialOffset, n)) < radius && OnFace(initialOffset))
                throw new ArgumentException("起始球与胶皮重叠，请移动挥拍起点或抬高球。");
            // Conservative table screen over the whole stroke, including its handle.
            for (double t = StartTime; t <= StartTime + Duration + 0.004; t += 0.004)
                result.MayTouchTable |= RacketGeometry.MayTouchTable(CenterAt(t) + n * radius, n, Velocity, Roll);
            BallState s = initial;
            result.Approach.Add(s);
            while (s.Time < end - 1e-10)
            {
                double dt = Math.Min(p.Step, end - s.Time);
                foreach (double boundary in new[] { StartTime, StartTime + Duration })
                    if (boundary > s.Time + 1e-10) dt = Math.Min(dt, boundary - s.Time);
                var next = BallPhysics.Advance(s, dt, p);
                double d0 = V3.Dot(s.Position - CenterAt(s.Time), n), d1 = V3.Dot(next.Position - CenterAt(next.Time), n);
                foreach (int side in new[] { 1, -1 })
                {
                    if (side * d0 < radius - 1e-10 || side * d1 > radius) continue;
                    double low = 0, high = dt;
                    for (int i = 0; i < 30; i++)
                    {
                        double mid = (low + high) * 0.5;
                        var m = BallPhysics.Advance(s, mid, p);
                        if (side * V3.Dot(m.Position - CenterAt(m.Time), n) > radius) low = mid; else high = mid;
                    }
                    var hit = BallPhysics.Advance(s, high, p);
                    if (hit.Time >= end - 1e-9 || !OnFace(hit.Position - CenterAt(hit.Time))) continue;
                    V3 outward = n * side, surface = VelocityAt(s.Time + dt * 0.5);
                    if (V3.Dot(hit.Velocity - surface, outward) >= 0) continue;
                    var after = BallPhysics.Strike(hit, outward, surface, Restitution, Friction, Tangent, ResponseId);
                    BallPhysics.ValidateState(after);
                    V3 relative = surface - hit.Velocity - V3.Cross(hit.Spin, -outward * BallPhysics.Radius);
                    result.Contact = new ServeContact { Before = hit, After = after, Normal = outward, RacketVelocity = surface,
                        BrushVelocity = relative - outward * V3.Dot(relative, outward) };
                    result.FrontFace = side == 1; result.Approach.Add(hit);
                    result.Flight = BallPhysics.Simulate(after, p);
                    return result;
                }
                result.Approach.Add(next); s = next;
            }
            result.MissReason = fall.Events.Count > 0 ? "漏球：击拍前先碰到" + fall.Events[0].Kind : "模拟时长内未击中球";
            return result;
        }
    }
}

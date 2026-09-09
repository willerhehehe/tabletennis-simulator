using System;

namespace TableTennis
{
    // Spin components are rps, viewed along the ball's horizontal travel direction.
    public static class BallSpin
    {
        public static V3 Forward(V3 travel)
        {
            V3 horizontal = new V3(travel.X, 0, travel.Z);
            return horizontal.Length > 1e-9 ? horizontal.Unit : new V3(0, 0, 1);
        }
        public static V3 Compose(V3 components, V3 travel)
        {
            V3 forward = Forward(travel), right = V3.Cross(new V3(0, 1, 0), forward);
            return right * components.X + new V3(0, components.Y, 0) + forward * components.Z;
        }
        public static V3 Components(V3 spin, V3 travel)
        {
            V3 forward = Forward(travel), right = V3.Cross(new V3(0, 1, 0), forward);
            return new V3(V3.Dot(spin, right), spin.Y, V3.Dot(spin, forward));
        }
        // 0 no spin, 1 top, 2 back, 3 side, 4 side-top, 5 side-back, 6 custom axial spin.
        public static int Kind(V3 c)
        {
            if (c.Length < 0.01) return 0;
            if (Math.Abs(c.Z) > 0.01) return 6;
            if (Math.Abs(c.Y) < 0.01) return c.X > 0 ? 1 : 2;
            return Math.Abs(c.X) < 0.01 ? 3 : c.X > 0 ? 4 : 5;
        }
        public static V3 Preset(int kind, double strength, double sideShare, int sideSign, V3 travel)
        {
            PhysicsSettings.Range(kind, 0, 5, nameof(kind)); PhysicsSettings.Range(strength, 0, 500, nameof(strength));
            PhysicsSettings.Range(sideShare, 0, 1, nameof(sideShare));
            if (sideSign != -1 && sideSign != 1) throw new ArgumentException("Side sign must be -1 or +1.");
            if (kind == 0) return new V3();
            double top = kind == 1 ? 1 : kind == 2 ? -1 : kind == 4 ? 1 - sideShare : kind == 5 ? sideShare - 1 : 0;
            double side = kind == 3 ? sideSign : kind >= 4 ? sideShare * sideSign : 0;
            return Compose(new V3(top, side, 0).Unit * strength, travel);
        }
    }

    public sealed class ServeContact
    {
        public const double MaxTossVerticalSpeed = 5;
        public BallState Before, After;
        public V3 Normal, RacketVelocity, BrushVelocity;
        public double RequiredFriction;
        public V3 Point => Before.Position - Normal * BallPhysics.Radius;

        public static ServeContact Solve(BallState target, double restitution, double friction, double tangent, int responseId = 0)
        {
            BallPhysics.ValidateState(target);
            if (responseId != 0) InvertedRubberResponses.Get(responseId);
            PhysicsSettings.Range(restitution, 0, 1, nameof(restitution)); PhysicsSettings.Range(friction, 0, 2, nameof(friction));
            PhysicsSettings.Range(tangent, -1, 1, nameof(tangent));
            if (new V3(target.Velocity.X, 0, target.Velocity.Z).Length < 1e-6)
                throw new ArgumentException("此示例需要向前出球速度。");
            // ponytail: bounded vertical, zero-spin toss examples; add measured pre-contact velocity/spin for coaching.
            var candidates = new System.Collections.Generic.List<double> { -1.5 };
            if (Math.Abs(target.Spin.Y) > 1e-9)
                candidates.Add(V3.Dot(target.Spin, target.Velocity) / target.Spin.Y);
            candidates.Add(target.Velocity.Y);
            for (double speed = 0; speed <= MaxTossVerticalSpeed; speed += 0.5)
            { candidates.Add(-speed); if (speed > 0) candidates.Add(speed); }
            foreach (double tossY in candidates)
            {
                if (double.IsNaN(tossY) || Math.Abs(tossY) > MaxTossVerticalSpeed) continue;
                try { return SolveAtToss(target, restitution, friction, tangent, tossY, responseId); }
                catch (ArgumentException) { }
            }
            throw new ArgumentException($"在无旋、无水平速度、竖直速度 ±{MaxTossVerticalSpeed:0} m/s 的抛球示例范围内，未找到满足当前胶皮参数的匹配动作；这不代表真实发球做不到。");
        }

        static double InvertImpulse(Func<double, double> impulse, double target)
        {
            if (target < 1e-12) return 0;
            double low = 0, high = 100;
            if (impulse(high) < target) throw new ArgumentException("目标超出此响应模型的反算范围。");
            for (int i = 0; i < 55; i++)
            { double mid = (low + high) * 0.5; if (impulse(mid) < target) low = mid; else high = mid; }
            return (low + high) * 0.5;
        }

        internal static ServeContact SolveAtToss(BallState target, double restitution, double friction, double tangent, double tossY, int responseId)
        {
            var before = new BallState(target.Position, new V3(0, tossY, 0), new V3());
            V3 dv = target.Velocity - before.Velocity;
            if (dv.Length < 1e-6) throw new ArgumentException("此示例需要向前出球速度。");
            if (Math.Abs(V3.Dot(target.Spin, dv.Unit)) > 1e-5)
                throw new ArgumentException("该抛球速度与目标角冲量不兼容。");
            V3 perpendicular = V3.Cross(target.Spin, dv) * (BallPhysics.Inertia / (BallPhysics.Mass * BallPhysics.Radius * dv.Length * dv.Length));
            double squared = V3.Dot(perpendicular, perpendicular);
            if (squared >= 1) throw new ArgumentException("旋转相对线速度过强，超出这组抛球假设的单次碰撞范围。");
            V3 normal = perpendicular + dv.Unit * Math.Sqrt(1 - squared);
            double vn = V3.Dot(dv, normal);
            V3 tangentDelta = dv - normal * vn;
            double needed = tangentDelta.Length / vn;
            var response = responseId == 0 ? null : InvertedRubberResponses.Get(responseId);
            if (response != null) friction = response.Friction;
            if (needed > friction + 1e-9)
                throw new ArgumentException($"这组抛球假设需摩擦 μ ≥ {needed:0.00}，当前 μ = {friction:0.00}。请减弱旋转或提高线速度。");
            V3 racket;
            if (response != null)
            {
                double normalSpeed = InvertImpulse(x => x * (1 + response.At(x, 0).Restitution), vn);
                double slipSpeed = InvertImpulse(x => x * (1 + response.At(normalSpeed, x).Tangent) / 2.5, tangentDelta.Length);
                racket = before.Velocity + normal * normalSpeed + tangentDelta.Unit * slipSpeed;
            }
            else
            {
            if (tangent <= -1 + 1e-12 && tangentDelta.Length > 1e-10)
                throw new ArgumentException("切向作用为零，不能由无旋抛球生成目标旋转。");
            V3 tangentVelocity = tangent <= -1 + 1e-12 ? new V3() : tangentDelta *
                ((1 + BallPhysics.Mass * BallPhysics.Radius * BallPhysics.Radius / BallPhysics.Inertia) / (1 + tangent));
            racket = before.Velocity + normal * (vn / (1 + restitution)) +
                tangentVelocity;
            }
            var after = BallPhysics.Strike(before, normal, racket, restitution, friction, tangent, responseId);
            if ((after.Velocity - target.Velocity).Length > 1e-7 || (after.Spin - target.Spin).Length > 1e-6)
                throw new ArgumentException("该触球示例未能复现目标，暂不显示动作。");
            V3 relative = racket - before.Velocity;
            return new ServeContact { Before = before, After = after, Normal = normal, RacketVelocity = racket,
                BrushVelocity = relative - normal * V3.Dot(relative, normal), RequiredFriction = needed };
        }
    }
}

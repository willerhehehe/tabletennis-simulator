using System;

namespace TableTennis
{
    // Synthetic test input, not measured player or equipment data.
    public static class ForehandBackspin
    {
        public const float Speed = 5, Descent = 8, SpinRps = 40;
        public const float X = 0.35f, Y = 1, Z = -1.65f;
        public const float FacePitch = 10, Forward = 1, Up = 6, WeakUp = 4.5f;
        public const float Restitution = 0.8f, Friction = 0.8f, Tangent = 0.15f;
        public static BallState Incoming => new BallState(new V3(X, Y, Z),
            new V3(0, -Speed * Math.Sin(Descent * Math.PI / 180), -Speed * Math.Cos(Descent * Math.PI / 180)),
            new V3(SpinRps * BallPhysics.Rps, 0, 0));
        public static V3 Normal => new V3(0, Math.Sin(FacePitch * Math.PI / 180), Math.Cos(FacePitch * Math.PI / 180));
        public static BallState Strike(double upwardSpeed = Up) => BallPhysics.Strike(Incoming, Normal,
            new V3(0, upwardSpeed, Forward), Restitution, Friction, Tangent);
    }
}

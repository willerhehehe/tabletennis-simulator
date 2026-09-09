using System;

namespace TableTennis
{
    // Right-handed banana-flick contact example, not measured wrist/arm motion.
    // Incoming ball is still backspin at a point over the near half of the table.
    public static class BackhandFlick
    {
        public const float Speed = 3, Descent = 18, SpinRps = 40;
        public const float X = -0.25f, Y = 1.02f, Z = -0.8f;
        public const float FacePitch = 10, FaceYaw = -20, Roll = 110;
        public const float Forward = 2, Up = 5, Side = 3;
        public static RubberCalibration Rubber => RubberCalibrations.All[0];
        public static BallState Incoming => new BallState(new V3(X,Y,Z),
            new V3(0,-Speed*Math.Sin(Descent*Math.PI/180),-Speed*Math.Cos(Descent*Math.PI/180)),
            new V3(SpinRps*BallPhysics.Rps,0,0));
        public static V3 Normal
        {
            get { double p=FacePitch*Math.PI/180, y=FaceYaw*Math.PI/180;
                return new V3(Math.Sin(y)*Math.Cos(p),Math.Sin(p),Math.Cos(y)*Math.Cos(p)); }
        }
        public static V3 Velocity => new V3(Side,Up,Forward);
        public static BallState Strike(double up = Up) => BallPhysics.Strike(Incoming,Normal,
            new V3(Side,up,Forward),Rubber.Restitution,Rubber.Friction,Rubber.Tangent);
    }
}

using System;
using System.Collections.Generic;

namespace TableTennis
{
    public readonly struct RubberCoefficients
    {
        public readonly double Restitution, Friction, Tangent;
        public RubberCoefficients(double e, double mu, double beta) { Restitution=e; Friction=mu; Tangent=beta; }
    }

    // Effective response of a measured blade+sponge+topsheet combination, not layer material constants.
    public sealed class InvertedRubberResponse
    {
        public readonly int Id, TestCount;
        public readonly double NormalMin, NormalMax, SlipMin, SlipMax, Friction;
        public readonly double VelocityError, SpinErrorRps;
        readonly double normalCenter, normalScale, slipCenter, slipScale, e0, e1, b0, b1, b2;
        public InvertedRubberResponse(int id, double normalMin, double normalMax, double slipMin, double slipMax,
            double nc, double ns, double sc, double ss, double mu, double e0, double e1, double b0, double b1, double b2,
            double velocityError, double spinError, int testCount)
        {
            Id=id; NormalMin=normalMin; NormalMax=normalMax; SlipMin=slipMin; SlipMax=slipMax;
            normalCenter=nc; normalScale=ns; slipCenter=sc; slipScale=ss; Friction=mu;
            this.e0=e0; this.e1=e1; this.b0=b0; this.b1=b1; this.b2=b2;
            VelocityError=velocityError; SpinErrorRps=spinError; TestCount=testCount;
        }
        static double Clamp(double x,double low,double high) => Math.Max(low,Math.Min(high,x));
        public bool Covers(double normalSpeed,double slipSpeed) => normalSpeed>=NormalMin && normalSpeed<=NormalMax && slipSpeed>=SlipMin && slipSpeed<=SlipMax;
        public RubberCoefficients At(double normalSpeed,double slipSpeed)
        {
            if(double.IsNaN(normalSpeed)||double.IsInfinity(normalSpeed)||normalSpeed<0||double.IsNaN(slipSpeed)||double.IsInfinity(slipSpeed)||slipSpeed<0)
                throw new ArgumentException("碰撞速度必须为有限非负数。");
            // Boundary clamping controls extrapolation; it does not validate out-of-domain shots.
            double n=(Clamp(normalSpeed,NormalMin,NormalMax)-normalCenter)/normalScale;
            double s=(Clamp(slipSpeed,SlipMin,SlipMax)-slipCenter)/slipScale;
            return new RubberCoefficients(Clamp(e0+e1*n,0,1),Friction,Clamp(b0+b1*n+b2*s,-1,1));
        }
        public RubberCoefficients At(BallState ball,V3 normal,V3 racketVelocity,out bool inRange)
        {
            normal=normal.Unit;
            V3 relative=ball.Velocity+V3.Cross(ball.Spin,-normal*BallPhysics.Radius)-racketVelocity;
            double vn=V3.Dot(relative,normal), speed=Math.Max(0,-vn), slip=(relative-normal*vn).Length;
            inRange=Covers(speed,slip); return At(speed,slip);
        }
        public string Key => "tt_rackets-R"+Id.ToString("00")+"-response-v1";
    }
    public static partial class InvertedRubberResponses
    {
        public static InvertedRubberResponse Find(int id)
        { foreach(var p in All)if(p.Id==id)return p; return null; }
        public static InvertedRubberResponse Get(int id) => Find(id) ?? throw new ArgumentException("未知的反胶响应配置："+id);
    }
}

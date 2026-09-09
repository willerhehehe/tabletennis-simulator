using System;
using System.Globalization;
using System.IO;
using TableTennis;

static class RubberResponseChecks
{
    public static void Run()
    {
        void Require(bool ok,string message) { if(!ok)throw new Exception(message); }
        double Number(string s)=>double.Parse(s,CultureInfo.InvariantCulture);
        V3 Rotate(V3 v)=>new V3(v.X,-v.Z,v.Y);
        var golden=File.ReadAllLines("Calibration/rubber/response-test-predictions.csv");int row=1,total=0;
        foreach(var model in InvertedRubberResponses.All)
        {
            var data=File.ReadAllLines($"Calibration/rubber/source/data/test/racket_{model.Id:00}.csv");
            double ev=0,ew=0;
            for(int i=1;i<data.Length;i++)
            {
                var a=Array.ConvertAll(data[i].Split(','),Number);var expected=Array.ConvertAll(golden[row++].Split(','),Number);
                var s=new BallState(new V3(),new V3(a[8],a[9],a[10]),new V3(a[2],a[3],a[4]));
                var o=BallPhysics.Strike(s,new V3(0,0,1),new V3(),.8,.8,.15,model.Id);
                Require((int)expected[0]==model.Id && (int)expected[1]==i-1 && Math.Abs(o.Velocity.X-expected[2])<1e-9 && Math.Abs(o.Velocity.Z-expected[3])<1e-9 && Math.Abs(o.Spin.Y-expected[4])<1e-8,"Response Python/C# mismatch");
                Require(BallPhysics.Energy(o)<=BallPhysics.Energy(s)+1e-9,"Passive speed response added energy");
                var boost=new V3(2,3,4);var transformed=new BallState(new V3(),Rotate(s.Velocity)+boost,Rotate(s.Spin));
                var rotated=BallPhysics.Strike(transformed,Rotate(new V3(0,0,1)),boost,.8,.8,.15,model.Id);
                Require((rotated.Velocity-(Rotate(o.Velocity)+boost)).Length<1e-8 && (rotated.Spin-Rotate(o.Spin)).Length<1e-7,"Moving racket frame or rotational invariance failed");
                ev+=Math.Sqrt(Math.Pow(o.Velocity.X-a[11],2)+Math.Pow(o.Velocity.Z-a[13],2));ew+=Math.Abs(o.Spin.Y-a[6])/BallPhysics.Rps;total++;
            }
            Require(Math.Abs(ev/(data.Length-1)-model.VelocityError)<1e-9 && Math.Abs(ew/(data.Length-1)-model.SpinErrorRps)<1e-9,"Reported response metrics differ from game solver");
            var low=model.At(0,0);var boundary=model.At(model.NormalMin,model.SlipMin);var high=model.At(1000,1000);
            Require(low.Restitution==boundary.Restitution && low.Tangent==boundary.Tangent && high.Restitution>=0 && high.Restitution<=1 && high.Tangent>=-1 && high.Tangent<=1 && !model.Covers(1000,1000),"Boundary clamping failed");
            var source=new BallState(new V3(0,1,-1.7),new V3(0,-1.5,0),new V3());
            var target=BallPhysics.Strike(source,new V3(0,.6,.8),new V3(0,3,5),.8,.8,.15,model.Id);
            var inverse=ServeContact.Solve(target,.8,.8,.15,model.Id);
            Require((inverse.After.Velocity-target.Velocity).Length<1e-7 && (inverse.After.Spin-target.Spin).Length<1e-6,"State-dependent inverse contact did not reproduce its target");
        }
        Require(total==827,"Unexpected held-out response sample count");
        var stroke=new ServeStroke {ResponseId=1};var result=stroke.Simulate(new PhysicsSettings());
        Require(result.Contact!=null,"Response stroke missed unexpectedly");var hit=result.Contact;
        var direct=BallPhysics.Strike(hit.Before,hit.Normal,hit.RacketVelocity,stroke.Restitution,stroke.Friction,stroke.Tangent,1);
        Require((direct.Spin-hit.After.Spin).Length<1e-9,"Forward serve ignored response model");
        var arrival=new BallState(new V3(0,1.2,1.5),new V3(0,-.5,4),new V3(-100,0,0));var action=new ReceiveAction(10,0,1,3,1);
        var settings=new ReceiveStudySettings {RubberResponseId=1,Trials=16,BallVelocityError=0,SpinErrorRps=0,FaceErrorDegrees=0,RacketVelocityError=0};
        var report=ReceiveStudy.TestCurrent(arrival,new PhysicsSettings(),settings,action,.8,.8,.15);
        var shot=BallPhysics.Strike(arrival,action.Normal,action.Velocity,.8,.8,.15,1);var flight=BallPhysics.Simulate(shot,new PhysicsSettings(),-1,true);
        Require(report.Settings.RubberResponseId==1 && report.Baseline.Trials[0].Landing.HasValue==flight.FirstTableHeightCrossing.HasValue,"Receive study dropped material profile");
        if(flight.FirstTableHeightCrossing.HasValue) Require((report.Baseline.Trials[0].Landing.Value-flight.FirstTableHeightCrossing.Value.Position).Length<1e-9,"Receive evaluation ignored speed-dependent response");
        Console.WriteLine($"PASS: {total} response holdout samples match C#; moving frames, passive energy, bounds, inverse serve, forward serve and receiving are consistent.");
    }
}

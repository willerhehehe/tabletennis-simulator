using System;
using System.Text;

namespace TableTennis
{
    // No Unity or test-framework dependency: runs from the Editor or dotnet run.
    public static class PhysicsChecks
    {
        public static string Run()
        {
            int count = 0;
            var report = new StringBuilder();
            Action<bool, string> check = (condition, name) =>
            { if (!condition) throw new Exception("FAIL: " + name); count++; report.AppendLine("PASS: " + name); };
            var vacuum = new PhysicsSettings { AirDensity = 0, SpinDecay = 0, NetEnabled = false };
            var initial = new BallState(new V3(0, 2, -1), new V3(2, 3, 7), new V3(12, 34, 56));
            var advanced = BallPhysics.Advance(initial, 0.25, vacuum);
            check((advanced.Position - (initial.Position + initial.Velocity * 0.25 + new V3(0, -0.5 * 9.81 * 0.25 * 0.25, 0))).Length < 1e-12,
                "Vacuum flight matches analytic parabola");
            check((advanced.Spin - initial.Spin).Length < 1e-12, "Zero decay preserves spin");

            var air = new PhysicsSettings { Gravity = 0, SpinDecay = 0 };
            V3 v = new V3(0, 0, 10), top = new V3(80 * BallPhysics.Rps, 0, 0);
            V3 baseForce = BallPhysics.Acceleration(v, new V3(), air);
            V3 topForce = BallPhysics.Acceleration(v, top, air);
            check(baseForce.Z < 0 && baseForce.X == 0 && baseForce.Y == 0, "Drag opposes motion without spurious lift");
            check(topForce.Y < 0 && BallPhysics.Acceleration(v, -top, air).Y > 0, "Topspin falls; backspin lifts");
            V3 sideA = BallPhysics.Acceleration(v, new V3(0, 80 * BallPhysics.Rps, 0), air);
            V3 sideB = BallPhysics.Acceleration(v, new V3(0, -80 * BallPhysics.Rps, 0), air);
            check(sideA.X > 0 && Math.Abs(sideA.X + sideB.X) < 1e-12, "Opposite sidespins bend symmetrically");
            check(Math.Abs(V3.Dot(topForce - baseForce, v)) < 1e-12, "Magnus force does no instantaneous work");
            check((BallPhysics.Acceleration(v, new V3(0, 0, 500), air) - baseForce).Length < 1e-12,
                "Spin parallel to flight produces no Magnus lift");
            check(BallPhysics.Acceleration(new V3(), top, air).Finite, "Zero-speed flight stays finite");

            var impact = new BallState(new V3(0, 0.78, 0.5), new V3(4, -3, 8), new V3(400, 200, 100));
            V3 up = new V3(0, 1, 0);
            var smooth = BallPhysics.Contact(impact, up, new V3(), 0.8, 0);
            check(Math.Abs(smooth.Velocity.Y - 2.4) < 1e-12 && smooth.Velocity.X == 4 && smooth.Velocity.Z == 8 &&
                (smooth.Spin - impact.Spin).Length < 1e-12, "Frictionless bounce preserves tangent motion and spin");
            var rough = BallPhysics.Contact(impact, up, new V3(), 0.8, 0.2);
            check(BallPhysics.Energy(rough) <= BallPhysics.Energy(impact) + 1e-12, "Stationary table does not create kinetic energy");
            V3 deltaP = (rough.Velocity - impact.Velocity) * BallPhysics.Mass;
            check(((rough.Spin - impact.Spin) * BallPhysics.Inertia - V3.Cross(up * -BallPhysics.Radius, deltaP)).Length < 1e-12,
                "Contact impulse and spin change satisfy angular momentum");
            var verticalSpin = new BallState(impact.Position, new V3(0, -3, 8), new V3(0, 500, 0));
            check(Math.Abs(BallPhysics.Contact(verticalSpin, up, new V3(), 0.8, 0.3).Velocity.X) < 1e-12,
                "Pure vertical-axis spin does not invent a sideways table kick");
            var sliding = new BallState(impact.Position, new V3(0, -3, 4), new V3());
            var sticking = BallPhysics.Contact(sliding, up, new V3(), 0.8, 2);
            V3 pointVelocity = sticking.Velocity + V3.Cross(sticking.Spin, up * -BallPhysics.Radius);
            check(Math.Abs(pointVelocity.Z) < 1e-12, "Sticking contact cancels tangential slip");
            var separating = new BallState(impact.Position, new V3(0, 3, 1), new V3());
            check((BallPhysics.Contact(separating, up, new V3(), 0.8, 0.3).Velocity - separating.Velocity).Length == 0,
                "Separating surfaces do not apply a second impulse");

            var pipsIn = new BallState(new V3(0, 1.1, 1.5), new V3(1, 0, -4), new V3(40, 70, 0));
            var pipsOut = BallPhysics.Contact(pipsIn, new V3(0,0,1), new V3(), .7, .8, -1);
            check((pipsOut.Spin-pipsIn.Spin).Length < 1e-12 && Math.Abs(pipsOut.Velocity.X-pipsIn.Velocity.X)<1e-12 && pipsOut.Velocity.Z>0,
                "Zero tangential response preserves world spin while returning the ball");
            check(BallSpin.Components(pipsIn.Spin,pipsIn.Velocity).X * BallSpin.Components(pipsOut.Spin,pipsOut.Velocity).X < 0,
                "Preserved angular velocity changes top/back naming when travel reverses");
            var partial = BallPhysics.Contact(pipsIn, new V3(0,0,1), new V3(), .7, 2, -.5);
            var slipIn = pipsIn.Velocity + V3.Cross(pipsIn.Spin,new V3(0,0,-BallPhysics.Radius));
            var slipOut = partial.Velocity + V3.Cross(partial.Spin,new V3(0,0,-BallPhysics.Radius));
            check(Math.Abs(slipOut.X-.5*slipIn.X)<1e-10 && Math.Abs(slipOut.Y-.5*slipIn.Y)<1e-10 && BallPhysics.Energy(partial)<=BallPhysics.Energy(pipsIn),
                "Negative beta permits partial same-direction slip without creating energy");
            var noSpinTarget = new BallState(new V3(0,1,-1.65),new V3(0,0,5),new V3());
            var noSpinContact = ServeContact.Solve(noSpinTarget,.7,.8,-1);
            check((noSpinContact.After.Velocity-noSpinTarget.Velocity).Length<1e-7 && noSpinContact.After.Spin.Length<1e-7,
                "Inverse contact handles zero tangential response without dividing by zero");
            bool impossibleSpin = false;
            try { var target = noSpinTarget; target.Spin = new V3(40,0,0); ServeContact.Solve(target,.7,.8,-1); }
            catch (ArgumentException) { impossibleSpin = true; }
            check(impossibleSpin,"Zero tangential response cannot fabricate spin from a no-spin toss");

            var incoming = new BallState(new V3(0, 1.1, -1.65), new V3(0, -0.5, -6), new V3());
            var struck = BallPhysics.Strike(incoming, new V3(0, 0, 1), new V3(0, 3, 3), 0.8, 0.8, 0.15);
            check(struck.Velocity.Z > 0 && struck.Spin.X > 0, "Upward brushing racket generates outgoing topspin");
            var smoothRubber = BallPhysics.Strike(incoming, new V3(0, 0, 1), new V3(0, 3, 3), 0.8, 0, 0.15);
            check(smoothRubber.Spin.Length == 0, "Zero rubber friction cannot generate spin");

            var flickIn = BackhandFlick.Incoming; var flickOut = BackhandFlick.Strike();
            var flick = BallPhysics.Simulate(flickOut, new PhysicsSettings(), 1, true);
            var flickSpin = BallSpin.Components(flickOut.Spin / BallPhysics.Rps, flickOut.Velocity);
            check(BallSpin.Components(flickIn.Spin / BallPhysics.Rps, flickIn.Velocity).X < -39 && flickIn.Position.Z > -BallPhysics.HalfLength && flickIn.Position.X < 0,
                "Right-handed backhand flick starts with backspin over the backhand half of the table");
            check(flick.Events.Count>0 && flick.Events[0].Kind=="球台" && flick.Events[0].Before.Position.Z>0 && flick.NetClearance>0.02 && flick.NetClearance<0.15 && flickSpin.X>20 && Math.Abs(flickSpin.Y)>10,
                "Calibrated backhand flick produces sidespin/topspin and lands legally with a low net clearance");
            check(!RacketGeometry.MayTouchTable(flickIn.Position,BackhandFlick.Normal,BackhandFlick.Velocity,BackhandFlick.Roll),
                "Backhand flick face and handle clear the tabletop");
            var weakFlick = BallPhysics.Simulate(BackhandFlick.Strike(0),new PhysicsSettings(),1,true);
            check(weakFlick.Events.Count==0 || weakFlick.Events[0].Kind!="球台" || weakFlick.Events[0].Before.Position.Z<0,
                "Side brushing alone does not reproduce this backhand flick's successful lift");
            var wideRange = new ReceiveStudySettings { MaxRacketSpeed=40, Trials=16, BallVelocityError=0, SpinErrorRps=0, FaceErrorDegrees=0, RacketVelocityError=0 };
            var fastAction = new ReceiveAction(0,0,12,11,10);
            var fastScore = ReceiveStudy.Evaluate(new BallState(new V3(0,2,2),new V3(0,0,4),new V3()),new PhysicsSettings(),wideRange,fastAction,.8,.8,.15);
            check(wideRange.Allows(fastAction) && !fastScore.Failures.ContainsKey("挥拍超限") && !fastScore.Failures.ContainsKey("动作超出所选接旋转思路或方向范围"),
                "Expanded front/up/side speeds pass both range and total-speed checks");

            var loopIn = ForehandBackspin.Incoming;
            var loopOut = ForehandBackspin.Strike();
            var loop = BallPhysics.Simulate(loopOut, new PhysicsSettings());
            var loopLanding = loop.Events.Find(e => e.Kind == "球台");
            check(loopIn.Velocity.Z < 0 && BallPhysics.Acceleration(loopIn.Velocity, loopIn.Spin, air).Y > 0,
                "Forehand preset starts with incoming backspin, including the reversed travel direction");
            check(loopOut.Velocity.Z > 0 && loopOut.Spin.X > loopIn.Spin.X && BallPhysics.Acceleration(loopOut.Velocity, loopOut.Spin, air).Y < 0,
                "Forehand brushing increases spin and produces outgoing topspin without flipping world spin by hand");
            check(loopLanding != null && loopLanding.Before.Position.Z > 0 && loop.NetClearance > 0,
                "Forehand loop clears the net and first bounces on the opponent half");
            var weakLoop = BallPhysics.Simulate(ForehandBackspin.Strike(ForehandBackspin.WeakUp), new PhysicsSettings());
            check(weakLoop.EndReason == "触网" && weakLoop.Events.Find(e => e.Kind == "球台") == null,
                "Insufficient upward racket speed hits the net before reaching the table");
            var fineLoop = BallPhysics.Simulate(loopOut, new PhysicsSettings { Step = 0.0005 });
            var fineLanding = fineLoop.Events.Find(e => e.Kind == "球台");
            check(fineLanding != null && (loopLanding.Before.Position - fineLanding.Before.Position).Length < 0.001,
                "Forehand first landing converges within 1 mm at half timestep");
            report.AppendLine(FormattableString.Invariant($"Forehand synthetic preset: incoming={loopIn.Velocity.Length:F2} m/s, backspin={ForehandBackspin.SpinRps:F1} rps; outgoing={loopOut.Velocity.Length:F3} m/s, topspin={loopOut.Spin.X / BallPhysics.Rps:F3} rps; net clearance={loop.NetClearance * 100:F2} cm; first landing X={loopLanding.Before.Position.X:F3}, Z={loopLanding.Before.Position.Z:F3} m."));

            var random = new Random(42);
            for (int i = 0; i < 300; i++)
            {
                var b = new BallState(impact.Position, new V3(random.NextDouble() * 20 - 10, -random.NextDouble() * 20, random.NextDouble() * 20 - 10),
                    new V3(random.NextDouble() * 1800 - 900, random.NextDouble() * 1800 - 900, random.NextDouble() * 1800 - 900));
                var after = BallPhysics.Contact(b, up, new V3(), random.NextDouble(), random.NextDouble(), random.NextDouble());
                if (BallPhysics.Energy(after) > BallPhysics.Energy(b) + 1e-10) throw new Exception("Passive contact increased energy");
            }
            check(true, "300 mixed-spin passive rubber contacts respect the energy bound");

            var fast = new BallState(new V3(0, 0.9, 0.5), new V3(0, -25, 1), new V3());
            vacuum.Step = 0.01; vacuum.Duration = 0.02;
            var swept = BallPhysics.Simulate(fast, vacuum);
            check(swept.Events.Count > 0 && swept.Events[0].Kind == "球台" && Math.Abs(swept.Events[0].Before.Position.Y - 0.78) < 1e-9,
                "25 m/s ball cannot tunnel through the table at 100 Hz");
            var net = new PhysicsSettings { Step = 0.01, Gravity = 0, AirDensity = 0, Duration = 0.1 };
            var netFlight = BallPhysics.Simulate(new BallState(new V3(0, 0.84, -0.1), new V3(0, 0, 25), new V3()), net);
            check(netFlight.EndReason == "触网" && netFlight.Samples[netFlight.Samples.Count - 1].Position.Z < 0,
                "Fast ball hits the near net face before crossing it");
            var offTable = BallPhysics.Simulate(new BallState(new V3(1, 1, 0.5), new V3(0, -4, 0), new V3()), new PhysicsSettings { NetEnabled = false });
            check(offTable.Events.Find(e => e.Kind == "球台") == null && offTable.EndReason == "地面", "Off-table shots do not bounce on an infinite table");

            var shot = new BallState(new V3(-0.15, 1.1, -1.65), new V3(0, 8 * Math.Sin(10 * Math.PI / 180), 8 * Math.Cos(10 * Math.PI / 180)),
                new V3(60, 0, 0) * BallPhysics.Rps);
            var coarse = BallPhysics.Simulate(shot, new PhysicsSettings());
            var fine = BallPhysics.Simulate(shot, new PhysicsSettings { Step = 0.0005 });
            var c = coarse.Events.Find(e => e.Kind == "球台"); var f = fine.Events.Find(e => e.Kind == "球台");
            check(c != null && f != null && c.Before.Position.Z > 0, "Direct-launch topspin shot clears the net and lands on the far half");
            double difference = (c.Before.Position - f.Before.Position).Length;
            check(difference < 0.001, "1 ms vs 0.5 ms first-landing difference < 1 mm");
            report.AppendLine(FormattableString.Invariant($"Landing difference: {difference * 1000:F9} mm; default landing X={c.Before.Position.X:F6}, Z={c.Before.Position.Z:F6} m"));
            bool rejected = false;
            try { BallPhysics.Simulate(shot, new PhysicsSettings { Step = double.NaN }); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Non-finite settings rejected at entry");
            rejected = false;
            try { BallPhysics.Simulate(new BallState(new V3(0, 0.5, 0.3), new V3(), new V3()), new PhysicsSettings()); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Initial table penetration rejected");

            check(PhysicsSettings.TryNumber("6.25", -10, 10, out double typed) && typed == 6.25 &&
                PhysicsSettings.TryNumber("-10", -10, 10, out _) && PhysicsSettings.TryNumber("10", -10, 10, out _),
                "Exact numeric input accepts decimals and range endpoints");
            foreach (string invalid in new[] { "-", "NaN", "Infinity", "1e309", "10.01", "6,25" })
                if (PhysicsSettings.TryNumber(invalid, -10, 10, out _)) throw new Exception("Invalid numeric input accepted: " + invalid);
            check(true, "Invalid, non-finite and out-of-range text never changes simulation parameters");
            check(loop.Verdict == "落入对方半台" && loop.EndReason == "地面" && weakLoop.Verdict == "触网",
                "Shot outcome uses the first contact instead of the final ground impact");
            check(offTable.Verdict == "侧出界" && offTable.FirstTableHeightCrossing.HasValue,
                "Side miss records its table-height crossing");
            var longShot = BallPhysics.Simulate(new BallState(new V3(0, 1, 2), new V3(0, -1, 2), new V3()), new PhysicsSettings());
            check(longShot.Verdict == "长出台", "Long miss is distinguished from a side miss");
            var ownHalf = BallPhysics.Simulate(new BallState(new V3(0, 1, -1), new V3(0, -1, 0), new V3()), new PhysicsSettings());
            check(ownHalf.Verdict == "先落本方半台", "Own-half bounce is not reported as a successful return");
            var beforeNet = BallPhysics.Simulate(new BallState(new V3(0, 0.9, -0.1), new V3(0, -25, 25), new V3()), net);
            check(beforeNet.Verdict == "触网" && !beforeNet.FirstTableHeightCrossing.HasValue,
                "Net collision does not report a later hypothetical landing");
            var preview = BallPhysics.PreviewIncoming(loopIn, new PhysicsSettings());
            check(Math.Abs(preview[0].Time + 0.12) < 1e-9 && preview[preview.Count - 1].Time == 0 &&
                (preview[preview.Count - 1].Position - loopIn.Position).Length == 0,
                "Incoming preview joins the exact contact state at time zero");
            var replayed = BallPhysics.Advance(preview[0], -preview[0].Time, new PhysicsSettings());
            check((replayed.Position - loopIn.Position).Length < 0.0001 && (replayed.Velocity - loopIn.Velocity).Length < 0.001,
                "Reconstructed incoming segment replays to contact within 0.1 mm");
            var nearTable = BallPhysics.PreviewIncoming(new BallState(new V3(0, 0.79, -0.5), new V3(0, 3, -5), new V3()), new PhysicsSettings());
            check(nearTable.Count < preview.Count && nearTable[0].Position.Y >= 0.78,
                "Incoming reconstruction stops at an earlier table collision");
            var nearNet = BallPhysics.PreviewIncoming(new BallState(new V3(0, 0.85, -0.1), new V3(0, 0, -5), new V3()), new PhysicsSettings());
            check(nearNet.Count < preview.Count && nearNet[0].Position.Z < -0.021,
                "Incoming reconstruction does not travel backward through the net");
            var extreme = BallPhysics.PreviewIncoming(new BallState(new V3(0, 1, -2), new V3(0, 0, -100), new V3()), new PhysicsSettings { AirDensity = 3, Drag = 2 });
            check(extreme.TrueForAll(s => s.Position.Finite && s.Velocity.Finite && s.Velocity.Length <= 100),
                "Backward reconstruction stops before leaving the validated speed range");

            var mirrored = new BallState(new V3(-loopOut.Position.X, loopOut.Position.Y, -loopOut.Position.Z),
                new V3(-loopOut.Velocity.X, loopOut.Velocity.Y, -loopOut.Velocity.Z), new V3(-loopOut.Spin.X, loopOut.Spin.Y, -loopOut.Spin.Z));
            var reverseReturn = BallPhysics.Simulate(mirrored, new PhysicsSettings(), -1);
            check(reverseReturn.Verdict == "落入对方半台" && Math.Abs(reverseReturn.Events[0].Before.Position.Z + loopLanding.Before.Position.Z) < 1e-8,
                "Return from right to left uses the correct target half and mirrored physics");
            var assessed = BallPhysics.Simulate(mirrored, new PhysicsSettings(), -1, true);
            check(assessed.Verdict == reverseReturn.Verdict && assessed.Samples.Count <= 3 &&
                (assessed.Events[0].Before.Position - reverseReturn.Events[0].Before.Position).Length < 1e-10,
                "Fast assessment matches full simulation at the first contact");
            var serve = BallPhysics.Simulate(new BallState(new V3(0, 1, -1.65),
                new V3(0, -6 * Math.Sin(15 * Math.PI / 180), 6 * Math.Cos(15 * Math.PI / 180)), new V3(-40 * BallPhysics.Rps, 0, 0)), new PhysicsSettings());
            var reception = Reception.FromServe(serve, new PhysicsSettings(), 0.1);
            check(reception.Incoming.Time == 0 && reception.Incoming.Position.Z > 0 && reception.Approach[0].Time < 0 &&
                reception.Bounces.Count == 2 && reception.Bounces[0].Before.Position.Z < 0 && reception.Bounces[1].Before.Position.Z > 0,
                "Fixed receiving point is derived after both serve bounces");
            rejected = false;
            try { Reception.FromServe(offTable, new PhysicsSettings(), 0.1); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid serve cannot be used for receiving analysis");
            rejected = false;
            try { Reception.FromServe(serve, new PhysicsSettings(), serve.Events[2].Before.Time - serve.Events[1].Before.Time + 0.01); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Receiving after the next collision is rejected");
            var receivingAction = new ReceiveAction(10, 0, 0.5, 4, 0);
            RacketGeometry.Axes(receivingAction.Normal, -70, out V3 racketRight, out V3 racketUp);
            check(Math.Abs(V3.Dot(racketUp, receivingAction.Normal)) < 1e-12 &&
                (V3.Cross(racketRight, racketUp) - receivingAction.Normal).Length < 1e-12,
                "Racket roll preserves the striking normal and an orthonormal face frame");
            check(!RacketGeometry.MayTouchTable(reception.Incoming.Position, receivingAction.Normal, receivingAction.Velocity, -70) &&
                RacketGeometry.MayTouchTable(reception.Incoming.Position, receivingAction.Normal, receivingAction.Velocity, 0),
                "Default slanted handle clears the table while the formerly vertical handle is rejected");
            check(RacketGeometry.MayTouchTable(new V3(0, 0.78, 0.8), new V3(0, 0, -1), new V3(), -70) &&
                !RacketGeometry.MayTouchTable(new V3(0, 0.78, 2), new V3(0, 0, -1), new V3(), -70),
                "Racket clearance rejects penetration above the table but allows low contacts behind it");
            var uncertainty = new ReceiveStudySettings { Candidates = 32, Trials = 32 };
            var noiseless = uncertainty.Copy(); noiseless.BallVelocityError = noiseless.SpinErrorRps = noiseless.FaceErrorDegrees = noiseless.RacketVelocityError = 0;
            var cleanScore = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), noiseless, receivingAction, 0.8, 0.8, 0.15);
            check(cleanScore.Successes == cleanScore.Attempts && cleanScore.Trials.Count == cleanScore.Attempts,
                "Zero uncertainty reproduces a successful nominal receive on every trial");
            double clearance = cleanScore.Trials[0].NetClearance.Value;
            var heightTest = noiseless.Copy(); heightTest.MaxNetClearance = Math.Max(0.01, clearance - 0.001);
            var tooHigh = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), heightTest, receivingAction, 0.8, 0.8, 0.15);
            heightTest.MaxNetClearance = clearance + 0.001;
            var lowEnough = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), heightTest, receivingAction, 0.8, 0.8, 0.15);
            check(tooHigh.Successes == tooHigh.Attempts && tooHigh.QualifiedSuccesses == 0 && lowEnough.QualifiedSuccesses == lowEnough.Attempts,
                "Height ceiling separates legal high returns from qualified returns without changing landing rate");
            var verticalHandle = noiseless.Copy(); verticalHandle.RacketRollDegrees = 0;
            var blockedStroke = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), verticalHandle, receivingAction, 0.8, 0.8, 0.15);
            check(blockedStroke.Successes == 0 && blockedStroke.Failures["球拍可能碰台"] == blockedStroke.Attempts,
                "A table-intersecting racket cannot earn a successful receiving score");
            var score1 = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), uncertainty, receivingAction, 0.8, 0.8, 0.15);
            var score2 = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), uncertainty, receivingAction, 0.8, 0.8, 0.15);
            bool same = score1.Successes == score2.Successes;
            for (int i = 0; i < score1.Trials.Count; i++) same &= score1.Trials[i].Reason == score2.Trials[i].Reason;
            check(same, "Common-seed uncertainty trials are reproducible");
            int accounted = score1.Successes; foreach (int failures in score1.Failures.Values) accounted += failures;
            check(accounted == score1.Attempts, "Every trial is counted as exactly one success or failure");
            var study = ReceiveStudy.Search(reception.Incoming, new PhysicsSettings(), uncertainty, new ReceiveAction(10, 0, 1, 1, 0), 0.8, 0.8, 0.15);
            check(study.EvaluatedCandidates >= 32 && study.EvaluatedCandidates <= 104 && study.Ranked[0].QualifiedRate >= study.Baseline.QualifiedRate && study.Baseline.Attempts == 32,
                "Search ranks verified candidates and retains the manual baseline");
            var verifyBest = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), uncertainty, study.Ranked[0].Action, 0.8, 0.8, 0.15);
            check(verifyBest.Successes == study.Ranked[0].Successes && verifyBest.QualifiedSuccesses == study.Ranked[0].QualifiedSuccesses, "Independent validation scores can be replayed exactly");
            var currentOnly = ReceiveStudy.TestCurrent(reception.Incoming, new PhysicsSettings(), uncertainty, receivingAction, 0.8, 0.8, 0.15);
            check(currentOnly.EvaluatedCandidates == 1 && currentOnly.Ranked.Count == 1 && currentOnly.Baseline == currentOnly.Ranked[0] &&
                currentOnly.Baseline.Action.Equals(receivingAction) && currentOnly.Baseline.Successes == score1.Successes,
                "Testing the current stroke preserves its action and reuses the same perturbation evaluation");
            var separatingReceive = new ReceiveAction(0, 0, -3, 0, 0);
            var slowArrival = new BallState(new V3(0, 1.1, 1.5), new V3(0, 0, 1), new V3());
            var failedCurrent = ReceiveStudy.TestCurrent(slowArrival, new PhysicsSettings(), noiseless, separatingReceive, 0.8, 0.8, 0.15);
            check(failedCurrent.Baseline.Successes == 0 && failedCurrent.Baseline.Failures["未形成有效击球"] == noiseless.Trials,
                "An invalid manual contact produces an explained zero-score report instead of blocking assessment");
            var forwardUp = uncertainty.Copy(); forwardUp.DirectionConstraint = 1;
            var directedStudy = ReceiveStudy.Search(reception.Incoming, new PhysicsSettings(), forwardUp, receivingAction, 0.8, 0.8, 0.15);
            check(directedStudy.Ranked.TrueForAll(s => forwardUp.Allows(s.Action)) && directedStudy.Ranked[0].Successes > 0,
                "Forward-up search keeps successful candidates within the chosen nominal stroke directions");
            var withSpin = noiseless.Copy(); withSpin.SpinDirection = SpinReceiveDirection.WithSpin;
            var againstSpin = noiseless.Copy(); againstSpin.SpinDirection = SpinReceiveDirection.AgainstSpin;
            var downStroke = new ReceiveAction(0, 0, 1, -1, 0);
            var upStroke = new ReceiveAction(0, 0, 1, 1, 0);
            var spinArrival = new BallState(new V3(0, 1.2, 1.5), new V3(0, 0, 3), new V3(40, 0, 0));
            check(withSpin.Allows(downStroke, spinArrival) && !withSpin.Allows(upStroke, spinArrival) && againstSpin.Allows(upStroke, spinArrival),
                "With/against spin follows the rotating surface at the receiver contact, not ball travel direction");
            spinArrival.Spin = -spinArrival.Spin;
            check(withSpin.Allows(upStroke, spinArrival) && againstSpin.Allows(downStroke, spinArrival),
                "Reversing incoming spin reverses the allowed receiving stroke");
            spinArrival.Spin = new V3(0, 40, 0);
            check(withSpin.Allows(new ReceiveAction(0, 0, 1, 0, 1), spinArrival) && againstSpin.Allows(new ReceiveAction(0, 0, 1, 0, -1), spinArrival),
                "Sidespin receiving directions use the actual contact surface motion");
            check(!withSpin.Allows(new ReceiveAction(0, 0, 1, 0, 0), spinArrival) && !againstSpin.Allows(new ReceiveAction(0, 0, 1, 0, 0), spinArrival),
                "Normal-only hits are not mislabelled as with or against spin");
            spinArrival.Spin = new V3(); bool noSpinRejected = false;
            try { ReceiveStudy.Search(spinArrival, new PhysicsSettings(), withSpin, upStroke, .8, .8, .15); }
            catch (ArgumentException) { noSpinRejected = true; }
            check(noSpinRejected, "No-spin directional search explains why with/against cannot be distinguished");
            foreach (var spinConstraint in new[] { SpinReceiveDirection.WithSpin, SpinReceiveDirection.AgainstSpin })
            {
                var spinSettings = uncertainty.Copy(); spinSettings.SpinDirection = spinConstraint; spinSettings.Candidates = spinSettings.Trials = 16;
                var spinStudy = ReceiveStudy.Search(reception.Incoming, new PhysicsSettings(), spinSettings, receivingAction, .8, .8, .15);
                check(spinSettings.Allows(spinStudy.Ranked[0].Action, reception.Incoming) &&
                    spinStudy.Ranked.TrueForAll(item => item == spinStudy.Baseline || spinSettings.Allows(item.Action, reception.Incoming)),
                    "Directional search and refinement keep candidates in " + spinConstraint + ", retaining the labelled manual baseline");
                var replay = ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), spinSettings, spinStudy.Ranked[0].Action, .8, .8, .15);
                check(replay.QualifiedSuccesses == spinStudy.Ranked[0].QualifiedSuccesses, "Directional holdout replay remains deterministic: " + spinConstraint);
            }
            using (var cancelled = new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel(); bool stopped = false;
                try { ReceiveStudy.Search(reception.Incoming, new PhysicsSettings(), uncertainty, receivingAction, 0.8, 0.8, 0.15, cancelled.Token); }
                catch (OperationCanceledException) { stopped = true; }
                check(stopped, "Receiving search respects cancellation");
            }
            rejected = false;
            try { ReceiveStudy.Evaluate(reception.Incoming, new PhysicsSettings(), uncertainty, receivingAction, double.NaN, 0.8, 0.15); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid material parameters are rejected before trials run");
            var fastArrival = new BallState(new V3(0, 1, 1.6), new V3(0, 0, 10), new V3());
            var retreat = new ReceiveAction(0, 0, -2, 0, 0);
            var softened = BallPhysics.Strike(fastArrival, retreat.Normal, retreat.Velocity, 0.8, 0, 0);
            check(softened.Velocity.Z < 0 && Math.Abs(softened.Velocity.Z) < 8,
                "Negative forward velocity allows a retreating racket to soften the return");

            bool spinFrameCorrect = true, serveReplayCorrect = true;
            foreach (V3 travel in new[] { new V3(0, 0, 1), new V3(0.6, 0, 0.8), new V3(-0.6, 0, -0.8) })
            {
                V3 backSpin = BallSpin.Preset(2, 30, 0.5, 1, travel);
                V3 topSpin = BallSpin.Preset(1, 30, 0.5, 1, travel);
                spinFrameCorrect &= V3.Dot(V3.Cross(backSpin, new V3(0, 1, 0)), travel) < 0 &&
                    V3.Dot(V3.Cross(topSpin, new V3(0, 1, 0)), travel) > 0;
                for (int kind = 0; kind <= 5; kind++) foreach (int sideSign in new[] { -1, 1 })
                {
                    V3 spin = BallSpin.Preset(kind, 30, 0.5, sideSign, travel);
                    spinFrameCorrect &= BallSpin.Kind(BallSpin.Components(spin, travel)) == kind;
                    var target = new BallState(new V3(0, 1, -1.65), travel * 6 + new V3(0, -1, 0), spin * BallPhysics.Rps);
                    var demo = ServeContact.Solve(target, 0.8, 0.8, 0.15);
                    serveReplayCorrect &= (demo.After.Velocity - target.Velocity).Length < 1e-9 &&
                        (demo.After.Spin - target.Spin).Length < 1e-8 && demo.RequiredFriction <= 0.8 &&
                        Math.Abs((demo.Point - target.Position).Length - BallPhysics.Radius) < 1e-10;
                }
            }
            check(spinFrameCorrect, "Spin presets follow travel direction: topspin surface moves forward and backspin surface returns toward the sender");
            check(serveReplayCorrect, "All six spin types and both side signs replay exactly from the displayed toss and racket contact at three headings");
            var topServe = ServeContact.Solve(new BallState(new V3(0, 1, -1.65), new V3(0, -1, 6), new V3(30 * BallPhysics.Rps, 0, 0)), 0.8, 0.8, 0.15);
            var backServe = ServeContact.Solve(new BallState(new V3(0, 1, -1.65), new V3(0, -1, 6), new V3(-30 * BallPhysics.Rps, 0, 0)), 0.8, 0.8, 0.15);
            check(topServe.Point.Y > 1 && topServe.BrushVelocity.Y > 0 && backServe.Point.Y < 1 && backServe.BrushVelocity.Y < 0,
                "Serve contact markers and brushing directions distinguish topspin from backspin");
            var screenshotCase = new BallState(new V3(0, 1, -1.65), new V3(0, 5 * Math.Sin(8 * Math.PI / 180), 5 * Math.Cos(8 * Math.PI / 180)), new V3(-40 * BallPhysics.Rps, 0, 0));
            var screenshotSolution = ServeContact.Solve(screenshotCase, 0.8, 0.8, 0.15);
            check((screenshotSolution.After.Velocity - screenshotCase.Velocity).Length < 1e-9 &&
                (screenshotSolution.After.Spin - screenshotCase.Spin).Length < 1e-8 && screenshotSolution.RequiredFriction <= 0.8 &&
                screenshotSolution.Before.Velocity.Y < 0 && Math.Abs(screenshotSolution.Before.Velocity.Y) <= ServeContact.MaxTossVerticalSpeed,
                "Regression: 5 m/s, 8 degrees, 40 rps backspin and friction 0.8 produce an exact contact without changing the requested shot");
            rejected = false;
            try { ServeContact.Solve(screenshotCase, 0.8, 0, 0.15); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && Math.Abs(screenshotCase.Spin.Length / BallPhysics.Rps - 40) < 1e-10,
                "Failed contact search preserves the requested spin rather than silently weakening it");
            RacketGeometry.Axes(new V3(0, 1, 0), -70, out V3 flatRight, out V3 flatUp);
            check(Math.Abs(flatRight.Length - 1) < 1e-10 && Math.Abs(flatUp.Length - 1) < 1e-10 &&
                (V3.Cross(flatRight, flatUp) - new V3(0, 1, 0)).Length < 1e-10,
                "A horizontal serve racket retains a valid face basis and clearance geometry");
            rejected = false;
            try { ServeContact.Solve(new BallState(new V3(0, 1, -1.65), new V3(0, -1, 5), new V3(-60 * BallPhysics.Rps, 0, 0)), 0.8, 0.8, 0.15); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Unreachable spin is rejected instead of fabricating a racket demonstration");
            rejected = false;
            try { ServeContact.Solve(new BallState(new V3(0, 1, -1.65), new V3(0, -1, 6), new V3(0, 0, 20 * BallPhysics.Rps)), 0.8, 0.8, 0.15); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Axial spin incompatible with the zero-spin vertical toss assumption is explained rather than silently removed");
            var stroke = new ServeStroke();
            var forwardServe = stroke.Simulate(new PhysicsSettings());
            check(forwardServe.TwoBounces && !forwardServe.MayTouchTable,
                "Default physical stroke lands on own then opponent half with racket clear of table");
            check(forwardServe.Contact != null && forwardServe.Contact.Before.Velocity.Y < 0 &&
                forwardServe.Contact.After.Spin.X < 0 && forwardServe.Contact.After.Velocity.Z > 0,
                "Falling ball and nearly horizontal forward stroke generate backspin through collision");
            check(Math.Abs(V3.Dot(forwardServe.Contact.Before.Position - stroke.CenterAt(forwardServe.Contact.Before.Time), stroke.Normal) -
                (BallPhysics.Radius + ServeStroke.FaceOffset)) < 1e-7,
                "Forward serve contact lies on the moving physical racket face");
            var noRubber = stroke.Copy(); noRubber.Friction = 0;
            check(noRubber.Simulate(new PhysicsSettings()).Contact.After.Spin.Length < 1e-10,
                "Forward stroke cannot create spin with frictionless rubber");
            report.AppendLine(FormattableString.Invariant($"Forward serve: contact {forwardServe.Contact.Before.Time:F6} s; velocity ({forwardServe.Contact.After.Velocity.X:F3},{forwardServe.Contact.After.Velocity.Y:F3},{forwardServe.Contact.After.Velocity.Z:F3}) m/s; spin {forwardServe.Contact.After.Spin.X / BallPhysics.Rps:F3} rps; two bounces {forwardServe.TwoBounces}; table clearance warning {forwardServe.MayTouchTable}."));
            foreach (var e in forwardServe.Flight.Events)
                report.AppendLine(FormattableString.Invariant($"Forward event {e.Kind}: t {e.Before.Time:F4} s, position ({e.Before.Position.X:F3},{e.Before.Position.Y:F3},{e.Before.Position.Z:F3}); spinX {e.Before.Spin.X / BallPhysics.Rps:F3} -> {e.After.Spin.X / BallPhysics.Rps:F3} rps."));
            var missStroke = stroke.Copy(); missStroke.Start += new V3(0.4, 0, 0); missStroke.End += new V3(0.4, 0, 0);
            var missed = missStroke.Simulate(new PhysicsSettings());
            check(missed.Contact == null && missed.Flight == null && missed.Approach.Count > 2,
                "A finite racket miss retains falling trajectory without fabricating an outgoing shot");
            var fineStroke = stroke.Simulate(new PhysicsSettings { Step = 0.0005 });
            check(fineStroke.Contact != null && Math.Abs(fineStroke.Contact.Before.Time - forwardServe.Contact.Before.Time) < 0.00001 &&
                (fineStroke.Contact.After.Velocity - forwardServe.Contact.After.Velocity).Length < 0.001 &&
                (fineStroke.Flight.Events[0].Before.Position - forwardServe.Flight.Events[0].Before.Position).Length < 0.001,
                "Moving racket contact and outgoing velocity converge with half timestep");
            var flipped = stroke.Copy(); flipped.BackFace = true;
            var redHit = flipped.Simulate(new PhysicsSettings());
            check(redHit.Contact != null && !redHit.FrontFace &&
                (redHit.Contact.After.Velocity - forwardServe.Contact.After.Velocity).Length < 1e-8,
                "Red back face and black front face use the same physical contact when material settings match");
            foreach (double angle in new[] { 0.0, 40.0, 90.0 })
            { var pose = stroke.Copy(); pose.TableAngle = angle;
                check(Math.Abs(Math.Acos(Math.Abs(pose.Normal.Y)) * 180 / Math.PI - angle) < 1e-8,
                    $"Racket angle {angle} degrees is measured from the table plane"); }
            rejected = false; var invalidStroke = stroke.Copy(); invalidStroke.Speed = -1;
            try { invalidStroke.Simulate(new PhysicsSettings()); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Negative stroke speed is rejected");
            var noGravityStroke = new ServeStroke { Apex = new V3(0, 1, -1.65), Start = new V3(0, 0.9, -1.65), End = new V3(0, 1.1, -1.65),
                TableAngle = 0, StartTime = 0, Speed = 1 };
            check(noGravityStroke.Simulate(new PhysicsSettings { Gravity = 0 }).Contact != null,
                "Racket collision still runs when free flight has no environmental event");
            var late = stroke.Copy(); late.StartTime = 1;
            check(late.Simulate(new PhysicsSettings()).Contact == null, "A late stroke cannot hit a ball after it reached the floor");
            var assisted = new ServeStroke(); assisted.Start += new V3(0.6, 0, 0); assisted.End += new V3(0.6, 0, 0);
            assisted.AlignContact(new PhysicsSettings(), 0.95);
            check(assisted.Simulate(new PhysicsSettings()).Contact != null,
                "Assisted alignment corrects a displaced stroke to intersect the falling ball");
            bool alignedHits = true;
            foreach (double angle in new[] { 0.0, 40.0, 80.0 }) foreach (double swingSpeed in new[] { 2.0, 5.0, 8.0 })
            {
                var a = new ServeStroke { TableAngle = angle };
                a.End = a.Start + new V3(0, -0.1, 1).Unit * a.PathLength; a.Speed = swingSpeed;
                a.AlignContact(new PhysicsSettings(), 0.95);
                var hit = a.Simulate(new PhysicsSettings());
                alignedHits &= hit.Contact != null && Math.Abs(hit.Contact.Before.Position.Y - 0.95) < 1e-5 &&
                    Math.Abs(a.Velocity.Length - swingSpeed) < 1e-8;
            }
            check(alignedHits, "Assisted contact remains on the selected height across normal angle and speed edits");
            var independent = new ServeStroke(); independent.AlignContact(new PhysicsSettings(), 0.95);
            V3 fixedStart = independent.Start, fixedEnd = independent.End, fixedNormal = independent.Normal;
            independent.Speed = 2; independent.AlignContact(new PhysicsSettings(), 0.95);
            check((independent.Start - fixedStart).Length < 1e-10 && (independent.End - fixedEnd).Length < 1e-10 &&
                (independent.Normal - fixedNormal).Length < 1e-10 && Math.Abs(independent.Velocity.Length - 2) < 1e-10 &&
                Math.Abs(independent.Duration - independent.PathLength / 2) < 1e-10,
                "Changing speed preserves path and face; duration is length divided by speed");
            double oldDuration = independent.Duration;
            V3 midpoint = (independent.Start + independent.End) * 0.5;
            independent.Start = midpoint + (independent.Start - midpoint) * 1.5;
            independent.End = midpoint + (independent.End - midpoint) * 1.5;
            independent.AlignContact(new PhysicsSettings(), 0.95);
            check(independent.Speed == 2 && Math.Abs(independent.Velocity.Length - 2) < 1e-10 &&
                Math.Abs(independent.Duration - oldDuration * 1.5) < 1e-10,
                "Lengthening the path preserves selected speed and increases duration proportionally");
            fixedStart = independent.Start; fixedEnd = independent.End;
            independent.Speed = 0.5; independent.AlignContact(new PhysicsSettings(), 0.95);
            check(independent.StartTime < 0 && (independent.Start - fixedStart).Length < 1e-10 &&
                (independent.End - fixedEnd).Length < 1e-10 && independent.Simulate(new PhysicsSettings()).Contact != null,
                "Slow aligned strokes may start before the toss apex without moving their path");
            independent.Speed = 0; independent.AlignContact(new PhysicsSettings(), 0.95);
            var stationary = independent.Simulate(new PhysicsSettings());
            check(independent.Duration == 0 && independent.Velocity.Length == 0 && stationary.Contact != null &&
                (independent.CenterAt(0) - independent.CenterAt(1)).Length < 1e-10,
                "Zero speed holds the racket at contact without dividing by zero");
            check(ServePresetLibrary.All.Count == 16, "Serve library contains eight right-handed styles in short and long versions");
            bool presetResults = true, presetCopies = true;
            foreach (var preset in ServePresetLibrary.All)
            {
                var p = new PhysicsSettings(); var action = preset.CreateStroke(p);
                var result = action.Simulate(p);
                presetResults &= preset.Matches(result) && result.Contact.Before.Velocity.Y < 0 && result.Flight.NetClearance > 0.01;
                action.Speed = 0;
                presetCopies &= preset.CreateStroke(p).Speed > 0;
                report.AppendLine($"Preset {preset.Name}: {ServePresetLibrary.Classify(result.Flight)}; matched={preset.Matches(result)}");
            }
            check(presetResults, "Every preset produces its labelled spin and actual short/long route with a clear racket");
            check(presetCopies, "Editing a loaded serve never mutates the stored preset");
            var floatPreset = ServePresetLibrary.Get(7, 0).CreateStroke(new PhysicsSettings());
            var floatResult = floatPreset.Simulate(new PhysicsSettings());
            check(floatPreset.Friction == 0.8 && floatResult.Contact.After.Spin.Length / BallPhysics.Rps < 0.05 &&
                floatResult.Flight.Events[0].After.Spin.Length > floatResult.Contact.After.Spin.Length,
                "No-spin preset uses normal contact with real rubber friction, and the table can add spin afterwards");
            bool calibratedPresets = true, materialsPreserved = true, stylePreserved = true;
            int combinations = 0;
            foreach (var rubber in RubberCalibrations.All)
            {
                if (InvertedRubberResponses.Find(rubber.Id) == null) continue;
                foreach (bool adaptive in new[] { false, true })
                    foreach (var preset in ServePresetLibrary.All)
                    {
                        var physics = new PhysicsSettings();
                        var original = preset.CreateStroke(physics);
                        int responseId = adaptive ? rubber.Id : 0;
                        var action = preset.CreateStroke(physics, new RubberCoefficients(rubber.Restitution, rubber.Friction, rubber.Tangent), responseId);
                        var result = action.Simulate(physics); combinations++;
                        calibratedPresets &= preset.Matches(result) && result.Flight.NetClearance >= 0.015;
                        materialsPreserved &= action.Restitution == rubber.Restitution && action.Friction == rubber.Friction &&
                            action.Tangent == rubber.Tangent && action.ResponseId == responseId && physics.TableFriction == 0.2;
                        stylePreserved &= (action.Apex - original.Apex).Length < 1e-9 && action.Roll == original.Roll &&
                            Math.Abs(action.PathLength - original.PathLength) < 1e-9 &&
                            V3.Dot(action.Normal, original.Normal) >= Math.Cos(20 * Math.PI / 180) &&
                            Math.Abs(result.Contact.Before.Position.Y - preset.ContactHeight) < 1e-7;
                    }
            }
            check(combinations == 160 && calibratedPresets, "All 160 inverted-rubber/mode/preset combinations clear the net and retain labelled spin and length");
            check(materialsPreserved, "Matching changes the action, never the selected rubber coefficients, response ID or table physics");
            check(stylePreserved, "Matched actions retain toss, contact height, handle rotation, path length and nearby face orientation");
            bool impossibleRejected = false;
            try { ServePresetLibrary.Get(6, 0).CreateStroke(new PhysicsSettings(), new RubberCoefficients(.8, 0, .15)); }
            catch (ArgumentException) { impossibleRejected = true; }
            check(impossibleRejected, "A frictionless racket does not fabricate a backspin preset or silently fall back to another material");
            report.AppendLine($"{count} checks passed. Numerical checks do not establish real-world calibration.");
            return report.ToString();
        }
    }
}

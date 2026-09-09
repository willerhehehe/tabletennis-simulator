using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TableTennis
{
    public enum ServeType { PendulumBackspin, PendulumTopspin, ReverseBackspin, ReverseTopspin, HookBackspin, HookTopspin, PureBackspin, NoSpin }
    public enum ServeLength { Short, Long, Other }

    public sealed class ServePreset
    {
        public readonly ServeType Type;
        public readonly ServeLength TargetLength;
        public readonly double ContactHeight;
        readonly V3 apex, velocity;
        readonly double angle, yaw, roll, pathLength;
        public string Name => ServePresetLibrary.TypeNames[(int)Type] + (TargetLength == ServeLength.Short ? "·短球" : "·长球");
        public string Id => Type + "-" + TargetLength;
        public string SourceUrl => Type <= ServeType.PendulumTopspin ? "https://blog.paddlepalace.com/2013/08/a-step-by-step-sequence-to-learning-pendulum-serves/" :
            Type <= ServeType.ReverseTopspin ? "https://blog.paddlepalace.com/2012/04/coaching-tip-reverse-forehand-pendulum-serve/" :
            Type <= ServeType.HookTopspin ? "https://www.sohu.com/a/141045212_161690" :
            Type == ServeType.PureBackspin ? "https://www.pingskills.com/table-tennis-forum/backspin-sidespin-pendulum" :
            "https://www.pingskills.com/table-tennis-forum/the-float-serve---with-a-fast-action";
        public string ActionHint => Type switch
        {
            ServeType.PendulumBackspin => "右→左摩擦球的后下部，拍面较开，拍头偏下。",
            ServeType.PendulumTopspin => "右→左横向摩擦，触球段带上提，拍面比侧下更立。",
            ServeType.ReverseBackspin => "左→右摩擦后下部，改变顺旋的侧向摩擦方向。",
            ServeType.ReverseTopspin => "左→右带上提摩擦球的后侧，形成反向侧旋和上旋。",
            ServeType.HookBackspin => "采用拍头偏上的轻勾变式，向右包侧并向下切摩擦。",
            ServeType.HookTopspin => "拍头偏上、包球侧部。上旋由触点相对下落球的摩擦决定，不只看手臂升降。",
            ServeType.PureBackspin => TargetLength == ServeLength.Short ? "较平拍面摩擦球底，去掉横向分量，以细摩擦控制短球。" : "适度立拍，向前摩擦后下部，多送球；旋转仍保持纯下旋。",
            _ => "接近球心方向撞击，减少切向滑动。不转指离拍时，落台后仍会产生旋转。"
        };
        public string Tactic => Type switch
        {
            ServeType.PendulumBackspin or ServeType.PendulumTopspin => "同一行切换侧上/侧下，搭配长短变化，观察接发拍型差异。",
            ServeType.ReverseBackspin or ServeType.ReverseTopspin => "与顺旋形成侧旋方向变化，再用同类上下旋干扰判断。",
            ServeType.HookBackspin or ServeType.HookTopspin => "勾手上下旋成对使用；外观可以相近，重点辨认触球瞬间。",
            _ => "纯下旋与不转配对，比较对方搓接时的下网和冒高风险。"
        };

        internal ServePreset(ServeType type, ServeLength length, V3 apex, double height, double angle, double yaw, double roll, V3 velocity, double path)
        { Type = type; TargetLength = length; this.apex = apex; ContactHeight = height; this.angle = angle; this.yaw = yaw; this.roll = roll; this.velocity = velocity; pathLength = path; }

        public ServeStroke CreateStroke(PhysicsSettings physics)
        {
            var s = new ServeStroke { Apex = apex, TableAngle = angle, Yaw = yaw, Roll = roll };
            V3 v = velocity;
            if (Type == ServeType.NoSpin)
            {
                s.Speed = 0; s.Start = s.End = new V3(0, 1, -1.65);
                var falling = s.AlignContact(physics, ContactHeight);
                // Keep rubber friction; zero tangential relative velocity produces the float contact.
                v = falling.Velocity + s.Normal * (velocity.Z / s.Normal.Z);
            }
            s.Speed = v.Length;
            s.Start = new V3(0, 1, -1.65) - v.Unit * (pathLength * 0.5);
            s.End = s.Start + v.Unit * pathLength;
            s.AlignContact(physics, ContactHeight);
            return s;
        }

        // Match the recipe to the chosen racket; every returned action is verified by full forward simulation.
        public ServeStroke CreateStroke(PhysicsSettings physics, RubberCoefficients rubber, int responseId = 0)
        {
            var reference = CreateStroke(physics);
            var candidate = reference.Copy();
            candidate.Restitution = rubber.Restitution; candidate.Friction = rubber.Friction;
            candidate.Tangent = rubber.Tangent; candidate.ResponseId = responseId;
            bool Usable(ServeStrokeResult r) => Matches(r) && r.Flight.NetClearance >= 0.015;
            if (Usable(candidate.Simulate(physics))) return candidate;
            var contact = reference.Simulate(physics).Contact;
            if (contact == null) throw new ArgumentException("当前落球条件不能形成预设触球，原动作已保留。");
            // ponytail: bounded local recipe matching, not a general serve optimizer. Widen only with measured motions.
            var options = new List<(double spin, double impulse)>();
            for (int spin = 0; spin <= 10; spin++)
                for (int impulse = -6; impulse <= 6; impulse++) options.Add((1 - spin * 0.05, 1 + impulse * 0.025));
            double Cost((double spin, double impulse) o) => Math.Pow((o.spin - 1) / 0.2, 2) + Math.Pow((o.impulse - 1) / 0.08, 2);
            options.Sort((a, b) => Cost(a).CompareTo(Cost(b)));
            foreach (var option in options)
            {
                try
                {
                    var target = contact.After;
                    target.Spin *= option.spin;
                    target.Velocity = contact.Before.Velocity + (contact.After.Velocity - contact.Before.Velocity) * option.impulse;
                    var hit = ServeContact.SolveAtToss(target, rubber.Restitution, rubber.Friction, rubber.Tangent, contact.Before.Velocity.Y, responseId);
                    if (V3.Dot(hit.Normal, reference.Normal) < Math.Cos(20 * Math.PI / 180)) continue;
                    candidate.TableAngle = Math.Acos(Math.Max(-1, Math.Min(1, hit.Normal.Y))) * 180 / Math.PI;
                    candidate.Yaw = Math.Atan2(hit.Normal.X, hit.Normal.Z) * 180 / Math.PI;
                    candidate.Speed = hit.RacketVelocity.Length;
                    candidate.End = candidate.Start + hit.RacketVelocity.Unit * reference.PathLength;
                    candidate.AlignContact(physics, ContactHeight);
                    if (Usable(candidate.Simulate(physics))) return candidate;
                }
                catch (ArgumentException) { } // A target may exceed this racket's friction or the allowed stroke speed.
            }
            throw new ArgumentException("当前材料与落球条件下未匹配到这种旋转和长短的有效发球，原动作已保留；可换一种预设或调整器材参数。");
        }

        public bool Matches(ServeStrokeResult result)
        {
            if (result?.Contact == null || !result.TwoBounces || result.MayTouchTable || ServePresetLibrary.Classify(result.Flight) != TargetLength) return false;
            var c = result.Contact.After;
            V3 spin = BallSpin.Components(c.Spin / BallPhysics.Rps, c.Velocity);
            if (Type == ServeType.NoSpin) return spin.Length < 0.1;
            if (Type == ServeType.PureBackspin) return spin.X < -10 && Math.Abs(spin.Y) < 0.05 && Math.Abs(spin.Z) < 0.05;
            int side = Type <= ServeType.PendulumTopspin ? 1 : -1;
            bool back = Type == ServeType.PendulumBackspin || Type == ServeType.ReverseBackspin || Type == ServeType.HookBackspin;
            return spin.Y * side > 5 && (back ? spin.X < -5 : spin.X > 5);
        }
    }

    public static class ServePresetLibrary
    {
        public static readonly string[] TypeNames = { "顺旋·侧下", "顺旋·侧上", "逆旋·侧下", "逆旋·侧上", "勾手·侧下", "勾手·侧上", "纯下旋", "不转球" };
        public static readonly IReadOnlyList<ServePreset> All = Array.AsReadOnly(new[]
        {
            new ServePreset((ServeType)0, ServeLength.Short, new V3(-0.45, 1.33554017931947, -1.63712805798144), 1.09554017931947, 48.7040328675434, 41.0141750169053, 155, new V3(-1.07644848207778, -2.08467843494596, 5.08661515735398), 0.26),
            new ServePreset((ServeType)0, ServeLength.Long, new V3(-0.45, 1.50672560503088, -1.5865919527349), 1.08672560503088, 57.3165509772098, 21.6090895727319, 155, new V3(-1.42738527535805, -3.43572745366754, 5.03622677644539), 0.26),
            new ServePreset((ServeType)1, ServeLength.Short, new V3(-0.45, 1.32419047809401, -1.56793542868827), 1.08419047809401, 76.2393049552288, 45.7131181050619, 155, new V3(-4.97305394842897, 2.41414382490988, 6.50149547006073), 0.26),
            new ServePreset((ServeType)1, ServeLength.Long, new V3(-0.45, 1.41295806361966, -1.54222814045019), 0.992958063619658, 91.2791465717736, 28.7377200241842, 155, new V3(-2.15240800760333, 0.515788442741981, 6.16079582141749), 0.26),
            new ServePreset((ServeType)2, ServeLength.Short, new V3(-0.3, 1.50056738361743, -1.58952134798724), 1.08056738361743, 45.5748415652545, -36.4052717017034, -155, new V3(3.68277905489447, -0.873832306160513, 4.43022533852152), 0.26),
            new ServePreset((ServeType)2, ServeLength.Long, new V3(-0.3, 1.43751763900626, -1.76329097218033), 1.01751763900626, 70.6509203625149, -11.4042970246655, -155, new V3(3.23726398150309, -4.04057451912229, 5.47761241881066), 0.26),
            new ServePreset((ServeType)3, ServeLength.Short, new V3(-0.3, 1.27494074333224, -1.72326233945008), 1.09494074333224, 81.7430776808146, -21.7109754894446, -155, new V3(3.42591642570957, 0.841261995370156, 3.47101575903176), 0.26),
            new ServePreset((ServeType)3, ServeLength.Long, new V3(-0.3, 1.20907891694879, -1.52032479610309), 1.02907891694879, 98.0889707212751, -16.0780161712682, -155, new V3(4.08904278063636, 0.877702836961347, 5.44829948174222), 0.26),
            new ServePreset((ServeType)4, ServeLength.Short, new V3(-0.15, 1.33698329878831, -1.53798361467104), 1.09698329878831, 57.015247728217, -22.5130267564734, -25, new V3(2.02620080710677, -2.64801641122811, 4.07124953394348), 0.22),
            new ServePreset((ServeType)4, ServeLength.Long, new V3(-0.15, 1.62619480991559, -1.52717098793349), 1.07619480991559, 57.0117950127515, -27.1367242145989, -25, new V3(4.03970143969157, -3.85250253377133, 6.74155739869064), 0.22),
            new ServePreset((ServeType)5, ServeLength.Short, new V3(-0.15, 1.39127977341007, -1.6641237303727), 1.07127977341007, 79.3040676295311, -29.0324430507759, -25, new V3(3.97890651951493, 1.86245531750026, 3.76233804354553), 0.22),
            new ServePreset((ServeType)5, ServeLength.Long, new V3(-0.15, 1.62681086750553, -1.75464692651976), 1.07681086750553, 79.7295124292045, -12.6494621283605, -25, new V3(2.51412653979572, -0.833445433542806, 4.31727648587771), 0.22),
            new ServePreset((ServeType)6, ServeLength.Short, new V3(0, 1.64333114934775, -1.54971193960389), 1.09333114934775, 27.4745479270232, 0, -70, new V3(0, -3.32357255724425, 5.45581501138202), 0.26),
            new ServePreset((ServeType)6, ServeLength.Long, new V3(0, 1.5192615388051, -1.57133011777482), 1.0992615388051, 55.336261043016, 0, -70, new V3(0, -4.07002937177663, 4.83338310468634), 0.26),
            new ServePreset((ServeType)7, ServeLength.Short, new V3(0, 1.33962405662966, -1.59687217439379), 1.09962405662966, 71.696680400845, 0, -70, new V3(0, -1.31898546563972, 2.46858660834536), 0.26),
            new ServePreset((ServeType)7, ServeLength.Long, new V3(0, 1.23791620557099, -1.67738683234779), 1.05791620557099, 86.2304326012872, 0, -70, new V3(0, -1.59885480410396, 3.91530382450806), 0.26),
        });

        public static ServePreset Get(int type, int length)
        {
            PhysicsSettings.Range(type, 0, 7, nameof(type)); PhysicsSettings.Range(length, 0, 1, nameof(length));
            return All[type * 2 + length];
        }

        public static ServeLength Classify(FlightResult flight)
        {
            if (flight == null || flight.Events.Count < 2 || flight.Events[0].Kind != "球台" || flight.Events[1].Kind != "球台" ||
                flight.Events[0].Before.Position.Z >= 0 || flight.Events[1].Before.Position.Z <= 0) return ServeLength.Other;
            if (flight.Events.Count >= 3 && flight.Events[2].Kind == "球台" && flight.Events[2].Before.Position.Z > 0) return ServeLength.Short;
            double start = flight.Events[1].After.Time;
            foreach (var s in flight.Samples)
            {
                if (s.Time <= start) continue;
                if (Math.Abs(s.Position.X) > BallPhysics.HalfWidth) return ServeLength.Other;
                if (s.Position.Z > BallPhysics.HalfLength) return ServeLength.Long;
                if (s.Position.Y < BallPhysics.TableHeight + BallPhysics.Radius - 1e-6) break;
            }
            return ServeLength.Other;
        }

        public static string Report(PhysicsSettings physics)
        {
            var csv = new StringBuilder("name,angle_deg,yaw_deg,roll_deg,racket_speed,ball_speed,top_rps,side_rps,axial_rps,net_clearance_m,bounce1_x,bounce1_z,bounce2_x,bounce2_z,actual_length,matched\n");
            foreach (var preset in All)
            {
                var action = preset.CreateStroke(physics); var result = action.Simulate(physics); var shot = result.Contact.After;
                V3 spin = BallSpin.Components(shot.Spin / BallPhysics.Rps, shot.Velocity);
                V3 first = result.Flight.Events[0].Before.Position, second = result.Flight.Events[1].Before.Position;
                csv.AppendLine(FormattableString.Invariant($"{preset.Name},{action.TableAngle:0.000},{action.Yaw:0.000},{action.Roll:0.000},{action.Speed:0.000},{shot.Velocity.Length:0.000},{spin.X:0.000},{spin.Y:0.000},{spin.Z:0.000},{result.Flight.NetClearance:0.000000},{first.X:0.000},{first.Z:0.000},{second.X:0.000},{second.Z:0.000},{Classify(result.Flight)},{preset.Matches(result)}"));
            }
            return csv.ToString();
        }
    }
}

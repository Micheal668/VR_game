using System;
using System.Collections.Generic;
using UnityEngine;

namespace LunarEscape
{
    public enum DockingState { Waiting,Approaching,Capturing,Docked,Failed }
    public enum DockCommand { Forward,Backward,Left,Right,Up,Down,YawLeft,YawRight,PitchUp,PitchDown,RollLeft,RollRight,Brake,MainBoost }

    // 米、米/秒、度/秒。目标对接口固定为原点，玩家飞船在目标坐标中运动。
    // 只由 AscentMission 的同一时钟推进；视图可移动轨道器，不移动真实头显。
    public sealed class DockingMission : MonoBehaviour
    {
        [SerializeField] private DockingConfig config;
        [SerializeField] private AscentMission flight;
        private readonly HashSet<DockCommand> commands=new();
        private float captureRemaining;
        private uint version;
        public DockingConfig Config=>config;
        public DockingState State {get;private set;}
        public Vector3 Position {get;private set;}
        public Vector3 Velocity {get;private set;}
        public Quaternion Attitude {get;private set;}=Quaternion.identity;
        public Vector3 AngularVelocity {get;private set;}
        public float RcsFuel {get;private set;}
        public float Distance=>Position.magnitude;
        public float LateralError=>new Vector2(Position.x,Position.y).magnitude;
        public float AlignmentError=>Quaternion.Angle(Attitude,Quaternion.identity);
        public float RelativeSpeed=>Velocity.magnitude;
        public float ClosingSpeed=>Vector3.Dot(Velocity,-Position.normalized);
        public bool AssistanceEnabled {get;private set;}=true;
        public bool AssistanceActive {get;private set;}
        public bool Active=>State==DockingState.Approaching;
        public bool CanAssist=>Active && AssistanceEnabled && Position.z<-.35f && -Position.z<=config.AssistDistance
            && LateralError<=config.AssistLateral && AlignmentError<=config.AssistAngle
            && RelativeSpeed<=config.AssistSpeed && AngularVelocity.magnitude<2 && RcsFuel>0;
        public bool CanBoost=>Active && Distance>12 && flight.MainFuel>0;
        public int ActiveCommandCount=>commands.Count;
        public event Action Changed;

        public void Configure(AscentMission task,DockingConfig settings)
        {if(task==null||settings==null)throw new ArgumentNullException();flight=task;config=settings;ResetDocking();}
        public void ResetDocking()
        {
            ++version;commands.Clear();State=DockingState.Waiting;AssistanceEnabled=true;AssistanceActive=false;captureRemaining=0;
            Position=config!=null?config.StartPosition:Vector3.back*35;Velocity=config!=null?config.StartVelocity:Vector3.zero;
            Attitude=config!=null?config.StartAttitude:Quaternion.identity;AngularVelocity=Vector3.zero;RcsFuel=config!=null?config.InitialRcs:0;Changed?.Invoke();
        }
        internal void BeginApproach(){if(State!=DockingState.Waiting)return;State=DockingState.Approaching;Changed?.Invoke();}
        public void SetCommand(DockCommand command,bool pressed)
        {if(!Active||!Enum.IsDefined(typeof(DockCommand),command)){commands.Remove(command);return;}if(pressed)commands.Add(command);else commands.Remove(command);}
        public void ReleaseControls()=>commands.Clear();
        public void ToggleAssistance(){if(!Active)return;AssistanceEnabled=!AssistanceEnabled;Changed?.Invoke();}
        internal void StopForFailure(){State=DockingState.Failed;commands.Clear();AssistanceActive=false;Changed?.Invoke();}
        internal void Tick(float seconds)
        {
            if(!float.IsFinite(seconds)||seconds<=0||flight.Phase==AscentPhase.Failed)return;
            uint current=version;
            float left=seconds;
            while(left>0 && current==version && (Active||State==DockingState.Capturing))
            {
                float step=Mathf.Min(.02f,left);left-=step;
                if(State==DockingState.Capturing)
                {
                    captureRemaining=Mathf.Max(0,captureRemaining-step);
                    Position=Vector3.Lerp(Position,new Vector3(0,0,-.22f),1-Mathf.Exp(-3*step));
                    Attitude=Quaternion.Slerp(Attitude,Quaternion.identity,1-Mathf.Exp(-3*step));
                    if(captureRemaining<=.00001f){Position=new Vector3(0,0,-.22f);Attitude=Quaternion.identity;State=DockingState.Docked;flight.CompleteDocking(this);}
                    continue;
                }
                Integrate(step);
            }
            if(current==version)Changed?.Invoke();
        }
        private float Axis(DockCommand positive,DockCommand negative)=>(commands.Contains(positive)?1:0)-(commands.Contains(negative)?1:0);
        private void Integrate(float dt)
        {
            Vector3 local=new(Axis(DockCommand.Right,DockCommand.Left),Axis(DockCommand.Up,DockCommand.Down),Axis(DockCommand.Forward,DockCommand.Backward));
            Vector3 rotation=new(Axis(DockCommand.PitchDown,DockCommand.PitchUp),Axis(DockCommand.YawRight,DockCommand.YawLeft),Axis(DockCommand.RollLeft,DockCommand.RollRight));
            Vector3 acceleration=Attitude*Vector3.ClampMagnitude(local,1)*config.TranslationAcceleration;
            Vector3 angularAcceleration=Vector3.ClampMagnitude(rotation,1)*config.RotationAcceleration;
            float thrustUse=local.magnitude+rotation.magnitude*.5f;
            bool braking=commands.Contains(DockCommand.Brake);
            AssistanceActive=CanAssist && commands.Count==0;
            if(braking)
            {
                acceleration=Vector3.ClampMagnitude(-Velocity/dt,config.TranslationAcceleration);
                angularAcceleration=Vector3.ClampMagnitude(-AngularVelocity/dt,config.RotationAcceleration);
                thrustUse=acceleration.magnitude/config.TranslationAcceleration+angularAcceleration.magnitude/config.RotationAcceleration*.5f;
            }
            else if(AssistanceActive)
            {
                Vector3 desired=new(Mathf.Clamp(-Position.x*.35f,-.12f,.12f),Mathf.Clamp(-Position.y*.35f,-.12f,.12f),Mathf.Min(.16f,Mathf.Max(.045f,(-Position.z-.22f)*.12f)));
                acceleration=Vector3.ClampMagnitude((desired-Velocity)*1.4f,.12f);
                Quaternion difference=Quaternion.Inverse(Attitude);difference.ToAngleAxis(out float angle,out Vector3 axis);
                if(angle>180)angle-=360;
                angularAcceleration=Vector3.ClampMagnitude(axis*angle*.5f-AngularVelocity*1.4f,2);
                thrustUse=acceleration.magnitude/config.TranslationAcceleration+angularAcceleration.magnitude/config.RotationAcceleration*.5f;
            }
            // 辅助和制动都真实消耗 RCS；油量不足时只提供剩余燃料对应的推力。
            float needed=thrustUse*config.RcsConsumption*dt;
            float fraction=needed>0?Mathf.Min(1,RcsFuel/needed):1;
            RcsFuel=Mathf.Max(0,RcsFuel-needed);acceleration*=fraction;angularAcceleration*=fraction;
            if(commands.Contains(DockCommand.MainBoost) && CanBoost)
            {
                float used=flight.ConsumeMainFuel(this,2.2f*dt);
                acceleration+=Attitude*Vector3.forward*(.7f*used/(2.2f*dt));
            }
            var previousPosition=Position;
            Velocity+=acceleration*dt;AngularVelocity+=angularAcceleration*dt;
            Position+=Velocity*dt;Attitude=Quaternion.Normalize(Attitude*Quaternion.Euler(AngularVelocity*dt));
            if(RcsFuel<=.00001f){Fail(AscentFailure.RcsDepleted);return;}
            // 连续检测越过接口平面的路径，高速度的一帧不能直接穿到轨道器背后。
            if(previousPosition.z<-.55f && Position.z>=-.55f)
            {
                float crossing=(-.55f-previousPosition.z)/(Position.z-previousPosition.z);
                Vector3 contact=Vector3.Lerp(previousPosition,Position,crossing);
                if(new Vector2(contact.x,contact.y).magnitude<3 && RelativeSpeed>config.CrashSpeed)
                {Fail(AscentFailure.DockingCollision);return;}
            }
            if(Position.z>=4.5f && Position.z<=5.9f && Mathf.Abs(Position.x)<=8.3f && Mathf.Abs(Position.y)<=2.2f)
            {Fail(RelativeSpeed>config.CrashSpeed?AscentFailure.DockingCollision:AscentFailure.DockingMisaligned);return;}
            if(Distance>config.MaximumDistance || LateralError>config.MaximumLateralDistance || Position.z>6)
            {Fail(AscentFailure.TargetLost);return;}
            // 进入捕获圈前必须已减速并对齐；高速撞到接口不能被吸附救回。
            if(Position.z>=-.55f && LateralError<3)
            {
                if(RelativeSpeed>config.CrashSpeed){Fail(AscentFailure.DockingCollision);return;}
                bool capture=AssistanceEnabled && Position.z<0 && LateralError<=.18f && AlignmentError<=3 && RelativeSpeed<=.12f && AngularVelocity.magnitude<=1;
                if(capture)
                {State=DockingState.Capturing;captureRemaining=2;Velocity=AngularVelocity=Vector3.zero;commands.Clear();AssistanceActive=false;flight.BeginDockingCapture(this);}
                else if(Position.z>=-.25f)Fail(AscentFailure.DockingMisaligned);
            }
        }
        private void Fail(AscentFailure reason){StopForFailure();flight.FailDocking(this,reason);}
    }
}

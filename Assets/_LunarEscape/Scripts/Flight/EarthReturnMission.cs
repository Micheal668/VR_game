using System;
using UnityEngine;

namespace LunarEscape
{
    public enum EarthReturnPhase
    {
        Inactive, AwaitingTransfer, TransferFade, CrewTransfer, AwaitingSeparation,
        Separating, AwaitingDeparture, DepartureBurn, Coast, EarthApproach,
        OrbitHold, DeorbitBurn, ServiceSeparation, Entry, AwaitingParachutes, Parachutes,
        AwaitingHeatShield, HeatShieldRelease, Splashdown, Complete,
        Breakup, SkipOut, Failed, HardLanding
    }

    public enum EarthReturnFailure { None, TooSteep, TooShallow, MissedParachutes, MissedHeatShield }

    // 游戏中的压缩航程。下降只有一条连续时钟，操作与舱体分离不能暂停下落。
    public sealed class EarthReturnMission : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        [SerializeField] private CrewMission crew;
        public const float MinimumEntryAngle = 5.5f, MaximumEntryAngle = 7.5f;
        public const float ParachuteMaximumKm = 3.2f, ParachuteMinimumKm = 1.8f;
        public const float ShieldMaximumKm = 1.0f, ShieldMinimumKm = .4f;
        public const float DescentSeconds = 22, NominalFlightSeconds = 60;
        public EarthReturnPhase Phase { get; private set; }
        public EarthReturnFailure Failure { get; private set; }
        public float Elapsed { get; private set; }
        public float FlightElapsed { get; private set; }
        public float DescentElapsed { get; private set; }
        public bool Paused { get; private set; }
        public bool CommanderTransferred { get; private set; }
        public bool PilotPresent { get; private set; }
        public bool InReturnCabin => Phase >= EarthReturnPhase.CrewTransfer;
        public bool IsComplete => Phase == EarthReturnPhase.Complete;
        public bool IsFailed => Phase == EarthReturnPhase.Failed;
        public bool TooSteep => Failure == EarthReturnFailure.TooSteep;
        public float EntryAngle { get; private set; } = 3;
        public float SpeedKmS { get; private set; }
        public float AltitudeKm { get; private set; }
        public float HeatC { get; private set; } = 20;
        public float GLoad { get; private set; } = 1;
        public float Damage { get; private set; }
        public float OutsideTemperatureC { get; private set; } = -120;
        public float Throttle { get; private set; } = .5f;
        public float HeadingDegrees { get; private set; }
        public bool ParachutesDeployed { get; private set; }
        public bool HeatShieldReleased { get; private set; }
        private float chuteOpenedAt, shieldReleasedAt, phaseDescentStart, failureAltitude, failureSpeed;
        public float ParachuteAge => ParachutesDeployed ? Mathf.Max(0, DescentElapsed - chuteOpenedAt) : 0;
        public float HeatShieldAge => HeatShieldReleased ? Mathf.Max(0, DescentElapsed - shieldReleasedAt) : 0;
        public bool InDescent => Phase >= EarthReturnPhase.AwaitingParachutes && Phase <= EarthReturnPhase.Splashdown;
        public bool HasEnteredAtmosphere => Phase == EarthReturnPhase.Entry || InDescent || IsComplete || Phase == EarthReturnPhase.HardLanding
            || IsFailed && (Failure == EarthReturnFailure.MissedParachutes || Failure == EarthReturnFailure.MissedHeatShield);
        public bool CanThrottle => Phase == EarthReturnPhase.AwaitingDeparture || Phase == EarthReturnPhase.DepartureBurn || Phase == EarthReturnPhase.OrbitHold || Phase == EarthReturnPhase.DeorbitBurn;
        public bool CanSteer => Phase == EarthReturnPhase.OrbitHold;
        public bool CanDeployParachutes => InDescent && !ParachutesDeployed && AltitudeKm <= ParachuteMaximumKm + .0001f && AltitudeKm >= ParachuteMinimumKm - .0001f;
        public bool CanReleaseHeatShield => InDescent && ParachutesDeployed && !HeatShieldReleased && ParachuteAge >= 2
            && AltitudeKm <= ShieldMaximumKm + .0001f && AltitudeKm >= ShieldMinimumKm - .0001f;
        public string FailureKey => Failure switch
        {
            EarthReturnFailure.TooSteep => "return.failure.steep",
            EarthReturnFailure.TooShallow => "return.failure.shallow",
            EarthReturnFailure.MissedParachutes => "return.failure.parachutes",
            EarthReturnFailure.MissedHeatShield => "return.failure.shield",
            _ => "return.failure.total"
        };
        public void IncreaseThrottle() => SetThrottle(Throttle + .1f);
        public void DecreaseThrottle() => SetThrottle(Throttle - .1f);
        public void SetThrottle(float value)
        {
            if (!CanThrottle || Paused || !float.IsFinite(value)) return;
            Throttle = value < .0001f ? 0 : value > .9999f ? 1 : Mathf.Clamp01(value); Changed?.Invoke();
        }
        public void TurnLeft() => Turn(-2);
        public void TurnRight() => Turn(2);
        private void Turn(float amount)
        { if (!CanSteer || Paused) return; HeadingDegrees = Mathf.Clamp(HeadingDegrees + amount, -20, 20); Changed?.Invoke(); }
        public void DeployParachutes()
        {
            if (Paused || !CanDeployParachutes) return;
            ParachutesDeployed = true; chuteOpenedAt = DescentElapsed; Enter(EarthReturnPhase.Parachutes);
        }
        public void ReleaseHeatShield()
        {
            if (Paused || !CanReleaseHeatShield) return;
            HeatShieldReleased = true; shieldReleasedAt = DescentElapsed; Enter(EarthReturnPhase.HeatShieldRelease);
        }
        public double SimulatedSeconds { get; private set; }
        public float EarthRotation => (float)(SimulatedSeconds / 86164.0 * 360.0 % 360.0);
        public float TimeCompression => Phase == EarthReturnPhase.Coast ? 259200f / 8
            : Phase == EarthReturnPhase.EarthApproach ? Mathf.Lerp(1800, 1, Mathf.SmoothStep(0, 1, Progress)) : 1;
        public bool InCorridor => EntryAngle >= MinimumEntryAngle && EntryAngle <= MaximumEntryAngle;
        public void Shallower() => AdjustAngle(-.5f);
        public void Steeper() => AdjustAngle(.5f);
        private void AdjustAngle(float delta)
        { if (!CanSteer || Paused) return; EntryAngle = Mathf.Clamp(EntryAngle + delta, 1, 12); Changed?.Invoke(); }
        public bool IsAnimating => Duration > 0;
        public float Progress => Duration > 0 ? Mathf.Clamp01(Elapsed / Duration) : 0;
        public float Duration => InDescent ? Mathf.Max(.01f, DescentSeconds - phaseDescentStart) : Phase switch
        {
            EarthReturnPhase.TransferFade => 1.2f,
            EarthReturnPhase.CrewTransfer => 7,
            EarthReturnPhase.Separating => 4,
            EarthReturnPhase.DepartureBurn => 4,
            EarthReturnPhase.Coast => 8,
            EarthReturnPhase.EarthApproach => 5,
            EarthReturnPhase.DeorbitBurn => 3,
            EarthReturnPhase.ServiceSeparation => 2,
            EarthReturnPhase.Entry => 16,
            EarthReturnPhase.Breakup => 5,
            EarthReturnPhase.SkipOut => 8,
            EarthReturnPhase.HardLanding => 3,
            _ => 0
        };
        public event Action Changed;
        public void Configure(AscentMission ascent, CrewMission members)
        {
            if (flight != null) flight.PhaseChanged -= OnFlight;
            flight = ascent; crew = members;
            if (isActiveAndEnabled && flight != null) flight.PhaseChanged += OnFlight;
            if (flight != null) OnFlight(flight.Phase);
        }
        private void OnEnable()
        { if (flight != null) { flight.PhaseChanged += OnFlight; OnFlight(flight.Phase); } }
        private void OnDisable() { if (flight != null) flight.PhaseChanged -= OnFlight; }
        private void OnFlight(AscentPhase phase)
        {
            if (phase == AscentPhase.AwaitingBoarding)
            {
                CommanderTransferred = PilotPresent = Paused = ParachutesDeployed = HeatShieldReleased = false;
                EntryAngle = 3; Failure = EarthReturnFailure.None; HeatC = 20; GLoad = 1; Damage = SpeedKmS = AltitudeKm = 0;
                SimulatedSeconds = 0; FlightElapsed = DescentElapsed = chuteOpenedAt = shieldReleasedAt = phaseDescentStart = 0;
                OutsideTemperatureC = -120; Throttle = .5f; HeadingDegrees = 0;
                Enter(EarthReturnPhase.Inactive);
            }
            else if (phase == AscentPhase.Docked && Phase == EarthReturnPhase.Inactive) Enter(EarthReturnPhase.AwaitingTransfer);
        }
        public void Confirm()
        {
            if (Paused) return;
            if (Phase == EarthReturnPhase.AwaitingTransfer)
            {
                CommanderTransferred = crew != null && crew.CommanderBoarded && crew.GetStatus(CrewRole.Commander).IsAlive;
                PilotPresent = crew != null && crew.GetStatus(CrewRole.OrbitalPilot).IsAlive;
                Enter(EarthReturnPhase.TransferFade);
            }
            else if (Phase == EarthReturnPhase.AwaitingSeparation) Enter(EarthReturnPhase.Separating);
            else if (Phase == EarthReturnPhase.AwaitingDeparture) Enter(EarthReturnPhase.DepartureBurn);
            else if (Phase == EarthReturnPhase.OrbitHold) Enter(EarthReturnPhase.DeorbitBurn);
        }
        public void TogglePause()
        {
            if (!IsAnimating || Phase == EarthReturnPhase.TransferFade) return;
            Paused = !Paused; Changed?.Invoke();
        }
        public void Tick(float dt)
        {
            if (dt <= 0 || !float.IsFinite(dt) || Paused) return;
            if (Phase == EarthReturnPhase.OrbitHold) { SimulatedSeconds += dt; Changed?.Invoke(); return; }
            // 有限步长跨过窗口也会判定失败，不能用大帧步越过操作期限。
            while (dt > .000001f && IsAnimating && !Paused)
            {
                float rate = Phase == EarthReturnPhase.DepartureBurn || Phase == EarthReturnPhase.DeorbitBurn ? Throttle * 2 : 1;
                float step = Mathf.Min(.02f, dt);
                if (!InDescent && rate > 0) step = Mathf.Min(step, Mathf.Max(.000001f, (Duration - Elapsed) / rate));
                SimulatedSeconds += step * TimeCompression;
                if (Phase >= EarthReturnPhase.DepartureBurn && Phase <= EarthReturnPhase.Splashdown || Phase == EarthReturnPhase.HardLanding) FlightElapsed += step;
                Elapsed = Mathf.Min(Duration, Elapsed + step * rate); dt -= step;
                if (InDescent) { StepDescent(step); Changed?.Invoke(); continue; }
                UpdateFlight(step);
                if (Elapsed >= Duration - .000001f && IsAnimating)
                {
                    if (Phase == EarthReturnPhase.Breakup || Phase == EarthReturnPhase.SkipOut || Phase == EarthReturnPhase.HardLanding) Enter(EarthReturnPhase.Failed);
                    else Enter(Phase + 1);
                }
                else Changed?.Invoke();
            }
        }
        private void UpdateFlight(float step)
        {
            float p = Progress;
            if (Phase == EarthReturnPhase.Entry)
            {
                float pulse = Mathf.Sin(p * Mathf.PI);
                float steep = EntryAngle > MaximumEntryAngle ? Mathf.Max(.8f, EntryAngle - MaximumEntryAngle) : 0;
                SpeedKmS = Mathf.Lerp(7.8f, .18f, Mathf.SmoothStep(0, 1, p)); AltitudeKm = Mathf.Lerp(120, 4.2f, p);
                HeatC = Mathf.Lerp(20, 180, p) + (1950 + steep * 1700) * pulse; GLoad = 1 + (4.5f + steep * 7) * pulse;
                OutsideTemperatureC = Mathf.Lerp(-80, -2, p) + (2100 + steep * 1900) * pulse;
                if (EntryAngle < MinimumEntryAngle)
                {
                    float skip = Mathf.Clamp01(p / .52f);
                    SpeedKmS = Mathf.Lerp(7.8f, 7.4f, skip); AltitudeKm = Mathf.Lerp(120, 58, skip);
                    HeatC = 20 + 1080 * Mathf.Sin(skip * Mathf.PI * .5f); GLoad = 1 + 1.1f * Mathf.Sin(skip * Mathf.PI * .5f);
                    OutsideTemperatureC = -80 + 1250 * Mathf.Sin(skip * Mathf.PI * .5f);
                }
                Damage += step * (Mathf.Max(0, HeatC - 2600) / 2200 + Mathf.Max(0, GLoad - 9) / 10);
                if (Damage >= 1) { Damage = 1; Failure = EarthReturnFailure.TooSteep; Enter(EarthReturnPhase.Breakup); }
                else if (EntryAngle < MinimumEntryAngle && p >= .52f) { Failure = EarthReturnFailure.TooShallow; Enter(EarthReturnPhase.SkipOut); }
            }
            else if (Phase == EarthReturnPhase.SkipOut)
            { AltitudeKm = Mathf.Lerp(58, 380, p); SpeedKmS = 7.4f; HeatC = Mathf.Lerp(1100, 20, p); OutsideTemperatureC = Mathf.Lerp(1170, -120, p); GLoad = 1; }
            else if (Phase == EarthReturnPhase.HardLanding)
            { AltitudeKm = Mathf.Lerp(failureAltitude, 0, p * p); SpeedKmS = Mathf.Lerp(failureSpeed, Failure == EarthReturnFailure.MissedParachutes ? .34f : .08f, p); }
            else if (Phase == EarthReturnPhase.DepartureBurn) { AltitudeKm = Mathf.Lerp(384400, 380000, p); SpeedKmS = Mathf.Lerp(1.6f, 2.4f, p); }
            else if (Phase == EarthReturnPhase.Coast) { AltitudeKm = Mathf.Lerp(380000, 10000, Mathf.SmoothStep(0, 1, p)); SpeedKmS = Mathf.Lerp(2.4f, 10.8f, p); }
            else if (Phase == EarthReturnPhase.EarthApproach) { AltitudeKm = Mathf.Lerp(10000, 220, Mathf.SmoothStep(0, 1, p)); SpeedKmS = Mathf.Lerp(10.8f, 7.8f, p); }
            else if (Phase == EarthReturnPhase.DeorbitBurn) { AltitudeKm = Mathf.Lerp(220, 120, p); SpeedKmS = 7.8f; }
        }
        private void StepDescent(float step)
        {
            DescentElapsed = Mathf.Min(DescentSeconds, DescentElapsed + step);
            float t = DescentElapsed;
            // 固定压缩高度曲线给两个操作各留约六秒；开伞和抛底只改变状态，不重置高度或计时。
            AltitudeKm = t < 3 ? Mathf.Lerp(4.2f, 3.2f, t / 3) : t < 9 ? Mathf.Lerp(3.2f, 1.8f, (t - 3) / 6)
                : t < 12 ? Mathf.Lerp(1.8f, 1, (t - 9) / 3) : t < 18 ? Mathf.Lerp(1, .4f, (t - 12) / 6) : Mathf.Lerp(.4f, 0, (t - 18) / 4);
            SpeedKmS = ParachutesDeployed ? Mathf.Lerp(.18f, .008f, Mathf.SmoothStep(0, 1, ParachuteAge / 2)) : .18f;
            HeatC = Mathf.Lerp(180, 40, t / DescentSeconds); OutsideTemperatureC = Mathf.Lerp(-2, 20, t / DescentSeconds);
            GLoad = 1 + (ParachutesDeployed ? 2 * Mathf.Sin(Mathf.Clamp01(ParachuteAge / 2) * Mathf.PI) : 0);
            if (!ParachutesDeployed && AltitudeKm < ParachuteMinimumKm - .0001f) { MissWindow(EarthReturnFailure.MissedParachutes); return; }
            if (!HeatShieldReleased && AltitudeKm < ShieldMinimumKm - .0001f) { MissWindow(EarthReturnFailure.MissedHeatShield); return; }
            if (t >= DescentSeconds) { Enter(EarthReturnPhase.Complete); return; }
            if (HeatShieldReleased && HeatShieldAge >= 1.5f && Phase != EarthReturnPhase.Splashdown) Enter(EarthReturnPhase.Splashdown);
            else if (!HeatShieldReleased && ParachutesDeployed && AltitudeKm <= ShieldMaximumKm && Phase != EarthReturnPhase.AwaitingHeatShield) Enter(EarthReturnPhase.AwaitingHeatShield);
        }
        private void MissWindow(EarthReturnFailure reason)
        { Failure = reason; failureAltitude = AltitudeKm; failureSpeed = SpeedKmS; Enter(EarthReturnPhase.HardLanding); }
        private void Enter(EarthReturnPhase phase)
        {
            Phase = phase; Elapsed = 0; Paused = false; phaseDescentStart = DescentElapsed;
            if (phase == EarthReturnPhase.TransferFade) { AltitudeKm = 384400; SpeedKmS = 1.6f; }
            if (phase == EarthReturnPhase.OrbitHold) { AltitudeKm = 220; SpeedKmS = 7.8f; }
            if (phase == EarthReturnPhase.Entry) { AltitudeKm = 120; SpeedKmS = 7.8f; }
            if (phase == EarthReturnPhase.AwaitingParachutes) { DescentElapsed = phaseDescentStart = 0; AltitudeKm = 4.2f; SpeedKmS = .18f; HeatC = 180; OutsideTemperatureC = -2; GLoad = 1; }
            if (phase == EarthReturnPhase.Complete) { AltitudeKm = 0; SpeedKmS = 0; HeatC = 40; OutsideTemperatureC = 20; GLoad = 1; }
            Changed?.Invoke();
        }
    }
}

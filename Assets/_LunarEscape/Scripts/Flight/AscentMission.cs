using System;
using UnityEngine;

namespace LunarEscape
{
    public enum AscentPhase { AwaitingBoarding, FadeOut, Black, FadeIn, Startup, Ignition, Ascent, Recovery, Completed, Failed, OrbitalInsertion, Circularizing, Orbit, Rendezvous, Docking, Docked }
    public enum AscentFailure { None, BaseExplosion, OxygenDepleted, HealthDepleted, PowerDepleted, MainFuelDepleted, RcsDepleted, TargetLost, DockingCollision, DockingMisaligned }
    public enum AscentDamage { None, Light, Heavy }
    public enum StartupOperation { None, Navigation, Engine }

    // 规则层：物资改变真实仪表值；爆炸时刻沿用撤离剩余时间，转场不会暂停危机。
    public sealed class AscentMission : MonoBehaviour
    {
        [SerializeField] private AscentConfig config;
        [SerializeField] private StationMission station;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private DockingMission docking;
        private float phaseRemaining;
        private float operationRemaining;
        private uint version;
        private bool recoveryComplete;
        public AscentConfig Config => config;
        public StationMission Station => station;
        public CargoInventory Inventory => inventory;
        public DockingMission Docking=>docking;
        public float MainFuel {get;private set;}=100;
        public AscentPhase Phase { get; private set; }
        public AscentFailure Failure { get; private set; }
        public AscentDamage Damage { get; private set; }
        public StartupOperation Operation { get; private set; }
        public float Oxygen { get; private set; }
        public float Health { get; private set; }
        public float Power { get; private set; }
        public float BaseRemaining { get; private set; }
        public float DepartureMargin { get; private set; }
        public float AirborneSeconds { get; private set; }
        public float RecoveryRemaining => Phase == AscentPhase.Recovery ? phaseRemaining : 0;
        public float PhaseRemaining => phaseRemaining;
        public float OperationRemaining => operationRemaining;
        public float LeakPerSecond { get; private set; }
        public float InjuryPerSecond { get; private set; }
        public float OxygenRate => 1 + LeakPerSecond;
        public float OxygenSupportSeconds => Oxygen / OxygenRate;
        public bool Powered { get; private set; }
        public bool NavigationReady { get; private set; }
        public bool EngineReady { get; private set; }
        public bool BaseExploded { get; private set; }
        public int SuppliesUsed { get; private set; }
        public string FeedbackKey { get; private set; } = "flight.feedback.ready";
        public bool IsTerminal => Phase == AscentPhase.Completed || Phase == AscentPhase.Failed || Phase == AscentPhase.Orbit || Phase==AscentPhase.Docked;
        public bool IsLocked => Phase != AscentPhase.AwaitingBoarding;
        public bool CanUseSupplies => Phase == AscentPhase.Startup || Phase == AscentPhase.Ignition
            || Phase == AscentPhase.Ascent || Phase == AscentPhase.Recovery || Phase == AscentPhase.OrbitalInsertion || Phase == AscentPhase.Circularizing || Phase==AscentPhase.Rendezvous || Phase==AscentPhase.Docking;
        public bool CanCircularize=>config.OrbitalFlight && Phase==AscentPhase.OrbitalInsertion && Power>=2 && (docking==null || MainFuel>=8);
        public float LowFlightSeconds=>Mathf.Max(18,DepartureMargin+config.BlastObservationSeconds);
        public float OrbitAscentSeconds=>Mathf.Max(config.MinimumOrbitAscentSeconds,LowFlightSeconds+config.OrbitalClimbSeconds);
        public float OrbitBurnProgress=>Phase==AscentPhase.Orbit || Phase==AscentPhase.Rendezvous || Phase==AscentPhase.Docking || Phase==AscentPhase.Docked ? 1 : Phase==AscentPhase.Circularizing ? 1-phaseRemaining/config.CircularizationSeconds : 0;
        public void Circularize()
        {
            if(!CanCircularize)return;
            Power-=2;if(docking!=null)MainFuel-=8;FeedbackKey="orbit.feedback.burn";Enter(AscentPhase.Circularizing,config.CircularizationSeconds);
        }
        public void ConfigureDocking(DockingMission controller){docking=controller;docking.ResetDocking();}
        internal float ConsumeMainFuel(DockingMission requester,float amount)
        {
            if(requester!=docking || Phase!=AscentPhase.Rendezvous || !float.IsFinite(amount) || amount<=0)return 0;
            float used=Mathf.Min(MainFuel,amount);MainFuel-=used;return used;
        }
        internal void BeginDockingCapture(DockingMission requester){if(requester==docking && Phase==AscentPhase.Rendezvous)Enter(AscentPhase.Docking);}
        internal void CompleteDocking(DockingMission requester){if(requester==docking && Phase==AscentPhase.Docking){FeedbackKey="dock.feedback.complete";Enter(AscentPhase.Docked);}}
        internal void FailDocking(DockingMission requester,AscentFailure reason){if(requester==docking && (Phase==AscentPhase.Rendezvous || Phase==AscentPhase.Docking))Fail(reason);}
        public bool CanPowerOn => Phase == AscentPhase.Startup && !Powered && Power >= 5;
        public bool CanNavigate => Phase == AscentPhase.Startup && Powered && !NavigationReady && Operation == StartupOperation.None;
        public bool CanPrepareEngine => Phase == AscentPhase.Startup && NavigationReady && !EngineReady && Operation == StartupOperation.None && Power >= 15;
        public bool CanIgnite => Phase == AscentPhase.Startup && EngineReady && Power >= 10;
        public event Action Changed;
        public event Action<AscentPhase> PhaseChanged;
        public event Action Exploded;

        public void Configure(AscentConfig settings, StationMission task, CargoInventory cargo)
        {
            if (settings == null || task == null || cargo == null) throw new ArgumentException("飞行需要配置、基地任务和物资清单。");
            if (station != null) station.PhaseChanged -= StationChanged;
            config = settings; station = task; inventory = cargo;
            inventory.ConfigureFlightUse(this);
            if (isActiveAndEnabled) station.PhaseChanged += StationChanged;
            ResetFlight();
        }
        private void OnEnable()
        {
            if (station == null) return;
            station.PhaseChanged += StationChanged;
            if (station.Phase == StationMissionPhase.Briefing) ResetFlight();
        }
        private void OnDisable() { if (station != null) station.PhaseChanged -= StationChanged; }
        private void StationChanged(StationMissionPhase phase)
        {
            if (phase == StationMissionPhase.Briefing) ResetFlight();
            else if (phase == StationMissionPhase.Completed && Phase == AscentPhase.AwaitingBoarding) BeginBoarding();
        }
        public void ResetFlight()
        {
            ++version;
            Phase = AscentPhase.AwaitingBoarding; Failure = AscentFailure.None; Damage = AscentDamage.None;
            Operation = StartupOperation.None; operationRemaining = phaseRemaining = 0;
            Oxygen = config != null ? config.InitialOxygen : 0; Health = config != null ? config.InitialHealth : 0;
            Power = config != null ? config.InitialPower : 0;
            BaseRemaining = DepartureMargin = AirborneSeconds = LeakPerSecond = InjuryPerSecond = 0;
            Powered = NavigationReady = EngineReady = BaseExploded = false; SuppliesUsed = 0;
            recoveryComplete=false;
            MainFuel=100;docking?.ResetDocking();
            FeedbackKey = "flight.feedback.ready";
            PhaseChanged?.Invoke(Phase); Changed?.Invoke();
        }
        private void BeginBoarding()
        {
            ++version;
            BaseRemaining = station.RemainingSeconds;
            Enter(AscentPhase.FadeOut, config.FadeOutSeconds);
        }
        public void PowerOn()
        {
            if (!CanPowerOn) { Feedback("flight.feedback.order"); return; }
            Powered = true; Power -= 5; Feedback("flight.feedback.power");
        }
        public void StartNavigation()
        {
            if (!CanNavigate) { Feedback("flight.feedback.order"); return; }
            Operation = StartupOperation.Navigation; operationRemaining = config.NavigationSeconds;
            Feedback("flight.feedback.navigation");
        }
        public void PrepareEngine()
        {
            if (!CanPrepareEngine) { Feedback("flight.feedback.order"); return; }
            Power -= 15; Operation = StartupOperation.Engine; operationRemaining = config.EngineSeconds;
            Feedback("flight.feedback.engine");
        }
        public void Ignite()
        {
            if (!CanIgnite) { Feedback("flight.feedback.order"); return; }
            Power -= 10;if(docking!=null)MainFuel=Mathf.Max(0,MainFuel-4); FeedbackKey = "flight.feedback.ignition";
            Enter(AscentPhase.Ignition, config.IgnitionSeconds);
        }
        public bool CanUse(CargoKind kind)
        {
            if (!CanUseSupplies || inventory == null || inventory.GetCount(kind) == 0) return false;
            return kind switch
            {
                CargoKind.Oxygen => Oxygen < config.OxygenCapacity - 0.01f,
                CargoKind.RepairKit => LeakPerSecond > 0,
                CargoKind.Battery => Power < 99.99f,
                CargoKind.MedicalKit => Health < 99.99f || InjuryPerSecond > 0,
                _ => false
            };
        }
        public bool UseSupply(CargoKind kind)
        {
            if (!CanUse(kind)) { if (CanUseSupplies) Feedback("flight.feedback.unavailable"); return false; }
            uint attempt = version;
            if (!inventory.TryConsumeLoaded(kind, this) || attempt != version) return false;
            switch (kind)
            {
                case CargoKind.Oxygen: Oxygen = Mathf.Min(config.OxygenCapacity, Oxygen + 90); break;
                case CargoKind.RepairKit: LeakPerSecond = 0; break;
                case CargoKind.Battery: Power = Mathf.Min(100, Power + 45); break;
                case CargoKind.MedicalKit: Health = Mathf.Min(100, Health + 40); InjuryPerSecond = 0; break;
            }
            ++SuppliesUsed; Feedback("flight.used." + kind); return true;
        }
        public void UseOxygen() => UseSupply(CargoKind.Oxygen);
        public void UseRepair() => UseSupply(CargoKind.RepairKit);
        public void UseBattery() => UseSupply(CargoKind.Battery);
        public void UseMedical() => UseSupply(CargoKind.MedicalKit);

        public void Tick(float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || !IsLocked || IsTerminal) return;
            uint current = version;
            float left = seconds;
            // 按下一个事件边界分段，大帧不会略过爆炸或在耗尽后继续成功。
            while (left > 0 && current == version && !IsTerminal)
            {
                var phaseAtStart=Phase;
                float step = left;
                bool approaching=Phase==AscentPhase.Rendezvous || Phase==AscentPhase.Docking;
                if(approaching)step=Mathf.Min(step,.02f);
                if (!BaseExploded) step = Mathf.Min(step, BaseRemaining);
                bool timed = Phase == AscentPhase.FadeOut || Phase == AscentPhase.Black || Phase == AscentPhase.FadeIn
                    || Phase == AscentPhase.Ignition || Phase == AscentPhase.Recovery || Phase==AscentPhase.Circularizing;
                if (timed) step = Mathf.Min(step, phaseRemaining);
                if(config.OrbitalFlight && Phase==AscentPhase.Ascent && BaseExploded && recoveryComplete)
                    step=Mathf.Min(step,Mathf.Max(0,OrbitAscentSeconds-AirborneSeconds));
                if (Operation != StartupOperation.None) step = Mathf.Min(step, operationRemaining);
                step = Mathf.Min(step, Oxygen / OxygenRate);
                if (InjuryPerSecond > 0) step = Mathf.Min(step, Health / InjuryPerSecond);
                if (Powered) step = Mathf.Min(step, Power / 0.12f);
                step = Mathf.Max(0, step);
                Oxygen = Mathf.Max(0, Oxygen - step * OxygenRate);
                if(docking!=null && (Phase==AscentPhase.Ascent || Phase==AscentPhase.Recovery))
                    MainFuel=Mathf.Max(0,MainFuel-Mathf.Min(step,Mathf.Max(0,OrbitAscentSeconds-AirborneSeconds))*60/OrbitAscentSeconds);
                Health = Mathf.Max(0, Health - step * InjuryPerSecond);
                if (Powered) Power = Mathf.Max(0, Power - step * 0.12f);
                if (!BaseExploded) BaseRemaining = Mathf.Max(0, BaseRemaining - step);
                if (timed) phaseRemaining = Mathf.Max(0, phaseRemaining - step);
                if (Operation != StartupOperation.None) operationRemaining = Mathf.Max(0, operationRemaining - step);
                if (Phase == AscentPhase.Ascent || Phase == AscentPhase.Recovery || Phase==AscentPhase.OrbitalInsertion || Phase==AscentPhase.Circularizing) AirborneSeconds += step;
                left = Mathf.Max(0, left - step);
                if (Oxygen <= 0.00001f) { Fail(AscentFailure.OxygenDepleted); break; }
                if (Health <= 0.00001f) { Fail(AscentFailure.HealthDepleted); break; }
                if (Powered && Power <= 0.00001f) { Fail(AscentFailure.PowerDepleted); break; }
                if(docking!=null && MainFuel<=.00001f && (Phase==AscentPhase.Ascent || Phase==AscentPhase.Recovery)){Fail(AscentFailure.MainFuelDepleted);break;}
                if(approaching){docking.Tick(step);if(current!=version || IsTerminal)break;}
                if (!BaseExploded && BaseRemaining <= 0.00001f)
                {
                    BaseExploded = true;
                    if (Phase != AscentPhase.Ascent && Phase != AscentPhase.Recovery)
                    {
                        Fail(AscentFailure.BaseExplosion); Exploded?.Invoke(); break;
                    }
                    Damage = DepartureMargin >= config.SafeDepartureSeconds ? AscentDamage.None
                        : DepartureMargin >= config.LightDepartureSeconds ? AscentDamage.Light : AscentDamage.Heavy;
                    if (Damage == AscentDamage.Light) { Health -= 20; Power -= 15; LeakPerSecond = 0.8f; }
                    else if (Damage == AscentDamage.Heavy) { Health -= 55; Power -= 55; LeakPerSecond = 5; InjuryPerSecond = 1.8f; }
                    Health = Mathf.Max(0, Health); Power = Mathf.Max(0, Power);
                    FeedbackKey = "flight.damage." + Damage;
                    Enter(AscentPhase.Recovery, config.RecoverySeconds);
                    if (current != version) return;
                    Exploded?.Invoke();
                    if (current != version) return;
                    if (Health <= 0) { Fail(AscentFailure.HealthDepleted); break; }
                    if (Power <= 0) { Fail(AscentFailure.PowerDepleted); break; }
                }
                else if (timed && phaseRemaining <= 0.00001f)
                {
                    switch (Phase)
                    {
                        case AscentPhase.FadeOut: Enter(AscentPhase.Black, config.BlackSeconds); break;
                        case AscentPhase.Black: Enter(AscentPhase.FadeIn, config.FadeInSeconds); break;
                        case AscentPhase.FadeIn: Enter(AscentPhase.Startup); break;
                        case AscentPhase.Ignition:
                            DepartureMargin = BaseRemaining; FeedbackKey = "flight.feedback.airborne";
                            Enter(AscentPhase.Ascent); break;
                        case AscentPhase.Recovery:
                            recoveryComplete=true;Enter(config.OrbitalFlight ? AscentPhase.Ascent : AscentPhase.Completed);break;
                        case AscentPhase.Circularizing:
                            FeedbackKey=docking!=null?"dock.feedback.ready":"orbit.feedback.stable";
                            if(docking!=null)docking.BeginApproach();
                            Enter(docking!=null?AscentPhase.Rendezvous:AscentPhase.Orbit);break;
                    }
                }
                if (current != version) return;
                if(config.OrbitalFlight && Phase==AscentPhase.Ascent && BaseExploded && recoveryComplete && AirborneSeconds>=OrbitAscentSeconds-.00001f)
                {FeedbackKey="orbit.feedback.ready";Enter(AscentPhase.OrbitalInsertion);if(current!=version)return;}
                if (Operation != StartupOperation.None && operationRemaining <= 0.00001f)
                {
                    if (Operation == StartupOperation.Navigation) NavigationReady = true;
                    else EngineReady = true;
                    Operation = StartupOperation.None; FeedbackKey = "flight.feedback.stepready";
                }
                if (step == 0 && Phase==phaseAtStart && !IsTerminal && !timed && BaseExploded && Operation == StartupOperation.None) break;
            }
            if (current == version) Changed?.Invoke();
        }
        private void Fail(AscentFailure reason) { Failure = reason;docking?.StopForFailure(); Enter(AscentPhase.Failed); }
        private void Feedback(string key) { FeedbackKey = key; Changed?.Invoke(); }
        private void Enter(AscentPhase phase, float duration = 0)
        {
            Phase = phase; phaseRemaining = duration;
            PhaseChanged?.Invoke(phase); Changed?.Invoke();
        }
    }
}

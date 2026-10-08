using System;
using UnityEngine;

namespace LunarEscape
{
    public enum CrewRole { Player, Commander, OrbitalPilot }
    public enum CrewState { Ready, Trapped, Following, Boarded, LeftBehind, Survived, Dead }

    // 结果读取只得到不可变快照，不能通过 UI 或评分直接修改人物生命值。
    public readonly struct CrewStatus
    {
        public CrewRole Role { get; }
        public CrewState State { get; }
        public float Health { get; }
        public float InjuryPerSecond { get; }
        public bool IsAlive { get; }
        public CrewStatus(CrewRole role, CrewState state, float health, float injury, bool alive)
        { Role = role; State = state; Health = health; InjuryPerSecond = injury; IsAlive = alive; }
    }

    // 只在主场景显式配置后工作。时间来自任务实际消耗的步长，没有独立 Update 或氧气扣耗。
    public sealed class CrewMission : MonoBehaviour
    {
        [SerializeField] private bool missionEnabled;
        [SerializeField] private CrewConfig config;
        [SerializeField] private StationMission station;
        [SerializeField] private AscentMission flight;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private CharacterController player;
        [SerializeField] private Transform rescueAnchor;
        [SerializeField] private Transform commander;
        [SerializeField] private Transform boardingAnchor;
        private StationMission boundStation;
        private AscentMission boundFlight;
        private LifeSupportMission boundLifeSupport;
        private uint version;
        private bool rescueHeld, boardingDecisionMade, treating;

        public CrewConfig Config => config;
        public StationMission Station => station;
        public AscentMission Flight => flight;
        public CargoInventory Inventory => inventory;
        public CharacterController Player => player;
        public Transform RescueAnchor => rescueAnchor;
        public Transform Commander => commander;
        public Transform BoardingAnchor => boardingAnchor;
        public bool IsConfigured => missionEnabled && config != null && station != null && flight != null && inventory != null
            && player != null && rescueAnchor != null && commander != null && boardingAnchor != null;
        public CrewState CommanderState { get; private set; } = CrewState.Ready;
        public CrewState PlayerState { get; private set; } = CrewState.Ready;
        public CrewState OrbitalPilotState { get; private set; } = CrewState.Ready;
        public float CommanderHealth { get; private set; } = 100;
        public float CommanderInjuryPerSecond { get; private set; }
        public float RescueProgressSeconds { get; private set; }
        public float RescueProgress01 => config == null ? 0 : Mathf.Clamp01(RescueProgressSeconds / config.RescueSeconds);
        public bool IsRescueHeld => rescueHeld;
        public bool CommanderRescued { get; private set; }
        public bool CommanderBoarded { get; private set; }
        public bool IsOutcomeResolved { get; private set; }
        public int GroundMedicalKitsUsed { get; private set; }
        public int FlightMedicalKitsUsed { get; private set; }
        public int MedicalKitsUsed => GroundMedicalKitsUsed + FlightMedicalKitsUsed;
        public int AliveCount => (GetStatus(CrewRole.Player).IsAlive ? 1 : 0) + (GetStatus(CrewRole.Commander).IsAlive ? 1 : 0)
            + (GetStatus(CrewRole.OrbitalPilot).IsAlive ? 1 : 0);
        public int SurvivorCount => IsOutcomeResolved ? AliveCount : 0;

        // 交互层可追加实体接触条件；任何委托都不能绕过本组件的距离、楼层和任务阶段检查。
        public Func<bool> HasRescueContact { get; set; }
        public Func<bool> CanBoardCommander { get; set; }
        public event Action Changed;
        public event Action<CrewState> CommanderStateChanged;
        public event Action RescueCompleted;
        // 跟随只使用本步救援完成之后的剩余秒数；监听者必须在此回调内完成实际位置更新。
        public event Action<float> FollowingTimeAdvanced;

        public bool CanRescue => GroundActionsAllowed && CommanderState == CrewState.Trapped && CommanderHealth > 0
            && commander.gameObject.activeInHierarchy && rescueAnchor.gameObject.activeInHierarchy
            && NearPlayer(rescueAnchor.position, commander.position.y, config.InteractionDistance)
            && NearPlayer(commander.position, commander.position.y, config.InteractionDistance);
        public bool IsCommanderAtBoardingPoint => IsConfigured && CommanderState == CrewState.Following && CommanderRescued
            && CommanderHealth > 0 && Near(commander.position, boardingAnchor.position, config.BoardingDistance, config.SameFloorTolerance);
        public bool CanUseLoadedMedicalOnCommander => IsConfigured && isActiveAndEnabled && !IsOutcomeResolved && NeedsMedical
            && CommanderState == CrewState.Boarded && CommanderBoarded && station.Phase == StationMissionPhase.Completed
            && flight.CanUseSupplies && !FlightDeadlinePending && HasLoadedMedical();

        public void Configure(CrewConfig settings, StationMission task, AscentMission ascent, CargoInventory cargo,
            CharacterController playerBody, Transform rescuePoint, Transform commanderBody, Transform boardingPoint)
        {
            if (settings == null || task == null || ascent == null || cargo == null || playerBody == null ||
                rescuePoint == null || commanderBody == null || boardingPoint == null) throw new ArgumentNullException();
            if (task.Phase != StationMissionPhase.Briefing) throw new InvalidOperationException("乘员只能在任务开始前配置。");
            if (ascent.Station != task || ascent.Inventory != cargo || cargo.Mission != task)
                throw new ArgumentException("乘员、飞行和物资必须共用同一基地任务。");
            Unbind();
            config = settings; station = task; flight = ascent; inventory = cargo; player = playerBody;
            rescueAnchor = rescuePoint; commander = commanderBody; boardingAnchor = boardingPoint; missionEnabled = true;
            inventory.ConfigureCrewUse(this);
            if (isActiveAndEnabled) Bind();
            ResetCrew();
        }

        public void ConfigureProbes(Func<bool> rescueContact, Func<bool> commanderCanBoard)
        { HasRescueContact = rescueContact; CanBoardCommander = commanderCanBoard; }

        private void OnEnable()
        {
            if (!IsConfigured) return;
            Bind();
            if (station.Phase == StationMissionPhase.Briefing) ResetCrew();
            else if (station.Phase == StationMissionPhase.Evacuation && CommanderState == CrewState.Ready) BeginAlarm();
            else if (station.Phase == StationMissionPhase.Completed) ResolveBoarding();
            if (station.Phase == StationMissionPhase.Failed) ResolveOutcome(false, true);
            else if (flight.Phase == AscentPhase.Failed) ResolveOutcome(false, false);
            else if (flight.Phase == AscentPhase.Docked) ResolveOutcome(true, false);
        }

        private void OnDisable() { Unbind(); rescueHeld = false; }

        private void Bind()
        {
            Unbind();
            inventory.ConfigureCrewUse(this);
            boundStation = station; boundFlight = flight;
            boundStation.PhaseChanged += StationChanged;
            boundStation.TimeAdvanced += GroundAdvanced;
            boundLifeSupport = station.LifeSupport;
            if (boundLifeSupport != null) boundLifeSupport.Changed += FollowThroughAirlock;
            boundFlight.PhaseChanged += FlightChanged;
            boundFlight.TimeAdvanced += FlightAdvanced;
            boundFlight.Exploded += BaseExploded;
        }

        private void Unbind()
        {
            if (boundStation != null)
            { boundStation.PhaseChanged -= StationChanged; boundStation.TimeAdvanced -= GroundAdvanced; }
            if (boundFlight != null)
            { boundFlight.PhaseChanged -= FlightChanged; boundFlight.TimeAdvanced -= FlightAdvanced; boundFlight.Exploded -= BaseExploded; }
            if (boundLifeSupport != null) boundLifeSupport.Changed -= FollowThroughAirlock;
            boundLifeSupport = null;
            boundStation = null; boundFlight = null;
        }

        public void ResetCrew()
        {
            if (!IsConfigured || station.Phase != StationMissionPhase.Briefing) return;
            uint current = ++version;
            IsOutcomeResolved = false;
            var previous = CommanderState;
            CommanderState = PlayerState = OrbitalPilotState = CrewState.Ready;
            CommanderHealth = 100; CommanderInjuryPerSecond = RescueProgressSeconds = 0;
            CommanderRescued = CommanderBoarded = rescueHeld = boardingDecisionMade = treating = false;
            GroundMedicalKitsUsed = FlightMedicalKitsUsed = 0;
            if (previous != CommanderState) PublishCommanderState(current);
            if (current == version) Changed?.Invoke();
        }

        public CrewStatus GetStatus(CrewRole role)
        {
            switch (role)
            {
                case CrewRole.Commander:
                    // 留在基地时尚可活着；任务结束仍未撤出者不能在生还评分中冒充获救。
                    bool alive = CommanderHealth > 0 && CommanderState != CrewState.Dead && !(IsOutcomeResolved && CommanderState == CrewState.LeftBehind);
                    return new CrewStatus(role, CommanderState, CommanderHealth, CommanderInjuryPerSecond, alive);
                case CrewRole.Player:
                    float health = PlayerState == CrewState.Dead ? 0 : flight != null ? flight.Health : 100;
                    return new CrewStatus(role, PlayerState, health, flight != null ? flight.InjuryPerSecond : 0, PlayerState != CrewState.Dead && health > 0);
                case CrewRole.OrbitalPilot:
                    return new CrewStatus(role, OrbitalPilotState, OrbitalPilotState == CrewState.Dead ? 0 : 100, 0, OrbitalPilotState != CrewState.Dead);
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        public void SetRescueHeld(bool held)
        {
            uint current = version;
            bool accepted = held && CanRescue && (HasRescueContact == null || HasRescueContact());
            if (current != version || accepted == rescueHeld) return;
            rescueHeld = accepted;
            Changed?.Invoke();
        }

        public bool CanTreatCommanderWith(CargoItem medical)
        {
            if (!GroundActionsAllowed || !NeedsMedical || (CommanderState != CrewState.Trapped && CommanderState != CrewState.Following)
                || !commander.gameObject.activeInHierarchy
                || medical == null || medical.Inventory != inventory || medical.Kind != CargoKind.MedicalKit || medical.State != CargoState.Held
                || !medical.isActiveAndEnabled || !medical.Grab.isSelected
                || !NearPlayer(commander.position, commander.position.y, config.InteractionDistance)) return false;
            var point = medical.transform.position;
            if (!Finite(point) || !Finite(commander.position)) return false;
            // 对手持医疗包做真实空间接触检查，而不是只检查背包中有这个类型。
            var nearest = commander.position + Vector3.up * Mathf.Clamp(point.y - commander.position.y, 0, 1.7f);
            return Vector3.Distance(point, nearest) <= config.MedicalContactDistance;
        }

        public bool TryTreatCommander(CargoItem heldMedical)
        {
            if (treating || !CanTreatCommanderWith(heldMedical)) return false;
            uint current = version; treating = true;
            try
            {
                if (!inventory.TryConsumeHeldMedicalForCrew(heldMedical, this) || current != version || !GroundActionsAllowed || !NeedsMedical) return false;
                ApplyMedical(); ++GroundMedicalKitsUsed;
                Changed?.Invoke();
                return current == version;
            }
            finally { if (current == version) treating = false; }
        }

        public bool TryUseLoadedMedicalOnCommander()
        {
            if (treating || !CanUseLoadedMedicalOnCommander) return false;
            uint current = version; treating = true;
            try
            {
                if (!inventory.TryConsumeLoadedMedicalForCrew(this) || current != version || CommanderState != CrewState.Boarded
                    || IsOutcomeResolved || FlightDeadlinePending || !NeedsMedical) return false;
                ApplyMedical(); ++FlightMedicalKitsUsed;
                Changed?.Invoke();
                return current == version;
            }
            finally { if (current == version) treating = false; }
        }

        private void ApplyMedical()
        { CommanderHealth = Mathf.Min(100, CommanderHealth + config.MedicalRestore); CommanderInjuryPerSecond = 0; }
        private bool NeedsMedical => CommanderHealth > 0 && CommanderState != CrewState.Dead && CommanderState != CrewState.Survived
            && (CommanderHealth < 99.999f || CommanderInjuryPerSecond > 0);
        private bool GroundActionsAllowed => IsConfigured && isActiveAndEnabled && !IsOutcomeResolved && station.Phase == StationMissionPhase.Evacuation
            && station.RemainingSeconds > 0 && flight.Phase == AscentPhase.AwaitingBoarding;
        private bool FlightDeadlinePending => flight.Oxygen <= .00001f || flight.Health <= .00001f ||
            (flight.Powered && flight.Power <= .00001f) || (!flight.BaseExploded && flight.BaseRemaining <= .00001f) ||
            (flight.Docking != null && flight.MainFuel <= .00001f && (flight.Phase == AscentPhase.Ascent || flight.Phase == AscentPhase.Recovery));

        private bool HasLoadedMedical()
        {
            foreach (var item in inventory.Items)
                if (item != null && item.Kind == CargoKind.MedicalKit && item.State == CargoState.Loaded) return true;
            return false;
        }

        private void StationChanged(StationMissionPhase phase)
        {
            if (!IsConfigured || phase != station.Phase) return;
            if (phase == StationMissionPhase.Briefing) ResetCrew();
            else if (phase == StationMissionPhase.Evacuation && CommanderState == CrewState.Ready) BeginAlarm();
            else if (phase == StationMissionPhase.Completed) ResolveBoarding();
            else if (phase == StationMissionPhase.Failed) ResolveOutcome(false, true);
        }

        private void FlightChanged(AscentPhase phase)
        {
            if (!IsConfigured || phase != flight.Phase) return;
            if (phase == AscentPhase.AwaitingBoarding && station.Phase == StationMissionPhase.Briefing) ResetCrew();
            else if (phase == AscentPhase.Failed) ResolveOutcome(false, false);
            else if (phase == AscentPhase.Docked) ResolveOutcome(true, false);
            else if (phase != AscentPhase.AwaitingBoarding && station.Phase == StationMissionPhase.Completed) ResolveBoarding();
        }

        private void BeginAlarm()
        {
            uint current = version;
            CommanderHealth = station.RepairRestored ? config.RepairedHealth : config.UnrepairedHealth;
            CommanderInjuryPerSecond = station.RepairRestored ? config.RepairedInjuryPerSecond : config.UnrepairedInjuryPerSecond;
            CommanderState = CrewState.Trapped; rescueHeld = false;
            PublishCommanderState(current);
            if (current == version) Changed?.Invoke();
        }

        // 场景 11 已同步为两人穿服；开门后同行，旧场景的手动救援流程保持原样。
        private void FollowThroughAirlock()
        {
            var life = boundLifeSupport;
            if (life == null || !life.DoorOpen || !life.SuitWorn || !GroundActionsAllowed || CommanderHealth <= 0
                || CommanderState != CrewState.Ready && CommanderState != CrewState.Trapped) return;
            uint current = version;
            CommanderRescued = true; CommanderState = CrewState.Following; rescueHeld = false;
            PublishCommanderState(current);
            if (current != version) return;
            PublishRescueCompleted(current);
            if (current == version) Changed?.Invoke();
        }

        private void ResolveBoarding()
        {
            if (boardingDecisionMade || IsOutcomeResolved || station.Phase != StationMissionPhase.Completed) return;
            uint current = version;
            bool canBoard = IsCommanderAtBoardingPoint && (CanBoardCommander == null || CanBoardCommander());
            if (current != version) return;
            boardingDecisionMade = true; rescueHeld = false; PlayerState = CrewState.Boarded;
            var previous = CommanderState;
            if (CommanderState != CrewState.Dead)
            {
                CommanderBoarded = canBoard;
                CommanderState = canBoard ? CrewState.Boarded : CrewState.LeftBehind;
            }
            if (previous != CommanderState) PublishCommanderState(current);
            if (current == version) Changed?.Invoke();
        }

        private void GroundAdvanced(float seconds)
        {
            if (!IsConfigured || IsOutcomeResolved || station.Phase != StationMissionPhase.Evacuation || !float.IsFinite(seconds) || seconds <= 0) return;
            // 剩余时间已经由 Station 扣除；本步到期时只结算伤势，不允许救援/走到出口覆盖失败。
            AdvanceCommander(seconds, true, station.RemainingSeconds > 0);
        }

        private void FlightAdvanced(float seconds)
        {
            if (!IsConfigured || IsOutcomeResolved || !float.IsFinite(seconds) || seconds <= 0) return;
            AdvanceCommander(seconds, false, false);
        }

        private void AdvanceCommander(float seconds, bool ground, bool actionsAllowed)
        {
            uint current = version;
            float left = seconds;
            while (left > 0 && current == version && !IsOutcomeResolved)
            {
                bool active = ground ? CommanderState == CrewState.Trapped || CommanderState == CrewState.Following
                    : CommanderState == CrewState.Boarded || CommanderState == CrewState.LeftBehind;
                if (!active || CommanderHealth <= 0) break;
                bool rescuing = ground && actionsAllowed && rescueHeld && CanRescue;
                if (rescuing && HasRescueContact != null) rescuing = HasRescueContact();
                if (current != version) return;
                if (rescueHeld && !rescuing && ground) rescueHeld = false;
                bool following = ground && actionsAllowed && CommanderState == CrewState.Following;
                float step = left;
                if (CommanderInjuryPerSecond > 0) step = Mathf.Min(step, CommanderHealth / CommanderInjuryPerSecond);
                if (rescuing) step = Mathf.Min(step, Mathf.Max(0, config.RescueSeconds - RescueProgressSeconds));
                CommanderHealth = Mathf.Max(0, CommanderHealth - CommanderInjuryPerSecond * step);
                if (rescuing) RescueProgressSeconds = Mathf.Min(config.RescueSeconds, RescueProgressSeconds + step);
                left = Mathf.Max(0, left - step);
                if (following && step > 0)
                {
                    PublishFollowingTime(step, current);
                    if (current != version) return;
                }
                // 同一时刻完成救援和伤重死亡时，死亡优先，医疗包不能复活角色。
                if (CommanderHealth <= .00001f)
                {
                    CommanderHealth = 0; CommanderInjuryPerSecond = 0; CommanderState = CrewState.Dead; rescueHeld = false;
                    PublishCommanderState(current);
                    break;
                }
                if (rescuing && RescueProgressSeconds >= config.RescueSeconds - .00001f)
                {
                    RescueProgressSeconds = config.RescueSeconds; CommanderRescued = true;
                    CommanderState = CrewState.Following; rescueHeld = false;
                    PublishCommanderState(current);
                    if (current != version) return;
                    PublishRescueCompleted(current);
                    if (current != version) return;
                }
                else if (step <= 0) break;
            }
            if (current == version) Changed?.Invoke();
        }

        private void BaseExploded()
        {
            if (!IsConfigured || IsOutcomeResolved || !flight.BaseExploded || CommanderState == CrewState.Dead) return;
            uint current = version; var previous = CommanderState;
            if (!CommanderBoarded)
            { CommanderHealth = 0; CommanderInjuryPerSecond = 0; CommanderState = CrewState.Dead; }
            else
            {
                float damage = flight.Damage == AscentDamage.Heavy ? config.HeavyBlastDamage : flight.Damage == AscentDamage.Light ? config.LightBlastDamage : 0;
                float injury = flight.Damage == AscentDamage.Heavy ? config.HeavyBlastInjuryPerSecond : flight.Damage == AscentDamage.Light ? config.LightBlastInjuryPerSecond : 0;
                CommanderHealth = Mathf.Max(0, CommanderHealth - damage);
                CommanderInjuryPerSecond = Mathf.Max(CommanderInjuryPerSecond, injury);
                if (CommanderHealth <= 0) { CommanderState = CrewState.Dead; CommanderInjuryPerSecond = 0; }
            }
            if (previous != CommanderState) PublishCommanderState(current);
            if (current == version) Changed?.Invoke();
        }

        private void ResolveOutcome(bool docked, bool groundFailure)
        {
            if (IsOutcomeResolved) return;
            uint current = version; var previous = CommanderState;
            rescueHeld = false;
            PlayerState = docked ? CrewState.Survived : CrewState.Dead;
            if (docked)
            {
                OrbitalPilotState = CrewState.Survived;
                if (CommanderBoarded && CommanderHealth > 0) CommanderState = CrewState.Survived;
                else if (CommanderState != CrewState.Dead) CommanderState = CrewState.LeftBehind;
            }
            else
            {
                bool pilotCollision = !groundFailure && config.DockingCollisionKillsPilot && flight.Failure == AscentFailure.DockingCollision;
                OrbitalPilotState = pilotCollision ? CrewState.Dead : CrewState.Ready;
                if (groundFailure || CommanderBoarded || flight.BaseExploded)
                { CommanderState = CrewState.Dead; CommanderHealth = CommanderInjuryPerSecond = 0; }
                else if (CommanderState != CrewState.Dead) CommanderState = CrewState.LeftBehind;
            }
            // 所有人一次性结算完毕才打开门闩；评分不能在早到的 PhaseChanged 回调里读旧状态。
            IsOutcomeResolved = true;
            if (previous != CommanderState) PublishCommanderState(current);
            if (current == version) Changed?.Invoke();
        }

        private void PublishCommanderState(uint current)
        {
            if (CommanderStateChanged == null) return;
            var state = CommanderState;
            foreach (Action<CrewState> listener in CommanderStateChanged.GetInvocationList())
            {
                if (current != version || CommanderState != state) return;
                listener(state);
            }
        }

        private void PublishFollowingTime(float seconds, uint current)
        {
            if (FollowingTimeAdvanced == null) return;
            foreach (Action<float> listener in FollowingTimeAdvanced.GetInvocationList())
            {
                if (current != version || CommanderState != CrewState.Following || IsOutcomeResolved) return;
                listener(seconds);
            }
        }

        private void PublishRescueCompleted(uint current)
        {
            if (RescueCompleted == null) return;
            foreach (Action listener in RescueCompleted.GetInvocationList())
            {
                if (current != version || CommanderState != CrewState.Following || IsOutcomeResolved) return;
                listener();
            }
        }

        private bool NearPlayer(Vector3 target, float floor, float distance)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy) return false;
            var centre = player.transform.TransformPoint(player.center);
            var foot = centre - Vector3.up * (player.height * Mathf.Abs(player.transform.lossyScale.y) * .5f);
            return Near(foot, new Vector3(target.x, floor, target.z), distance, config.SameFloorTolerance);
        }

        private static bool Near(Vector3 a, Vector3 b, float distance, float floorTolerance)
        {
            if (!Finite(a) || !Finite(b)) return false;
            var difference = a - b;
            return difference.x * difference.x + difference.z * difference.z <= distance * distance && Mathf.Abs(difference.y) <= floorTolerance;
        }
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}

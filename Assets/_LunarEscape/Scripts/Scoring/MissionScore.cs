using System;
using UnityEngine;

namespace LunarEscape
{
    // 放在始终激活的任务对象上；只在乘员结局已稳定后生成一次阶段结算。
    public sealed class MissionScore : MonoBehaviour
    {
        [SerializeField] private StationMission station;
        [SerializeField] private AscentMission flight;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private CrewMission crew;
        private bool listening;
        private bool departed;
        private bool orbitReached;

        public MissionScoreResult Result { get; private set; }
        public bool HasResult => Result != null;
        public event Action Changed;

        public void Configure(StationMission task, AscentMission ascent, CargoInventory cargo, CrewMission members)
        {
            if (task == null || ascent == null || cargo == null || members == null) throw new ArgumentNullException();
            if (ascent.Station != task || ascent.Inventory != cargo || cargo.Mission != task
                || members.Station != task || members.Flight != ascent || members.Inventory != cargo)
                throw new ArgumentException("阶段评分必须使用同一任务和物资清单。");
            Unsubscribe();
            station = task; flight = ascent; inventory = cargo; crew = members;
            ResetAttempt(true);
            if (isActiveAndEnabled) Subscribe();
            ObserveFlight(); TryFinalize();
        }

        private void OnEnable()
        {
            Subscribe();
            if (station == null || flight == null || crew == null || inventory == null) return;
            if (station.Phase == StationMissionPhase.Briefing) ResetAttempt(false);
            ObserveFlight(); TryFinalize();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (listening || station == null || flight == null || inventory == null || crew == null) return;
            station.PhaseChanged += OnStationPhase;
            station.Changed += OnStationChanged;
            flight.PhaseChanged += OnFlightPhase;
            flight.Changed += OnFlightChanged;
            crew.Changed += OnCrewChanged;
            listening = true;
        }

        private void Unsubscribe()
        {
            if (!listening) return;
            if (station != null) { station.PhaseChanged -= OnStationPhase; station.Changed -= OnStationChanged; }
            if (flight != null) { flight.PhaseChanged -= OnFlightPhase; flight.Changed -= OnFlightChanged; }
            if (crew != null) crew.Changed -= OnCrewChanged;
            listening = false;
        }

        private void OnStationPhase(StationMissionPhase phase)
        {
            if (phase == StationMissionPhase.Briefing) ResetAttempt(false);
            else TryFinalize();
        }

        private void OnStationChanged()
        {
            // 已在简报页再次重试时没有 PhaseChanged，因此还要响应 Changed。
            if (station.Phase == StationMissionPhase.Briefing) ResetAttempt(false);
            else TryFinalize();
        }

        private void OnFlightPhase(AscentPhase phase) { ObserveFlight(); TryFinalize(); }
        private void OnFlightChanged() { ObserveFlight(); TryFinalize(); }
        private void OnCrewChanged() { ObserveFlight(); TryFinalize(); }

        private void ObserveFlight()
        {
            if (flight == null || station == null || station.Phase == StationMissionPhase.Briefing) return;
            // 事件留痕让后续失败也保留已真实完成的起飞与圆化，而非只看最后的 Failed 状态。
            switch (flight.Phase)
            {
                case AscentPhase.Ascent:
                case AscentPhase.Recovery:
                case AscentPhase.Completed:
                case AscentPhase.OrbitalInsertion:
                case AscentPhase.Circularizing:
                    departed = true;
                    break;
                case AscentPhase.Orbit:
                case AscentPhase.Rendezvous:
                case AscentPhase.Docking:
                case AscentPhase.Docked:
                    departed = orbitReached = true;
                    break;
            }
            if (flight.DepartureMargin > 0 || flight.AirborneSeconds > 0) departed = true;
        }

        private void TryFinalize()
        {
            if (Result != null || station == null || flight == null || crew == null || inventory == null) return;
            MissionScoreOutcome outcome;
            if (station.Phase == StationMissionPhase.Failed) outcome = MissionScoreOutcome.StationFailed;
            else if (flight.Phase == AscentPhase.Failed) outcome = MissionScoreOutcome.FlightFailed;
            else if (flight.Phase == AscentPhase.Docked) outcome = MissionScoreOutcome.Docked;
            else return;
            // Crew 在写完三个人的状态后才开启此门闩；不依赖组件的订阅顺序或下一帧回调。
            if (!crew.IsOutcomeResolved) return;
            ObserveFlight();
            int data = 0, samples = 0;
            foreach (var item in inventory.Items)
            {
                if (item == null || item.State != CargoState.Loaded) continue;
                if (item.Kind == CargoKind.DataCore) ++data;
                else if (item.Kind == CargoKind.LunarSample) ++samples;
            }
            var snapshot = new MissionScoreSnapshot(outcome, flight.Failure,
                crew.GetStatus(CrewRole.Player), crew.GetStatus(CrewRole.Commander), crew.GetStatus(CrewRole.OrbitalPilot),
                station.RepairRestored, departed, orbitReached, crew.CommanderRescued, crew.CommanderBoarded,
                flight.DepartureMargin, flight.Config != null ? flight.Config.SafeDepartureSeconds : 0,
                data, samples, flight.OxygenSupportSeconds, flight.Power, flight.MainFuel,
                flight.Docking != null ? flight.Docking.RcsFuel : 0, station.FailureReason);
            Result = MissionScoreEvaluator.Evaluate(snapshot);
            Changed?.Invoke();
        }

        private void ResetAttempt(bool forceNotify)
        {
            bool hadState = Result != null || departed || orbitReached;
            Result = null; departed = orbitReached = false;
            if (forceNotify || hadState) Changed?.Invoke();
        }
    }
}

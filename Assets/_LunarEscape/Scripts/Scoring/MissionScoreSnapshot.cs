using System;

namespace LunarEscape
{
    public enum MissionScoreOutcome { StationFailed, FlightFailed, Docked }

    // 只保存本次月面撤离阶段的事实，不持有会随重试变化的组件或物资引用。
    public sealed class MissionScoreSnapshot
    {
        public MissionScoreOutcome Outcome { get; }
        public AscentFailure Failure { get; }
        public StationMissionFailure StationFailure { get; }
        public CrewStatus Player { get; }
        public CrewStatus Commander { get; }
        public CrewStatus OrbitalPilot { get; }
        public bool RepairRestored { get; }
        public bool Departed { get; }
        public bool OrbitReached { get; }
        public bool CommanderRescued { get; }
        public bool CommanderBoarded { get; }
        public float DepartureMargin { get; }
        public float SafeDepartureSeconds { get; }
        public int LoadedDataCores { get; }
        public int LoadedLunarSamples { get; }
        public float OxygenSupportSeconds { get; }
        public float Power { get; }
        public float MainFuel { get; }
        public float RcsFuel { get; }
        public bool Docked => Outcome == MissionScoreOutcome.Docked;
        public int SurvivorCount => (Player.IsAlive ? 1 : 0) + (Commander.IsAlive ? 1 : 0) + (OrbitalPilot.IsAlive ? 1 : 0);
        public int RecoveredDataCores => Docked ? LoadedDataCores : 0;
        public int RecoveredLunarSamples => Docked ? LoadedLunarSamples : 0;

        public MissionScoreSnapshot(MissionScoreOutcome outcome, AscentFailure failure,
            CrewStatus player, CrewStatus commander, CrewStatus orbitalPilot,
            bool repairRestored, bool departed, bool orbitReached,
            bool commanderRescued, bool commanderBoarded, float departureMargin,
            float safeDepartureSeconds, int loadedDataCores, int loadedLunarSamples,
            float oxygenSupportSeconds, float power, float mainFuel, float rcsFuel,
            StationMissionFailure stationFailure = StationMissionFailure.None)
        {
            if (!Enum.IsDefined(typeof(MissionScoreOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (player.Role != CrewRole.Player || commander.Role != CrewRole.Commander || orbitalPilot.Role != CrewRole.OrbitalPilot)
                throw new ArgumentException("结算需要玩家、指挥官和轨道器驾驶员各一份独立状态。");
            Outcome = outcome; Failure = failure; StationFailure = stationFailure;
            Player = player; Commander = commander; OrbitalPilot = orbitalPilot;
            RepairRestored = repairRestored; Departed = departed; OrbitReached = orbitReached;
            CommanderRescued = commanderRescued; CommanderBoarded = commanderBoarded;
            DepartureMargin = NonNegative(departureMargin); SafeDepartureSeconds = NonNegative(safeDepartureSeconds);
            LoadedDataCores = Math.Max(0, loadedDataCores); LoadedLunarSamples = Math.Max(0, loadedLunarSamples);
            OxygenSupportSeconds = NonNegative(oxygenSupportSeconds); Power = NonNegative(power);
            MainFuel = NonNegative(mainFuel); RcsFuel = NonNegative(rcsFuel);
        }

        private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value);
    }
}

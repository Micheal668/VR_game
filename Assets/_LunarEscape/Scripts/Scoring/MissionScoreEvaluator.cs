using System;

namespace LunarEscape
{
    // 纯评估器：只计算已实现阶段的真实事实，不访问场景、时间或可变清单。
    public static class MissionScoreEvaluator
    {
        public const int CrewMaximum = 3900;
        public const int FlightMaximum = 2000;
        public const int StationMaximum = 1200;
        public const int ScienceMaximum = 1200;
        public const int EmergencyMaximum = 1200;
        public const int ResourceMaximum = 500;
        public const int Maximum = CrewMaximum + FlightMaximum + StationMaximum + ScienceMaximum + EmergencyMaximum + ResourceMaximum;
        public const float OxygenReserveSeconds = 60;
        public const float PowerReserve = 30;
        public const float MainFuelReserve = 10;
        public const float RcsReserve = 25;

        public static MissionScoreResult Evaluate(MissionScoreSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            // 原文六项建议权重合计 9600；本版把差额 400 补给生命项，使基础总分为 10000。
            // 缺员会显著降低此项；每个人的生死来自乘员系统，不能按飞行成功虚构三人生还。
            int crew = snapshot.SurvivorCount switch { 3 => CrewMaximum, 2 => 1800, 1 => 500, _ => 0 };
            int flight = (snapshot.Departed ? 500 : 0) + (snapshot.OrbitReached ? 500 : 0) + (snapshot.Docked ? 1000 : 0);
            int station = snapshot.RepairRestored ? StationMaximum : 0;
            // 同类科研物资不刷分；只有实际对接后仍在船上的物资才算交付。
            int science = (snapshot.RecoveredDataCores > 0 ? 600 : 0) + (snapshot.RecoveredLunarSamples > 0 ? 600 : 0);
            int emergency = (snapshot.CommanderRescued ? 500 : 0) + (snapshot.CommanderBoarded ? 300 : 0)
                + (snapshot.Departed ? Portion(snapshot.DepartureMargin, snapshot.SafeDepartureSeconds, 400) : 0);
            // 成功抵达后的安全储备计分；必要治疗或修补不会直接扣分。
            int resources = snapshot.Docked
                ? Portion(snapshot.OxygenSupportSeconds, OxygenReserveSeconds, 150)
                    + Portion(snapshot.Power, PowerReserve, 150)
                    + Portion(snapshot.MainFuel, MainFuelReserve, 100)
                    + Portion(snapshot.RcsFuel, RcsReserve, 100)
                : 0;
            return new MissionScoreResult(snapshot, crew, flight, station, science, emergency, resources);
        }

        private static int Portion(float actual, float target, int maximum)
        {
            if (target <= 0 || actual <= 0) return 0;
            double fraction = Math.Min(1, (double)actual / target);
            return (int)Math.Round(maximum * fraction, MidpointRounding.AwayFromZero);
        }
    }
}

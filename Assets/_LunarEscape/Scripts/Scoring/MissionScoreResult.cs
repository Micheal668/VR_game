using System;
using System.Collections.Generic;

namespace LunarEscape
{
    public enum MissionScoreCategory
    {
        CrewSurvival, FlightOperations, StationOperations, ScientificRecovery, EmergencyResponse, ResourceManagement
    }

    public readonly struct MissionScoreLine
    {
        public MissionScoreCategory Category { get; }
        public int Points { get; }
        public int Maximum { get; }
        internal MissionScoreLine(MissionScoreCategory category, int points, int maximum)
        { Category = category; Points = points; Maximum = maximum; }
    }

    // 分项、总分和事实快照一起冻结；界面可反复读取，不能再次累加。
    public sealed class MissionScoreResult
    {
        public MissionScoreSnapshot Snapshot { get; }
        public int Total { get; }
        public int Maximum => MissionScoreEvaluator.Maximum;
        public int CrewScore { get; }
        public int FlightScore { get; }
        public int StationScore { get; }
        public int ScienceScore { get; }
        public int EmergencyScore { get; }
        public int ResourceScore { get; }
        public IReadOnlyList<MissionScoreLine> Lines { get; }

        internal MissionScoreResult(MissionScoreSnapshot snapshot, int crew, int flight, int station,
            int science, int emergency, int resources)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            CrewScore = crew; FlightScore = flight; StationScore = station;
            ScienceScore = science; EmergencyScore = emergency; ResourceScore = resources;
            Total = crew + flight + station + science + emergency + resources;
            Lines = Array.AsReadOnly(new[]
            {
                new MissionScoreLine(MissionScoreCategory.CrewSurvival, crew, MissionScoreEvaluator.CrewMaximum),
                new MissionScoreLine(MissionScoreCategory.FlightOperations, flight, MissionScoreEvaluator.FlightMaximum),
                new MissionScoreLine(MissionScoreCategory.StationOperations, station, MissionScoreEvaluator.StationMaximum),
                new MissionScoreLine(MissionScoreCategory.ScientificRecovery, science, MissionScoreEvaluator.ScienceMaximum),
                new MissionScoreLine(MissionScoreCategory.EmergencyResponse, emergency, MissionScoreEvaluator.EmergencyMaximum),
                new MissionScoreLine(MissionScoreCategory.ResourceManagement, resources, MissionScoreEvaluator.ResourceMaximum)
            });
        }
    }
}

using System.Linq;
using NUnit.Framework;

namespace LunarEscape.Tests
{
    // 纯快照验证评分边界，场景中的任务事件与结局另由 CrewRescueTests 验证。
    public sealed class MissionScoreTests
    {
        [Test]
        public void FullCrewAndDeliveredScienceReachExactly10000WithoutDuplicateBonus()
        {
            var result = MissionScoreEvaluator.Evaluate(Snapshot(MissionScoreOutcome.Docked,
                aliveCommander: true, scienceCount: 99, resourceScale: 100));

            Assert.That(result.Maximum, Is.EqualTo(10000));
            Assert.That(result.Total, Is.EqualTo(10000));
            Assert.That(result.CrewScore, Is.EqualTo(3900));
            Assert.That(result.ScienceScore, Is.EqualTo(1200), "同类科研和超额储备不能刷分。");
            Assert.That(result.ResourceScore, Is.EqualTo(500));
            Assert.That(result.Lines.Sum(line => line.Maximum), Is.EqualTo(10000));
            Assert.That(result.Lines.Sum(line => line.Points), Is.EqualTo(result.Total));
            Assert.That(result.Lines.All(line => line.Points >= 0 && line.Points <= line.Maximum), Is.True);
        }

        [Test]
        public void FailedFlightPreservesRealMilestonesButCannotDeliverScienceOrReservePoints()
        {
            var result = MissionScoreEvaluator.Evaluate(Snapshot(MissionScoreOutcome.FlightFailed,
                aliveCommander: false, scienceCount: 1, resourceScale: 1));

            Assert.That(result.Snapshot.SurvivorCount, Is.EqualTo(1));
            Assert.That(result.CrewScore, Is.EqualTo(500), "只有轨道器驾驶员生还，不能根据装船标记伪造全员生还。");
            Assert.That(result.FlightScore, Is.EqualTo(1000), "实际起飞和入轨可以保留，但失败不能获得对接分。");
            Assert.That(result.StationScore, Is.EqualTo(1200));
            Assert.That(result.Snapshot.LoadedDataCores, Is.EqualTo(1));
            Assert.That(result.Snapshot.RecoveredDataCores, Is.Zero);
            Assert.That(result.Snapshot.RecoveredLunarSamples, Is.Zero);
            Assert.That(result.ScienceScore, Is.Zero);
            Assert.That(result.ResourceScore, Is.Zero);
        }

        [Test]
        public void DockingCannotInventMissingCrewAndInvalidReservesCannotIncreaseScore()
        {
            var result = MissionScoreEvaluator.Evaluate(new MissionScoreSnapshot(
                MissionScoreOutcome.Docked, AscentFailure.None,
                Status(CrewRole.Player, true), Status(CrewRole.Commander, false), Status(CrewRole.OrbitalPilot, true),
                repairRestored: false, departed: true, orbitReached: true,
                commanderRescued: false, commanderBoarded: false,
                departureMargin: float.NaN, safeDepartureSeconds: 30,
                loadedDataCores: -1, loadedLunarSamples: -1,
                oxygenSupportSeconds: float.PositiveInfinity, power: -20, mainFuel: float.NaN, rcsFuel: -10));

            Assert.That(result.Snapshot.SurvivorCount, Is.EqualTo(2));
            Assert.That(result.CrewScore, Is.EqualTo(1800));
            Assert.That(result.FlightScore, Is.EqualTo(2000));
            Assert.That(result.Total, Is.EqualTo(3800));
            Assert.That(result.EmergencyScore, Is.Zero);
            Assert.That(result.ResourceScore, Is.Zero);
            Assert.That(result.ScienceScore, Is.Zero);
        }

        private static MissionScoreSnapshot Snapshot(MissionScoreOutcome outcome, bool aliveCommander,
            int scienceCount, float resourceScale)
        {
            bool docked = outcome == MissionScoreOutcome.Docked;
            return new MissionScoreSnapshot(outcome, docked ? AscentFailure.None : AscentFailure.OxygenDepleted,
                Status(CrewRole.Player, docked), Status(CrewRole.Commander, aliveCommander), Status(CrewRole.OrbitalPilot, true),
                repairRestored: true, departed: true, orbitReached: true,
                commanderRescued: true, commanderBoarded: true, departureMargin: 60, safeDepartureSeconds: 30,
                loadedDataCores: scienceCount, loadedLunarSamples: scienceCount,
                oxygenSupportSeconds: 60 * resourceScale, power: 30 * resourceScale,
                mainFuel: 10 * resourceScale, rcsFuel: 25 * resourceScale);
        }

        private static CrewStatus Status(CrewRole role, bool alive) => new(role,
            alive ? CrewState.Survived : CrewState.Dead, alive ? 100 : 0, 0, alive);
    }
}

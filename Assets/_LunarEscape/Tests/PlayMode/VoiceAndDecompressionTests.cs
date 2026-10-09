using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LunarEscape.Tests
{
    // 生命保障主场景：开局只剩总闸红光与屏幕；地面指挥口头引导每一步、指挥官吐槽每个维修动作；
    // 开门瞬间爆发式泄压，尘埃与松散物体冲向门口，玩家被轻拽。
    public sealed class VoiceAndDecompressionTests
    {
        private StationMissionSession session; private StationMission mission; private LifeSupportMission life;
        private HabitatBreaker breaker; private MissionVoice voice; private MissionVoiceDirector director;
        private AirlockDecompression decompression; private AirlockTestDriver driver; private LifeSupportSteps steps;
        private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) simulator.gameObject.SetActive(false);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            mission = session.Mission; life = session.GetComponent<LifeSupportMission>();
            breaker = Object.FindAnyObjectByType<HabitatBreaker>();
            voice = Object.FindAnyObjectByType<MissionVoice>();
            director = Object.FindAnyObjectByType<MissionVoiceDirector>();
            decompression = Object.FindAnyObjectByType<AirlockDecompression>();
            Assert.That(voice, Is.Not.Null, "请先执行 Lunar Escape → Install Voice Hints and Decompression (Life Support Scene)");
            Assert.That(decompression, Is.Not.Null);
            driver = new AirlockTestDriver(life.AirlockRepair); steps = new LifeSupportSteps(session);
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; driver?.Dispose(); steps?.Dispose(); }

        [UnityTest] public IEnumerator StationStartsDarkExceptTheBreakerLightAndScreens()
        {
            yield return null;
            Assert.That(RenderSettings.reflectionIntensity, Is.LessThan(0.1f), "断电时压低天空盒反射，墙面不再发亮");
            Assert.That(RenderSettings.ambientLight.maxColorComponent, Is.LessThan(0.02f), "断电时几乎没有环境光");
            var emergency = breaker.GetComponentsInChildren<Light>().Single(l => l.name == "Alarm Emergency Light");
            Assert.That(emergency.enabled, "总闸红色应急灯亮着");
            Assert.That(emergency.type, Is.EqualTo(LightType.Spot), "窄光束只照总闸");
            Assert.That(emergency.range, Is.LessThanOrEqualTo(2f));
            var environment = session.GetComponent<LifeSupportEnvironment>();
            Assert.That(environment.GlowRenderers, Is.Not.Empty, "顶棚灯带等自发光灯具纳入断电控制");
            Assert.That(MaxGlow(environment, "LB_LightDiffuser"), Is.LessThan(0.05f), "顶棚灯带断电后不再发光");

            session.BeginMission();
            breaker.SwitchOn();
            for (float t = 0; t < 2f; t += Time.deltaTime) yield return null;
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(1f).Within(0.001f), "合闸后恢复原有反射");
            Assert.That(MaxGlow(environment, "LB_LightDiffuser"), Is.GreaterThan(0.5f), "合闸后灯带重新发光");
            Assert.That(emergency.enabled, Is.False, "照明恢复后应急灯熄灭");
        }

        [UnityTest] public IEnumerator MissionControlGuidesEachStageByVoice()
        {
            Assert.That(voice.Lines.Count(l => l.speaker == VoiceSpeaker.MissionControl), Is.GreaterThanOrEqualTo(20));
            Assert.That(voice.Lines.Count(l => l.speaker == VoiceSpeaker.Commander), Is.GreaterThanOrEqualTo(20));
            Assert.That(voice.Lines.All(l => l.clip != null && l.clip.length > 0.5f), "每条语音都有音频");

            Assert.That(director.Objective(), Is.EqualTo("cup_hello"));
            yield return Said("cup_hello", 6f);
            session.BeginMission();
            yield return Said("cup_breaker", 20f);
            breaker.SwitchOn();
            yield return Said("cmd_lights", 20f);
            yield return Said("cup_suit", 20f);
            steps.Don();
            yield return Said("cmd_suit", 20f);
            yield return Said("cup_suit_done", 20f);
            Assert.That(director.Objective(), Is.EqualTo("cup_fuse"), "穿好宇航服后的下一步是配电盒");
            yield return Said("cup_fuse", 30f);
        }

        [UnityTest] public IEnumerator CommanderCommentsOnEachRepairStep()
        {
            session.BeginMission(); breaker.SwitchOn(); steps.Don();
            yield return Quiet();
            yield return driver.ReplaceFuse();
            Assert.That(driver.Fuse.IsFixed);
            yield return Said("cmd_spare", 20f);
            yield return Said("cup_fuse_done", 20f);
            Assert.That(voice.History, Does.Contain("cmd_burnt"), "拔出烧坏的保险丝时指挥官吐槽");
            yield return Quiet();
            yield return driver.TurnValve(720f);
            Assert.That(driver.Valve.IsFixed);
            yield return Said("cmd_valve_done", 20f);
            yield return Said("cup_valve_done", 20f);
            Assert.That(voice.History, Does.Contain("cmd_valve_turn"));
            yield return Quiet();
            yield return driver.UnboltAll(Object.FindAnyObjectByType<RepairTool>());
            yield return Said("cup_latch_done", 30f);
            Assert.That(voice.History.Any(id => id.StartsWith("cmd_bolt")), "拧松螺栓时指挥官吐槽");
            Assert.That(voice.History, Does.Contain("cmd_wrench"), "拿起扳手时指挥官吐槽");
            yield return Said("cup_lever", 20f);
            yield return Quiet();
            yield return driver.PullLever(1.8f);
            Assert.That(life.AirlockRepair.IsReleased);
            yield return Said("cmd_released", 20f);
            yield return Said("cup_released", 20f);
        }

        [UnityTest] public IEnumerator UnsuitedPlayerAtTheReleasedHatchIsWarnedUrgently()
        {
            session.BeginMission(); breaker.SwitchOn();
            yield return Quiet();
            life.AirlockRepair.SkipRepair();
            steps.MoveBody(life.DoorControl.position + Vector3.left * .8f);
            Assert.That(life.CanOpenDoor);
            yield return Said("cup_no_suit", 3f);
        }

        [UnityTest] public IEnumerator RetryStopsOldLinesAndCallsAgain()
        {
            yield return Said("cup_hello", 6f);
            session.BeginMission();
            yield return Said("cup_breaker", 20f);
            session.RetryMission();
            yield return null;
            Assert.That(voice.IsSpeaking, Is.False, "重新开始时停止上一轮的话");
            yield return Said("cup_hello_repeat", 6f);
        }

        [UnityTest] public IEnumerator OpeningTheHatchBlowsDustItemsAndPlayerTowardTheDoor()
        {
            session.BeginMission(); breaker.SwitchOn(); steps.Don();
            life.AirlockRepair.SkipRepair();
            var outlet = decompression.Outlet.position;
            var inward = Vector3.ProjectOnPlane(life.HabitatVolume.bounds.center - outlet, Vector3.up).normalized;
            // 沿门洞向舱内找一处空地放一个松散的小箱子。
            var floor = new Vector3(outlet.x, 0.12f, outlet.z);
            float offset = 1.4f;
            while (offset < 3.2f && Physics.CheckBox(floor + inward * offset, Vector3.one * 0.12f, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) offset += 0.1f;
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Loose test crate"; crate.transform.localScale = Vector3.one * 0.15f;
            crate.transform.position = floor + inward * offset;
            var body = crate.AddComponent<Rigidbody>(); body.mass = 0.5f;
            for (int i = 0; i < 20; i++) yield return null;
            float before = Flat(crate.transform.position - outlet);

            steps.MoveBody(life.DoorControl.position + Vector3.left * .8f);
            Assert.That(life.TryOpenDoor(), Is.True);
            // 开门后玩家站在舱室中部，离门洞较远，才能看到被拽动。
            steps.MoveBody(outlet + inward * 3f);
            var start = session.Exit.PlayerBody.transform.position;
            yield return null; yield return null;
            Assert.That(decompression.Bursting, "开门触发泄压");
            Assert.That(decompression.AffectedBodies, Does.Contain(body), "松散物体受气流影响");
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(decompression.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount), Is.GreaterThan(100), "尘埃喷涌");

            for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
            Assert.That(Flat(crate.transform.position - outlet), Is.LessThan(before - 0.5f),
                $"箱子被吸向门口：起点 {floor + inward * offset} 现在 {crate.transform.position} 门洞 {outlet}");

            for (float t = 0; t < 2f; t += Time.deltaTime) yield return null;
            Assert.That(decompression.Bursting, Is.False, "约 3 秒后气流平息");
            var moved = session.Exit.PlayerBody.transform.position - start;
            Assert.That(decompression.PlayerPulled.magnitude, Is.InRange(0.1f, 0.6f), "玩家只被轻轻拽动一小段");
            Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(moved, Vector3.up).normalized, -inward), Is.GreaterThan(0.7f), "朝门口方向");
            Assert.That(life.SuitWorn && !mission.IsTerminal, "穿着宇航服的玩家安然无恙");
            yield return Said("cup_vented", 15f);
            Object.Destroy(crate);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static float MaxGlow(LifeSupportEnvironment environment, string material)
        {
            var block = new MaterialPropertyBlock(); float max = 0f;
            foreach (var renderer in environment.GlowRenderers)
                for (int m = 0; m < renderer.sharedMaterials.Length; m++)
                {
                    if (renderer.sharedMaterials[m] == null || renderer.sharedMaterials[m].name != material) continue;
                    renderer.GetPropertyBlock(block, m);
                    max = Mathf.Max(max, block.GetColor("_EmissionColor").maxColorComponent);
                }
            return max;
        }

        private IEnumerator Said(string id, float timeout)
        {
            for (float end = Time.time + timeout; Time.time < end && !voice.History.Contains(id);) yield return null;
            Assert.That(voice.History, Does.Contain(id), "应说出 " + id + "；已说：" + string.Join(", ", voice.History)
                + $"；正在说 {voice.CurrentId}，排队 {voice.QueuedCount}，当前步骤 {director.Objective()}，阶段 {mission.Phase}");
        }

        // 等当前排队的话说完，避免俏皮话因排队过长被丢弃。
        private IEnumerator Quiet()
        {
            // 导演在下一帧的 Update 才把刚发生的事排进队列。
            yield return null; yield return null;
            for (float end = Time.time + 60f; Time.time < end && (voice.IsSpeaking || voice.QueuedCount > 0);) yield return null;
        }
    }
}

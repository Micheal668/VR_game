using System.Collections.Generic;
using UnityEngine;

namespace LunarEscape
{
    // 语音提示的“导演”：逐帧读取任务状态，在关键节点让地面指挥（ЦУП）口头告诉玩家下一步做什么，
    // 代替屏幕上的长段说明；指挥官对玩家的每个维修动作吐槽一句。长时间没有进展时重复当前提示。
    // 默认（stationHints 关闭）基地里地面指挥只说开场和开门后的出发指令，逐步反应交给指挥官。
    // 只读状态、只调用 MissionVoice，不改变任何任务规则。
    public sealed class MissionVoiceDirector : MonoBehaviour
    {
        [SerializeField] private MissionVoice voice;
        [SerializeField] private StationMission mission;
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private HabitatBreaker breaker;
        [SerializeField] private AirlockRepair airlock;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private RepairTool[] wrenches = new RepairTool[0];
        [SerializeField] private AscentMission flight;
        [Tooltip("载入后等多久地面指挥开口。")]
        [SerializeField, Min(0f)] private float helloDelay = 2.5f;
        [Tooltip("没有任何语音这么久，就重复当前步骤的提示；再过同样时长换指挥官闲聊一句。")]
        [SerializeField, Min(5f)] private float reminderSeconds = 28f;
        [Tooltip("基地内地面指挥是否逐步口头提示。关闭时基地里只保留开场呼叫与开门后的出发指令（以及失败呼叫），逐步反应只由指挥官吐槽；飞船上的地面指挥台词不受影响。")]
        [SerializeField] private bool stationHints;
        [Tooltip("撤离剩余时间低于该值时催促一次。")]
        [SerializeField, Min(0f)] private float hurrySeconds = 30f;

        private FuseFault fuse;
        private ValveFault valve;
        private LatchFault latch;
        private readonly HashSet<string> said = new();
        private readonly Dictionary<string, float> lastSaid = new();
        private StationMissionPhase lastPhase;
        private AscentPhase lastFlightPhase;
        private float helloAt, ventLineAt = -1f;
        private bool burntSeated, lastLights, lastSuit, lastFuseFixed, lastValveFixed, lastValveOver, lastReleased, lastDoorOpen, lastCycling;
        private int lastBolts, idleIndex;
        private float lastRemaining = float.MaxValue, lastSuitOxygen = float.MaxValue;
        private bool reminderNext = true;

        public MissionVoice Voice => voice;
        public bool StationHints { get => stationHints; set => stationHints = value; }

        // 关闭逐步提示时仍保留的地面指挥台词：基地第一句、最后一句、失败呼叫，以及飞船上的全部台词。
        private static readonly HashSet<string> KeptMissionControl = new()
        { "cup_hello", "cup_vented", "cup_failed", "cup_aboard", "cup_liftoff", "cup_orbit", "cup_rendezvous", "cup_docked" };

        private bool Speak(string id, VoicePriority priority = VoicePriority.Hint, System.Func<bool> stillValid = null)
            => (stationHints || !id.StartsWith("cup_") || KeptMissionControl.Contains(id)) && voice.Say(id, priority, stillValid);

        public void Configure(MissionVoice player, StationMission task, LifeSupportMission support, HabitatBreaker mainBreaker,
            AirlockRepair repair, CargoInventory cargo, RepairTool[] tools, AscentMission ascent)
        {
            voice = player; mission = task; life = support; breaker = mainBreaker; airlock = repair;
            inventory = cargo; wrenches = tools ?? new RepairTool[0]; flight = ascent;
            ResolveFaults();
        }

        private void Awake() => ResolveFaults();

        private void ResolveFaults()
        {
            if (airlock == null) return;
            foreach (var fault in airlock.Faults)
            {
                if (fault is FuseFault f) fuse = f;
                else if (fault is ValveFault v) valve = v;
                else if (fault is LatchFault l) latch = l;
            }
        }

        private void OnEnable()
        {
            if (airlock != null) airlock.Denied += OnLeverDenied;
            if (mission != null) { lastPhase = mission.Phase; if (lastPhase == StationMissionPhase.Briefing) ResetAttempt(helloDelay); }
            if (flight != null) lastFlightPhase = flight.Phase;
        }

        private void OnDisable() { if (airlock != null) airlock.Denied -= OnLeverDenied; }

        // 当前最该做的一步对应的提示。顺序与地面指挥的口头流程一致：照明 → 宇航服 → 气闸三处故障 → 拉杆 → 开门。
        public string Objective(bool repeat = false)
        {
            if (mission == null || life == null) return null;
            if (mission.Phase == StationMissionPhase.Briefing) return repeat ? "cup_hello_repeat" : "cup_hello";
            if (mission.IsTerminal) return null;
            if (life.DoorOpen) return "cup_route";
            if (breaker != null && !breaker.IsOn) return repeat ? "cup_breaker_repeat" : "cup_breaker";
            if (!life.SuitWorn) return repeat ? "cup_suit_repeat" : "cup_suit";
            if (airlock != null && !airlock.IsReleased)
            {
                if (fuse != null && !fuse.IsFixed) return fuse.Burnt != null && fuse.IsSeated(fuse.Burnt) ? "cup_fuse" : "cup_fuse_spare";
                if (valve != null && !valve.IsFixed) return valve.OverPressure ? "cup_valve_over" : "cup_valve";
                if (latch != null && !latch.IsFixed) return "cup_latch";
                return "cup_lever";
            }
            return "cup_released";
        }

        private void Update()
        {
            if (voice == null || mission == null) return;
            var phase = mission.Phase;
            if (phase != lastPhase) OnPhase(lastPhase, phase);
            lastPhase = phase;

            if (phase == StationMissionPhase.Briefing) Briefing();
            else if (!mission.IsTerminal && life != null) Ground();
            if (flight != null) Flight();
        }

        private void OnPhase(StationMissionPhase from, StationMissionPhase to)
        {
            switch (to)
            {
                case StationMissionPhase.Briefing:
                    // 重新开始：清掉上一轮还没说完的话，稍后简短地重新呼叫。
                    voice.Clear();
                    ResetAttempt(1.5f);
                    said.Add("cup_hello"); said.Add("cmd_intro");
                    break;
                case StationMissionPhase.Repair when from == StationMissionPhase.Briefing:
                    voice.Clear(true);
                    SayObjective();
                    break;
                case StationMissionPhase.Failed:
                    voice.Clear();
                    Speak("cup_failed", VoicePriority.Urgent);
                    break;
            }
        }

        private void ResetAttempt(float delay)
        {
            said.Clear(); lastSaid.Clear();
            helloAt = Time.time + delay; ventLineAt = -1f;
            burntSeated = lastLights = lastSuit = lastFuseFixed = lastValveFixed = lastValveOver = lastReleased = lastDoorOpen = lastCycling = false;
            lastBolts = 0; lastRemaining = lastSuitOxygen = float.MaxValue; reminderNext = true;
        }

        private void Briefing()
        {
            if (Time.time >= helloAt && Once("hello"))
            {
                // 首次载入完整呼叫；重新开始只简短呼叫。
                if (said.Contains("cup_hello") && stationHints) Say("cup_hello_repeat");
                else { Say("cup_hello"); SayOnce("cmd_intro", VoicePriority.Quip); }
            }
            // 任务开始前就去拉总闸：被联锁拒绝，指挥官调侃一句。
            if (breaker != null && breaker.Lever != null && breaker.Lever.Pulled && !breaker.IsOn) SayOnce("cmd_breaker_early", VoicePriority.Quip);
            Remind();
        }

        private void Ground()
        {
            bool lights = breaker == null || breaker.IsOn;
            if (lights && !lastLights && breaker != null) { SayOnce("cmd_lights", VoicePriority.Quip); SayObjective(); }
            lastLights = lights;

            if (life.SuitWorn && !lastSuit)
            {
                SayOnce("cmd_suit", VoicePriority.Quip);
                if (SayOnce("cup_suit_done")) SayObjective();
            }
            lastSuit = life.SuitWorn;

            Supplies();
            if (airlock != null && !life.DoorOpen) Airlock();
            Door();

            // 撤离阶段剩余时间跌破阈值：紧急催促。
            float remaining = mission.Phase == StationMissionPhase.Evacuation ? mission.RemainingSeconds : float.MaxValue;
            if (remaining <= hurrySeconds && lastRemaining > hurrySeconds && Once("hurry"))
            { Speak("cup_hurry", VoicePriority.Urgent); Speak("cmd_hurry", VoicePriority.Quip); }
            lastRemaining = remaining;

            // 航天服氧气不足 30 秒：提醒接上备用氧气瓶（每次跌破只提醒一次）。
            float oxygen = life.SuitWorn ? life.SuitOxygenSeconds : float.MaxValue;
            if (oxygen <= 30f && lastSuitOxygen > 30f) SayAgain("cup_air_low", 40f, VoicePriority.Urgent);
            lastSuitOxygen = oxygen;

            Remind();
        }

        private void Supplies()
        {
            if (inventory == null) return;
            foreach (var item in inventory.Items)
            {
                if (item == null || item.State != CargoState.Packed) continue;
                if (item.Kind == CargoKind.Oxygen) SayOnce("cmd_oxygen", VoicePriority.Quip);
                else if (item.Kind == CargoKind.Battery) SayOnce("cmd_battery", VoicePriority.Quip);
            }
        }

        private void Airlock()
        {
            if (!airlock.Active && !airlock.IsReleased) return;
            if (fuse != null)
            {
                if (fuse.CoverOpen) SayOnce("cmd_cover", VoicePriority.Quip);
                // 烧坏的保险丝已拔出：吐槽一句，并告诉玩家备件在哪。
                // 先确认它在座里出现过：开局与重开时插座要过一两帧才把它装回去。
                bool seated = fuse.Burnt != null && fuse.IsSeated(fuse.Burnt);
                if (seated) burntSeated = true;
                else if (burntSeated && !fuse.IsFixed && Once("burnt"))
                { SayOnce("cmd_burnt", VoicePriority.Quip); SayObjective(); }
                if (fuse.IsFixed && !lastFuseFixed && Once("fuse_fixed"))
                { SayOnce("cmd_spare", VoicePriority.Quip); Say("cup_fuse_done"); SayObjective(); }
                lastFuseFixed = fuse.IsFixed;
            }
            foreach (var wrench in wrenches)
                if (wrench != null && wrench.IsHeld) SayOnce("cmd_wrench", VoicePriority.Quip);
            if (valve != null)
            {
                if (valve.Valve != null && valve.Valve.Angle > 90f && !valve.IsFixed && !valve.OverPressure) SayOnce("cmd_valve_turn", VoicePriority.Quip);
                if (valve.OverPressure && !lastValveOver)
                { SayAgain("cup_valve_over", 8f, VoicePriority.Urgent, () => valve.OverPressure); SayOnce("cmd_valve_over", VoicePriority.Quip); }
                if (valve.IsFixed && !lastValveFixed && Once("valve_fixed"))
                { SayOnce("cmd_valve_done", VoicePriority.Quip); Say("cup_valve_done"); SayObjective(); }
                lastValveOver = valve.OverPressure; lastValveFixed = valve.IsFixed;
            }
            if (latch != null)
            {
                int bolts = latch.ReleasedCount;
                if (bolts > lastBolts && bolts >= 1 && bolts <= 3) SayOnce("cmd_bolt" + bolts, VoicePriority.Quip);
                if (latch.IsFixed && lastBolts < latch.Bolts.Length && Once("latch_fixed")) { Say("cup_latch_done"); SayObjective(); }
                lastBolts = bolts;
            }
            // 开锁循环拉到一半松手：提醒要一直握住。
            bool cycling = airlock.CycleProgress > 0f && !airlock.IsReleased;
            if (lastCycling && !cycling && !airlock.IsReleased) SayAgain("cup_lever_hold", 10f);
            lastCycling = cycling;
            if (airlock.IsReleased && !lastReleased)
            {
                SayOnce("cmd_released", VoicePriority.Quip);
                if (life.SuitWorn) Say("cup_released", VoicePriority.Hint, () => !life.DoorOpen);
                else SayAgain("cup_no_suit", 15f, VoicePriority.Urgent);
            }
            lastReleased = airlock.IsReleased;
            // 门已解锁却没穿宇航服就走到开门屏前：紧急制止（开门会致命）。
            if (airlock.IsReleased && !life.SuitWorn && life.CanOpenDoor) SayAgain("cup_no_suit", 15f, VoicePriority.Urgent);
        }

        private void OnLeverDenied()
        {
            if (mission == null || mission.Phase == StationMissionPhase.Briefing) return;
            if (!SayOnce("cmd_lever_early", VoicePriority.Quip)) SayAgainObjective(10f);
        }

        private void Door()
        {
            if (life.DoorOpen && !lastDoorOpen)
            {
                // 泄压轰鸣先响一阵，再开口；没穿宇航服时几秒后就会窒息，交给失败台词。
                voice.Clear(true);
                ventLineAt = life.SuitWorn ? Time.time + 2.2f : -1f;
            }
            lastDoorOpen = life.DoorOpen;
            if (ventLineAt > 0f && Time.time >= ventLineAt)
            {
                ventLineAt = -1f;
                SayOnce("cmd_vent", VoicePriority.Hint);
                SayOnce("cup_vented");
            }
        }

        private void Flight()
        {
            var phase = flight.Phase;
            if (phase == lastFlightPhase) return;
            lastFlightPhase = phase;
            switch (phase)
            {
                case AscentPhase.Startup: voice.Clear(); SayOnce("cmd_boarding", VoicePriority.Hint); SayOnce("cup_aboard"); break;
                case AscentPhase.Ascent: SayOnce("cup_liftoff"); SayOnce("cmd_liftoff", VoicePriority.Quip); break;
                case AscentPhase.OrbitalInsertion: SayOnce("cup_orbit"); break;
                case AscentPhase.Rendezvous: case AscentPhase.Docking: SayOnce("cup_rendezvous"); break;
                case AscentPhase.Docked: SayOnce("cup_docked"); SayOnce("cmd_docked", VoicePriority.Quip); break;
                case AscentPhase.Failed: voice.Clear(); Speak("cup_failed", VoicePriority.Urgent); break;
            }
        }

        // 长时间没有语音：先重复当前步骤的提示，下一次换指挥官闲聊（三句用完后只重复提示）。
        private void Remind()
        {
            if (voice.IdleSeconds < reminderSeconds) return;
            // 闲聊只在开门前：撤离途中指挥官没心情聊天。
            if (mission.Phase != StationMissionPhase.Briefing && !life.DoorOpen && !reminderNext && idleIndex < 3)
            {
                Speak("cmd_idle" + (++idleIndex), VoicePriority.Quip);
                reminderNext = true;
                return;
            }
            reminderNext = false;
            string id = Objective(true);
            if (id == null || !Speak(id, VoicePriority.Hint, ObjectiveIs(Objective()))) reminderNext = true;
        }

        private void SayObjective()
        {
            string id = Objective();
            if (id != null) Speak(id, VoicePriority.Hint, ObjectiveIs(id));
        }

        private void SayAgainObjective(float cooldown)
        {
            string id = Objective(true);
            if (id != null && Cooled(id, cooldown)) Speak(id, VoicePriority.Hint, ObjectiveIs(Objective()));
        }

        // 提示排队期间玩家可能已经完成了这一步：轮到播放时步骤已变就不再说。
        private System.Func<bool> ObjectiveIs(string id) => () => Objective() == id;

        private bool Say(string id, VoicePriority priority = VoicePriority.Hint, System.Func<bool> stillValid = null)
        {
            lastSaid[id] = Time.time;
            return Speak(id, priority, stillValid);
        }

        // 每轮任务只说一次（重新开始后重置）。
        private bool SayOnce(string id, VoicePriority priority = VoicePriority.Hint)
            => said.Add(id) && Say(id, priority);

        private bool SayAgain(string id, float cooldown, VoicePriority priority = VoicePriority.Hint, System.Func<bool> stillValid = null)
            => Cooled(id, cooldown) && Say(id, priority, stillValid);

        private bool Cooled(string id, float cooldown) => !lastSaid.TryGetValue(id, out float time) || Time.time - time >= cooldown;

        private bool Once(string key) => said.Add("#" + key);
    }
}

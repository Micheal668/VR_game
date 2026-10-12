using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 第一天：普通的一个班次，没有倒计时，也不会失败。
    // 醒来 → 穿航天服 → 经气闸出舱 → 用氮气枪吹净太阳能板、用取芯钻钻三根岩芯（顺序随意）
    // → 回站把岩芯放上实验室样品架 → 回卧室睡觉 → 黑幕“第二天” → 载入事故场景（场景 11）。
    // 地面指挥只在早上和收工时各说一句；中间由指挥官边干活边吐槽、顺带提示。
    public sealed class DayOneMission : MonoBehaviour
    {
        public enum Stage { Waking, Working, Evening, Sleeping }

        [SerializeField] private CharacterController player;
        [SerializeField] private Bounds[] habitat = new Bounds[0];
        [SerializeField] private SuitLocker suit;
        [SerializeField] private DayOneAirlock airlock;
        [SerializeField] private DustyPanel[] panels = new DustyPanel[0];
        [SerializeField] private DrillSite[] sites = new DrillSite[0];
        [SerializeField] private SampleRack rack;
        [SerializeField] private NitrogenBlower blower;
        [SerializeField] private CoreDrill drill;
        [SerializeField] private XRBaseInteractable bed;
        [SerializeField] private MissionVoice voice;
        [SerializeField] private ScreenFade fade;
        [SerializeField] private TMP_Text[] taskBoards = new TMP_Text[0];
        [SerializeField] private string nextScene = "11_LunarStation_LifeSupport";
        [SerializeField, Min(5f)] private float idleQuipSeconds = 45f;

        private readonly HashSet<string> said = new();
        private LocalizationService language;
        private bool wasOutside;
        private int idleIndex;

        public Stage Current { get; private set; } = Stage.Waking;
        public int PanelsClean { get { int n = 0; foreach (var p in panels) if (p != null && p.IsClean) n++; return n; } }
        public int CoresDrilled { get { int n = 0; foreach (var s in sites) if (s != null && s.Completed) n++; return n; } }
        public int CoresStored => rack != null ? rack.Stored : 0;
        public int PanelCount => panels.Length;
        public int CoreCount => sites.Length;
        public bool SuitWorn => suit != null && suit.SuitWorn;
        public bool WorkDone => SuitWorn && PanelsClean == panels.Length && CoresStored >= sites.Length;
        public bool PlayerOutside => player != null && !InsideHabitat(player.transform.TransformPoint(player.center));
        // 玩家所在处有空气：基地舱内，或者气闸室已复压。
        public bool PlayerHasAir
        {
            get
            {
                if (player == null) return true;
                var center = player.transform.TransformPoint(player.center);
                if (airlock != null && airlock.Contains(center)) return airlock.Pressurized;
                return InsideHabitat(center);
            }
        }
        public DayOneAirlock Airlock => airlock;
        public SuitLocker Suit => suit;
        public DustyPanel[] Panels => panels;
        public DrillSite[] Sites => sites;
        public SampleRack Rack => rack;
        public XRBaseInteractable Bed => bed;
        public NitrogenBlower Blower => blower;
        public CoreDrill Drill => drill;
        public MissionVoice Voice => voice;
        public string NextScene { get => nextScene; set => nextScene = value; }

        public void Configure(CharacterController body, Bounds[] rooms, SuitLocker locker, DayOneAirlock hatch, DustyPanel[] solar,
            DrillSite[] drillSites, SampleRack samples, NitrogenBlower gasGun, CoreDrill coreDrill, XRBaseInteractable sleepSpot,
            MissionVoice speaker, ScreenFade screen, TMP_Text[] boards)
        {
            player = body; habitat = rooms; suit = locker; airlock = hatch; panels = solar; sites = drillSites; rack = samples;
            blower = gasGun; drill = coreDrill; bed = sleepSpot; voice = speaker; fade = screen; taskBoards = boards;
        }

        public bool InsideHabitat(Vector3 point)
        {
            foreach (var room in habitat) if (room.Contains(point)) return true;
            return false;
        }

        private void Awake() => language = FindAnyObjectByType<LocalizationService>();

        private void OnEnable()
        {
            if (suit != null) suit.Donned += OnSuit;
            if (airlock != null) { airlock.Denied += OnAirlockDenied; airlock.SuitCheck = () => SuitWorn; }
            foreach (var p in panels) if (p != null) p.Cleaned += OnPanel;
            foreach (var s in sites) if (s != null) s.Drilled += OnCore;
            if (rack != null) rack.Changed += OnStored;
            if (blower != null) blower.Emptied += OnBlowerEmpty;
            if (drill != null) drill.Jammed += OnDrillJam;
            if (bed != null) bed.selectEntered.AddListener(OnBed);
            if (language != null) language.LanguageChanged += Refresh;
        }

        private void OnDisable()
        {
            if (suit != null) suit.Donned -= OnSuit;
            if (airlock != null) airlock.Denied -= OnAirlockDenied;
            foreach (var p in panels) if (p != null) p.Cleaned -= OnPanel;
            foreach (var s in sites) if (s != null) s.Drilled -= OnCore;
            if (rack != null) rack.Changed -= OnStored;
            if (blower != null) blower.Emptied -= OnBlowerEmpty;
            if (drill != null) drill.Jammed -= OnDrillJam;
            if (bed != null) bed.selectEntered.RemoveListener(OnBed);
            if (language != null) language.LanguageChanged -= Refresh;
        }

        private IEnumerator Start()
        {
            Refresh();
            // 标题与淡入由 ScreenFade 负责；醒来后地面指挥问早，指挥官接一句。
            for (float t = 0f; t < 5.5f; t += Time.deltaTime) yield return null;
            Current = Stage.Working;
            Say("cup_d1_morning");
            Say("cmd_d1_wake", VoicePriority.Quip);
            Say("cmd_d1_suit_hint");
            Refresh();
        }

        private void Update()
        {
            if (Current != Stage.Working && Current != Stage.Evening) return;
            bool outside = PlayerOutside;
            if (outside && !wasOutside) { Say("cmd_d1_outside", VoicePriority.Quip); }
            wasOutside = outside;
            if (drill != null && drill.IsHeld) Say("cmd_d1_drill_hint");
            if (blower != null && blower.IsHeld) Say("cmd_d1_blower");
            // 长时间没人说话：指挥官闲聊一句（两句用完为止）。
            if (voice != null && voice.IdleSeconds > idleQuipSeconds && idleIndex < 2)
                voice.Say("cmd_d1_idle" + (++idleIndex), VoicePriority.Quip);
        }

        private void OnSuit(SuitLocker _) { Say("cmd_d1_suited", VoicePriority.Quip); Say("cmd_d1_tools"); Refresh(); }
        private void OnAirlockDenied(DayOneAirlock _) => SayAgain("cmd_d1_no_suit");
        private void OnBlowerEmpty() => SayAgain("cmd_d1_tank_empty");
        private void OnDrillJam(CoreDrill _) => SayAgain("cmd_d1_drill_hot");

        private void OnPanel(DustyPanel _)
        {
            if (PanelsClean == panels.Length) Say("cmd_d1_panels_all", VoicePriority.Quip);
            else Say("cmd_d1_panel_done", VoicePriority.Quip);
            CheckEvening();
            Refresh();
        }

        private void OnCore(DrillSite _)
        {
            if (CoresDrilled == sites.Length) Say("cmd_d1_cores_all");
            else Say("cmd_d1_core_done", VoicePriority.Quip);
            Refresh();
        }

        private void OnStored(SampleRack _)
        {
            if (CoresStored >= sites.Length) Say("cmd_d1_stored", VoicePriority.Quip);
            CheckEvening();
            Refresh();
        }

        private void CheckEvening()
        {
            if (Current != Stage.Working || !WorkDone) return;
            Current = Stage.Evening;
            // 收工：白天还没说完的提示已经没有意义，只留正在说的一句。
            if (voice != null) voice.Clear(true);
            Say("cup_d1_night");
            Say("cmd_d1_sleep");
            Refresh();
        }

        private void OnBed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args) => Sleep();

        // 收工后在卧室握住自己的床即可入睡。
        public bool Sleep()
        {
            if (Current != Stage.Evening) { if (Current == Stage.Working) SayAgain("cmd_d1_not_yet"); return false; }
            Current = Stage.Sleeping;
            StartCoroutine(GoToSleep());
            return true;
        }

        private IEnumerator GoToSleep()
        {
            if (voice != null) voice.Clear(true);
            if (fade != null)
            {
                yield return fade.Fade(1f, 2.5f);
                yield return fade.Title("day.title.2", "day.title.sub.2", 2.5f);
            }
            if (language != null) LocalizationService.CarriedLanguage = language.CurrentLanguage;
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene)) SceneManager.LoadScene(nextScene);
        }

        private void Say(string id, VoicePriority priority = VoicePriority.Hint)
        {
            if (voice != null && said.Add(id)) voice.Say(id, priority);
        }

        private readonly Dictionary<string, float> lastSaid = new();
        private void SayAgain(string id, float cooldown = 12f)
        {
            if (voice == null || lastSaid.TryGetValue(id, out var t) && Time.time - t < cooldown) return;
            lastSaid[id] = Time.time;
            voice.Say(id, VoicePriority.Hint);
        }

        // 任务清单：大厅屏幕与左手腕小屏共用同一份文字；完成的项变绿。
        public void Refresh()
        {
            if (taskBoards == null || taskBoards.Length == 0) return;
            var text = new StringBuilder();
            text.AppendLine("<b>" + T("day1.board.title") + "</b>");
            Line(text, SuitWorn, T("day1.task.suit"));
            Line(text, PanelsClean == panels.Length, T("day1.task.panels", PanelsClean, panels.Length));
            Line(text, CoresDrilled == sites.Length, T("day1.task.cores", CoresDrilled, sites.Length));
            Line(text, CoresStored >= sites.Length, T("day1.task.store", CoresStored, sites.Length));
            Line(text, Current == Stage.Sleeping, T(WorkDone ? "day1.task.sleep" : "day1.task.sleep_later"));
            foreach (var board in taskBoards) if (board != null) board.text = text.ToString();
        }

        private static void Line(StringBuilder text, bool done, string label)
            => text.AppendLine(done ? "<color=#5CFF9A>+ " + label + "</color>" : "<color=#FFFFFF>- " + label + "</color>");

        private string T(string key, params object[] values) => language != null ? language.Format(key, values) : key;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 气闸 A 诊断屏（门旁）和主任务面板上的维修进度：逐项显示断电、压差、锁销和开门拉杆状态。
    // 红=故障，黄=进行中，绿=正常。拉杆被拒绝时未完成的项目闪烁，提示还差什么。
    public sealed class AirlockPresenter : MonoBehaviour
    {
        [SerializeField] private AirlockRepair airlock;
        [SerializeField] private FuseFault power;
        [SerializeField] private ValveFault pressure;
        [SerializeField] private LatchFault latches;
        [SerializeField] private LocalizedText powerRow, pressureRow, latchRow, leverRow;
        [Header("生命保障（可选）")]
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private LocalizedText suitRow;
        [Header("主任务面板")]
        [SerializeField] private LocalizedText summary;
        [SerializeField] private LocalizedText percentage;
        [SerializeField] private Image fill;
        private static readonly Color Bad = new(1f, 0.32f, 0.22f), Busy = new(1f, 0.72f, 0.25f), Good = new(0.3f, 1f, 0.6f);
        private float flashUntil;
        private float nextRefresh;

        public void Configure(AirlockRepair repair, FuseFault fuse, ValveFault valve, LatchFault latch,
            LocalizedText powerText, LocalizedText pressureText, LocalizedText latchText, LocalizedText leverText,
            LocalizedText summaryText, LocalizedText percentText, Image progress)
        {
            airlock = repair; power = fuse; pressure = valve; latches = latch;
            powerRow = powerText; pressureRow = pressureText; latchRow = latchText; leverRow = leverText;
            summary = summaryText; percentage = percentText; fill = progress;
        }

        // 生命保障场景：拉杆会直接打开舱门，没穿航天服开门致命，因此单独显示一行航天服状态。
        public void ConfigureLifeSupport(LifeSupportMission mission, LocalizedText suitText)
        { life = mission; suitRow = suitText; }

        private void OnEnable()
        {
            if (airlock == null) return;
            airlock.Changed += Refresh;
            airlock.Denied += OnDenied;
            Refresh();
        }

        private void OnDisable()
        {
            if (airlock == null) return;
            airlock.Changed -= Refresh;
            airlock.Denied -= OnDenied;
        }

        private void OnDenied() { flashUntil = Time.time + 1.2f; Refresh(); }

        private void Update()
        {
            // 压力读数和闪烁需要连续刷新；每秒 10 次足够，文字不必每帧重建。
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            if (airlock == null) return;
            bool flash = Time.time < flashUntil && Mathf.PingPong(Time.time * 6f, 1f) > 0.5f;

            Row(powerRow, power.IsFixed ? "airlock.row.power.ok" : power.CoverOpen ? "airlock.row.power.open" : "airlock.row.power.bad",
                power.IsFixed ? Good : flash ? Color.white : Bad);
            Row(pressureRow, pressure.IsFixed ? "airlock.row.pressure.ok" : pressure.OverPressure ? "airlock.row.pressure.over" : "airlock.row.pressure.bad",
                pressure.IsFixed ? Good : flash ? Color.white : pressure.OverPressure ? Bad : pressure.Valve.Angle > 1f ? Busy : Bad,
                pressure.DifferentialKPa.ToString("0.0"));
            Row(latchRow, latches.IsFixed ? "airlock.row.latch.ok" : "airlock.row.latch.bad",
                latches.IsFixed ? Good : flash ? Color.white : latches.ReleasedCount > 0 ? Busy : Bad,
                latches.ReleasedCount, latches.Bolts.Length);
            bool unsuited = life != null && !life.SuitWorn;
            string leverKey = airlock.IsReleased ? "airlock.row.lever.open" : airlock.CycleProgress > 0f ? "airlock.row.lever.cycling"
                : airlock.AllFixed ? unsuited ? "airlock.row.lever.nosuit" : "airlock.row.lever.ready" : "airlock.row.lever.locked";
            if (life != null) Row(suitRow, life.SuitWorn ? "airlock.row.suit.on" : "airlock.row.suit.off", life.SuitWorn ? Good : flash || Time.time % 1f > 0.5f ? Bad : Busy);
            Row(leverRow, leverKey, airlock.IsReleased ? Good : airlock.AllFixed ? Busy : Bad, Mathf.RoundToInt(airlock.CycleProgress * 100f));

            // 主面板：已排除的故障数与开锁循环共同组成进度。
            int fixedCount = airlock.FixedCount;
            float progress = airlock.IsReleased ? 1f : (fixedCount + airlock.CycleProgress) / (airlock.Faults.Length + 1f);
            Row(summary, airlock.IsReleased ? "airlock.status.released" : airlock.AllFixed ? "airlock.status.ready" : "airlock.status.faults",
                airlock.IsReleased ? Good : airlock.AllFixed ? Busy : Bad, airlock.Faults.Length - fixedCount);
            if (percentage != null) percentage.SetKey("repair.progress", Mathf.FloorToInt(progress * 100f));
            if (fill != null) { fill.fillAmount = progress; fill.color = airlock.IsReleased ? Good : Busy; }
        }

        private static void Row(LocalizedText label, string key, Color color, params object[] values)
        {
            if (label == null) return;
            label.SetKey(key, values);
            label.GetComponent<TMP_Text>().color = color;
        }
    }
}

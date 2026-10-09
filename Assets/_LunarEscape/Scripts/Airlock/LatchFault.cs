using UnityEngine;

namespace LunarEscape
{
    // 故障三：舱体变形把三颗锁销螺栓卡死。用工作台上的扳手逐颗拧松，三颗都松开才算修好。
    // 螺栓高低不同，最低一颗需要蹲下。
    public sealed class LatchFault : AirlockFault
    {
        [SerializeField] private LatchBolt[] bolts = new LatchBolt[0];

        public LatchBolt[] Bolts => bolts;
        public int ReleasedCount { get; private set; }
        public override bool IsFixed => bolts.Length > 0 && ReleasedCount == bolts.Length;

        public void Configure(LatchBolt[] latchBolts) => bolts = latchBolts;

        private void OnEnable() { foreach (var bolt in bolts) bolt.Changed += OnBolt; Count(); }
        private void OnDisable() { foreach (var bolt in bolts) bolt.Changed -= OnBolt; }

        private void Update()
        {
            foreach (var bolt in bolts) bolt.Enabled = CanWork;
        }

        private void OnBolt(LatchBolt _) { Count(); RaiseChanged(); }

        private void Count()
        {
            int count = 0;
            foreach (var bolt in bolts) if (bolt.Released) count++;
            ReleasedCount = count;
        }

        public override void ResetFault()
        {
            foreach (var bolt in bolts) bolt.ResetBolt();
            Count();
            RaiseChanged();
        }
    }
}

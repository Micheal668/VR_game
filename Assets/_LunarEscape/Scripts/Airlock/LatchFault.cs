using UnityEngine;

namespace LunarEscape
{
    // 故障三：舱体变形把三颗锁销螺栓卡死。用工作台上的扳手逐颗拧松，三颗都松开才算修好。
    // 扩建场景在门上八个底座中每轮随机选择三个，高低不同，最低一颗需要蹲下。
    public sealed class LatchFault : AirlockFault
    {
        [SerializeField] private LatchBolt[] bolts = new LatchBolt[0];
        [SerializeField] private Transform[] boltPositions = new Transform[0];
        private System.Random random;

        public LatchBolt[] Bolts => bolts;
        public Transform[] BoltPositions => boltPositions;
        public int ReleasedCount { get; private set; }
        public override bool IsFixed => bolts.Length > 0 && ReleasedCount == bolts.Length;

        public void Configure(LatchBolt[] latchBolts) => bolts = latchBolts;
        public void ConfigureRandomPositions(Transform[] positions) => boltPositions = positions;

        private void Start() => ResetFault();

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
            if (boltPositions.Length >= bolts.Length && bolts.Length > 0)
            {
                random ??= new System.Random(System.Guid.NewGuid().GetHashCode());
                var available = new int[boltPositions.Length];
                for (int i = 0; i < available.Length; i++) available[i] = i;
                // Partial Fisher-Yates: each of the three bolts gets a different
                // physical mounting point, with equal chances for all eight pads.
                for (int i = 0; i < bolts.Length; i++)
                {
                    int pick = random.Next(i, available.Length);
                    (available[i], available[pick]) = (available[pick], available[i]);
                    var target = boltPositions[available[i]];
                    bolts[i].transform.SetPositionAndRotation(target.position, target.rotation);
                }
            }
            Count();
            RaiseChanged();
        }
    }
}

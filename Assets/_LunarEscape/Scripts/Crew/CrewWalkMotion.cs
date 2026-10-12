using UnityEngine;

namespace LunarEscape
{
    // 队友走动时的程序化步态：按实际移动速度上下起伏、躯干轻摆、双臂交替前后摆动；
    // 停下时平滑回到站姿。只在原有骨骼姿态上叠加偏移，不改变路线和位置逻辑。
    public sealed class CrewWalkMotion : MonoBehaviour
    {
        [SerializeField] private CrewSuitRig[] rigs = new CrewSuitRig[0];
        [Tooltip("月球低重力下步子更大、起伏更明显。")]
        [SerializeField, Min(0.1f)] private float strideMeters = 1.1f;
        [SerializeField, Min(0f)] private float bobMeters = 0.045f;
        [SerializeField, Min(0f)] private float armSwingDegrees = 22f;

        private Transform[][] bones;
        private Vector3[][] restPositions;
        private Quaternion[][] restRotations;
        private Vector3 lastPosition;
        private float phase, amount;

        public float Speed { get; private set; }

        public void Configure(CrewSuitRig[] models) => rigs = models;

        private void Awake()
        {
            bones = new Transform[rigs.Length][];
            restPositions = new Vector3[rigs.Length][];
            restRotations = new Quaternion[rigs.Length][];
            for (int r = 0; r < rigs.Length; r++)
            {
                bones[r] = rigs[r] != null ? rigs[r].Bones : new Transform[0];
                restPositions[r] = new Vector3[bones[r].Length];
                restRotations[r] = new Quaternion[bones[r].Length];
                for (int i = 0; i < bones[r].Length; i++)
                    if (bones[r][i] != null) { restPositions[r][i] = bones[r][i].localPosition; restRotations[r][i] = bones[r][i].localRotation; }
            }
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            var delta = transform.position - lastPosition; delta.y = 0f;
            lastPosition = transform.position;
            // 瞬移（重开、剧情换位）不算走路。
            float speed = delta.magnitude > 1f ? 0f : delta.magnitude / dt;
            Speed = Mathf.Lerp(Speed, speed, dt * 8f);
            amount = Mathf.MoveTowards(amount, Speed > 0.15f ? 1f : 0f, dt * 3f);
            phase += Speed / strideMeters * Mathf.PI * dt;
            float s = Mathf.Sin(phase);
            for (int r = 0; r < bones.Length; r++)
                for (int i = 0; i < bones[r].Length; i++)
                {
                    var bone = bones[r][i];
                    if (bone == null) continue;
                    string n = bone.name;
                    bone.localPosition = restPositions[r][i];
                    bone.localRotation = restRotations[r][i];
                    if (amount <= 0f) continue;
                    if (n == "Hips")
                    {
                        bone.localPosition += Vector3.up * (Mathf.Abs(s) * bobMeters * amount);
                        bone.localRotation *= Quaternion.Euler(0f, s * 4f * amount, s * 2.5f * amount);
                    }
                    else if (n == "Chest") bone.localRotation *= Quaternion.Euler(3f * amount, -s * 5f * amount, 0f);
                    else if (n.StartsWith("Left") && n.Contains("Upper")) bone.localRotation *= Quaternion.Euler(s * armSwingDegrees * amount, 0f, 0f);
                    else if (n.StartsWith("Right") && n.Contains("Upper")) bone.localRotation *= Quaternion.Euler(-s * armSwingDegrees * amount, 0f, 0f);
                }
        }
    }
}

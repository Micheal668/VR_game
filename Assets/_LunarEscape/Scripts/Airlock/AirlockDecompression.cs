using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 舱门打开的一瞬间基地爆发式泄压：一声闷爆后气流轰鸣着冲出门外，
    // 尘埃和冷凝雾被卷向舱门，没固定、没拿在手里的物体被吸向门口，双手剧烈震动，玩家也被轻轻拽向门口一小段。
    // 气流强度按指数迅速衰减，约 3 秒后舱内恢复真空的寂静。只读取“门是否已开”，不改变任何任务规则。
    public sealed class AirlockDecompression : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [Tooltip("门洞中心。气流从这里冲出舱外。")]
        [SerializeField] private Transform outlet;
        [Tooltip("指向舱外的方向（门洞法线）。")]
        [SerializeField] private Vector3 outward = Vector3.right;
        [SerializeField] private ParticleSystem[] streams = new ParticleSystem[0];
        [SerializeField] private HapticImpulsePlayer[] hands = new HapticImpulsePlayer[0];
        [Tooltip("气流强度衰减的时间常数（秒）：e^(-t/τ)。")]
        [SerializeField, Min(0.1f)] private float decaySeconds = 0.75f;
        [SerializeField, Min(0.5f)] private float burstSeconds = 3.2f;
        [Tooltip("刚开门时 1 kg 以内物体受到的加速度（m/s²）；更重的物体按质量减小。")]
        [SerializeField, Min(0f)] private float itemAcceleration = 11f;
        [SerializeField, Min(0.5f)] private float maxItemSpeed = 3.5f;
        [Tooltip("尘埃在门口附近的最大流速（m/s）。")]
        [SerializeField, Min(0f)] private float dustSpeed = 7f;
        [Tooltip("舒适性开关：是否把玩家拽向门口。")]
        [SerializeField] private bool pullPlayer = true;
        [Tooltip("玩家总共被拽动的距离（米）。保持很小，避免晕动。")]
        [SerializeField, Range(0f, 1f)] private float playerPullMeters = 0.4f;
        [Tooltip("离门洞这么近时不再拽动，不会把人拖出门。")]
        [SerializeField, Min(0f)] private float keepFromOutlet = 1.1f;
        [SerializeField, Range(0f, 1f)] private float rumbleAmplitude = 0.85f;

        private readonly List<Rigidbody> bodies = new();
        private readonly HapticRumble rumble = new();
        private ParticleSystem.Particle[] particles = new ParticleSystem.Particle[0];
        private AudioSource doorSource, roomSource;
        private bool wasOpen;
        private float startTime = -1f;

        public bool Bursting => startTime >= 0f && Time.time - startTime < burstSeconds;
        public float Strength => startTime < 0f ? 0f : StrengthAt(Time.time - startTime);
        public IReadOnlyList<Rigidbody> AffectedBodies => bodies;
        public Transform Outlet => outlet;
        public Vector3 PlayerPulled { get; private set; }
        public bool PullPlayer { get => pullPlayer; set => pullPlayer = value; }
        public event Action Burst;

        public void Configure(LifeSupportMission support, Transform doorway, Vector3 outside, ParticleSystem[] effects, HapticImpulsePlayer[] controllers)
        {
            life = support; outlet = doorway; outward = outside.normalized;
            streams = effects ?? new ParticleSystem[0];
            hands = controllers ?? new HapticImpulsePlayer[0];
        }

        private void Awake()
        {
            var anchor = outlet != null ? outlet : transform;
            doorSource = FeedbackSounds.CreateSource(anchor, "Decompression Door Audio", 1f);
            doorSource.maxDistance = 25f; doorSource.minDistance = 1.5f;
            // 第二路声源放在舱内正中、偏 2D：轰鸣包围玩家，而不只是从门口传来。
            roomSource = FeedbackSounds.CreateSource(transform, "Decompression Room Audio", 0.35f);
        }

        private void OnEnable() => wasOpen = life != null && life.DoorOpen;

        private float StrengthAt(float t) => t < 0f || t > burstSeconds ? 0f : Mathf.Min(1f, t / 0.06f) * Mathf.Exp(-t / decaySeconds);

        private void Update()
        {
            if (life == null) return;
            bool open = life.DoorOpen;
            if (open && !wasOpen) Begin();
            else if (!open && wasOpen) Stop();
            wasOpen = open;
            if (!Bursting) return;
            float strength = Strength;
            Pull(strength);
            foreach (var hand in hands) rumble.Drive(hand, rumbleAmplitude * strength * UnityEngine.Random.Range(0.75f, 1.1f));
        }

        private void FixedUpdate()
        {
            if (!Bursting) return;
            float strength = Strength;
            foreach (var body in bodies)
            {
                if (body == null || body.isKinematic || IsHeld(body)) continue;
                Vector3 toOutlet = outlet.position + outward * 0.8f - body.worldCenterOfMass;
                // 稍微上扬：桌面上的东西被掀起来，而不是贴着桌面滑。
                Vector3 direction = (toOutlet.normalized + Vector3.up * 0.25f).normalized;
                body.AddForce(direction * (itemAcceleration * strength / Mathf.Max(1f, body.mass)), ForceMode.Acceleration);
                if (body.linearVelocity.sqrMagnitude > maxItemSpeed * maxItemSpeed) body.linearVelocity = body.linearVelocity.normalized * maxItemSpeed;
            }
        }

        private void LateUpdate()
        {
            if (!Bursting) return;
            float strength = Strength;
            foreach (var stream in streams) Steer(stream, strength);
        }

        private void Begin()
        {
            startTime = Time.time;
            PlayerPulled = Vector3.zero;
            CollectBodies();
            foreach (var body in bodies)
            {
                body.WakeUp();
                // 轻微翻滚，看起来是被气流卷走而不是被绳子拉走。
                body.AddTorque(UnityEngine.Random.insideUnitSphere * 1.5f, ForceMode.VelocityChange);
            }
            foreach (var stream in streams) if (stream != null) { stream.Clear(); stream.Play(); }
            FeedbackSounds.Play(doorSource, FeedbackSound.Decompression, 1f);
            FeedbackSounds.Play(roomSource, FeedbackSound.Decompression, 0.55f);
            foreach (var hand in hands) HandHaptics.Pulse(hand, 1f, 0.3f);
            Burst?.Invoke();
        }

        private void Stop()
        {
            startTime = -1f;
            bodies.Clear();
            rumble.Clear();
            foreach (var stream in streams) if (stream != null) { stream.Stop(); stream.Clear(); }
        }

        // 基地舱内（含门口一圈）所有可自由运动的刚体；玩家自己身上的、手里拿着的不算。
        private void CollectBodies()
        {
            bodies.Clear();
            var volume = life.HabitatVolume;
            if (volume == null) return;
            var bounds = volume.bounds;
            bounds.Expand(0.4f);
            var player = life.Player != null ? life.Player.transform : null;
            foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (body.isKinematic || !life.ContainsHabitat(body.worldCenterOfMass)) continue;
                if (player != null && body.transform.IsChildOf(player)) continue;
                bodies.Add(body);
            }
        }

        private static bool IsHeld(Rigidbody body) => body.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected;

        // 玩家被拽动的速度曲线与气流强度一致；积分后总位移约为 playerPullMeters。
        private void Pull(float strength)
        {
            var player = life.Player;
            if (!pullPlayer || player == null || !player.enabled || !life.IsInsideHabitat || outlet == null) return;
            Vector3 center = player.transform.TransformPoint(player.center);
            Vector3 toOutlet = Vector3.ProjectOnPlane(outlet.position - center, Vector3.up);
            if (toOutlet.magnitude <= keepFromOutlet) return;
            float speed = playerPullMeters / decaySeconds * strength;
            Vector3 step = toOutlet.normalized * (speed * Time.deltaTime);
            Vector3 before = player.transform.position;
            player.Move(step);
            PlayerPulled += player.transform.position - before;
        }

        // 粒子沿“流向门洞再冲出门外”的方向加速；离门越近越快，形成向门口汇聚的气流。
        private void Steer(ParticleSystem stream, float strength)
        {
            if (stream == null || outlet == null) return;
            int max = stream.main.maxParticles;
            if (particles.Length < max) particles = new ParticleSystem.Particle[max];
            int count = stream.GetParticles(particles);
            Vector3 exit = outlet.position + outward * 2.5f;
            float dt = Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = particles[i].position;
                Vector3 toOutlet = outlet.position - position;
                // 越过门洞平面后直接沿门洞法线冲出去。
                Vector3 target = Vector3.Dot(position - outlet.position, outward) > -0.1f ? exit : outlet.position;
                Vector3 direction = (target - position).normalized;
                float near = 1f / (1f + toOutlet.magnitude * 0.45f);
                Vector3 desired = direction * (dustSpeed * strength * (0.35f + 0.65f * near)) + UnityEngine.Random.insideUnitSphere * (0.6f * strength);
                particles[i].velocity = Vector3.Lerp(particles[i].velocity, desired, Mathf.Clamp01(dt * 6f));
            }
            stream.SetParticles(particles, count);
        }
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 压缩氮气除尘枪：扳机越深，气流越强、越远、越宽，吹尘越快，但耗气也越快。
    // 月面真空里用水会立刻沸腾结冰，所以站上用的是氮气。气瓶空了就插回工具架上的接口充气。
    public sealed class NitrogenBlower : TriggerTool
    {
        [SerializeField] private ParticleSystem jet;
        [SerializeField] private ParticleSystem dustPuff;
        [Tooltip("气瓶读数（世界空间小字）。")]
        [SerializeField] private TMPro.TMP_Text gauge;
        [SerializeField, Min(1f)] private float fullTankSeconds = 70f;
        [SerializeField, Min(0f)] private float refillPerSecond = 0.25f;
        [SerializeField, Min(0.1f)] private float maxRange = 1.8f;
        [SerializeField, Range(1f, 30f)] private float maxConeDegrees = 12f;
        [SerializeField, Min(0f)] private float cleanRate = 2.6f;
        [SerializeField, Range(1, 16)] private int raysPerFrame = 7;
        private bool emptyWarned;

        public float Tank { get; private set; } = 1f;
        public bool Docked => grab.isSelected && grab.firstInteractorSelecting is XRSocketInteractor;
        public float Range => Mathf.Lerp(0.45f, maxRange, Trigger);
        public event System.Action Emptied;
        protected override bool CanWork => Tank > 0f;
        protected override FeedbackSound LoopSound => FeedbackSound.ThrusterLoop;

        public void Configure(Transform nozzle, ParticleSystem gas, ParticleSystem puff, TMPro.TMP_Text tankGauge)
        { muzzle = nozzle; jet = gas; dustPuff = puff; gauge = tankGauge; }

        protected override void Update()
        {
            base.Update();
            if (Docked) Tank = Mathf.Min(1f, Tank + refillPerSecond * Time.deltaTime);
            if (Tank > 0.05f) emptyWarned = false;
            if (gauge != null) gauge.text = "N2 " + Mathf.RoundToInt(Tank * 100f) + "%";
        }

        protected override float Work(float power, float deltaTime)
        {
            var emission = jet != null ? jet.emission : default;
            if (jet != null)
            {
                emission.rateOverTime = power * 260f;
                var main = jet.main;
                main.startSpeed = Mathf.Lerp(1.2f, 5.5f, power);
                var shape = jet.shape;
                shape.angle = Mathf.Lerp(3f, maxConeDegrees, power);
            }
            if (power <= 0f) return 0f;
            Tank = Mathf.Max(0f, Tank - power * deltaTime / fullTankSeconds);
            if (Tank <= 0f && !emptyWarned)
            {
                emptyWarned = true;
                FeedbackSounds.Play(oneShot, FeedbackSound.Deny, 0.7f);
                Emptied?.Invoke();
            }
            BlowDust(power, deltaTime);
            return power;
        }

        // 在喷射锥内随机打几条射线：近处、正对喷口的位置吹得最狠。
        private void BlowDust(float power, float deltaTime)
        {
            var nozzle = Muzzle;
            float range = Range, cone = Mathf.Lerp(3f, maxConeDegrees, power);
            for (int i = 0; i < raysPerFrame; i++)
            {
                var offset = Random.insideUnitCircle * Mathf.Tan(cone * Mathf.Deg2Rad);
                var direction = (nozzle.forward + nozzle.right * offset.x + nozzle.up * offset.y).normalized;
                if (!Physics.Raycast(nozzle.position, direction, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var panel = hit.collider.GetComponentInParent<DustyPanel>();
                if (panel == null) continue;
                float falloff = 1f - hit.distance / range;
                float radius = Mathf.Lerp(0.05f, 0.11f, power) + hit.distance * Mathf.Tan(cone * Mathf.Deg2Rad);
                float removed = panel.Blow(hit.point, radius, cleanRate * power * falloff * deltaTime * 3f / raysPerFrame);
                if (removed > 0f && dustPuff != null)
                {
                    dustPuff.transform.position = hit.point;
                    dustPuff.Emit(1 + Mathf.RoundToInt(removed * 12f));
                }
            }
        }

        protected override float LoopVolume(float effort) => 0.2f + 0.5f * effort;
        protected override float LoopPitch(float effort) => 0.8f + 0.5f * effort;
        protected override float RumbleAmplitude(float effort) => 0.08f + 0.3f * effort;

        public override void ResetTool() { base.ResetTool(); Tank = 1f; emptyWarned = false; }
    }
}

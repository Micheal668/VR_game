using System;
using UnityEngine;

namespace LunarEscape
{
    // 第一天的正常出舱气闸：内门通基地、外门通月面，两门不会同时打开。
    // 按任一侧面板的按钮：气闸室先关上当前开着的门，泄压或复压约 3 秒（嘶嘶声、红灯转绿），再打开另一侧的门。
    // 没穿航天服不能往月面一侧泄压。
    public sealed class DayOneAirlock : MonoBehaviour
    {
        public enum Side { Inside, Outside }

        [SerializeField] private Transform innerDoor, outerDoor;
        [SerializeField] private Bounds chamber;
        [SerializeField] private Light[] statusLights = new Light[0];
        [SerializeField, Min(0.1f)] private float doorLift = 2.7f;
        [SerializeField, Min(0.1f)] private float doorSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float pumpSeconds = 3f;
        private Vector3 innerClosed, outerClosed;
        private float inner = 1f, outer;
        private AudioSource pump, oneShot;
        private Side target = Side.Inside;
        private float pumpProgress = 1f;
        private int stage; // 0 待命，1 关门，2 抽/充气，3 开门

        // 气闸室内有气：门向基地一侧且复压完成。
        public bool Pressurized { get; private set; } = true;
        public bool Cycling => stage != 0;
        public Side OpenSide => Pressurized ? Side.Inside : Side.Outside;
        public bool InnerOpen => inner > 0.98f;
        public bool OuterOpen => outer > 0.98f;
        public Bounds Chamber => chamber;
        public Func<bool> SuitCheck { get; set; }
        public event Action<DayOneAirlock> Denied;
        public event Action<DayOneAirlock, Side> Opened;

        public void Configure(Transform innerHatch, Transform outerHatch, Bounds room, Light[] lamps)
        { innerDoor = innerHatch; outerDoor = outerHatch; chamber = room; statusLights = lamps; }

        private void Awake()
        {
            innerClosed = innerDoor.localPosition; outerClosed = outerDoor.localPosition;
            pump = FeedbackSounds.CreateLoop(transform, "Airlock Pump", FeedbackSound.ThrusterLoop, 1f);
            oneShot = FeedbackSounds.CreateSource(transform, "Airlock Audio", 1f);
            pump.bypassListenerEffects = oneShot.bypassListenerEffects = true;
            transform.position = chamber.center;
            Apply();
        }

        private void OnEnable() { if (!pump.isPlaying) pump.Play(); }

        public bool Contains(Vector3 point) => chamber.Contains(point);

        // 面板按钮：请求打开某一侧的门（若该侧已开则忽略）。
        public void Request(Side side)
        {
            if (Cycling || side == OpenSide && (side == Side.Inside ? InnerOpen : OuterOpen)) return;
            if (side == Side.Outside && SuitCheck != null && !SuitCheck())
            {
                FeedbackSounds.Play(oneShot, FeedbackSound.Deny, 0.8f);
                Denied?.Invoke(this);
                return;
            }
            target = side;
            stage = 1;
            FeedbackSounds.Play(oneShot, FeedbackSound.Click, 0.7f);
        }

        public void RequestInside() => Request(Side.Inside);
        public void RequestOutside() => Request(Side.Outside);
        // 气闸室内的按钮：去另一侧。
        public void RequestOther() => Request(OpenSide == Side.Inside ? Side.Outside : Side.Inside);

        private void Update()
        {
            float step = Time.deltaTime / doorSeconds;
            switch (stage)
            {
                case 1:
                    inner = Mathf.MoveTowards(inner, 0f, step); outer = Mathf.MoveTowards(outer, 0f, step);
                    if (inner <= 0f && outer <= 0f)
                    {
                        FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, 0.8f);
                        bool change = (target == Side.Inside) != Pressurized;
                        stage = change ? 2 : 3;
                        pumpProgress = 0f;
                    }
                    break;
                case 2:
                    pumpProgress = Mathf.Min(1f, pumpProgress + Time.deltaTime / pumpSeconds);
                    // 泄压时嘶声由强变弱，复压时由弱变强。
                    pump.volume = 0.45f * (target == Side.Outside ? 1f - pumpProgress : pumpProgress) + 0.05f;
                    pump.pitch = 0.8f + 0.4f * pumpProgress;
                    if (pumpProgress >= 1f)
                    {
                        Pressurized = target == Side.Inside;
                        pump.volume = 0f;
                        FeedbackSounds.Play(oneShot, FeedbackSound.Chime, 0.6f);
                        stage = 3;
                    }
                    break;
                case 3:
                    if (target == Side.Inside) inner = Mathf.MoveTowards(inner, 1f, step); else outer = Mathf.MoveTowards(outer, 1f, step);
                    if ((target == Side.Inside ? inner : outer) >= 1f) { stage = 0; Opened?.Invoke(this, target); }
                    break;
            }
            Apply();
        }

        private void Apply()
        {
            innerDoor.localPosition = innerClosed + Vector3.up * doorLift * Mathf.SmoothStep(0f, 1f, inner);
            outerDoor.localPosition = outerClosed + Vector3.up * doorLift * Mathf.SmoothStep(0f, 1f, outer);
            // 绿灯：有气；红灯闪烁：抽气或充气中；琥珀：真空。
            var color = stage == 2 ? (Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? new Color(1f, 0.15f, 0.1f) : new Color(0.3f, 0.05f, 0.03f))
                : Pressurized ? new Color(0.25f, 1f, 0.45f) : new Color(1f, 0.6f, 0.15f);
            foreach (var lamp in statusLights) if (lamp != null) lamp.color = color;
        }

        // 测试：瞬间完成当前循环。
        public void FinishCycle()
        {
            if (!Cycling) return;
            Pressurized = target == Side.Inside;
            inner = target == Side.Inside ? 1f : 0f; outer = target == Side.Outside ? 1f : 0f;
            stage = 0; pump.volume = 0f; Apply();
            Opened?.Invoke(this, target);
        }
    }
}

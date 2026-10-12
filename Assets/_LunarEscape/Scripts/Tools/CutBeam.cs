using System;
using UnityEngine;

namespace LunarEscape
{
    // 卡在出口的钢梁：两端各有一条切割线，两条都切开后钢梁脱落、在月球重力下慢慢倒下；
    // 落地后不再阻挡玩家（与角色控制器忽略碰撞），以免碎梁把路又堵上。
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CutBeam : MonoBehaviour
    {
        [SerializeField] private CutLine[] cuts = new CutLine[0];
        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private AudioSource source;
        private float droppedAt = -1f;
        [SerializeField] private CharacterController ignored;

        public bool Dropped { get; private set; }
        public CutLine[] Cuts => cuts;
        public event Action<CutBeam> Fell;

        public void Configure(CutLine[] lines) => cuts = lines;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPosition = transform.position; startRotation = transform.rotation;
            source = FeedbackSounds.CreateSource(transform, "Beam Audio", 1f);
            source.bypassListenerEffects = true;
        }

        private void OnEnable() { foreach (var cut in cuts) cut.Cut += OnCut; }
        private void OnDisable() { foreach (var cut in cuts) cut.Cut -= OnCut; }

        private void OnCut(CutLine _)
        {
            foreach (var cut in cuts) if (!cut.Severed) { FeedbackSounds.Play(source, FeedbackSound.Click, 0.7f); return; }
            Drop();
        }

        private void Drop()
        {
            if (Dropped) return;
            Dropped = true; droppedAt = Time.time;
            body.isKinematic = false;
            body.AddTorque(transform.right * 0.4f, ForceMode.VelocityChange);
            FeedbackSounds.Play(source, FeedbackSound.Clunk, 1f);
            Fell?.Invoke(this);
        }

        // 测试与开发菜单：直接切断两端。
        public void CutInstantly() { foreach (var cut in cuts) cut.SeverInstantly(); }

        public void IgnorePlayer(CharacterController player) => ignored = player;

        private void Update()
        {
            if (!Dropped || droppedAt < 0f || Time.time - droppedAt < 1.5f) return;
            droppedAt = -1f;
            if (ignored != null) foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c, ignored, true);
            FeedbackSounds.Play(source, FeedbackSound.Knock, 0.9f);
        }

        public void ResetBeam()
        {
            Dropped = false; droppedAt = -1f;
            body.isKinematic = true;
            transform.SetPositionAndRotation(startPosition, startRotation);
            if (ignored != null) foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c, ignored, false);
            foreach (var cut in cuts) cut.ResetLine();
        }
    }
}

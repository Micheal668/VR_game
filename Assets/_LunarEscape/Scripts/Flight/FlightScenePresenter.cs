using UnityEngine;

namespace LunarEscape
{
    // 只在完全黑屏时切换环境；舱内和玩家保持稳定，运动由窗外背景承担。
    public sealed class FlightScenePresenter : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private FlightSeatLock seatLock;
        [SerializeField] private GameObject groundRoot;
        [SerializeField] private GameObject flightWorld;
        [SerializeField] private GameObject panel;
        [SerializeField] private Renderer fade;
        [SerializeField] private Transform seat;
        private Material fadeMaterial;
        private bool seated;
        public bool IsSeated => seated;
        public float FadeAlpha { get; private set; }
        public GameObject GroundRoot => groundRoot;
        public GameObject FlightWorld => flightWorld;
        public GameObject Panel => panel;
        public Transform Seat => seat;
        public void Configure(AscentMission task, FlightSeatLock lockedSeat, GameObject ground,
            GameObject cabin, GameObject controls, Renderer overlay, Transform location)
        {
            if (mission != null) mission.PhaseChanged -= OnPhase;
            mission = task; seatLock = lockedSeat; groundRoot = ground; flightWorld = cabin;
            panel = controls; fade = overlay; seat = location;
            if (isActiveAndEnabled) mission.PhaseChanged += OnPhase;
            OnPhase(mission.Phase);
        }
        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += OnPhase; OnPhase(mission.Phase);
        }
        private void OnDisable() { if (mission != null) mission.PhaseChanged -= OnPhase; }
        private void OnDestroy() { if (fadeMaterial != null) Destroy(fadeMaterial); }
        private void OnPhase(AscentPhase phase)
        {
            if (phase == AscentPhase.AwaitingBoarding)
            {
                seated = false;
                groundRoot.SetActive(true); flightWorld.SetActive(false); panel.SetActive(false);
            }
            else if (phase == AscentPhase.Black && !seated)
            {
                seated = true;
                seatLock.Relocate(seat.position, seat.forward);
                groundRoot.SetActive(false); flightWorld.SetActive(true); panel.SetActive(true);
            }
            else if (phase == AscentPhase.Failed) panel.SetActive(false);
            RefreshFade();
        }
        private void LateUpdate() => RefreshFade();
        private void RefreshFade()
        {
            if (mission == null || fade == null) return;
            FadeAlpha = mission.Phase switch
            {
                AscentPhase.FadeOut => 1 - mission.PhaseRemaining / mission.Config.FadeOutSeconds,
                AscentPhase.Black => 1,
                AscentPhase.FadeIn => mission.PhaseRemaining / mission.Config.FadeInSeconds,
                _ => 0
            };
            fade.enabled = FadeAlpha > 0;
            if (!Application.isPlaying) return;
            if (fadeMaterial == null) fadeMaterial = fade.material;
            fadeMaterial.SetFloat("_Alpha", Mathf.Clamp01(FadeAlpha));
        }
    }
}

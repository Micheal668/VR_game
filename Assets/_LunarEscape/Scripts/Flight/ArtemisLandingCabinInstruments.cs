using TMPro;
using UnityEngine;

namespace LunarEscape
{
    public sealed class ArtemisLandingCabinInstruments : MonoBehaviour
    {
        [SerializeField] private Transform stationShell, pilotEye;
        [SerializeField] private Camera optics;
        [SerializeField] private TMP_Text engineState, thrust, opticsTitle, navTitle, readout;
        [SerializeField] private RectTransform thrustBar;
        private ArtemisLanding mission;
        private LocalizationService language;
        private float nextRefresh;
        public Transform StationShell => stationShell;
        public Transform PilotEye => pilotEye;
        public Camera Optics => optics;

        public void Configure(Transform sourceCabin, Transform eye, Camera camera, TMP_Text engine,
            TMP_Text throttle, TMP_Text opticalHeading, TMP_Text navigationHeading, TMP_Text data, RectTransform bar)
        {
            stationShell = sourceCabin;
            pilotEye = eye;
            optics = camera;
            engineState = engine;
            thrust = throttle;
            opticsTitle = opticalHeading;
            navTitle = navigationHeading;
            readout = data;
            thrustBar = bar;
        }

        private void OnEnable()
        {
            if (mission == null) mission = FindAnyObjectByType<ArtemisLanding>();
            if (language == null) language = FindAnyObjectByType<LocalizationService>();
            nextRefresh = 0;
        }

        private void LateUpdate()
        {
            if (mission == null || language == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .10f;
            bool atSite = mission.Phase == LandingPhase.Approach || mission.Phase == LandingPhase.Retry
                || mission.Phase == LandingPhase.Landed;
            optics.transform.localRotation = Quaternion.Euler(atSite ? Mathf.Lerp(82, 43, Mathf.Clamp01(mission.Height / 120f)) : 36, 0, 0);
            var power = mission.EngineOn ? mission.Thrust : 0;
            engineState.text = language.Format(mission.EngineOn ? "landing.panel.engineon" : "landing.panel.engineoff");
            thrust.text = Mathf.RoundToInt(100 * power) + "%";
            thrustBar.localScale = new Vector3(power, 1, 1);
            opticsTitle.text = language.Format("landing.panel.optics");
            navTitle.text = language.Format("landing.panel.navigation");
            readout.text = atSite
                ? language.Format("landing.panel.data", Mathf.Max(0, mission.Height), mission.Range, mission.Velocity.y, mission.Drift)
                : language.Format("landing." + mission.Phase + ".status");
        }
    }
}

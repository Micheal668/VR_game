using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 复用原结算窗口的交互白名单。结果按值读取，转头与头手追踪始终保持自然。
    public sealed class MissionScorePresenter : MonoBehaviour
    {
        [SerializeField] private MissionScore score;
        [SerializeField] private LocalizationService localization;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject panel;
        [SerializeField] private LocalizedText outcome, total, crewLine, scienceLine;
        [SerializeField] private LocalizedText[] scoreLines;
        [SerializeField] private Button retry;
        private bool showing;
        private bool listening;
        private Vector3 viewOffset;
        public GameObject Panel => panel;
        public Button RetryButton => retry;
        public void Configure(MissionScore source, LocalizationService language, Camera camera, GameObject root,
            LocalizedText result, LocalizedText points, LocalizedText members, LocalizedText science,
            LocalizedText[] lines, Button restart)
        {
            Unsubscribe();
            score = source; localization = language; playerCamera = camera; panel = root;
            outcome = result; total = points; crewLine = members; scienceLine = science; scoreLines = lines; retry = restart;
            if (isActiveAndEnabled) Subscribe();
        }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        { if (score != null && !listening) { listening = true; score.Changed += Refresh; localization.LanguageChanged += Refresh; Refresh(); } }
        private void Unsubscribe()
        { if (score != null && listening) { score.Changed -= Refresh; localization.LanguageChanged -= Refresh; } listening = false; }
        private void OnDisable()
        { Unsubscribe(); if (panel != null) panel.SetActive(false); showing = false; }
        private void Refresh()
        {
            if (panel == null || score == null) return;
            var result = score.Result;
            if (result == null) { panel.SetActive(false); showing = false; return; }
            if (!showing)
            {
                var head = playerCamera.transform; viewOffset = head.forward * 1.9f;
                var up = Mathf.Abs(Vector3.Dot(head.forward, Vector3.up)) < .98f ? Vector3.up : head.up;
                panel.transform.SetPositionAndRotation(head.position + viewOffset, Quaternion.LookRotation(head.forward, up));
            }
            showing = true; panel.SetActive(true);
            var snapshot = result.Snapshot;
            outcome.SetKey(snapshot.Docked ? "score.outcome.docked" : snapshot.Outcome == MissionScoreOutcome.StationFailed
                ? snapshot.StationFailure == StationMissionFailure.None || snapshot.StationFailure == StationMissionFailure.EvacuationTimeout
                    ? "failure.reason" : "life.failure." + snapshot.StationFailure
                : "flight.failure." + snapshot.Failure);
            string grade = result.Total >= 9000 ? "S" : result.Total >= 7500 ? "A" : result.Total >= 5500 ? "B" : result.Total >= 3000 ? "C" : "D";
            total.SetKey("score.total", result.Total, result.Maximum, grade);
            crewLine.SetKey("score.crew", snapshot.SurvivorCount,
                localization.Format("crew.state." + snapshot.Player.State), localization.Format("crew.state." + snapshot.Commander.State), localization.Format("crew.state." + snapshot.OrbitalPilot.State));
            scienceLine.SetKey("score.science", snapshot.RecoveredDataCores, snapshot.RecoveredLunarSamples);
            for (int i = 0; i < scoreLines.Length; i++)
            { var line = result.Lines[i]; scoreLines[i].SetKey("score.line." + line.Category, line.Points, line.Maximum); }
            retry.interactable = true;
        }
        private void LateUpdate()
        { if (showing && playerCamera != null) panel.transform.position = playerCamera.transform.position + viewOffset; }
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace LunarEscape
{
    // 基地事故的身体信号：警报响起的瞬间双手猛震一下，撤离超时失败时长震一次。
    // 警报声本身仍由 MissionEnvironment 播放，这里只补充震动。
    public sealed class AlarmFeedback : MonoBehaviour
    {
        [SerializeField] private StationMissionSession session;
        [SerializeField] private HapticImpulsePlayer[] hands = new HapticImpulsePlayer[0];
        [SerializeField, Range(0f, 1f)] private float alarmAmplitude = 0.7f;
        [SerializeField, Range(0f, 1f)] private float failureAmplitude = 0.5f;

        public void Configure(StationMissionSession flow, HapticImpulsePlayer[] controllers)
        {
            session = flow;
            hands = controllers ?? new HapticImpulsePlayer[0];
        }

        private void OnEnable() { if (session != null && session.Mission != null) session.Mission.PhaseChanged += OnPhaseChanged; }
        private void OnDisable() { if (session != null && session.Mission != null) session.Mission.PhaseChanged -= OnPhaseChanged; }

        private void OnPhaseChanged(StationMissionPhase phase)
        {
            float amplitude = phase switch
            {
                StationMissionPhase.Evacuation => alarmAmplitude,
                StationMissionPhase.Failed => failureAmplitude,
                _ => 0f
            };
            if (amplitude <= 0f) return;
            foreach (var hand in hands)
                HandHaptics.Pulse(hand, amplitude, phase == StationMissionPhase.Failed ? 0.6f : 0.35f);
        }
    }
}

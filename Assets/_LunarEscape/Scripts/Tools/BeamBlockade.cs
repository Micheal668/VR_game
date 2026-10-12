using System;
using UnityEngine;

namespace LunarEscape
{
    // 第二天：事故中坍塌的钢梁堵住了出口通道外门。全部钢梁切落后通道畅通。
    // 只负责“是否已清除”及重开复位；激光切割由 LaserCutter 完成。
    public sealed class BeamBlockade : MonoBehaviour
    {
        [SerializeField] private CutBeam[] beams = new CutBeam[0];
        [SerializeField] private StationMission mission;
        [SerializeField] private LaserCutter cutter;

        public CutBeam[] Beams => beams;
        public LaserCutter Cutter => cutter;
        public bool Cleared { get { foreach (var b in beams) if (b != null && !b.Dropped) return false; return true; } }
        public int DroppedCount { get { int n = 0; foreach (var b in beams) if (b != null && b.Dropped) n++; return n; } }
        public event Action<BeamBlockade, CutBeam> BeamDropped;
        public event Action<BeamBlockade> ClearedEvent;

        public void Configure(CutBeam[] steel, StationMission task, LaserCutter tool, CharacterController player)
        {
            beams = steel; mission = task; cutter = tool;
            foreach (var beam in beams) beam.IgnorePlayer(player);
        }

        private void OnEnable()
        {
            foreach (var beam in beams) if (beam != null) beam.Fell += OnFell;
            if (mission != null) mission.PhaseChanged += OnPhase;
        }

        private void OnDisable()
        {
            foreach (var beam in beams) if (beam != null) beam.Fell -= OnFell;
            if (mission != null) mission.PhaseChanged -= OnPhase;
        }

        private void OnFell(CutBeam beam)
        {
            BeamDropped?.Invoke(this, beam);
            if (Cleared) ClearedEvent?.Invoke(this);
        }

        private void OnPhase(StationMissionPhase phase)
        {
            if (phase != StationMissionPhase.Briefing) return;
            foreach (var beam in beams) if (beam != null) beam.ResetBeam();
            if (cutter != null) cutter.ResetTool();
        }

        // 开发菜单：直接清除。
        public void ClearInstantly() { foreach (var beam in beams) if (beam != null) beam.CutInstantly(); }
    }
}

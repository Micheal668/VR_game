using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 每根已采集岩芯只产生一份数据和一罐月壤；拔出暂停，重新插入不会重复产出。
    public sealed class LunarResearchStation : MonoBehaviour
    {
        [SerializeField] private StationPreparation preparation;
        [SerializeField] private SampleRack rack;
        [SerializeField] private CoreSample[] cores = Array.Empty<CoreSample>();
        [SerializeField] private PreparedCargo[] data = Array.Empty<PreparedCargo>();
        [SerializeField] private PreparedCargo[] soil = Array.Empty<PreparedCargo>();
        [SerializeField] private TMP_Text readout;
        [SerializeField] private float researchSeconds = 18;
        private float[] progress;
        private LocalizationService language;
        public SampleRack Rack => rack;
        public CoreSample[] Cores => cores;
        public PreparedCargo[] Data => data;
        public PreparedCargo[] Soil => soil;
        public int Completed { get { int count = 0; foreach (var p in Progress) if (p >= 1) count++; return count; } }
        private float[] Progress => progress ??= new float[cores.Length];
        public void Configure(StationPreparation owner, SampleRack sampleRack, CoreSample[] samples, PreparedCargo[] drives, PreparedCargo[] canisters, TMP_Text label)
        { preparation = owner; rack = sampleRack; cores = samples; data = drives; soil = canisters; readout = label; progress = null; }
        private void Awake() => language = FindAnyObjectByType<LocalizationService>();
        public float GetProgress(int index) => Progress[index];
        internal void RestoreProgress(int index, float value) => Progress[index] = Mathf.Clamp01(value);
        public int SlotFor(int index)
        {
            for (int i = 0; i < rack.Slots.Length; i++)
                if (rack.Slots[i].firstInteractableSelected == (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)cores[index].Grab) return i;
            return -1;
        }
        private void Update() { Advance(Time.deltaTime); Refresh(); }
        public void Advance(float seconds)
        {
            if (seconds <= 0 || !float.IsFinite(seconds) || preparation == null || !preparation.CanWork || !preparation.HasPower) return;
            for (int i = 0; i < cores.Length; i++)
            {
                if (Progress[i] >= 1 || !cores[i].gameObject.activeInHierarchy || SlotFor(i) < 0) continue;
                Progress[i] = Mathf.Min(1, Progress[i] + seconds / researchSeconds);
                if (Progress[i] < 1) continue;
                data[i].CompleteResearch(); soil[i].CompleteResearch();
            }
        }
        private void Refresh()
        {
            if (readout == null || language == null) return;
            string text = language.Format("prep.research.title") + "\n";
            for (int i = 0; i < cores.Length; i++)
            {
                string state = Progress[i] >= 1 ? language.Format("prep.research.done")
                    : SlotFor(i) < 0 ? language.Format(Progress[i] > 0 ? "prep.research.paused" : "prep.research.insert")
                    : !preparation.HasPower ? language.Format("prep.dock.power") : Mathf.RoundToInt(Progress[i] * 100) + "%";
                text += language.Format("prep.research.row", i + 1, state) + "\n";
            }
            readout.text = text + language.Format("prep.research.take");
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // Both puzzles use real grab objects and XR sockets. The circuit panel has colored
    // cable ends; the laboratory uses numbered data modules with different silhouettes.
    public sealed class StationPatchPuzzle : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private HabitatBreaker requiredPower;
        [SerializeField] private StationPatchSocket[] sockets = Array.Empty<StationPatchSocket>();
        [SerializeField] private StationPatchModule[] modules = Array.Empty<StationPatchModule>();
        [SerializeField] private TMP_Text status;
        [SerializeField] private bool penalizeErrors;
        private int lastAttempt = -1;
        private LocalizationService language;
        private int resetUntilFrame;
        public bool IsResetting { get; private set; }
        public event Action WrongConnection;
        public event Action Changed;
        public bool Active => life != null && lastAttempt == life.AttemptNumber && !IsResetting && life.IsGroundActive && (requiredPower == null || requiredPower.IsOn);
        public bool IsSolved
        {
            get
            {
                if (IsResetting || sockets.Length == 0) return false;
                foreach (var socket in sockets) if (socket == null || !socket.Correct) return false;
                return true;
            }
        }
        public StationPatchSocket[] Sockets => sockets;
        public StationPatchModule[] Modules => modules;
        public void Configure(LifeSupportMission source, HabitatBreaker power, StationPatchSocket[] ports,
            StationPatchModule[] plugs, TMP_Text readout, bool penalties)
        { life = source; requiredPower = power; sockets = ports; modules = plugs; status = readout; penalizeErrors = penalties; }
        private void Awake() => language = FindAnyObjectByType<LocalizationService>();
        private string Text(string zh, string en, string ru) => language != null && language.CurrentLanguage == GameLanguage.English ? en
            : language != null && language.CurrentLanguage == GameLanguage.Russian ? ru : zh;

        private void Update()
        {
            if (life == null) return;
            if (lastAttempt != life.AttemptNumber) { lastAttempt = life.AttemptNumber; ResetPuzzle(); }
            if (IsResetting && Time.frameCount > resetUntilFrame)
            {
                IsResetting = false;
                foreach (var socket in sockets) socket.Socket.socketActive = true;
            }
            if (status == null) return;
            int connected = 0;
            foreach (var socket in sockets) if (socket.Correct) ++connected;
            status.text = requiredPower != null && !requiredPower.IsOn ? Text("先恢复备用电源", "RESTORE BACKUP POWER", "ВОССТАНОВИТЕ ПИТАНИЕ")
                : IsSolved ? "3 / 3  OK\n" + (penalizeErrors ? Text("拉下总闸", "PULL BREAKER", "ВКЛЮЧИТЕ РУБИЛЬНИК") : Text("前往中控解锁", "CONFIRM AT CONTROL", "ПОДТВЕРДИТЕ НА ПУЛЬТЕ"))
                : connected + " / " + sockets.Length + "\n" + (penalizeErrors ? Text("按颜色与编号接线", "MATCH COLOR + NUMBER", "СОЕДИНИТЕ ЦВЕТ И НОМЕР") : Text("按形状与编号插入模块", "MATCH SHAPE + NUMBER", "СОПОСТАВЬТЕ ФОРМУ И НОМЕР"));
            status.color = IsSolved ? new Color(.35f, 1, .7f) : Color.white;
        }

        internal void Inserted(StationPatchSocket socket)
        {
            if (!Active) return;
            if (!socket.Correct && penalizeErrors)
            {
                life.DrainBasePower(5);
                WrongConnection?.Invoke();
            }
            Changed?.Invoke();
        }

        public void ResetPuzzle()
        {
            // Keep sockets closed until physics has observed the returned modules.
            // Otherwise their old overlap lists can immediately reinsert a plug.
            IsResetting = true;
            resetUntilFrame = Time.frameCount + 2;
            foreach (var socket in sockets) socket.Socket.socketActive = false;
            foreach (var socket in sockets) socket.Clear();
            foreach (var module in modules)
            {
                module.gameObject.SetActive(true);
                module.GetComponent<ReturnFallenTool>().ResetToStart();
            }
            Physics.SyncTransforms();
            Changed?.Invoke();
        }
    }
}

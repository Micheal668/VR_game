using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // Owns the bedroom incident and independent crew dressing. Resource deductions use
    // LifeSupportMission's actual time steps, so pausing/retrying never leaves a second clock running.
    public sealed class StationExpansionMission : MonoBehaviour
    {
        [SerializeField] private StationMissionSession session;
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private CrewMission crew;
        [SerializeField] private GroundCrewController follower;
        [SerializeField] private HabitatBreaker breaker;
        [SerializeField] private StationPatchPuzzle circuit, laboratory;
        [SerializeField] private BoxCollider bedroom;
        [SerializeField] private Transform bedroomDoor, sleepingCommander, controlPoint;
        [SerializeField] private Transform[] routeToRack = Array.Empty<Transform>();
        [SerializeField] private Behaviour[] locomotion = Array.Empty<Behaviour>();
        [SerializeField] private Light[] emergencyLights = Array.Empty<Light>();
        [SerializeField] private Image eyeCover;
        [SerializeField] private TMP_Text status;
        private bool[] locomotionBefore;
        private float[] emergencyIntensity;
        private AudioSource audioSource;
        private float introTime, outsideSeconds, dressingSeconds, commanderHypoxia;
        private int waypoint, lastAttempt = -1;
        private bool sawBedroom, controlsLocked;
        private Vector3 doorClosed, doorOpen;
        private Vector3 standingOrigin, seatedOrigin;
        private LocalizationService language;
        public bool IntroComplete { get; private set; }
        public bool BedroomLocked { get; private set; }
        public bool DoorUnlocked { get; private set; }
        public bool CommanderSuited { get; private set; }
        public StationPatchPuzzle Circuit => circuit;
        public StationPatchPuzzle Laboratory => laboratory;
        public BoxCollider Bedroom => bedroom;
        public Transform ControlPoint => controlPoint;

        public void Configure(StationMissionSession source, CrewMission team, GroundCrewController walking,
            HabitatBreaker power, StationPatchPuzzle wires, StationPatchPuzzle modules, BoxCollider room,
            Transform door, Transform bedPose, Transform console, Transform[] rackRoute, Behaviour[] movement,
            Light[] redLights, Image eyelids, TMP_Text readout)
        {
            session = source; life = source.GetComponent<LifeSupportMission>(); crew = team; follower = walking;
            breaker = power; circuit = wires; laboratory = modules; bedroom = room; bedroomDoor = door;
            sleepingCommander = bedPose; controlPoint = console; routeToRack = rackRoute;
            locomotion = movement; emergencyLights = redLights; eyeCover = eyelids; status = readout;
        }
        private void Awake()
        {
            language = FindAnyObjectByType<LocalizationService>();
            audioSource = FeedbackSounds.CreateSource(transform, "Bedroom Incident Audio", 0);
            doorClosed = bedroomDoor.localPosition; doorOpen = doorClosed + Vector3.up * 2.65f;
            emergencyIntensity = new float[emergencyLights.Length];
            for (int i = 0; i < emergencyLights.Length; i++) emergencyIntensity[i] = emergencyLights[i].intensity;
        }
        private void OnEnable()
        {
            if (life == null) return;
            life.GroundTimeAdvanced += Advance;
        }
        private void OnDisable()
        {
            if (life != null) life.GroundTimeAdvanced -= Advance;
            UnlockControls();
            if (eyeCover != null) eyeCover.color = Color.clear;
        }
        private void ResetAttempt()
        {
            lastAttempt = life.AttemptNumber;
            IntroComplete = BedroomLocked = DoorUnlocked = CommanderSuited = false;
            introTime = outsideSeconds = dressingSeconds = commanderHypoxia = 0;
            waypoint = 0; sawBedroom = false;
            follower.ResetRoute();
            crew.Commander.SetPositionAndRotation(sleepingCommander.position, sleepingCommander.rotation);
            bedroomDoor.localPosition = doorOpen;
            eyeCover.color = Color.black;
            LockControls();
            standingOrigin = session.Player.transform.position;
            seatedOrigin = standingOrigin + new Vector3(-1.0f, -.42f, 0);
            session.Player.transform.position = seatedOrigin;
            FeedbackSounds.Play(audioSource, FeedbackSound.StationAlarm, .6f);
        }
        private void LockControls()
        {
            if (controlsLocked) return;
            locomotionBefore = new bool[locomotion.Length];
            for (int i = 0; i < locomotion.Length; i++) if (locomotion[i] != null)
            { locomotionBefore[i] = locomotion[i].enabled; locomotion[i].enabled = false; }
            controlsLocked = true;
        }
        private void UnlockControls()
        {
            if (!controlsLocked) return;
            for (int i = 0; i < locomotion.Length; i++) if (locomotion[i] != null) locomotion[i].enabled = locomotionBefore[i];
            controlsLocked = false;
        }
        private void Update()
        {
            if (life == null) return;
            if (lastAttempt != life.AttemptNumber) ResetAttempt();
            if (!IntroComplete && !life.Station.IsTerminal)
            {
                introTime += Time.deltaTime;
                // Animate the origin from the bed edge to the aisle, never the tracked head.
                // Most translation happens while the eyes are still mostly closed.
                session.Player.transform.position = Vector3.Lerp(seatedOrigin, standingOrigin,
                    Mathf.SmoothStep(0, 1, (introTime - 1.5f) / 2f));
                eyeCover.color = new Color(0, 0, 0, 1 - Mathf.SmoothStep(0, 1, (introTime - 1.6f) / 2.2f));
                if (introTime >= 4.2f)
                {
                    IntroComplete = true; eyeCover.color = Color.clear; session.Player.transform.position = standingOrigin; UnlockControls();
                    sawBedroom = bedroom.bounds.Contains(life.Player.transform.TransformPoint(life.Player.center));
                    if (session.Mission.Phase == StationMissionPhase.Briefing) session.BeginMission();
                }
            }
            Vector3 target = BedroomLocked && !DoorUnlocked ? doorClosed : doorOpen;
            bedroomDoor.localPosition = Vector3.MoveTowards(bedroomDoor.localPosition, target, Time.deltaTime * 7);
            for (int i = 0; i < emergencyLights.Length; i++) if (emergencyLights[i] != null)
            {
                emergencyLights[i].enabled = !breaker.IsOn && life.BasePower > 0;
                emergencyLights[i].intensity = emergencyIntensity[i] * (.82f + .18f * Mathf.Sin(Time.time * 1.5f));
            }
            if (status != null) status.text = Message();
        }
        private string Text(string zh, string en, string ru) => language != null && language.CurrentLanguage == GameLanguage.English ? en
            : language != null && language.CurrentLanguage == GameLanguage.Russian ? ru : zh;
        private string Message()
        {
            if (!IntroComplete) return Text("警报 · 正在苏醒", "ALARM · WAKING UP", "ТРЕВОГА · ПРОБУЖДЕНИЕ");
            if (!BedroomLocked) return Text("离开卧室，检查备用电源", "LEAVE QUARTERS · CHECK BACKUP POWER", "ВЫЙДИТЕ И ПРОВЕРЬТЕ ПИТАНИЕ");
            if (!breaker.IsOn) return Text("卧室门故障：先接通三色电路", "CREW TRAPPED · CONNECT THREE CIRCUITS", "ЭКИПАЖ ЗАПЕРТ · СОЕДИНИТЕ ЦЕПИ");
            if (!laboratory.IsSolved) return Text("去实验室配对三个数据模块", "MATCH THREE LAB DATA MODULES", "СОПОСТАВЬТЕ ТРИ МОДУЛЯ");
            if (!DoorUnlocked) return Text("配对成功：在中控按下解锁", "MODULES READY · CONFIRM AT CONTROL", "МОДУЛИ ГОТОВЫ · ПОДТВЕРДИТЕ");
            if (!CommanderSuited) return Text("队友获救，正在前往衣架穿服", "CREW RESCUED · SUITING UP", "ЭКИПАЖ СПАСЕН · НАДЕВАЕТ СКАФАНДР");
            return Text("队友已穿服：修好气闸后撤离", "CREW SUITED · REPAIR AIRLOCK AND EVACUATE", "СКАФАНДР ГОТОВ · ЭВАКУАЦИЯ");
        }
        private void Advance(float seconds)
        {
            if (!IntroComplete || life.Station.IsTerminal) return;
            var playerCenter = life.Player.transform.TransformPoint(life.Player.center);
            if (bedroom.bounds.Contains(playerCenter)) { sawBedroom = true; outsideSeconds = 0; }
            else if (sawBedroom && !BedroomLocked)
            {
                outsideSeconds += seconds;
                if (outsideSeconds >= 2)
                {
                    BedroomLocked = true; crew.TrapInBedroom();
                    FeedbackSounds.Play(audioSource, FeedbackSound.Clunk, .9f);
                }
            }
            if (!CommanderSuited && (life.DoorOpen || life.BaseOxygen <= 0))
            {
                commanderHypoxia += seconds;
                if (commanderHypoxia >= life.Config.SuffocationSeconds) crew.SuffocateUnsuitedCommander();
            }
            else commanderHypoxia = 0;
            if (!DoorUnlocked || CommanderSuited || crew.CommanderHealth <= 0) return;
            float remaining = seconds;
            while (waypoint < routeToRack.Length && remaining > 0)
            {
                var destination = routeToRack[waypoint].position;
                var delta = destination - crew.Commander.position;
                float travel = Mathf.Min(remaining, delta.magnitude / 1.5f);
                crew.Commander.position = Vector3.MoveTowards(crew.Commander.position, destination, travel * 1.5f);
                if (delta.sqrMagnitude > .001f) crew.Commander.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                remaining -= travel;
                if (Vector3.Distance(crew.Commander.position, destination) < .005f) waypoint++;
            }
            if (waypoint >= routeToRack.Length)
            {
                dressingSeconds += remaining;
                if (dressingSeconds >= life.Config.DonSeconds)
                { CommanderSuited = true; follower.ResetRoute(); crew.FinishIndependentDressing(); }
            }
        }
        public bool TryUnlockBedroom()
        {
            if (!IntroComplete || !life.IsGroundActive || !BedroomLocked || DoorUnlocked || !breaker.IsOn || !laboratory.IsSolved) return false;
            var center = life.Player.transform.TransformPoint(life.Player.center);
            if (Vector3.Distance(center, controlPoint.position) > 2.2f) return false;
            DoorUnlocked = true;
            // First stand beside the bed, then walk through the corridor; never cross a wall.
            if (routeToRack.Length > 0) crew.Commander.SetPositionAndRotation(routeToRack[0].position, routeToRack[0].rotation);
            waypoint = 1; crew.ReleaseFromBedroom();
            FeedbackSounds.Play(audioSource, FeedbackSound.Chime, .7f);
            return true;
        }
        public void UnlockBedroom() => TryUnlockBedroom();
    }
}

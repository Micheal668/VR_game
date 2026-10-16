using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 相机与舱体不参与动画；NPC、分离物与天体在舱外坐标系中运动。
    public sealed class EarthReturnPresenter : MonoBehaviour
    {
        // 返回舱现有顶部通道的端面高度，与 Blender 生成的通道保持一致。
        public const float DockingPortHeight = 4.08f;
        // 上升器连接端面来自现有网格实测，不能使用包含天线的模型总高。
        public const float AscentPortHeight = 6.536f;
        [SerializeField] private EarthReturnMission mission;
        [SerializeField] private FlightScenePresenter flightScene;
        [SerializeField] private FlightSeatLock seatLock;
        [SerializeField] private GameObject world, transferPanel, dashboard;
        [SerializeField] private Transform seat, commander, pilot, hatch, lander, moon, service, ocean, parachutes;
        [SerializeField] private Transform earth;
        [SerializeField] private GameObject guidance;
        [SerializeField] private LocalizedText telemetry;
        [SerializeField] private Button shallower, steeper;
        [SerializeField] private GameObject corridor;
        [SerializeField] private RectTransform angleCursor;
        [SerializeField] private Renderer shockLayer;
        [SerializeField] private Light heatLight;
        [SerializeField] private Transform shell;
        [SerializeField] private Transform heatShield;
        [SerializeField] private GameObject altitudePanel, throttlePanel;
        [SerializeField] private RectTransform altitudeMarker;
        [SerializeField] private LocalizedText altitudeStatus;
        private MaterialPropertyBlock oceanProperties;
        private float originalFarClip;
        public Transform ServiceModule => service;
        public Transform Ocean => ocean;
        public RectTransform AltitudeMarker => altitudeMarker;
        public float EarthSurfaceDistance => earth.localPosition.magnitude - earth.localScale.x * .5f;
        public void ConfigureAltitude(GameObject gauge, GameObject throttleRoot, RectTransform marker, LocalizedText status)
        { altitudePanel = gauge; throttlePanel = throttleRoot; altitudeMarker = marker; altitudeStatus = status; Refresh(); }
        [SerializeField] private Button throttleDown, throttleUp, turnLeft, turnRight, deployChutes, releaseShield;
        [SerializeField] private LocalizedText throttleLabel, directionLabel;
        [SerializeField] private Image throttleBar;
        public Button ThrottleDownButton=>throttleDown;
        public Button ThrottleUpButton=>throttleUp;
        public Button TurnRightButton=>turnRight;
        public Button DeployChutesButton=>deployChutes;
        public Button ReleaseShieldButton=>releaseShield;
        public Transform HeatShield=>heatShield;
        public void ConfigureControls(Button[] controls,LocalizedText throttleText,LocalizedText direction,Image bar,Transform shield)
        {
            throttleDown=controls[0];throttleUp=controls[1];turnLeft=controls[2];turnRight=controls[3];deployChutes=controls[4];releaseShield=controls[5];
            throttleLabel=throttleText;directionLabel=direction;throttleBar=bar;heatShield=shield;Refresh();
        }
        private MaterialPropertyBlock shockProperties;
        public void ConfigureShock(Renderer envelope,Light light) {shockLayer=envelope;heatLight=light;Refresh();}
        public void ConfigureStructure(Transform model) {shell=model;Refresh();}
        public Transform Earth => earth;
        public Transform Moon => moon;
        public Button ShallowerButton => shallower;
        public Button SteeperButton => steeper;
        public void ConfigureGuidance(GameObject panel,LocalizedText label,Button down,Button up,GameObject gauge,RectTransform cursor)
        { guidance=panel;telemetry=label;shallower=down;steeper=up;corridor=gauge;angleCursor=cursor;Refresh(); }
        [SerializeField] private LineRenderer[] plasma;
        [SerializeField] private LocalizedText transferText, phaseText, detailText, crewText, actionText, pauseText;
        [SerializeField] private Button transferButton, actionButton, pauseButton;
        [SerializeField] private Image progress;
        private bool relocated;
        private EarthReturnPhase lastPhase;
        private AudioSource effects, rumble, wind;
        private MaterialPropertyBlock trailProperties;
        public GameObject World => world;
        public GameObject Dashboard => dashboard;
        public Transform Commander => commander;
        public Transform Seat => seat;
        public Transform Lander => lander;
        public Button TransferButton => transferButton;
        public Button ActionButton => actionButton;
        public Button PauseButton => pauseButton;
        public float EarthDiameter { get; private set; } = 6;
        public float Atmosphere { get; private set; }
        public void Configure(EarthReturnMission task, FlightScenePresenter scene, FlightSeatLock lockedSeat,
            GameObject cabin, GameObject transfer, GameObject panel, Transform[] actors, LineRenderer[] trails,
            LocalizedText[] labels, Button[] buttons, Image bar)
        {
            if (mission != null) mission.Changed -= Refresh;
            mission = task; flightScene = scene; seatLock = lockedSeat; world = cabin; transferPanel = transfer; dashboard = panel;
            seat = actors[0]; commander = actors[1]; pilot = actors[2]; hatch = actors[3]; lander = actors[4]; moon = actors[5];
            service = actors[6]; ocean = actors[7]; parachutes = actors[8]; plasma = trails;
            earth=actors[9];
            transferText = labels[0]; phaseText = labels[1]; detailText = labels[2]; crewText = labels[3]; actionText = labels[4]; pauseText = labels[5];
            transferButton = buttons[0]; actionButton = buttons[1]; pauseButton = buttons[2]; progress = bar;
            if (isActiveAndEnabled) mission.Changed += Refresh;
            Refresh();
        }
        private void OnEnable() { if (mission != null) { mission.Changed += Refresh; Refresh(); } }
        private void OnDisable()
        {
            if (mission != null) mission.Changed -= Refresh;
            if (rumble != null) rumble.Stop(); if (wind != null) wind.Stop();
            flightScene?.SetReturnFade(0);
        }
        private void Start()
        {
            effects = FeedbackSounds.CreateSource(transform, "Return events", 0);
            rumble = FeedbackSounds.CreateLoop(transform, "Return engine", FeedbackSound.EngineLoop, 0);
            wind = FeedbackSounds.CreateLoop(transform, "Return atmosphere", FeedbackSound.ThrusterLoop, 0);
            rumble.Play(); wind.Play(); Refresh();
        }
        private void Refresh()
        {
            if (mission == null || world == null) return;
            var phase = mission.Phase; float p = mission.Progress;
            bool inside = mission.InReturnCabin;
            if (phase == EarthReturnPhase.Inactive)
            {
                relocated = false;
                if (Camera.main != null && originalFarClip > 0) Camera.main.farClipPlane = originalFarClip;
            }
            world.SetActive(inside);
            transferPanel.SetActive(phase == EarthReturnPhase.AwaitingTransfer || phase == EarthReturnPhase.TransferFade);
            dashboard.SetActive(inside && !mission.IsComplete && !mission.IsFailed);
            if (phase != EarthReturnPhase.Inactive) flightScene.Panel.SetActive(false);
            if (inside && !relocated)
            {
                // 先盖住视野再改变身体位置，保持设备追踪局部坐标。
                flightScene.SetReturnFade(1);
                seatLock.Relocate(seat.position, seat.forward); relocated = true;
                flightScene.FlightWorld.SetActive(false);
                if (Camera.main != null) { originalFarClip = Camera.main.farClipPlane; Camera.main.farClipPlane = 150000; }
            }
            flightScene.SetReturnFade(phase == EarthReturnPhase.TransferFade ? p : phase == EarthReturnPhase.CrewTransfer ? 1 - Mathf.Clamp01(mission.Elapsed / 1.2f) : 0);
            if (phase != lastPhase)
            {
                if (phase == EarthReturnPhase.Separating || phase == EarthReturnPhase.ServiceSeparation || phase == EarthReturnPhase.Parachutes)
                    FeedbackSounds.Play(effects, FeedbackSound.Clunk, .45f);
                if (phase == EarthReturnPhase.AwaitingTransfer || phase == EarthReturnPhase.AwaitingDeparture || phase == EarthReturnPhase.Complete)
                    FeedbackSounds.Play(effects, FeedbackSound.Chime, .45f);
                if (phase == EarthReturnPhase.Splashdown) FeedbackSounds.Play(effects, FeedbackSound.Hiss, .65f);
                if (phase == EarthReturnPhase.Breakup) FeedbackSounds.Play(effects,FeedbackSound.Decompression,.9f);
                if (phase == EarthReturnPhase.SkipOut || phase == EarthReturnPhase.HardLanding) FeedbackSounds.Play(effects,FeedbackSound.StationAlarm,.6f);
                lastPhase = phase;
            }
            if (rumble != null) rumble.volume = !mission.Paused && (phase == EarthReturnPhase.DepartureBurn || phase == EarthReturnPhase.EarthApproach || phase == EarthReturnPhase.DeorbitBurn) ? .24f : 0;
            if (wind != null) wind.volume = !mission.Paused && phase == EarthReturnPhase.Entry ? .22f * Mathf.Sin(p * Mathf.PI) : 0;
            transferText.SetKey("return.transfer.detail");
            transferButton.interactable = phase == EarthReturnPhase.AwaitingTransfer;
            phaseText.SetKey("return.phase." + phase);
            detailText.SetKey(mission.Paused ? "return.paused" : phase == EarthReturnPhase.CrewTransfer && !mission.CommanderTransferred ? "return.detail.solo" : "return.detail." + phase);
            if (phase == EarthReturnPhase.HardLanding || mission.IsFailed) detailText.SetKey(mission.FailureKey);
            crewText.SetKey(mission.CommanderTransferred ? "return.crew.all" : "return.crew.absent");
            bool entryControls=phase==EarthReturnPhase.OrbitHold;
            bool instruments=inside && phase!=EarthReturnPhase.Complete;
            crewText.gameObject.SetActive(true);
            if(guidance!=null)
            {
                guidance.SetActive(instruments);
                shallower.gameObject.SetActive(true);steeper.gameObject.SetActive(true);
                corridor.SetActive(true);angleCursor.anchoredPosition=new Vector2(465+(mission.EntryAngle-6.5f)/11*400,-78);
                shallower.interactable=entryControls&&mission.EntryAngle>1;steeper.interactable=entryControls&&mission.EntryAngle<12;
                telemetry.SetKey("return.telemetry",mission.SpeedKmS.ToString("F3"),mission.AltitudeKm.ToString("F1"),mission.OutsideTemperatureC.ToString("F0"),mission.HeatC.ToString("F0"),mission.GLoad.ToString("F1"),((1-mission.Damage)*100).ToString("F0"));
                telemetry.GetComponent<TMPro.TMP_Text>().color=mission.Damage>.1f?new Color(1,.4f,.18f):new Color(.55f,.95f,1);
                if(entryControls)detailText.SetKey(mission.InCorridor?"return.corridor.safe":mission.EntryAngle<5.5f?"return.corridor.shallow":"return.corridor.steep");
            }
            if(throttleDown!=null)
            {
                throttleDown.interactable=mission.CanThrottle&&!mission.Paused&&mission.Throttle>0;
                throttleUp.interactable=mission.CanThrottle&&!mission.Paused&&mission.Throttle<1;
                turnLeft.interactable=mission.CanSteer&&mission.HeadingDegrees>-20;turnRight.interactable=mission.CanSteer&&mission.HeadingDegrees<20;
                deployChutes.interactable=mission.CanDeployParachutes&&!mission.Paused;releaseShield.interactable=mission.CanReleaseHeatShield&&!mission.Paused;
                throttleLabel.SetKey("return.throttle",Mathf.RoundToInt(mission.Throttle*100).ToString());throttleBar.fillAmount=mission.Throttle;
                directionLabel.SetKey("return.direction",mission.EntryAngle.ToString("F1"),mission.HeadingDegrees.ToString("F0"));
            }
            bool separation = phase == EarthReturnPhase.AwaitingSeparation;
            actionButton.gameObject.SetActive(separation || phase == EarthReturnPhase.AwaitingDeparture || entryControls);
            actionText.SetKey(entryControls ? "return.action.deorbit" : separation ? "return.action.separate" : "return.action.depart");
            pauseButton.gameObject.SetActive(inside && mission.IsAnimating);
            bool descending = mission.HasEnteredAtmosphere;
            if (altitudePanel != null)
            {
                altitudePanel.SetActive(descending);
                throttlePanel.SetActive(!descending);
                altitudeMarker.anchoredPosition = new Vector2(-650, -235 + Mathf.Clamp01(mission.AltitudeKm / 4.2f) * 360);
                altitudeStatus.SetKey(mission.IsFailed || phase == EarthReturnPhase.HardLanding ? "return.altitude.missed"
                    : mission.CanDeployParachutes ? "return.altitude.chute_now" : mission.CanReleaseHeatShield ? "return.altitude.shield_now"
                    : mission.HeatShieldReleased ? "return.altitude.landing" : mission.ParachutesDeployed ? "return.altitude.shield_next" : "return.altitude.chute_next");
                pauseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(descending ? 0 : -480, descending ? -280 : -205);
                crewText.gameObject.SetActive(!descending);
            }
            pauseText.SetKey(mission.Paused ? "return.resume" : "return.pause");
            progress.fillAmount = Mathf.Clamp01(mission.FlightElapsed / EarthReturnMission.NominalFlightSeconds);
            commander.gameObject.SetActive(inside && mission.CommanderTransferred);
            pilot.gameObject.SetActive(inside && mission.PilotPresent);
            float transfer = phase == EarthReturnPhase.CrewTransfer ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((mission.Elapsed - 1) / 5)) : 1;
            commander.localPosition = Vector3.Lerp(new Vector3(0,2.65f,0), new Vector3(-.96f,0,-.45f), transfer);
            commander.localRotation = Quaternion.Euler(Mathf.Lerp(65,0,transfer),0,0);
            bool hatchOpen = phase == EarthReturnPhase.CrewTransfer || phase == EarthReturnPhase.AwaitingSeparation;
            hatch.localRotation = Quaternion.Euler(0,0,hatchOpen ? 102 : 0);
            float separationProgress = phase < EarthReturnPhase.Separating ? 0 : phase == EarthReturnPhase.Separating ? p : 1;
            lander.localPosition = Vector3.Lerp(new Vector3(0,DockingPortHeight+AscentPortHeight,0),new Vector3(-12,18,34),Mathf.SmoothStep(0,1,separationProgress));
            lander.localRotation = Quaternion.Euler(Mathf.Lerp(180,155,separationProgress),195,separationProgress*5);
            lander.gameObject.SetActive(inside && phase <= EarthReturnPhase.Coast);
            float departure = phase < EarthReturnPhase.DepartureBurn ? 0 : phase == EarthReturnPhase.DepartureBurn ? p * .25f : phase == EarthReturnPhase.Coast ? .25f + p * .75f : 1;
            // 同一舱外参考系：飞船向上离月时，月球和抛弃的上升级都向下后方移动。
            var departureOffset=new Vector3(0,-180,400)*departure;
            lander.localPosition+=departureOffset;
            moon.localPosition = new Vector3(-115,-22,320)+departureOffset*3;
            moon.localScale = Vector3.one * 180;
            moon.gameObject.SetActive(inside && phase <= EarthReturnPhase.Coast);
            // 地球半径固定，表面距离由同一高度驱动；再入和开伞都不切换天体。
            const float radius = 6000;
            float altitude = phase == EarthReturnPhase.Inactive ? 384400 : Mathf.Max(0, mission.AltitudeKm);
            float surface = altitude <= 220 ? altitude * 10 : 2200 + 112000 * Mathf.Pow(Mathf.InverseLerp(220, 384400, altitude), .55f);
            var orbitalDirection = new Vector3(0, -.78f, .625f).normalized;
            var direction = altitude <= 220 ? Vector3.Slerp(Vector3.down, orbitalDirection, Mathf.Clamp01((altitude - 4.2f) / 215.8f))
                : Vector3.Slerp(orbitalDirection, new Vector3(.25f, .25f, .935f).normalized, Mathf.InverseLerp(220, 384400, altitude));
            var yaw = Quaternion.Euler(0, -mission.HeadingDegrees, 0);
            earth.localPosition = yaw * direction * (radius + surface + .25f);
            earth.localScale = Vector3.one * radius * 2;
            earth.localRotation = yaw * Quaternion.Euler(0, mission.EarthRotation, 23.4f);
            earth.gameObject.SetActive(inside);
            EarthDiameter = phase == EarthReturnPhase.Inactive ? 6 : Mathf.Asin(radius / (radius + surface + .25f)) * 2 * Mathf.Rad2Deg;
            Atmosphere = inside && phase >= EarthReturnPhase.Entry ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(100, 8, altitude)) : 0;
            // 服务舱随返回舱飞行，直到地球再入前才分离；顶部只保留返回舱原有对接口。
            float detached = phase < EarthReturnPhase.ServiceSeparation ? 0 : phase == EarthReturnPhase.ServiceSeparation ? p : 1;
            float drifting = phase == EarthReturnPhase.Entry ? mission.Elapsed : phase > EarthReturnPhase.Entry ? 16 : 0;
            float separationDistance = Mathf.SmoothStep(0, 1, detached);
            service.gameObject.SetActive(inside && phase <= EarthReturnPhase.Entry);
            service.localPosition = new Vector3(0, 3.30f, 0) + new Vector3(-8, -10, 15) * separationDistance + new Vector3(-3, -4, 5) * drifting;
            service.localRotation = Quaternion.Euler(90 + detached * 12, -detached * 10, detached * 8);
            ocean.gameObject.SetActive(inside && mission.HasEnteredAtmosphere);
            ocean.localPosition = new Vector3(0, -altitude * 10 + .08f, 0);
            oceanProperties ??= new MaterialPropertyBlock();
            oceanProperties.SetFloat("_Opacity", Mathf.SmoothStep(0, 1, Mathf.InverseLerp(30, 6, altitude)));
            oceanProperties.SetFloat("_WaveTime", mission.FlightElapsed);
            oceanProperties.SetVector("_CapsulePosition", world.transform.position);
            oceanProperties.SetFloat("_Splash", Mathf.Clamp01(1 - altitude / .08f));
            ocean.GetComponent<Renderer>().SetPropertyBlock(oceanProperties);
            parachutes.gameObject.SetActive(inside);
            parachutes.localScale = Vector3.one;
            parachutes.localPosition = Vector3.zero;
            if(heatShield!=null && phase!=EarthReturnPhase.Breakup && !(mission.IsFailed&&mission.TooSteep))
            {
                float age = mission.HeatShieldAge;
                heatShield.localPosition = new Vector3(age * .65f, -age * 1.3f - age * age * 1.8f, age * .4f);
                heatShield.localRotation = Quaternion.Euler(age * 27, 0, age * 8);
            }
            for (int i=0;i<plasma.Length;i++)
            {
                plasma[i].enabled = phase == EarthReturnPhase.Entry || phase==EarthReturnPhase.Breakup;
                float energy = phase == EarthReturnPhase.Entry ? Mathf.Sin(p*Mathf.PI) : phase==EarthReturnPhase.Breakup?1:0;
                plasma[i].widthMultiplier = (.04f + .20f*energy) * (1 + .25f*Mathf.Sin(mission.Elapsed*4+i));
                plasma[i].startColor = new Color(1,.18f+.45f*energy,.04f); plasma[i].endColor = new Color(.6f,.035f,.005f);
                trailProperties ??= new MaterialPropertyBlock();trailProperties.SetFloat("_FlowTime",mission.Elapsed+i*.3f);plasma[i].SetPropertyBlock(trailProperties);
            }
            if(shockLayer!=null)
            {
                bool heating=phase==EarthReturnPhase.Entry || phase==EarthReturnPhase.Breakup;
                float energy=phase==EarthReturnPhase.Entry?Mathf.Clamp01((mission.HeatC-250)/1900):phase==EarthReturnPhase.Breakup?1:0;
                shockLayer.enabled=heating;shockProperties??=new MaterialPropertyBlock();
                shockProperties.SetFloat("_Energy",energy);shockProperties.SetFloat("_FlowTime",mission.Elapsed);shockLayer.SetPropertyBlock(shockProperties);
                heatLight.intensity=energy*(phase==EarthReturnPhase.Breakup?8*(1-p):2);
            }
            // 解体碎片向舱外飞散；保持玩家相机固定，不强制晃动 VR 头部。
            if(shell!=null && (phase==EarthReturnPhase.Breakup || mission.IsFailed&&mission.TooSteep || phase==EarthReturnPhase.Inactive))
                foreach(Transform part in shell)
                {
                    if(part==hatch)continue;
                    if(phase==EarthReturnPhase.Breakup || mission.IsFailed&&mission.TooSteep)
                    {float f=mission.IsFailed?1:p;var dir=part.GetComponent<Renderer>().localBounds.center.normalized;part.localPosition=dir*f*f*12;part.localRotation=Quaternion.Euler(dir*f*95);}
                    else{part.localPosition=Vector3.zero;part.localRotation=Quaternion.identity;}
                }
        }
    }
}

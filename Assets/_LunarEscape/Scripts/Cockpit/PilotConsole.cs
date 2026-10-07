using UnityEngine;

namespace LunarEscape
{
    // 实体驾驶台：把开关、保护盖和手控器连接到已有的 AscentMission / DockingMission 接口。
    // 与原有面板按钮调用完全相同的方法，规则、扣电和顺序检查都留在任务里；
    // 这里只负责“谁被按下 → 调用哪个方法”，以及用指示灯和拨杆位置显示任务状态。
    public sealed class PilotConsole : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        [Header("启动")]
        [SerializeField] private CockpitSwitch power;
        [SerializeField] private CockpitSwitch navigation;
        [SerializeField] private CockpitSwitch engine;
        [SerializeField] private CockpitSwitch ignitionCover;
        [SerializeField] private CockpitSwitch ignition;
        [SerializeField] private CockpitLamp[] startupLamps = new CockpitLamp[4];
        [Header("入轨")]
        [SerializeField] private CockpitSwitch burnCover;
        [SerializeField] private CockpitSwitch burn;
        [SerializeField] private CockpitLamp burnLamp;
        [Header("对接")]
        [SerializeField] private CockpitStick translation;
        [SerializeField] private CockpitStick rotation;
        [SerializeField] private CockpitSwitch brake;
        [SerializeField] private CockpitSwitch boost;
        [SerializeField] private CockpitSwitch assist;
        [SerializeField] private CockpitLamp assistLamp;
        [SerializeField] private CockpitLamp boostLamp;
        private Vector3Int sentTranslation;
        private Vector3Int sentRotation;
        private bool sentBrake;
        private bool sentBoost;

        public CockpitSwitch Power => power;
        public CockpitSwitch Navigation => navigation;
        public CockpitSwitch Engine => engine;
        public CockpitSwitch IgnitionCover => ignitionCover;
        public CockpitSwitch Ignition => ignition;
        public CockpitSwitch BurnCover => burnCover;
        public CockpitSwitch Burn => burn;
        public CockpitStick Translation => translation;
        public CockpitStick Rotation => rotation;
        public CockpitSwitch Brake => brake;
        public CockpitSwitch Boost => boost;
        public CockpitSwitch Assist => assist;
        public CockpitControl[] ThrustControls => new CockpitControl[] { translation, rotation, brake, boost };

        public void ConfigureStartup(AscentMission mission, CockpitSwitch powerSwitch, CockpitSwitch navigationSwitch,
            CockpitSwitch engineSwitch, CockpitSwitch cover, CockpitSwitch ignitionButton, CockpitLamp[] lamps)
        {
            flight = mission; power = powerSwitch; navigation = navigationSwitch; engine = engineSwitch;
            ignitionCover = cover; ignition = ignitionButton; startupLamps = lamps;
        }

        public void ConfigureOrbit(CockpitSwitch cover, CockpitSwitch button, CockpitLamp lamp)
        { burnCover = cover; burn = button; burnLamp = lamp; }

        public void ConfigureDocking(CockpitStick translationStick, CockpitStick rotationStick, CockpitSwitch brakeButton,
            CockpitSwitch boostButton, CockpitSwitch assistSwitch, CockpitLamp assistIndicator, CockpitLamp boostIndicator)
        {
            translation = translationStick; rotation = rotationStick; brake = brakeButton; boost = boostButton;
            assist = assistSwitch; assistLamp = assistIndicator; boostLamp = boostIndicator;
        }

        private void OnEnable()
        {
            if (flight == null) return;
            power.Pressed += OnPower;
            navigation.Pressed += OnNavigation;
            engine.Pressed += OnEngine;
            ignition.Pressed += OnIgnition;
            burn.Pressed += OnBurn;
            assist.Pressed += OnAssist;
            brake.Pressed += OnDockingHold;
            boost.Pressed += OnDockingHold;
            flight.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (flight == null) return;
            power.Pressed -= OnPower;
            navigation.Pressed -= OnNavigation;
            engine.Pressed -= OnEngine;
            ignition.Pressed -= OnIgnition;
            burn.Pressed -= OnBurn;
            assist.Pressed -= OnAssist;
            brake.Pressed -= OnDockingHold;
            boost.Pressed -= OnDockingHold;
            flight.PhaseChanged -= OnPhaseChanged;
            ReleaseDockingCommands();
        }

        private void OnPower(CockpitSwitch control) => Attempt(control, startupLamps[0], () => { flight.PowerOn(); return flight.Powered; });
        private void OnNavigation(CockpitSwitch control) => Attempt(control, startupLamps[1], () => { flight.StartNavigation(); return flight.Operation == StartupOperation.Navigation || flight.NavigationReady; });
        private void OnEngine(CockpitSwitch control) => Attempt(control, startupLamps[2], () => { flight.PrepareEngine(); return flight.Operation == StartupOperation.Engine || flight.EngineReady; });

        private void OnIgnition(CockpitSwitch control)
        {
            // 保护盖关着时按钮压不下去：先掀盖，再按。
            if (!ignitionCover.Latched) { Reject(control, startupLamps[3]); return; }
            Attempt(control, startupLamps[3], () => { flight.Ignite(); return flight.Phase != AscentPhase.Startup; });
        }

        private void OnBurn(CockpitSwitch control)
        {
            if (!burnCover.Latched) { Reject(control, burnLamp); return; }
            Attempt(control, burnLamp, () => { flight.Circularize(); return flight.Phase == AscentPhase.Circularizing; });
        }

        private void OnAssist(CockpitSwitch control)
        {
            var docking = flight.Docking;
            if (docking == null || !docking.Active) { Reject(control, assistLamp); return; }
            docking.ToggleAssistance();
        }

        // 刹车与主推只在接近阶段有效；主推还要求距离大于 12 m（与原面板按钮规则相同）。
        private void OnDockingHold(CockpitSwitch control)
        {
            var docking = flight.Docking;
            bool allowed = docking != null && docking.Active && (control != boost || docking.CanBoost);
            if (!allowed) Reject(control, control == boost ? boostLamp : null);
        }

        // 已经完成的步骤再拨一次不算错误；只有任务真正拒绝时才给出“嗡嗡”反馈和红灯。
        private void Attempt(CockpitSwitch control, CockpitLamp lamp, System.Func<bool> action)
        {
            if (control.Latched) return;
            if (!action()) Reject(control, lamp);
        }

        private static void Reject(CockpitSwitch control, CockpitLamp lamp)
        {
            control.Deny();
            if (lamp != null) lamp.Flash();
        }

        private void OnPhaseChanged(AscentPhase phase)
        {
            if (phase == AscentPhase.AwaitingBoarding)
            {
                // 重新开始：所有开关回到“关”，保护盖合上。
                ignitionCover.Latched = false;
                burnCover.Latched = false;
                // 驾驶台随后会被隐藏、不再 Update，这里立即同步一次开关位置。
                Refresh();
            }
            if (phase != AscentPhase.Rendezvous) ReleaseDockingCommands();
        }

        private void Update()
        {
            if (flight == null) return;
            Refresh();
            DriveDocking();
        }

        private void Refresh()
        {
            power.Latched = flight.Powered;
            navigation.Latched = flight.NavigationReady || flight.Operation == StartupOperation.Navigation;
            engine.Latched = flight.EngineReady || flight.Operation == StartupOperation.Engine;
            ignition.Latched = flight.Phase != AscentPhase.AwaitingBoarding && flight.Phase != AscentPhase.Startup
                && flight.Phase != AscentPhase.FadeOut && flight.Phase != AscentPhase.Black && flight.Phase != AscentPhase.FadeIn;

            startupLamps[0].State = flight.Powered ? LampState.Done : flight.CanPowerOn ? LampState.Next : LampState.Off;
            startupLamps[1].State = flight.NavigationReady ? LampState.Done : flight.Operation == StartupOperation.Navigation ? LampState.Busy
                : flight.CanNavigate ? LampState.Next : LampState.Off;
            startupLamps[2].State = flight.EngineReady ? LampState.Done : flight.Operation == StartupOperation.Engine ? LampState.Busy
                : flight.CanPrepareEngine ? LampState.Next : LampState.Off;
            startupLamps[3].State = ignition.Latched ? LampState.Done : flight.CanIgnite ? LampState.Next : LampState.Off;

            burn.Latched = flight.OrbitBurnProgress > 0f;
            burnLamp.State = flight.OrbitBurnProgress >= 1f ? LampState.Done : flight.Phase == AscentPhase.Circularizing ? LampState.Busy
                : flight.CanCircularize ? LampState.Next : LampState.Off;

            var docking = flight.Docking;
            bool active = docking != null && docking.Active;
            assist.Latched = docking != null && docking.AssistanceEnabled;
            assistLamp.State = !active ? LampState.Off : docking.AssistanceActive ? LampState.Busy : docking.AssistanceEnabled ? LampState.Done : LampState.Off;
            boostLamp.State = active && docking.CanBoost ? LampState.Done : LampState.Off;
        }

        // 手控器与按住类按钮 → DockingMission 指令。只在状态变化时发送，
        // 不会每帧覆盖原有面板按钮发出的同名指令。
        private void DriveDocking()
        {
            var docking = flight.Docking;
            if (docking == null || !docking.Active) { ReleaseDockingCommands(); return; }

            Vector3Int move = translation.Engaged;
            SendAxis(docking, sentTranslation.x, move.x, DockCommand.Right, DockCommand.Left);
            SendAxis(docking, sentTranslation.y, move.y, DockCommand.Up, DockCommand.Down);
            SendAxis(docking, sentTranslation.z, move.z, DockCommand.Forward, DockCommand.Backward);
            sentTranslation = move;

            // 球向前倾（绕 +X 转）= 低头；向右转（绕 +Y）= 右偏航；绕 +Z 转 = 左滚转，与 DockingMission 的轴向一致。
            Vector3Int turn = rotation.Engaged;
            SendAxis(docking, sentRotation.x, turn.x, DockCommand.PitchDown, DockCommand.PitchUp);
            SendAxis(docking, sentRotation.y, turn.y, DockCommand.YawRight, DockCommand.YawLeft);
            SendAxis(docking, sentRotation.z, turn.z, DockCommand.RollLeft, DockCommand.RollRight);
            sentRotation = turn;

            if (brake.IsHeld != sentBrake) { docking.SetCommand(DockCommand.Brake, brake.IsHeld); sentBrake = brake.IsHeld; }
            bool boosting = boost.IsHeld && docking.CanBoost;
            if (boosting != sentBoost) { docking.SetCommand(DockCommand.MainBoost, boosting); sentBoost = boosting; }
        }

        private static void SendAxis(DockingMission docking, int previous, int current, DockCommand positive, DockCommand negative)
        {
            if (previous == current) return;
            if (previous > 0) docking.SetCommand(positive, false);
            if (previous < 0) docking.SetCommand(negative, false);
            if (current > 0) docking.SetCommand(positive, true);
            if (current < 0) docking.SetCommand(negative, true);
        }

        private void ReleaseDockingCommands()
        {
            var docking = flight != null ? flight.Docking : null;
            if (docking != null)
            {
                SendAxis(docking, sentTranslation.x, 0, DockCommand.Right, DockCommand.Left);
                SendAxis(docking, sentTranslation.y, 0, DockCommand.Up, DockCommand.Down);
                SendAxis(docking, sentTranslation.z, 0, DockCommand.Forward, DockCommand.Backward);
                SendAxis(docking, sentRotation.x, 0, DockCommand.PitchDown, DockCommand.PitchUp);
                SendAxis(docking, sentRotation.y, 0, DockCommand.YawRight, DockCommand.YawLeft);
                SendAxis(docking, sentRotation.z, 0, DockCommand.RollLeft, DockCommand.RollRight);
                if (sentBrake) docking.SetCommand(DockCommand.Brake, false);
                if (sentBoost) docking.SetCommand(DockCommand.MainBoost, false);
            }
            sentTranslation = sentRotation = Vector3Int.zero;
            sentBrake = sentBoost = false;
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LunarEscape
{
    [DefaultExecutionOrder(140)]
    public sealed class ArtemisLandingPilot : MonoBehaviour
    {
        [SerializeField] private ArtemisLanding flight;
        [SerializeField] private ArtemisViewRig rig;
        [SerializeField] private GameObject cabin;
        [SerializeField] private LocalizationService language;
        [SerializeField] private TMP_Text guidance, telemetry, controls;
        [SerializeField] private RectTransform plot, blip;
        [SerializeField] private Image curtain;
        private InputAction attitude, thrustYaw, stage, engine, view;
        private AudioSource rumble;
        private float nextRefresh;
        private bool loading;
        public bool Cockpit { get; private set; }

        public void Configure(ArtemisLanding landing, ArtemisViewRig observer, GameObject cabinInterior, LocalizationService localization,
            TMP_Text instruction, TMP_Text readings, TMP_Text bindings, RectTransform radar, RectTransform dot, Image fade)
        {
            flight = landing;
            rig = observer;
            cabin = cabinInterior;
            language = localization;
            guidance = instruction;
            telemetry = readings;
            controls = bindings;
            plot = radar;
            blip = dot;
            curtain = fade;
        }

        private static InputAction Axis(string name, string hand, string up, string down, string left, string right)
        {
            var action = new InputAction(name, InputActionType.Value, expectedControlType: "Vector2");
            foreach (var control in new[] { "primary2DAxis", "trackpad", "thumbstick" })
                action.AddBinding("<XRController>{" + hand + "}/" + control);
            action.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/" + up)
                .With("Down", "<Keyboard>/" + down).With("Left", "<Keyboard>/" + left).With("Right", "<Keyboard>/" + right);
            return action;
        }

        private static InputAction Button(string name, string hand, string button, string key)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding("<XRController>{" + hand + "}/" + button + "Pressed");
            action.AddBinding("<XRController>{" + hand + "}/" + button);
            action.AddBinding("<Keyboard>/" + key);
            return action;
        }

        private void Awake()
        {
            attitude = Axis("Pitch and roll", "LeftHand", "w", "s", "a", "d");
            thrustYaw = Axis("Engine and heading", "RightHand", "upArrow", "downArrow", "leftArrow", "rightArrow");
            stage = Button("Undock or retry", "LeftHand", "grip", "x");
            engine = Button("Engine ignition or cutoff", "RightHand", "trigger", "space");
            view = Button("Change pilot view", "RightHand", "grip", "tab");
            stage.performed += _ => flight.SeparateOrRetry();
            engine.performed += _ => flight.ToggleEngine();
            view.performed += _ => SetCockpit(!Cockpit);
            rumble = FeedbackSounds.CreateLoop(transform, "Lander engine", FeedbackSound.EngineLoop, 0);
        }

        private void OnEnable()
        {
            attitude.Enable();
            thrustYaw.Enable();
            stage.Enable();
            engine.Enable();
            view.Enable();
        }

        private void OnDisable()
        {
            attitude.Disable();
            thrustYaw.Disable();
            stage.Disable();
            engine.Disable();
            view.Disable();
        }

        private void OnDestroy()
        {
            attitude.Dispose();
            thrustYaw.Dispose();
            stage.Dispose();
            engine.Dispose();
            view.Dispose();
        }

        private void Start() => rumble.Play();

        public void SetCockpit(bool inside)
        {
            Cockpit = inside;
            flight.Model.Exterior.SetActive(!inside);
            cabin.SetActive(inside);
        }

        private void Update()
        {
            flight.Advance(Time.deltaTime, attitude.ReadValue<Vector2>(), thrustYaw.ReadValue<Vector2>());
            rumble.volume = Mathf.MoveTowards(rumble.volume, flight.Thrust * (Cockpit ? .18f : .32f), Time.deltaTime);
            if (flight.Phase == LandingPhase.Landed && flight.PhaseTime >= 3.5f && !loading)
            {
                loading = true;
                LocalizationService.CarriedLanguage = language.CurrentLanguage;
                SceneManager.LoadSceneAsync(ArtemisPrologue.DayOneScene);
            }
        }

        private void LateUpdate()
        {
            bool surface = flight.Phase == LandingPhase.Approach || flight.Phase == LandingPhase.Retry || flight.Phase == LandingPhase.Landed;
            var craft = flight.Craft;
            if (Cockpit)
                rig.Follow(flight.Model.PilotEye.position, flight.Model.PilotEye.rotation);
            else
            {
                var heading = Quaternion.Euler(0, flight.Heading, 0);
                Vector3 offset = surface ? new Vector3(9, 13, -17) : new Vector3(13, 8, -22);
                var position = craft.position + heading * offset;
                Vector3 target = craft.position + heading * (surface ? new Vector3(1, 2, 4) : new Vector3(0, 5.5f, 1.6f));
                rig.Follow(position, Quaternion.LookRotation(target - position));
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .08f;
                RefreshDisplay();
            }
            float fade = flight.Phase switch
            {
                LandingPhase.Docked => 1f - Mathf.Clamp01(flight.PhaseTime / .55f),
                LandingPhase.DepartureBurn => Mathf.InverseLerp(3.45f, 3.98f, flight.PhaseTime),
                LandingPhase.Approach => 1f - Mathf.Clamp01(flight.PhaseTime / .65f),
                LandingPhase.Landed => Mathf.InverseLerp(2.75f, 3.48f, flight.PhaseTime),
                _ => 0f
            };
            curtain.color = new Color(0, 0, 0, fade);
        }

        private void RefreshDisplay()
        {
            string context = rig.HasHeadset ? ".vr" : ".keys";
            bool steering = flight.Phase == LandingPhase.Approach;
            plot.gameObject.SetActive(steering);
            string instruction = flight.Phase.ToString();
            if (steering)
            {
                if (!flight.EngineOn) instruction = "EngineOff";
                else if (!flight.FeetInsidePad()) instruction = "Align";
                else if (flight.Drift > ArtemisLanding.SafeDrift || flight.Tilt > ArtemisLanding.SafeTilt) instruction = "Level";
                else if (flight.Height < 12 && flight.Velocity.y < -ArtemisLanding.SafeDescent) instruction = "Brake";
            }
            if (flight.Phase == LandingPhase.Retry) instruction = flight.Fault.ToString();
            guidance.text = language.Format("landing." + instruction);
            string input = flight.Phase switch
            {
                LandingPhase.Docked => "undock",
                LandingPhase.Separated => "ignite",
                LandingPhase.Approach => "pilot",
                LandingPhase.Retry => "retry",
                _ => "view"
            };
            controls.text = language.Format("landing." + input + context);
            telemetry.text = steering ? language.Format("landing.instruments", Mathf.Max(0, flight.Height), flight.Range,
                flight.Velocity.y, flight.Drift, 100 * flight.Thrust) : language.Format("landing." + flight.Phase + ".status");
            if (steering)
            {
                var local = Quaternion.Inverse(Quaternion.Euler(0, flight.Heading, 0)) * flight.OffsetToPad;
                blip.anchoredPosition = Vector2.ClampMagnitude(new Vector2(local.x, local.z) * 1.5f, 52f);
            }
        }
    }
}

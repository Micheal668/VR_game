using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LunarEscape
{
    // The shot moves the observation frame. Head tracking stays local to that frame.
    [DefaultExecutionOrder(150)]
    public sealed class ArtemisPrologue : MonoBehaviour
    {
        public const string SceneName = "00_ArtemisPrologue";
        public const string DayOneScene = "12_LunarStation_DayOne";
        [SerializeField] private LaunchCinematic timeline;
        [SerializeField] private ArtemisViewRig rig;
        [SerializeField] private LocalizationService language;
        [SerializeField] private TMP_Text title, controls;
        [SerializeField] private CanvasGroup caption;
        [SerializeField] private Image curtain;

        private InputAction switchShot, skip;
        private bool showedHeadsetHint;
        private int lastPhase = -1;
        private float captionUntil;
        private bool leaving;
        private AudioSource engine, cabin;

        public bool CrewPerspective { get; private set; }
        public bool Leaving => leaving;
        public Camera Eye => rig.Eye;
        public LaunchCinematic Timeline => timeline;
        public Transform Mount => CrewPerspective ? timeline.CrewView : timeline.RocketView;

        // Entering Play from the station should start at the same point as the build.
        // AfterSceneLoad runs only at application startup, so the film can then load Day One.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PlayOpeningFromDayOne()
        {
            if (SceneManager.GetActiveScene().name == DayOneScene && Application.CanStreamedLevelBeLoaded(SceneName))
                SceneManager.LoadScene(SceneName);
        }

        public void Configure(LaunchCinematic film, ArtemisViewRig view,
            LocalizationService localization, TMP_Text heading, TMP_Text instructions, CanvasGroup captionPanel, Image fade)
        {
            timeline = film;
            rig = view;
            language = localization;
            title = heading;
            controls = instructions;
            caption = captionPanel;
            curtain = fade;
        }

        private void Awake()
        {
            switchShot = new InputAction("Change cinematic camera", InputActionType.Button);
            switchShot.AddBinding("<XRController>/triggerPressed");
            switchShot.AddBinding("<XRController>/trigger");
            switchShot.AddBinding("<Keyboard>/space");
            skip = new InputAction("Finish cinematic", InputActionType.Button);
            skip.AddBinding("<XRController>/gripPressed").WithInteraction("hold(duration=1.5)");
            skip.AddBinding("<XRController>/grip").WithInteraction("hold(duration=1.5)");
            skip.AddBinding("<Keyboard>/escape");
            engine = FeedbackSounds.CreateLoop(transform, "Launch rumble", FeedbackSound.EngineLoop, 0);
            cabin = FeedbackSounds.CreateLoop(transform, "Cabin air", FeedbackSound.VentLoop, 0);
        }

        private void OnEnable()
        {
            switchShot.performed += SwitchShot;
            skip.performed += Skip;
            switchShot.Enable();
            skip.Enable();
            timeline.Completed += Finish;
        }

        private void OnDisable()
        {
            timeline.Completed -= Finish;
            switchShot.performed -= SwitchShot;
            skip.performed -= Skip;
            switchShot.Disable();
            skip.Disable();
        }

        private void OnDestroy()
        {
            switchShot?.Dispose();
            skip?.Dispose();
        }

        private void Start()
        {
            engine.Play();
            cabin.Play();
            timeline.Play();
            SelectView(false);
        }

        private void SwitchShot(InputAction.CallbackContext input) => SelectView(!CrewPerspective);
        private void Skip(InputAction.CallbackContext input) => Finish();

        public void SelectView(bool crew)
        {
            if (leaving) return;
            CrewPerspective = crew;
            timeline.SetCrewView(crew);
            FollowMount();
            ShowCaption();
        }

        private void FollowMount()
        {
            var mount = Mount;
            rig.Follow(mount.position, mount.rotation);
        }

        private void LateUpdate()
        {
            FollowMount();
            if ((int)timeline.Phase != lastPhase || showedHeadsetHint != rig.HasHeadset) ShowCaption();
            caption.alpha = leaving ? 0 : Mathf.Clamp01((captionUntil - Time.unscaledTime) * 2);
            float thrust = timeline.Phase switch
            {
                LaunchPhase.Liftoff => .55f,
                LaunchPhase.BoosterSeparation => .36f,
                LaunchPhase.CoreSeparation => .16f,
                _ => 0f
            };
            engine.volume = Mathf.MoveTowards(engine.volume, leaving ? 0f : thrust * (CrewPerspective ? .48f : 1), Time.unscaledDeltaTime * .55f);
            cabin.volume = Mathf.MoveTowards(cabin.volume, CrewPerspective && !leaving ? .08f : 0f, Time.unscaledDeltaTime * .22f);
        }

        private void ShowCaption()
        {
            lastPhase = (int)timeline.Phase;
            title.text = language.Format("prologue." + timeline.Phase);
            showedHeadsetHint = rig.HasHeadset;
            controls.text = language.Format(rig.HasHeadset ? "prologue.hands" : "prologue.keys");
            captionUntil = Time.unscaledTime + (timeline.Phase == LaunchPhase.Boarding ? 9f : 3.6f);
        }

        private void Finish()
        {
            if (leaving) return;
            leaving = true;
            StartCoroutine(EnterLanding());
        }

        private IEnumerator EnterLanding()
        {
            var load = SceneManager.LoadSceneAsync(ArtemisLanding.SceneName);
            load.allowSceneActivation = false;
            for (float elapsed = 0; elapsed < .55f; elapsed += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, elapsed / .55f));
                yield return null;
            }
            curtain.color = Color.black;
            LocalizationService.CarriedLanguage = language.CurrentLanguage;
            load.allowSceneActivation = true;
        }
    }
}

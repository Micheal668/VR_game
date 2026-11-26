using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    public sealed class CrewHoop : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable ball;
        [SerializeField] private Transform rim;
        [SerializeField] private TMP_Text scoreboard;
        // Rim radius minus ball radius and rim thickness.
        [SerializeField] private float scoringRadius = .18f;
        private Rigidbody body;
        private AudioSource chime;
        private Vector3 home, previous;
        private bool awaitingReturn, canScore;
        private float returnAt;

        public int Score { get; private set; }
        public XRGrabInteractable Ball => ball;
        public Transform Rim => rim;

        public void Configure(XRGrabInteractable gameBall, Transform hoop, TMP_Text display)
        {
            ball = gameBall;
            rim = hoop;
            scoreboard = display;
        }

        private void Awake()
        {
            body = ball.GetComponent<Rigidbody>();
            home = body.position;
            chime = FeedbackSounds.CreateSource(rim, "Basket Chime", 1f);
        }

        private void OnEnable() => ball.selectExited.AddListener(Release);
        private void OnDisable() => ball.selectExited.RemoveListener(Release);

        private void Release(SelectExitEventArgs args)
        {
            if (ball.isSelected) return;
            canScore = !args.isCanceled;
            awaitingReturn = true;
            returnAt = Time.time + 8f;
            previous = body.position;
        }

        private void FixedUpdate()
        {
            if (ball.isSelected) return;
            var point = body.position;
            float distanceHomeSquared = (point - home).sqrMagnitude;
            if (!awaitingReturn)
            {
                // Another prop can knock the ball off its shelf before anyone grabs it.
                if (distanceHomeSquared < .15f * .15f) return;
                awaitingReturn = true;
                returnAt = Time.time + 8f;
            }
            var center = rim.position;
            if (canScore && previous.y > center.y && point.y <= center.y)
            {
                float fraction = (previous.y - center.y) / (previous.y - point.y);
                var crossing = Vector3.Lerp(previous, point, fraction) - center;
                if (crossing.sqrMagnitude <= scoringRadius * scoringRadius)
                {
                    canScore = false;
                    Score++;
                    scoreboard.text = Score.ToString("00");
                    FeedbackSounds.Play(chime, FeedbackSound.Chime, .5f);
                }
            }
            previous = point;

            // Return missed shots, including balls stranded on furniture.
            if (Time.time < returnAt && point.y > -.5f && distanceHomeSquared < 36f) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = home;
            body.rotation = Quaternion.identity;
            ball.SetTargetPose(new Pose(home, Quaternion.identity));
            awaitingReturn = canScore = false;
        }
    }
}

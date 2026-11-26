using UnityEngine;

namespace LunarEscape
{
    // The stride depends on distance along the bridge so direct scrubbing reproduces a walking frame.
    public sealed class LaunchCrewActor : MonoBehaviour
    {
        [SerializeField] private CrewSuitRig rig;
        [SerializeField] private Transform[] legs;
        [SerializeField] private Transform seated;
        [SerializeField] private Renderer seatedBody, seatedHelmet, firstPersonLap;
        private float hipsHeight;
        public bool Boarded { get; private set; }
        public Transform Seated => seated;

        public void Configure(CrewSuitRig model, Transform[] legJoints, Transform seat,
            Renderer body, Renderer helmet, Renderer lap)
        {
            rig = model; legs = legJoints; seated = seat;
            seatedBody = body; seatedHelmet = helmet; firstPersonLap = lap;
        }

        private void Awake() => hipsHeight = rig.Bones[0].localPosition.y;

        public void Place(Vector3 feet, Quaternion facing, float distance, float stride, bool boarded, bool hideHelmet)
        {
            Boarded = boarded;
            transform.SetPositionAndRotation(feet, facing);
            rig.Body.enabled = !boarded;
            seated.gameObject.SetActive(boarded);
            SetFirstPerson(hideHelmet);
            if (boarded) return;
            if (hipsHeight == 0) hipsHeight = rig.Bones[0].localPosition.y;
            float beat = distance * 5.8f;
            float swing = Mathf.Sin(beat) * stride;
            var hips = rig.Bones[0];
            var origin = hips.localPosition; origin.y = hipsHeight + Mathf.Abs(swing) * .033f;
            hips.localPosition = origin;
            hips.localRotation = Quaternion.Euler(0, -swing * 3, swing * 1.5f);
            rig.Bones[1].localRotation = Quaternion.Euler(2 * stride, swing * 4, -swing);
            rig.Bones[3].localRotation = Quaternion.Euler(swing * 20f, 0, 0);
            rig.Bones[6].localRotation = Quaternion.Euler(-swing * 20f, 0, 0);
            for (int i = 0; i < 2; i++)
            {
                float phase = beat + i * Mathf.PI;
                legs[2*i].localRotation = Quaternion.Euler(-Mathf.Sin(phase) * 22f * stride, 0, 0);
                legs[2*i+1].localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Cos(phase)) * 32f * stride, 0, 0);
            }
        }

        public void SetFirstPerson(bool value)
        {
            rig.Helmet.enabled = !Boarded && !value;
            seatedHelmet.enabled = !value;
            if (seatedBody != null) seatedBody.enabled = !value;
            if (firstPersonLap != null) firstPersonLap.enabled = value;
        }
    }
}

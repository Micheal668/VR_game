using UnityEngine;

namespace LunarEscape
{
    // A marker attached to the actual port, visible through the cockpit window
    // and docking camera. It never changes the flight or capture coordinates.
    public sealed class DockingApproachGuide : MonoBehaviour
    {
        [SerializeField] private DockingMission docking;
        [SerializeField] private LineRenderer ring;
        private AudioSource source;
        private bool wasAssisting;
        private DockingState previousState;
        public LineRenderer Ring => ring;
        public void Configure(DockingMission controller, LineRenderer indicator) { docking=controller;ring=indicator; }
        private void Awake() => source=FeedbackSounds.CreateSource(transform,"Docking guidance audio",0);
        private void LateUpdate()
        {
            if(docking==null||ring==null)return;
            bool active=docking.AssistanceActive||docking.State==DockingState.Capturing||docking.State==DockingState.Docked;
            ring.enabled=docking.Active||docking.State==DockingState.Capturing||docking.State==DockingState.Docked;
            var tint=active ? new Color(.2f,1,.5f) : new Color(1,.68f,.12f);
            ring.startColor=ring.endColor=tint;
            ring.widthMultiplier=active ? .045f+.012f*(1+Mathf.Sin(Time.time*4)) : .04f;
            if(docking.AssistanceActive&&!wasAssisting)FeedbackSounds.Play(source,FeedbackSound.Chime,.4f);
            if(docking.State==DockingState.Docked&&previousState!=DockingState.Docked)FeedbackSounds.Play(source,FeedbackSound.Clunk,.6f);
            wasAssisting=docking.AssistanceActive;previousState=docking.State;
        }
    }
}

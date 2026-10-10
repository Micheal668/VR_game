using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(ReturnFallenTool))]
    public sealed class StationPatchModule : MonoBehaviour
    {
        [SerializeField] private StationPatchPuzzle puzzle;
        [SerializeField] private int identity;
        [SerializeField] private Transform cableAnchor;
        [SerializeField] private LineRenderer cable;
        public StationPatchPuzzle Puzzle => puzzle;
        public int Identity => identity;
        public void Configure(StationPatchPuzzle owner, int id, Transform anchor, LineRenderer wire)
        { puzzle = owner; identity = id; cableAnchor = anchor; cable = wire; }
        private void LateUpdate()
        {
            if (cable == null || cableAnchor == null) return;
            cable.positionCount = 17;
            var a = cableAnchor.position; var b = transform.position;
            for (int i = 0; i < 17; i++)
            {
                float t = i / 16f;
                cable.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * .2f));
            }
        }
    }
}

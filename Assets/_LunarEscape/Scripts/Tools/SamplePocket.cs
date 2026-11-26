using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // The exposed rod ends are the original grabbables. Taking a rod uses the same grip as picking it up at the drill.
    public sealed class SamplePocket : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        private CoreSample[] cores;
        private XRSocketInteractor[] slots;
        private CoreSample[] pending;
        public bool canProcess => true;
        public bool HoldingCore
        {
            get
            {
                foreach (var core in cores)
                    if (core.Grab.isSelected && !(core.Grab.firstInteractorSelecting is XRSocketInteractor)) return true;
                return false;
            }
        }

        public static SamplePocket Install(StationPreparation preparation)
        {
            var rack = preparation.Research.Rack;
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return null;
            var existing = FindAnyObjectByType<CargoPackZone>(FindObjectsInactive.Include);
            Transform belt;
            if (existing != null)
            {
                belt = existing.transform;
                existing.GetComponent<CargoPackFollower>().ConfigureWaistPocket();
                // Original frame is 65 cm across. Keep its trigger, artwork and manifest registered to one another.
                belt.localScale = Vector3.one * .48f;
            }
            else
            {
                belt = new GameObject("Core sample belt").transform;
                belt.SetParent(preparation.transform, false);
                var follower = belt.gameObject.AddComponent<CargoPackFollower>();
                follower.Configure(origin);
                follower.ConfigureWaistPocket();
            }
            var root = new GameObject("Core pocket").transform;
            root.SetParent(belt, false);
            root.localScale = Vector3.one / belt.lossyScale.x;
            var pocket = root.gameObject.AddComponent<SamplePocket>();
            pocket.cores = preparation.Research.Cores;
            belt.GetComponent<CargoPackFollower>().SetSamplePocket(pocket);
            pocket.slots = new XRSocketInteractor[pocket.cores.Length];
            pocket.pending = new CoreSample[pocket.slots.Length];
            var material = rack.transform.Find("Slot Collar").GetComponent<Renderer>().sharedMaterial;
            if (existing == null)
            {
                pocket.Panel("Pocket floor", new Vector3(0, -.085f, 0), new Vector3(.29f, .018f, .16f), material);
                foreach (int side in new[] { -1, 1 })
                {
                    pocket.Panel("Side", new Vector3(side * .14f, -.035f, 0), new Vector3(.012f, .11f, .16f), material);
                    pocket.Panel("Lip", new Vector3(0, -.035f, side * .075f), new Vector3(.28f, .11f, .012f), material);
                }
                var label = new GameObject("Core pocket instruction").AddComponent<TextMeshPro>();
                label.transform.SetParent(root, false);
                label.transform.SetLocalPositionAndRotation(new Vector3(0, .035f, -.09f), Quaternion.Euler(65f, 0, 0));
                label.font = rack.GetComponentInChildren<TMP_Text>(true).font;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(.29f, .10f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = .15f;
                label.fontSizeMax = .22f;
                label.gameObject.AddComponent<LocalizedText>().Configure(FindAnyObjectByType<LocalizationService>(), "prep.pocket.cores");
            }
            for (int i = 0; i < pocket.slots.Length; i++)
            {
                var holder = new GameObject("Rod sleeve").transform;
                holder.SetParent(root, false);
                holder.localPosition = new Vector3((i - (pocket.slots.Length - 1) * .5f) * .088f, .07f, 0);
                var trigger = holder.gameObject.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = .04f;
                var socket = holder.gameObject.AddComponent<XRSocketInteractor>();
                socket.attachTransform = holder;
                socket.showInteractableHoverMeshes = false;
                socket.keepSelectedTargetValid = true;
                socket.selectFilters.Add(pocket);
                socket.hoverFilters.Add(pocket);
                pocket.slots[i] = socket;
            }
            foreach (var core in pocket.cores) core.Grab.selectExited.AddListener(pocket.OnReleased);
            return pocket;
        }

        internal int SlotFor(CoreSample core)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsSelecting(core.Grab) || pending[i] == core) return i;
            return -1;
        }

        internal void ClearPending()
        {
            if (pending != null) System.Array.Clear(pending, 0, pending.Length);
        }

        private void OnDisable() => ClearPending();

        internal void RestoreSlot(int index, CoreSample core)
        {
            if (index < 0 || index >= slots.Length) return;
            var socket = slots[index];
            pending[index] = core;
            var manager = socket.interactionManager != null ? socket.interactionManager : FindAnyObjectByType<XRInteractionManager>();
            manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)core.Grab);
            pending[index] = null;
        }

        private void Panel(string name, Vector3 position, Vector3 size, Material material)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            var collider = panel.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            panel.transform.SetParent(transform, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = size;
            panel.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!isActiveAndEnabled || args.isCanceled || args.interactorObject is XRSocketInteractor) return;
            var core = args.interactableObject.transform.GetComponent<CoreSample>();
            if (core == null || core.Grab.isSelected) return;
            var local = transform.InverseTransformPoint(core.transform.position);
            if (Mathf.Abs(local.x) > .22f || Mathf.Abs(local.z) > .17f || local.y < -.12f || local.y > .24f) return;
            int nearest = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].hasSelection || pending[i] != null) continue;
                float distance = (slots[i].transform.position - core.transform.position).sqrMagnitude;
                if (distance < best) { best = distance; nearest = i; }
            }
            if (nearest < 0) return;
            pending[nearest] = core;
            HandHaptics.Pulse(HandHaptics.FromInteractor(args.interactorObject), .35f, .05f);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var core = pending[i];
                if (core == null) continue;
                var socket = slots[i];
                if (core.isActiveAndEnabled && !core.Grab.isSelected && !socket.hasSelection)
                    socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)core.Grab);
                pending[i] = null;
            }
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable item)
        {
            int index = System.Array.IndexOf(slots, interactor as XRSocketInteractor);
            return index >= 0 && (slots[index].IsSelecting(item) || pending[index] != null && (IXRSelectInteractable)pending[index].Grab == item);
        }
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable item) => false;

        private void OnDestroy()
        {
            if (cores == null) return;
            foreach (var core in cores) if (core != null) core.Grab.selectExited.RemoveListener(OnReleased);
        }
    }
}

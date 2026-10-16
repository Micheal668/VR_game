using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 物品仍贴手握持。瞄准空接口后扣扳机，明确地把物品放入；错误类型和遮挡不会穿透。
    [RequireComponent(typeof(XRBaseInputInteractor))]
    public sealed class RemoteSocketPlacement : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] private LineRenderer guide;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private float maxDistance = 4.5f;
        private XRBaseInputInteractor hand;
        private XRSocketInteractor[] sockets;
        private LocalizationService language;
        private Camera eye;
        private bool pressed, placing;
        private XRGrabInteractable transferring;
        private XRSocketInteractor destination;
        private bool originalThrow;
        private bool hasSurface;
        private RaycastHit surface;
        private readonly List<IXRSelectFilter> filters = new();
        public XRSocketInteractor Target { get; private set; }
        public bool IsPlacing => placing;
        public bool HasSurfaceTarget => hasSurface;
        public bool canProcess => isActiveAndEnabled;
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable item) => !placing || interactor == (IXRSelectInteractor)destination;
        public void Configure(LineRenderer line, TMP_Text text) { guide = line; hint = text; }
        private void Awake() { hand = GetComponent<XRBaseInputInteractor>(); language = FindAnyObjectByType<LocalizationService>(); eye = Camera.main; }
        private void Start() => sockets = FindObjectsByType<XRSocketInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        private Transform Aim => hand is NearFarInteractor near && near.farInteractionCaster != null ? near.farInteractionCaster.castOrigin
            : hand is XRRayInteractor ray && ray.rayOriginTransform != null ? ray.rayOriginTransform : hand.transform;
        private static bool Portable(XRGrabInteractable item) => item != null && (item.GetComponent<CargoItem>() != null
            || item.GetComponent<CoreSample>() != null || item.GetComponent<AirlockFuse>() != null || item.GetComponent<StationPatchModule>() != null);
        public bool Accepts(XRSocketInteractor socket, XRGrabInteractable item)
        {
            if (!Portable(item) || socket == null || !socket.isActiveAndEnabled || !socket.socketActive || socket.hasSelection
                || (socket.interactionLayers.value & item.interactionLayers.value) == 0) return false;
            socket.selectFilters.GetAll(filters);
            foreach (var filter in filters) if (filter.canProcess && !filter.Process(socket, item)) return false;
            item.selectFilters.GetAll(filters);
            foreach (var filter in filters) if (filter.canProcess && !filter.Process(socket, item)) return false;
            return true;
        }
        public bool HasClearPath(XRSocketInteractor socket, XRGrabInteractable item)
        {
            Vector3 start = Aim.position, end = socket.attachTransform != null ? socket.attachTransform.position : socket.transform.position;
            float distance = Vector3.Distance(start, end);
            if (distance > maxDistance || distance < .08f) return false;
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized, Mathf.Max(0, distance - .10f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(item.transform) || hit.transform.IsChildOf(hand.transform.root)
                    || hit.transform.IsChildOf(socket.transform)) continue;
                return false;
            }
            return true;
        }
        public bool HasPlacementTarget(XRGrabInteractable item)
        {
            Target = null; hasSurface = false;
            if (placing || !Portable(item) || sockets == null) return false;
            {
                float best = float.PositiveInfinity;
                foreach (var socket in sockets)
                {
                    if (!Accepts(socket, item)) continue;
                    Vector3 point = socket.attachTransform != null ? socket.attachTransform.position : socket.transform.position;
                    Vector3 delta = point - Aim.position;
                    float ahead = Vector3.Dot(Aim.forward, delta);
                    float error = Vector3.Cross(Aim.forward, delta).magnitude;
                    if (ahead <= .08f || ahead > maxDistance || error > Mathf.Min(.22f, .09f + ahead * .035f) || !HasClearPath(socket, item)) continue;
                    float score = error + ahead * .002f;
                    if (score < best) { best = score; Target = socket; }
                }
            }
            hasSurface = Target == null && TrySurface(item, out surface);
            return Target != null || hasSurface;
        }
        private bool TrySurface(XRGrabInteractable item, out RaycastHit result)
        {
            result = default;
            var hits = Physics.RaycastAll(Aim.position, Aim.forward, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(item.transform) || hit.transform.IsChildOf(hand.transform.root)) continue;
                if (hit.distance < .25f || hit.normal.y < .70f || hit.rigidbody != null) return false;
                result = hit; return true;
            }
            return false;
        }
        private void Update()
        {
            var item = hand.firstInteractableSelected as XRGrabInteractable;
            bool trigger = hand.activateInput.ReadValue() > .55f;
            bool available = HasPlacementTarget(item);
            if (trigger && !pressed && available)
            {
                if (Target != null) TryPlace(Target);
                else StartCoroutine(PlaceOnSurface(item, surface));
            }
            pressed = trigger;
            if (guide != null)
            {
                guide.enabled = available && !placing;
                if (guide.enabled) { guide.positionCount = 2; guide.SetPosition(0, Aim.position); guide.SetPosition(1, Target != null ? Target.attachTransform.position : surface.point); }
            }
            if (hint != null)
            {
                hint.gameObject.SetActive(available && !placing);
                if (hint.gameObject.activeSelf)
                {
                    hint.text = language != null ? language.Format(Target != null ? "prep.remote.place" : "prep.remote.surface") : "TRIGGER: PLACE";
                    hint.transform.position = (Target != null ? Target.attachTransform.position : surface.point) + Vector3.up * .24f;
                    if (eye != null) hint.transform.rotation = Quaternion.LookRotation(hint.transform.position - eye.transform.position, eye.transform.up);
                }
            }
        }
        public bool TryPlace(XRSocketInteractor socket)
        {
            var item = hand.firstInteractableSelected as XRGrabInteractable;
            if (placing || !Accepts(socket, item) || !HasClearPath(socket, item)) return false;
            StartCoroutine(Place(socket, item)); return true;
        }
        private IEnumerator Place(XRSocketInteractor socket, XRGrabInteractable item)
        {
            placing = true;
            transferring = item; destination = socket; originalThrow = item.throwOnDetach;
            item.throwOnDetach = false; item.selectFilters.Add(this);
            // 取消握持不会被腰包误认为主动收纳；等 XRI 完成本帧释放后再对接。
            hand.interactionManager.CancelInteractableSelection((IXRSelectInteractable)item);
            yield return null;
            if (item != null && Accepts(socket, item))
            {
                var attach = item.GetAttachTransform(socket);
                Quaternion rotation = socket.attachTransform.rotation * Quaternion.Inverse(Quaternion.Inverse(item.transform.rotation) * attach.rotation);
                Vector3 offset = Quaternion.Inverse(item.transform.rotation) * (attach.position - item.transform.position);
                Vector3 position = socket.attachTransform.position - rotation * offset;
                item.transform.SetPositionAndRotation(position, rotation);
                var body = item.GetComponent<Rigidbody>(); body.position = position; body.rotation = rotation;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                Physics.SyncTransforms();
                socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)item);
                HandHaptics.Pulse(HandHaptics.FromInteractor(hand), .45f, .08f);
            }
            if (item != null) { item.throwOnDetach = originalThrow; item.selectFilters.Remove(this); }
            transferring = null; destination = null;
            placing = false;
        }
        private IEnumerator PlaceOnSurface(XRGrabInteractable item, RaycastHit hit)
        {
            placing = true; transferring = item; destination = null; originalThrow = item.throwOnDetach;
            item.throwOnDetach = false; item.selectFilters.Add(this);
            hand.interactionManager.CancelInteractableSelection((IXRSelectInteractable)item);
            yield return null;
            if (item != null && hit.collider != null && hit.collider.enabled)
            {
                item.transform.rotation = Quaternion.Euler(0, Aim.eulerAngles.y + 180, 0);
                var renderers = item.GetComponentsInChildren<MeshRenderer>().Where(r => r.GetComponent<TMP_Text>() == null).ToArray();
                var bounds = renderers.Length > 0 ? renderers[0].bounds : item.GetComponent<Collider>().bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var position = item.transform.position + hit.point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + Vector3.up * .015f;
                item.transform.position = position;
                var body = item.GetComponent<Rigidbody>(); body.position = position; body.rotation = item.transform.rotation;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                Physics.SyncTransforms(); HandHaptics.Pulse(HandHaptics.FromInteractor(hand), .35f, .06f);
            }
            if (item != null) { item.throwOnDetach = originalThrow; item.selectFilters.Remove(this); }
            transferring = null; placing = false;
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (guide != null) guide.enabled = false;
            if (hint != null) hint.gameObject.SetActive(false);
            if (transferring != null) { transferring.throwOnDetach = originalThrow; transferring.selectFilters.Remove(this); }
            transferring = null; destination = null; placing = false;
            Target = null;
        }
    }
}

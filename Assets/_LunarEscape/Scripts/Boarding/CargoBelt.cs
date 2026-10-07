using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 撤离腰带：三个磁吸插槽对应最多三类物资。拿着物资靠近腰前，目标插槽亮起、手上轻震一下；
    // 松手后物资“吸”进插槽并显示缩小模型和数量。抓住插槽可取回一件。
    // 收纳判定仍是原有腰前收纳区（CargoPackZone）；腰带只负责看得见的插槽、吸附动画和取回。
    public sealed class CargoBelt : MonoBehaviour
    {
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private CargoPackZone zone;
        [SerializeField] private BeltSlot[] slots = new BeltSlot[0];
        [SerializeField] private GameObject visualRoot;
        [SerializeField, Min(0.02f)] private float replicaSize = 0.11f;
        private readonly Dictionary<CargoItem, CargoState> states = new();
        private readonly HashSet<CargoItem> nearItems = new();
        private readonly List<GameObject> flying = new();
        private AudioSource source;

        public BeltSlot[] Slots => slots;

        public void Configure(CargoInventory cargo, CargoPackZone packZone, BeltSlot[] beltSlots, GameObject visuals)
        { inventory = cargo; zone = packZone; slots = beltSlots; visualRoot = visuals; }

        private void Awake() => source = FeedbackSounds.CreateSource(transform, "Belt Audio", 0.3f);

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.Changed += Refresh;
            foreach (var item in inventory.Items) if (item != null) states[item] = item.State;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Refresh;
            states.Clear();
            nearItems.Clear();
        }

        private void Update()
        {
            if (inventory == null) return;
            // 只有撤离阶段允许取放，腰带也只在此时出现，平时不在视野下方晃动。
            bool visible = inventory.Mission != null && inventory.Mission.Phase == StationMissionPhase.Evacuation;
            if (visualRoot != null && visualRoot.activeSelf != visible) visualRoot.SetActive(visible);
            if (!visible) { nearItems.Clear(); return; }

            BeltSlot target = null;
            foreach (var item in inventory.Items)
            {
                if (item == null || item.State != CargoState.Held) { nearItems.Remove(item); continue; }
                bool near = zone != null && (zone.Contains(item) || zone.IsNearOpening(item));
                if (near && nearItems.Add(item))
                {
                    // 靠近的一瞬间“吸”一下：轻震和短促的声音，提示现在松手就会收进腰带。
                    HandHaptics.Pulse(HandHaptics.FromGrab(item.Grab), 0.3f, 0.04f);
                    FeedbackSounds.Play(source, FeedbackSound.Click, 0.35f);
                }
                else if (!near) nearItems.Remove(item);
                if (near) target = SlotFor(item.Kind) ?? FreeSlot();
            }
            foreach (var slot in slots)
                slot.ShowRing(slot == target ? 2 : slot.Kind.HasValue ? 1 : 0);
        }

        private void Refresh()
        {
            // 新收纳的物资从松手处飞进插槽；数量归零的插槽释放给其他类别。
            var packed = new Dictionary<CargoKind, int>();
            foreach (var item in inventory.Items)
            {
                if (item == null) continue;
                if (item.State == CargoState.Packed) packed[item.Kind] = packed.TryGetValue(item.Kind, out int n) ? n + 1 : 1;
            }
            foreach (var slot in slots)
                if (slot.Kind.HasValue && !packed.ContainsKey(slot.Kind.Value)) SetSlot(slot, null);
            foreach (var pair in packed)
                if (SlotFor(pair.Key) == null && FreeSlot() is BeltSlot free) SetSlot(free, pair.Key);
            foreach (var slot in slots)
                slot.ShowCount(slot.Kind.HasValue && packed.TryGetValue(slot.Kind.Value, out int n) ? n : 0);

            foreach (var item in inventory.Items)
            {
                if (item == null) continue;
                states.TryGetValue(item, out var previous);
                if (item.State == CargoState.Packed && previous != CargoState.Packed && SlotFor(item.Kind) is BeltSlot slot && isActiveAndEnabled)
                    StartCoroutine(FlyIn(item, slot));
                states[item] = item.State;
            }
        }

        private BeltSlot SlotFor(CargoKind kind)
        {
            foreach (var slot in slots) if (slot.Kind == kind) return slot;
            return null;
        }

        private BeltSlot FreeSlot()
        {
            foreach (var slot in slots) if (!slot.Kind.HasValue) return slot;
            return null;
        }

        private void SetSlot(BeltSlot slot, CargoKind? kind)
        {
            slot.Kind = kind;
            for (int i = slot.ReplicaRoot.childCount - 1; i >= 0; i--) Destroy(slot.ReplicaRoot.GetChild(i).gameObject);
            if (!kind.HasValue) return;
            foreach (var item in inventory.Items)
                if (item != null && item.Kind == kind.Value) { BuildReplica(item, slot.ReplicaRoot, replicaSize); break; }
        }

        private IEnumerator FlyIn(CargoItem item, BeltSlot slot)
        {
            var ghost = new GameObject("Belt Snap Ghost").transform;
            flying.Add(ghost.gameObject);
            BuildReplica(item, ghost, replicaSize * 1.6f);
            Vector3 from = item.transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                if (slot == null || ghost == null) yield break;
                float eased = 1f - (1f - t) * (1f - t);
                ghost.SetPositionAndRotation(Vector3.Lerp(from, slot.ReplicaRoot.position, eased), slot.ReplicaRoot.rotation);
                ghost.localScale = Vector3.one * Mathf.Lerp(1f, 1f / 1.6f, eased);
                yield return null;
            }
            FeedbackSounds.Play(source, FeedbackSound.Pack, 0.4f);
            flying.Remove(ghost.gameObject);
            Destroy(ghost.gameObject);
        }

        internal void RequestTakeOut(BeltSlot slot, IXRSelectInteractor interactor)
        {
            if (isActiveAndEnabled) StartCoroutine(TakeOut(slot, interactor));
        }

        // 下一帧再转交选择：在选择事件回调中直接改选会打乱交互管理器本帧的处理顺序。
        private IEnumerator TakeOut(BeltSlot slot, IXRSelectInteractor interactor)
        {
            yield return null;
            var manager = slot.interactionManager;
            if (manager == null) yield break;
            if (interactor.IsSelecting(slot)) manager.SelectExit(interactor, slot);
            if (!slot.Kind.HasValue) yield break;
            CargoItem chosen = null;
            foreach (var item in inventory.Items)
                if (item != null && item.Kind == slot.Kind.Value && item.State == CargoState.Packed) { chosen = item; break; }
            if (chosen == null || !inventory.TryUnpack(chosen, slot.ReplicaRoot.position, slot.ReplicaRoot.rotation)) yield break;
            states[chosen] = chosen.State;
            manager.SelectEnter(interactor, (IXRSelectInteractable)chosen.Grab);
            HandHaptics.Pulse(HandHaptics.FromInteractor(interactor), 0.35f, 0.05f);
        }

        // 只复制网格和材质，不复制抓取、刚体或碰撞，缩小到插槽尺寸并居中。
        private static void BuildReplica(CargoItem item, Transform parent, float size)
        {
            var root = new GameObject("Replica " + item.Kind).transform;
            root.SetParent(parent, false);
            var filters = item.GetComponentsInChildren<MeshFilter>(true);
            var bounds = new Bounds();
            bool any = false;
            var toItem = item.transform.worldToLocalMatrix;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer source) || !source.enabled) continue;
                var copy = new GameObject(filter.name);
                copy.transform.SetParent(root, false);
                var matrix = toItem * filter.transform.localToWorldMatrix;
                copy.transform.SetLocalPositionAndRotation(matrix.GetColumn(3), matrix.rotation);
                copy.transform.localScale = matrix.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                var meshBounds = TransformBounds(matrix, filter.sharedMesh.bounds);
                if (any) bounds.Encapsulate(meshBounds); else { bounds = meshBounds; any = true; }
            }
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = largest > 0.0001f ? size / largest : 1f;
            root.localScale = Vector3.one * scale;
            root.localPosition = -bounds.center * scale;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds local)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return result;
        }

        private void OnDestroy()
        {
            foreach (var ghost in flying) if (ghost != null) Destroy(ghost);
        }
    }
}

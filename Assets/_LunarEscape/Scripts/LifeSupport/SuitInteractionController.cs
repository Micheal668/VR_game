using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 真实 XR 激活事件和屏幕按钮复用同一份物资，物品绝不能同时供给宇航服与船舱。
    public sealed class SuitInteractionController : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        private readonly HashSet<SuitRackInteractor> held = new();
        private readonly List<XRGrabInteractable> supplies = new();
        private bool listening;
        public LifeSupportMission Life => life;
        public void Configure(LifeSupportMission source)
        { Unsubscribe(); life = source; if (isActiveAndEnabled) Subscribe(); }
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Subscribe()
        {
            if (listening || life == null) return;
            listening = true; life.ConfigureDonContact(HasContact);
            foreach (var item in life.Inventory.Items)
                if (item != null && (item.Kind == CargoKind.Oxygen || item.Kind == CargoKind.Battery))
                { supplies.Add(item.Grab); item.Grab.activated.AddListener(ActivateSupply); }
        }
        private void Unsubscribe()
        {
            if (!listening) return;
            foreach (var grab in supplies) if (grab != null) grab.activated.RemoveListener(ActivateSupply);
            supplies.Clear(); held.Clear(); life.SetDonHeld(false); life.ConfigureDonContact(null); listening = false;
        }
        private bool HasContact()
        {
            held.RemoveWhere(source => source == null || !source.isActiveAndEnabled);
            return held.Count > 0;
        }
        public void SetHeld(SuitRackInteractor source, bool value)
        {
            if (source == null || life == null) return;
            if (value && source.isActiveAndEnabled) held.Add(source); else held.Remove(source);
            life.SetDonHeld(HasContact());
        }
        private void ActivateSupply(ActivateEventArgs args)
        {
            if (args.interactableObject is XRGrabInteractable grab && grab.TryGetComponent<CargoItem>(out var item)) life.TryUseSupply(item);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        private static void BuildSupplies()
        {
            var inventory = life.Inventory;
            foreach (var oldLabel in Object.FindObjectsByType<CargoItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldLabel.gameObject.SetActive(false);
            var shelfRoot = Find("Evacuation Supply Rack");
            foreach (Transform child in shelfRoot)
                if (child.name.StartsWith("Cargo Shelf Label")) child.gameObject.SetActive(false);
            foreach (Transform child in shelfRoot.Cast<Transform>().Where(t => t.name.StartsWith("ExpansionCargo ")).ToArray()) Object.DestroyImmediate(child.gameObject);
            var originals = shelfRoot.GetComponentsInChildren<CargoItem>(true).Where(i => !i.name.StartsWith("ExpansionCargo ")).ToArray();
            var candidates = new List<CargoItem>(); var groups = new List<StationRandomSupplies.Group>();
            CargoKind[] types = { CargoKind.Oxygen, CargoKind.Battery, CargoKind.RepairKit, CargoKind.MedicalKit, CargoKind.DataCore, CargoKind.LunarSample, CargoKind.LunarSample };
            string[] names = { "Oxygen", "Battery", "Repair Kit", "Medical Kit", "Research Drive", "Meteorite", "Moon Soil" };
            for (int group = 0; group < types.Length; group++)
            {
                var available = originals.Where(i => i.Kind == types[group]).ToArray();
                var items = new List<CargoItem>();
                for (int i = 0; i < 3; i++)
                {
                    CargoItem item;
                    if (group != 6 && i < available.Length) item = available[i];
                    else
                    {
                        var clone = Object.Instantiate(available[0].gameObject, shelfRoot);
                        clone.name = "ExpansionCargo " + names[group] + " " + i; item = clone.GetComponent<CargoItem>();
                    }
                    Vector3 position;
                    if (group < 4)
                    {
                        float angle = (group * 90 + i * 25 + 10) * Mathf.Deg2Rad;
                        position = new Vector3(Mathf.Cos(angle) * 1.2f, 1.02f, Mathf.Sin(angle) * 1.2f);
                    }
                    else position = new Vector3(-8.65f + i * .73f, 1.005f, -.98f - (group - 4) * .53f);
                    item.gameObject.SetActive(true);
                    item.transform.SetPositionAndRotation(position, Quaternion.identity);
                    if (group == 6) BuildSoilVisual(item);
                    else if (group == 5) BuildMeteoriteVisual(item);
                    var bounds = new Bounds(position, Vector3.zero);
                    bool first = true;
                    foreach (var renderer in item.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r is MeshRenderer))
                    { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                    item.transform.position += Vector3.up * (position.y - bounds.min.y + .008f);
                    // The authored renderer and the physical bounds share the same stable floor plane.
                    item.Configure(inventory, types[group]);
                    item.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    var recovery = item.GetComponent<ReturnFallenTool>() ?? item.gameObject.AddComponent<ReturnFallenTool>();
                    recovery.ConfigureFloorRecovery(-.08f);
                    candidates.Add(item); items.Add(item); EditorUtility.SetDirty(item);
                }
                groups.Add(new StationRandomSupplies.Group { name = names[group], items = items.ToArray() });
            }
            var serialized = new SerializedObject(inventory);
            var drop = (Transform)serialized.FindProperty("dropPoint").objectReferenceValue;
            inventory.Configure(session.Mission, inventory.Config, candidates.ToArray(), drop);
            var random = session.GetComponent<StationRandomSupplies>() ?? session.gameObject.AddComponent<StationRandomSupplies>();
            random.Configure(life, groups.ToArray());
            EditorUtility.SetDirty(inventory); EditorUtility.SetDirty(random);
        }

        private static void BuildSoilVisual(CargoItem item)
        {
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var previous = item.transform.Find("Research Sample Appearance"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var visual = Frame(item.transform, "Research Sample Appearance", Vector3.zero);
            visual.localScale = new Vector3(1 / item.transform.lossyScale.x, 1 / item.transform.lossyScale.y, 1 / item.transform.lossyScale.z);
            Primitive(visual, "Sealed soil canister", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.15f, .10f, .15f), "EX_White");
            Primitive(visual, "Canister blue lid", PrimitiveType.Cylinder, new Vector3(0, .106f, 0), new Vector3(.17f, .013f, .17f), "EX_Blue");
            Primitive(visual, "Soil ID band", PrimitiveType.Cube, new Vector3(0, 0, -.077f), new Vector3(.10f, .07f, .005f), "EX_Copper");
            foreach (var collider in item.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            var box = item.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(.17f / item.transform.lossyScale.x, .235f / item.transform.lossyScale.y, .17f / item.transform.lossyScale.z);
            box.center = new Vector3(0, .015f / item.transform.lossyScale.y, 0);
            item.Grab.colliders.Clear(); item.Grab.colliders.Add(box);
        }
        private static void BuildMeteoriteVisual(CargoItem item)
        {
            // Existing imported meteorite / regolith sample remains the visible scientific specimen.
            var old = item.transform.Find("Research Sample Appearance"); if (old != null) Object.DestroyImmediate(old.gameObject);
        }
    }
}

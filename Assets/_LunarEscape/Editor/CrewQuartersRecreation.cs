using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static LunarEscape.Editor.LunarToolBuilder;

namespace LunarEscape.Editor
{
    public static class CrewQuartersRecreation
    {
        private const float RimRadius = .30f;
        private const float BallRadius = .105f;

        [MenuItem("Lunar Escape/Update Crew Hoop and Supply Shelves (Both Days)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string path in new[] { BuildLifeSupportScene.ScenePath, BuildDayOneScene.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var preparation = Object.FindAnyObjectByType<StationPreparation>();
                Apply(preparation.transform.parent);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CREW_QUARTERS_UPDATED");
        }

        public static void Apply(Transform ground)
        {
            var station = ground.Find("Expanded Station Story");
            foreach (Transform part in station.GetComponentsInChildren<Transform>(true))
            {
                if (!part.name.StartsWith("SupplyTable__") && part.name != "Solid Round Supply Table" && part.name != "Table Pedestal") continue;
                part.gameObject.SetActive(false);
                if (PrefabUtility.IsPartOfPrefabInstance(part))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            var shelf = ground.Find("First Aid and Repair Shelves");
            if (shelf == null)
            {
                shelf = Frame(ground, "First Aid and Repair Shelves", new Vector3(1.30f, 0, -3.53f));
                foreach (float y in new[] { .80f, 1.38f })
                {
                    Prim(shelf, "Shelf", PrimitiveType.Cube, new Vector3(0, y - .025f, 0), new Vector3(2.03f, .05f, .64f), "EX_White", collider: true);
                    foreach (float x in new[] { -.78f, .78f })
                        Prim(shelf, "Wall Bracket", PrimitiveType.Cube, new Vector3(x, y - .10f, -.20f), new Vector3(.065f, .16f, .25f), "EX_Blue");
                }
            }
            foreach (var kind in new[] { CargoKind.RepairKit, CargoKind.MedicalKit })
            {
                int slot = 0;
                foreach (var item in ground.GetComponentsInChildren<PreparedCargo>(true).Where(c => c.Item.Kind == kind).OrderBy(c => c.Id))
                {
                    var meshes = item.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.GetComponent<TMP_Text>() == null).ToArray();
                    var b = meshes[0].bounds;
                    foreach (var mesh in meshes.Skip(1)) b.Encapsulate(mesh.bounds);
                    var target = shelf.TransformPoint(new Vector3((slot++ - 1) * .65f, kind == CargoKind.RepairKit ? .81f : 1.39f, 0));
                    item.transform.position += target - new Vector3(b.center.x, b.min.y, b.center.z);
                    if (PrefabUtility.IsPartOfPrefabInstance(item))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                }
            }
            if (ground.Find("Quarters Basketball") == null) BuildHoop(ground);
        }

        private static void BuildHoop(Transform ground)
        {
            var court = Frame(ground, "Quarters Basketball", new Vector3(-7.80f, 0, 7.42f));
            Prim(court, "Backboard", PrimitiveType.Cube, new Vector3(0, 2.16f, -.03f), new Vector3(1.02f, .70f, .065f), "EX_Blue", collider: true);
            // The target square and score face the bedroom aisle.
            foreach (float x in new[] { -.23f, .23f })
                Prim(court, "Target Side", PrimitiveType.Cube, new Vector3(x, 2.14f, -.068f), new Vector3(.018f, .25f, .008f), "Panel White");
            foreach (float y in new[] { 2.02f, 2.26f })
                Prim(court, "Target Edge", PrimitiveType.Cube, new Vector3(0, y, -.068f), new Vector3(.47f, .018f, .008f), "Panel White");
            var score = Text(court, "Basket Score", new Vector3(0, 2.385f, -.070f), Quaternion.identity, new Vector2(.50f, .16f), .94f, Color.white);
            score.text = "00";
            var rim = Frame(court, "Hoop", new Vector3(0, 1.99f, -.435f));
            Prim(court, "Rim Bracket", PrimitiveType.Cube, new Vector3(0, 1.99f, -.102f), new Vector3(.10f, .038f, .15f), "Safety Amber", collider: true);
            const int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                var a = Radial(i, segments) * RimRadius;
                var b = Radial(i + 1, segments) * RimRadius;
                Prim(rim, "Rim Segment", PrimitiveType.Cube, (a + b) * .5f, new Vector3(.024f, .024f, Vector3.Distance(a, b) + .005f), "Safety Amber", Quaternion.LookRotation(b - a), true);
            }
            for (int i = 0; i < 10; i++)
            {
                var top = Radial(i, 10) * RimRadius;
                foreach (float offset in new[] { -.5f, .5f })
                    Stroke(rim, "Net", new[] { top, Radial(i + offset, 10) * .21f + Vector3.down * .24f }, .008f, "Panel White");
            }

            var shelf = new Vector3(0, 1.02f, -.22f);
            Prim(court, "Ball Shelf", PrimitiveType.Cube, shelf, new Vector3(.44f, .05f, .46f), "EX_White", collider: true);
            var sphere = Frame(court, "Crew Basketball", shelf + Vector3.up * (.026f + BallRadius));
            Prim(sphere, "Orange Shell", PrimitiveType.Sphere, Vector3.zero, Vector3.one * (2 * BallRadius), "Safety Amber");
            for (int i = 0; i < 3; i++)
            {
                var rotate = i == 0 ? Quaternion.identity : Quaternion.Euler(90, i == 1 ? 0 : 90, 0);
                var points = Enumerable.Range(0, 40).Select(j => rotate * Radial(j, 40) * (BallRadius + .0005f)).ToArray();
                Stroke(sphere, "Ball Seam", points, .0035f, "Station Graphite", true);
            }
            var collider = sphere.gameObject.AddComponent<SphereCollider>();
            collider.radius = BallRadius;
            const string path = "Assets/_LunarEscape/Materials/Crew Basketball.physicMaterial";
            var rubber = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (rubber == null)
            {
                rubber = new PhysicsMaterial("Crew Basketball") { bounciness = .52f, bounceCombine = PhysicsMaterialCombine.Maximum };
                AssetDatabase.CreateAsset(rubber, path);
            }
            collider.sharedMaterial = rubber;
            var body = sphere.gameObject.AddComponent<Rigidbody>();
            body.mass = .32f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = sphere.gameObject.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 1f;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.colliders.Clear();
            grab.colliders.Add(collider);
            court.gameObject.AddComponent<CrewHoop>().Configure(grab, rim, score);
        }

        private static Vector3 Radial(float index, int count)
        {
            float angle = index * Mathf.PI * 2 / count;
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        }

        private static void Stroke(Transform parent, string name, Vector3[] points, float width, string material, bool loop = false)
        {
            var line = Frame(parent, name, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = Mat(material);
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = width;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
    }
}

using System;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LunarEscape.Editor.LunarToolBuilder;

namespace LunarEscape.Editor
{
    public static class ArtemisLanderBuilder
    {
        private static Vector3 ReadStationExteriorScale(GameObject exterior)
        {
            var parts = exterior.GetComponentsInChildren<MeshFilter>(true).ToDictionary(p => p.name);
            Vector3? fittedScale = null;
            foreach (string path in new[] { BuildDayOneScene.ScenePath, BuildLifeSupportScene.ScenePath })
            {
                // Inspect the authored scenes before Unity combines static base geometry for rendering.
                var station = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var original = station.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                        .Single(t => t.name == BuildApolloExterior.VisualName && t.parent != null && t.parent.name == "Apollo Lander Exterior");
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original) != BuildApolloExterior.PrefabPath ||
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(exterior) != BuildApolloExterior.PrefabPath)
                        throw new InvalidOperationException("Landing and station must share the Apollo exterior prefab: " + path);
                    if (fittedScale.HasValue && (fittedScale.Value - original.lossyScale).sqrMagnitude > .000001f)
                        throw new InvalidOperationException("The two stations use different lander dimensions: " + path);
                    fittedScale = original.lossyScale;
                    var source = original.GetComponentsInChildren<MeshFilter>(true);
                    if (source.Length != parts.Count) throw new InvalidOperationException("Exterior part count differs: " + path);
                    foreach (var part in source)
                    {
                        if (!parts.TryGetValue(part.name, out var copy) || copy.sharedMesh != part.sharedMesh ||
                            !copy.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(part.GetComponent<MeshRenderer>().sharedMaterials))
                            throw new InvalidOperationException("Exterior mesh or material mismatch in " + path + " / " + part.name);
                    }
                    Debug.Log("Verified Apollo prefab, scale " + fittedScale + ", " + parts.Count + " source meshes and all material slots in " + path);
                }
                finally { EditorSceneManager.ClosePreviewScene(station); }
            }
            return fittedScale.Value;
        }

        public static ArtemisLanderVisual Build(Transform movingCraft, Transform orbitalModule)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BuildApolloExterior.PrefabPath)
                ?? throw new InvalidOperationException("The station's Apollo Exterior prefab could not be loaded.");
            var exterior = (GameObject)PrefabUtility.InstantiatePrefab(source, movingCraft);
            exterior.name = BuildApolloExterior.VisualName;
            // The station model faces -Z. Flight controls and the crew look along +Z.
            exterior.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 180, 0));
            // The parked base instances are wider than the imported prefab. Fit all flight landmarks after scaling.
            exterior.transform.localScale = ReadStationExteriorScale(exterior);
            PrefabUtility.RecordPrefabInstancePropertyModifications(exterior);
            PrefabUtility.RecordPrefabInstancePropertyModifications(exterior.transform);

            var geometry = exterior.transform.Find("Geometry");
            var renderers = geometry.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var part in renderers)
            {
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            var ascent = renderers.Where(r => InstallEarthReturnScene.IsAscentExterior(r.name)).ToArray();
            var descent = renderers.Except(ascent).ToArray();
            var references = Frame(movingCraft, "Apollo flight references", Vector3.zero);
            var footAnchors = geometry.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Anchor_Foot", StringComparison.Ordinal))
                .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            if (footAnchors.Length != 4 || ascent.Length != 3 || descent.Length != 6)
                throw new InvalidOperationException("Unexpected stage or landing-gear hierarchy in the station's Apollo exterior.");
            var feet = new Transform[footAnchors.Length];
            var rimMin = Vector3.one * float.PositiveInfinity;
            var rimMax = Vector3.one * float.NegativeInfinity;
            for (int i = 0; i < feet.Length; i++)
                feet[i] = MeasureGear(footAnchors[i], geometry, references, ref rimMin, ref rimMax);
            if (float.IsInfinity(rimMin.y))
                throw new InvalidOperationException("The central descent engine rim was not found.");

            // In Geometry space the centroid of the commander's triangular window
            // is (.94,4.71,-2.3568), from Tools/build_apollo_blender.py. Place the
            // eye 46 cm behind that pane along the model's authored forward axis.
            var eyePoint = geometry.TransformPoint(new Vector3(.94f, 4.71f, -1.8968f));
            var eye = Frame(references, "Commander eye", movingCraft.InverseTransformPoint(eyePoint));
            var rim = (rimMin + rimMax) * .5f;
            rim.y = rimMin.y;
            var nozzle = Frame(references, "Descent engine bell exit", rim,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            var port = Frame(references, "Ascent docking contact", Vector3.up * EarthReturnPresenter.AscentPortHeight);
            var orion = ArtemisRocketBuilder.CreateOrbitingOrion(orbitalModule);
            var face = orion.Find("Docking contact face");
            var orbiterPort = Frame(orion, "Orion docking contact", orion.InverseTransformPoint(face.TransformPoint(Vector3.up)),
                Quaternion.Inverse(orion.rotation) * face.rotation);

            var visual = movingCraft.gameObject.AddComponent<ArtemisLanderVisual>();
            visual.Configure(exterior, eye, nozzle, port, orbiterPort, feet, footAnchors, ascent, descent);
            return visual;
        }

        private static Transform MeasureGear(Transform anchor, Transform geometry, Transform references,
            ref Vector3 rimMin, ref Vector3 rimMax)
        {
            // The original FBX splits the central bell across its four landing-gear meshes.
            // Inspect their editor mesh data without changing the station's import settings.
            var part = geometry.Find(anchor.name.Substring("Anchor_Foot".Length)).GetComponent<MeshFilter>();
            var toCraft = references.worldToLocalMatrix * part.transform.localToWorldMatrix;
            var pad = references.InverseTransformPoint(anchor.position);
            float horizontalScale = geometry.TransformVector(Vector3.right).magnitude;
            var sole = new Vector3(pad.x, float.PositiveInfinity, pad.z);
            using var data = MeshUtility.AcquireReadOnlyMeshData(part.sharedMesh);
            using var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
            data[0].GetVertices(vertices);
            foreach (var vertex in vertices)
            {
                var p = toCraft.MultiplyPoint3x4(vertex);
                float dx = p.x - pad.x, dz = p.z - pad.z;
                if (dx * dx + dz * dz < .49f * horizontalScale * horizontalScale && p.y < sole.y) sole = p;
                if (p.x * p.x + p.z * p.z < horizontalScale * horizontalScale && p.y < .025f)
                {
                    rimMin = Vector3.Min(rimMin, p);
                    rimMax = Vector3.Max(rimMax, p);
                }
            }
            if (float.IsInfinity(sole.y)) throw new InvalidOperationException("No sole under " + anchor.name);
            return Frame(references, part.name + " sole", sole);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    public static class BuildArtemisLaunchPad
    {
        private const string Art = "Assets/_LunarEscape/Art/ArtemisPad/";
        [Serializable] private sealed class Part { public string name; public float[] offset; public string[] surfaces; }
        [Serializable] private sealed class Manifest { public Part[] parts; }

        public static Transform Build(Transform parent)
        {
            var pad = new GameObject("Kennedy Space Center - Launch Complex 39B").transform;
            pad.SetParent(parent, false);
            var manifest = JsonUtility.FromJson<Manifest>(AssetDatabase.LoadAssetAtPath<TextAsset>(Art + "Pad meshes.json").text);
            var materials = new Dictionary<string, Material>();
            foreach (var part in manifest.parts)
            {
                var go = new GameObject(part.name);
                go.transform.SetParent(pad, false);
                go.transform.localPosition = new Vector3(part.offset[0], part.offset[1], part.offset[2]);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + part.name + ".asset");
                if (mesh == null || mesh.subMeshCount != part.surfaces.Length) throw new InvalidDataException("Missing or incomplete pad mesh " + part.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                var used = new Material[part.surfaces.Length];
                for (int i = 0; i < used.Length; i++)
                {
                    var name = part.surfaces[i];
                    if (!materials.TryGetValue(name, out used[i])) materials.Add(name, used[i] = Surface(name));
                }
                renderer.sharedMaterials = used;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (part.name == "Cape Canaveral coastline") renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            Mark(pad, "Crew Bridge Start", new Vector3(14.6f, 82, 0));
            Mark(pad, "Crew Bridge End", new Vector3(2.35f, 82, 0));
            return pad;
        }

        private static void Mark(Transform pad, string label, Vector3 position)
        {
            var marker = new GameObject(label).transform;
            marker.SetParent(pad, false);
            marker.localPosition = position;
            marker.localRotation = Quaternion.LookRotation(Vector3.left);
        }

        private static Material Surface(string name)
        {
            var (color, metallic, smooth, texture) = name switch
            {
                "Launcher" => ("#F7F7F5", .26f, .34f, "Mobile Launcher finish.jpg"),
                "Railings" => ("#FFFFFF", .16f, .30f, "Mobile Launcher railings.png"),
                "Coast" => ("#FFFFFF", 0f, .09f, "Kennedy coastline.jpg"),
                "Atlantic" => ("#506B78", .05f, .23f, ""),
                "Concrete" => ("#A6A398", .02f, .27f, "Cement grain.jpg"),
                "Firebrick" => ("#806950", .02f, .18f, "Cement grain.jpg"),
                "Scorched" => ("#56554E", .13f, .26f, "Cement grain.jpg"),
                "Turf" => ("#788052", 0f, .16f, "Scrub grass.jpg"),
                "Ballast" => ("#C6BCAE", .015f, .14f, "Road aggregate.jpg"),
                "Asphalt" => ("#59605F", 0f, .13f, "Road aggregate.jpg"),
                "Road paint" => ("#D6D2BD", .015f, .23f, ""),
                "Joint" => ("#73726B", .015f, .2f, ""),
                "Steel" => ("#667174", .51f, .4f, ""),
                "Pale steel" => ("#BCC1BB", .35f, .42f, ""),
                "White enamel" => ("#E2E5DC", .22f, .41f, ""),
                "Safety rail" => ("#DBA52D", .19f, .42f, ""),
                "Wire" => ("#707778", .32f, .3f, ""),
                "Tank enamel" => ("#DCE3DB", .2f, .4f, ""),
                "Light siding" => ("#CFD1C9", .26f, .35f, ""),
                "Door" => ("#566975", .34f, .31f, ""),
                "Siding seam" => ("#A4AAA4", .25f, .37f, ""),
                "Roof" => ("#9BA3A4", .32f, .32f, ""),
                "Tread" => ("#DAE0DA", .37f, .42f, "Bridge tread.png"),
                "Lamp" => ("#FFEDD2", .09f, .55f, ""),
                "Beacon" => ("#F37850", .1f, .44f, ""),
                _ => throw new InvalidDataException("Unknown pad finish " + name)
            };
            if (!AssetDatabase.IsValidFolder(Art + "Materials")) AssetDatabase.CreateFolder(Art.TrimEnd('/'), "Materials");
            string path = Art + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            ColorUtility.TryParseHtmlString(color, out var baseColor);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smooth);
            if (texture.Length > 0)
            {
                var map = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + texture);
                if (map == null) throw new FileNotFoundException(Art + texture);
                material.SetTexture("_BaseMap", map);
                if (name == "Tread") material.SetTextureScale("_BaseMap", Vector2.one * 28);
            }
            if (name == "Railings")
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", .37f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (name == "Lamp" || name == "Beacon")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", baseColor * .9f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

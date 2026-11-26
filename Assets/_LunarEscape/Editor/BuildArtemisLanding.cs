using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LunarEscape.Editor
{
    public static class BuildArtemisLanding
    {
        private const string Root = "Assets/_LunarEscape";
        public const string ScenePath = Root + "/Scenes/" + ArtemisLanding.SceneName + ".unity";

        internal static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Lunar sunlight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(27f, 30f, 0f);
            sun.color = new Color(1f, .965f, .91f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.29f, .30f, .33f);
            var surface = new GameObject("Lunar base approach");
            var marker = ArtemisLandingSiteBuilder.Build(surface.transform);
            string skyPath = Root + "/Art/ArtemisLanding/Landing starfield.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Earth Sky.mat"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_HideEarth", 1f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            var orbit = new GameObject("Lunar orbit");
            var orbiter = LunarToolBuilder.Frame(orbit.transform, "Orbital module", Vector3.zero);
            var moon = Globe(orbit.transform, "Moon", new Vector3(45, -710, 860), 1740);
            var earth = Globe(null, "Earth", new Vector3(1250, 2470, 11500), 485);
            earth.localRotation = Quaternion.Euler(8, 56, -23);
            var craft = new GameObject("Crewed lunar lander").transform;
            var model = ArtemisLanderBuilder.Build(craft, orbiter);
            var cabin = ArtemisLandingCabinBuilder.Build(craft, model.PilotEye);
            var plume = LunarToolBuilder.Frame(craft, "Descent engine exhaust", craft.InverseTransformPoint(model.Nozzle.position));
            var flame = LunarToolBuilder.Particles(plume, "Exhaust", 180, new Vector2(.12f, .28f), new Vector2(.16f, .39f),
                new Color(.72f, .82f, 1f, .55f), ParticleSystemSimulationSpace.Local);
            flame.transform.rotation = Quaternion.LookRotation(model.Nozzle.forward, craft.forward);
            var main = flame.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var shape = flame.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .28f;
            shape.angle = 7f;
            var emission = flame.emission;
            emission.rateOverTime = 160f;
            plume.gameObject.SetActive(false);
            // Site surfaces use Default. Exclude the ship and its instruments from ground-contact queries.
            foreach (var col in craft.GetComponentsInChildren<Collider>(true)) col.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var col in surface.GetComponentsInChildren<Collider>(true)) col.gameObject.layer = 0;
            surface.SetActive(false);

            var owner = new GameObject("Lunar landing mission");
            var flight = owner.AddComponent<ArtemisLanding>();
            flight.Configure(craft, model, orbiter, moon, earth, marker, plume, orbit, surface, 1);
            var language = owner.AddComponent<LocalizationService>();
            language.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Localization/repair-texts.json"), GameLanguage.Russian);
            var rig = BuildArtemisPrologue.ViewRig();
            var display = BuildArtemisPrologue.Display(rig.Eye);
            var panel = Block(display, "Flight instruments", new Vector2(1490, 196), new Vector2(0, -520), new Color(.018f, .033f, .052f, .92f));
            var title = Readout(panel, "Guidance", 39, new Vector2(-61, 54), new Color(.81f, .96f, .96f));
            var telemetry = Readout(panel, "Navigation", 32, new Vector2(-61, 0), Color.white);
            var controls = Readout(panel, "Pilot controls", 30, new Vector2(-61, -56), new Color(.73f, .82f, .89f));
            var plot = Block(panel, "Landing target bearing", new Vector2(130, 130), new Vector2(648, 0), new Color(.10f,.16f,.19f,1));
            var line = new Color(.36f,.54f,.56f,1);
            Block(plot, "Forward and aft", new Vector2(2, 118), Vector2.zero, line);
            Block(plot, "Port and starboard", new Vector2(118, 2), Vector2.zero, line);
            Block(plot, "Landing tolerance", new Vector2(36, 36), Vector2.zero, new Color(.40f,.68f,.64f,.3f));
            var blip = Block(plot, "Target", new Vector2(12, 12), Vector2.zero, new Color(.98f,.77f,.30f,1));
            blip.localRotation = Quaternion.Euler(0, 0, 45);
            var curtain = Block(display, "Transition", display.sizeDelta, Vector2.zero, Color.black).GetComponent<Image>();
            display.gameObject.AddComponent<HelmetHudOverlay>().Configure(
                Shader.Find("LunarEscape/Visor UI Overlay"), Shader.Find("TextMeshPro/Mobile/Distance Field Overlay"));
            owner.AddComponent<ArtemisLandingPilot>().Configure(flight, rig, cabin, language, title, telemetry, controls, plot, blip, curtain);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new System.IO.IOException("Unable to save " + ScenePath);
        }

        private static Transform Globe(Transform parent, string name, Vector3 position, float diameter)
        {
            var body = LunarToolBuilder.Frame(parent, name, position);
            body.localScale = Vector3.one * diameter;
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Art/EarthReturn/Earth globe.asset");
            var surface = body.gameObject.AddComponent<MeshRenderer>();
            surface.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/ArtemisFilm/" + name + " from space.mat");
            surface.shadowCastingMode = ShadowCastingMode.Off;
            surface.receiveShadows = false;
            return body;
        }

        private static RectTransform Block(Transform parent, string name, Vector2 size, Vector2 at, Color color)
        {
            var rect = BuildArtemisPrologue.Rect(parent, name, size, at);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text Readout(Transform parent, string name, int size, Vector2 at, Color color)
        {
            var label = BuildArtemisPrologue.Caption(parent, name, size, at.y, color);
            label.rectTransform.anchoredPosition = at;
            label.rectTransform.sizeDelta = new Vector2(1300, 54);
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = size;
            return label;
        }
    }
}

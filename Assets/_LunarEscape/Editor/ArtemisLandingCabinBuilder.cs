using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LunarEscape.Editor
{
    public static class ArtemisLandingCabinBuilder
    {
        private const string FontPath = "Assets/_LunarEscape/Fonts/Station Multilingual SDF.asset";

        public static GameObject Build(Transform craft, Transform pilotEye)
        {
            var root = new GameObject("Station landing cockpit").transform;
            root.SetParent(craft, false);
            // The Day Two Flight Seat floor anchor is (-.9,0,-.6). Its seat lock preserves HMD height.
            root.SetPositionAndRotation(pilotEye.position - pilotEye.rotation * new Vector3(-.9f, 1.65f, -.6f), pilotEye.rotation);
            root.gameObject.SetActive(false);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportLifeSupportArt.CabinPrefab)
                ?? throw new InvalidOperationException("The station flight cabin prefab is missing.");
            var shell = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            var cockpitLayer = LayerMask.NameToLayer("Cockpit Interior");

            // These are the exact physical display rectangles in 11_LunarStation_LifeSupport.
            var left = Screen(root, "Cockpit Operations Screen", -1.38f, 1060);
            var center = Screen(root, "Cockpit Navigation Screen", -.05f, 1430);
            var right = Screen(root, "Cockpit Resources Screen", 1.23f, 840);
            var amber = new Color(1, .79f, .39f);
            var aqua = new Color(.43f, .91f, .89f);
            var engineTitle = Label(left, "Engine heading", 90, 291, new Vector2(995, 133), aqua);
            var throttle = Label(left, "Engine thrust", 156, 20, new Vector2(985, 238), Color.white);
            var bar = BuildArtemisPrologue.Rect(left, "Throttle scale", new Vector2(790, 45), new Vector2(0, -207));
            bar.gameObject.AddComponent<Image>().color = new Color(.12f, .23f, .26f);
            var fill = BuildArtemisPrologue.Rect(bar, "Thrust", bar.sizeDelta, Vector2.zero);
            fill.pivot = new Vector2(0, .5f);
            fill.anchoredPosition = new Vector2(-395, 0);
            fill.gameObject.AddComponent<Image>().color = amber;

            var cameraLabel = Label(center, "Landing optics heading", 79, 357, new Vector2(1360, 116), aqua);
            var video = BuildArtemisPrologue.Rect(center, "Landing camera image", new Vector2(1340, 585), new Vector2(0, -31));
            var image = video.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var sight = new Color(.84f, .98f, .91f, .9f);
            BuildArtemisPrologue.Rect(video,"Reticle horizontal",new Vector2(52,3),Vector2.zero).gameObject.AddComponent<Image>().color = sight;
            BuildArtemisPrologue.Rect(video,"Reticle vertical",new Vector2(3,52),Vector2.zero).gameObject.AddComponent<Image>().color = sight;
            var optics = new GameObject("Landing optics").AddComponent<Camera>();
            optics.transform.SetParent(root, false);
            optics.transform.localPosition = new Vector3(0, .9f, 1.8f);
            optics.transform.localRotation = Quaternion.Euler(40, 0, 0);
            optics.fieldOfView = 63;
            optics.nearClipPlane = .05f;
            optics.farClipPlane = 32000;
            optics.depth = -10;
            optics.cullingMask = ~LayerMask.GetMask("UI", "Cockpit Interior", "Ignore Raycast", "Player Head");
            optics.stereoTargetEye = StereoTargetEyeMask.None;
            optics.GetUniversalAdditionalCameraData().allowXRRendering = false;
            optics.enabled = false;
            center.gameObject.AddComponent<DockingCameraDisplay>().Configure(optics, image);

            var navTitle = Label(right, "Telemetry heading", 76, 311, new Vector2(794, 126), aqua);
            var telemetry = Label(right, "Altitude and velocity", 66, -75, new Vector2(790, 602), Color.white);
            var lamp = new GameObject("Console illumination").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(-.45f, 2.53f, -.2f);
            lamp.type = LightType.Point;
            lamp.color = new Color(.84f, .92f, 1);
            lamp.range = 5.4f;
            lamp.intensity = 1.9f;
            lamp.shadows = LightShadows.None;
            root.gameObject.AddComponent<ArtemisLandingCabinInstruments>().Configure(shell.transform, pilotEye, optics,
                engineTitle, throttle, cameraLabel, navTitle, telemetry, fill);
            foreach (var piece in root.GetComponentsInChildren<Transform>(true)) piece.gameObject.layer = cockpitLayer;
            return root.gameObject;
        }

        private static RectTransform Screen(Transform parent, string name, float x, float width)
        {
            var canvas = new GameObject(name, typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var panel = (RectTransform)canvas.transform;
            panel.SetParent(parent, false);
            panel.localPosition = new Vector3(x, 1.57f, 1.375f);
            panel.localScale = Vector3.one * .001f;
            panel.sizeDelta = new Vector2(width, 920);
            panel.gameObject.layer = LayerMask.NameToLayer("UI");
            var backing = panel.gameObject.AddComponent<Image>();
            backing.color = new Color(.016f, .057f, .065f);
            backing.raycastTarget = false;
            return panel;
        }

        private static TMP_Text Label(Transform parent, string name, int fontSize, float y, Vector2 size, Color color)
        {
            var rect = BuildArtemisPrologue.Rect(parent, name, size, new Vector2(0, y));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * .65f;
            text.fontSizeMax = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}

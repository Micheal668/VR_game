using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    public static class BuildEarthSky
    {
        private const string Root = "Assets/_LunarEscape";
        public static void ApplyToCurrentScene()
        {
            const string photoPath = Root + "/Art/Orbit/Earthrise.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(photoPath);
            if (importer == null) throw new InvalidOperationException("缺少用户提供的地球照片。");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            const string starsPath = Root + "/Art/Orbit/Starfield.png";
            var starsImporter = (TextureImporter)AssetImporter.GetAtPath(starsPath);
            if (starsImporter == null) throw new InvalidOperationException("缺少用户提供的星空照片。");
            starsImporter.textureType = TextureImporterType.Default;
            starsImporter.sRGBTexture = true;
            starsImporter.mipmapEnabled = true;
            starsImporter.wrapMode = TextureWrapMode.Clamp;
            starsImporter.filterMode = FilterMode.Trilinear;
            starsImporter.textureCompression = TextureImporterCompression.Uncompressed;
            starsImporter.maxTextureSize = 2048;
            starsImporter.npotScale = TextureImporterNPOTScale.None;
            starsImporter.SaveAndReimport();
            const string path = Root + "/Materials/Earth Sky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("LunarEscape/Earth Sky"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_EarthPhoto", AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath));
            material.SetTexture("_StarPhoto", AssetDatabase.LoadAssetAtPath<Texture2D>(starsPath));
            EditorUtility.SetDirty(material);
            var scene = UnityEngine.Object.FindAnyObjectByType<FlightScenePresenter>();
            if (scene == null) throw new InvalidOperationException("当前场景缺少飞行环境。");
            var animation = scene.FlightWorld.GetComponentInChildren<LunarWindowAnimation>(true);
            var earth = UnityEngine.Object.FindAnyObjectByType<EarthSkyView>();
            if (earth == null) earth = new GameObject("Earth Sky").AddComponent<EarthSkyView>();
            earth.Configure(material, scene, animation.OutsideWorld);
            RenderSettings.skybox = material;
            EditorUtility.SetDirty(earth);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Lunar Escape/Add Earth to Docking Scene")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("EARTH_SKY_INSTALLED");
        }
    }
}

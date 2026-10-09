using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace LunarEscape.Editor
{
    // 生命保障主场景（11）的语音提示与舱门泄压：
    //   · 地面指挥（ЦУП）在关键节点用无线电口头说明下一步，代替屏幕上的长段说明；指挥官对每个维修动作吐槽；
    //   · 打开舱门的瞬间爆发式泄压：轰鸣、尘埃与冷凝雾冲向门口、松散物体被吸走、手柄震动、玩家被轻拽。
    // 语音素材由 Tools/generate_voice_lines.ps1 生成到 Audio/Voice（文件名前缀 cup_ = 地面指挥，cmd_ = 指挥官）。
    // 须在 Install Station Start 之后执行；可重复执行，旧的语音与泄压节点会先删除再重建。
    public static class InstallVoiceAndDecompression
    {
        private const string Root = "Assets/_LunarEscape";
        private const string VoiceFolder = Root + "/Audio/Voice";
        private const string VoiceName = "Mission Voice";
        private const string DecompressionName = "Airlock Decompression";
        private const string ParticleTexturePath = Root + "/Art/Effects/Soft Particle.png";
        private const string DustMaterialPath = Root + "/Materials/Decompression Dust.mat";

        [MenuItem("Lunar Escape/Install Voice Hints and Decompression (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("VOICE_AND_DECOMPRESSION_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var life = session.GetComponent<LifeSupportMission>();
            if (life == null || life.AirlockRepair == null)
                throw new InvalidOperationException("请先执行 Install Life Support and Cockpit 与 Install Airlock Repair。");
            var breaker = UnityEngine.Object.FindAnyObjectByType<HabitatBreaker>(FindObjectsInactive.Include);
            if (breaker == null) throw new InvalidOperationException("请先执行 Install Station Start（照明总闸）。");
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var hands = Find<HapticImpulsePlayer>();

            BuildVoice(session, life, breaker);
            BuildDecompression(session, life, ground, hands);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // —— 语音 ——
        private static void BuildVoice(StationMissionSession session, LifeSupportMission life, HabitatBreaker breaker)
        {
            var old = session.transform.Find(VoiceName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var lines = AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == VoiceFolder)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => new VoiceLine
                {
                    id = Path.GetFileNameWithoutExtension(p),
                    speaker = Path.GetFileName(p).StartsWith("cup_") ? VoiceSpeaker.MissionControl : VoiceSpeaker.Commander,
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p)
                }).ToArray();
            if (lines.Length == 0) throw new InvalidOperationException("缺少语音：请先运行 Tools/generate_voice_lines.ps1。");

            var root = new GameObject(VoiceName);
            root.transform.SetParent(session.transform, false);
            var crew = session.GetComponent<CrewMission>();
            var voice = root.AddComponent<MissionVoice>();
            voice.Configure(lines, session.Player.Camera.transform, crew != null ? crew.Commander : null);
            var director = root.AddComponent<MissionVoiceDirector>();
            director.Configure(voice, session.Mission, life, breaker, life.AirlockRepair, life.Inventory,
                Find<RepairTool>(), session.GetComponent<AscentMission>());
            EditorUtility.SetDirty(voice);
            EditorUtility.SetDirty(director);
            Debug.Log("VOICE_LINES " + lines.Length);
        }

        // —— 泄压 ——
        private static void BuildDecompression(StationMissionSession session, LifeSupportMission life, Transform ground, HapticImpulsePlayer[] hands)
        {
            var old = ground.Find(DecompressionName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var door = session.GetComponent<MissionEnvironment>().Door;
            if (door == null) throw new InvalidOperationException("场景里没有撤离舱门。");
            var renderers = door.GetComponentsInChildren<Renderer>(true);
            var doorBounds = renderers[0].bounds;
            foreach (var r in renderers) doorBounds.Encapsulate(r.bounds);
            var habitat = HabitatBounds(life.HabitatVolume);
            // 舱外方向：从舱室中心指向门洞，取水平主轴。
            Vector3 toDoor = doorBounds.center - habitat.center;
            Vector3 outward = Mathf.Abs(toDoor.x) >= Mathf.Abs(toDoor.z) ? new Vector3(Mathf.Sign(toDoor.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toDoor.z));

            var root = new GameObject(DecompressionName).transform;
            root.SetParent(ground, false);
            root.position = new Vector3(habitat.center.x, 1.3f, habitat.center.z);
            var outlet = new GameObject("Decompression Outlet").transform;
            outlet.SetParent(root, false);
            outlet.position = new Vector3(doorBounds.center.x, Mathf.Clamp(doorBounds.center.y, 0.9f, 1.4f), doorBounds.center.z);

            var material = DustMaterial();
            var interior = new Vector3(habitat.size.x - 0.5f, 2.3f, habitat.size.z - 0.5f);
            var dust = Particles(root, "Decompression Dust", material, interior, 900,
                new ParticleSystem.MinMaxCurve(1.6f, 2.8f), new ParticleSystem.MinMaxCurve(0.015f, 0.055f), new Color(0.84f, 0.85f, 0.87f, 0.85f),
                new[] { new ParticleSystem.Burst(0f, 450), new ParticleSystem.Burst(0.25f, 220), new ParticleSystem.Burst(0.6f, 120) });
            // 骤降的气压让空气中的水汽凝结成一阵薄雾。
            var fog = Particles(root, "Decompression Fog", material, interior, 120,
                new ParticleSystem.MinMaxCurve(1.2f, 2.2f), new ParticleSystem.MinMaxCurve(0.5f, 1.1f), new Color(0.86f, 0.89f, 0.93f, 0.16f),
                new[] { new ParticleSystem.Burst(0f, 60), new ParticleSystem.Burst(0.2f, 40) });
            var growth = fog.sizeOverLifetime; growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.7f));
            var startRotation = fog.main; startRotation.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var effect = root.gameObject.AddComponent<AirlockDecompression>();
            effect.Configure(life, outlet, outward, new[] { dust, fog }, hands);
            EditorUtility.SetDirty(effect);
            Debug.Log($"DECOMPRESSION outlet={outlet.position} outward={outward} habitat={habitat}");
        }

        private static Bounds HabitatBounds(BoxCollider volume)
        {
            var t = volume.transform;
            var size = Vector3.Scale(volume.size, t.lossyScale);
            return new Bounds(t.TransformPoint(volume.center), new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        private static ParticleSystem Particles(Transform parent, string name, Material material, Vector3 box, int max,
            ParticleSystem.MinMaxCurve lifetime, ParticleSystem.MinMaxCurve size, Color color, ParticleSystem.Burst[] bursts)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 3f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = lifetime; main.startSpeed = 0f; main.startSize = size; main.startColor = color;
            main.gravityModifier = 0f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = max;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(bursts);
            var shape = system.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            return system;
        }

        // 柔边圆点贴图 + URP 粒子无光照透明材质（尘埃不受舱内灯光影响，暗处也能看到气流）。
        private static Material DustMaterial()
        {
            if (!File.Exists(ParticleTexturePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ParticleTexturePath));
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                        float a = Mathf.SmoothStep(1f, 0f, d);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                File.WriteAllBytes(ParticleTexturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(ParticleTexturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(ParticleTexturePath);
                importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, DustMaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T[] Find<T>() where T : Component
            => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene.IsValid()).ToArray();
    }
}

using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LunarEscape.Editor
{
    // 编辑器工具：沿用上一课的房间和 XR 玩家，生成可直接编辑的维修练习场景。
    public static class BuildRepairScene
    {
        public const string ScenePath = "Assets/_LunarEscape/Scenes/05_LunarStation_Repair.unity";
        private const string Root = "Assets/_LunarEscape";
        private const string FontPath = Root + "/Fonts/Station Multilingual SDF.asset";
        private static TMP_FontAsset font;
        private static LocalizationService localization;
        private static LocalizedPanelBuilder ui;

        [MenuItem("Lunar Escape/Open Repair Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("维修场景已存在，拒绝覆盖手动编辑内容。");
            AssetDatabase.Refresh();
            font = PrepareFont();
            var scene = EditorSceneManager.OpenScene(BuildFoundation.ScenePath);
            // 保存为新文件，上一课仍可单独打开和对照学习。
            EditorSceneManager.SaveScene(scene, ScenePath);
            foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>())
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Systems Display"));
            var tool = UnityEngine.Object.FindAnyObjectByType<ToolPractice>().gameObject;
            PrefabUtility.UnpackPrefabInstance(tool, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(tool.GetComponent<ToolPractice>());
            tool.transform.position = new Vector3(-0.45f, 1.09f, 0.45f);
            var tip = new GameObject("Repair Tip").transform;
            tip.SetParent(tool.transform, false);
            tip.localPosition = new Vector3(0, 0, 0.25f);
            var repairTool = tool.AddComponent<RepairTool>();
            repairTool.Configure(tip, "maintenance");
            // 新预制体保存可复用的工具能力，不包含某个具体维修站的引用。
            PrefabUtility.SaveAsPrefabAssetAndConnect(tool, Root + "/Prefabs/RepairTool.prefab", InteractionMode.AutomatedAction);

            localization = new GameObject("Localization").AddComponent<LocalizationService>();
            localization.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Localization/repair-texts.json"));
            ui = new LocalizedPanelBuilder(font, localization);
            var station = new GameObject("Oxygen Repair Station");
            var task = station.AddComponent<TimedRepairTask>();
            task.Configure(2f);
            Solid("Oxygen Unit", station.transform, new Vector3(0.58f, 1.16f, 0.76f),
                new Vector3(0.5f, 0.32f, 0.34f), "Station Graphite");
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "Repair Point";
            target.transform.SetParent(station.transform);
            target.transform.position = new Vector3(0.58f, 1.22f, 0.49f);
            target.transform.localScale = Vector3.one * 0.18f;
            target.GetComponent<Renderer>().sharedMaterial = Material("Safety Amber");
            // 指示球不阻挡工具；实际外壳保留碰撞，维修范围略大于指示球。
            UnityEngine.Object.DestroyImmediate(target.GetComponent<Collider>());
            var contact = station.AddComponent<RepairContact>();
            contact.Configure(target.transform, new[] { repairTool }, "maintenance", 0.14f);
            ui.WorldLabel("Repair Point Label", "repair.target", new Vector3(0.58f, 1.06f, 0.47f), new Vector2(0.75f, 0.16f), 0.42f, Quaternion.identity);
            ui.WorldLabel("Repair Tool Label", "repair.tool", new Vector3(-0.45f, 0.94f, 0.08f), new Vector2(0.75f, 0.16f), 0.42f, Quaternion.identity);

            var canvas = ui.Panel("Repair Panel", new Vector3(0, 2.16f, 1.3f), new Vector2(1200, 720), 0.002f, Quaternion.identity);
            ui.Label(canvas, "Station Title", "station.title", new Vector2(0, 280), new Vector2(1080, 90), 28, new Color(0.4f, 0.95f, 0.87f));
            ui.Label(canvas, "Repair Title", "repair.title", new Vector2(0, 195), new Vector2(1080, 72), 42, Color.white);
            ui.Label(canvas, "Repair Instructions", "repair.instruction", new Vector2(0, 110), new Vector2(1080, 88), 30, new Color(0.8f, 0.88f, 0.92f));
            var status = ui.Label(canvas, "Repair Status", "repair.ready", new Vector2(0, 5), new Vector2(1080, 112), 34, Color.white);
            var track = ui.Rectangle(canvas, "Progress Track", new Vector2(0, -100), new Vector2(1040, 18), new Color(0.16f, 0.23f, 0.29f));
            var fill = ui.Rectangle(track.transform, "Progress Fill", Vector2.zero, new Vector2(1040, 18), Color.white);
            // Filled 模式需要 Sprite，否则 Unity 会按普通满宽矩形绘制。
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (fill.sprite == null) throw new InvalidOperationException("缺少进度条图像。");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            var progress = ui.Label(canvas, "Repair Percentage", "repair.progress", new Vector2(0, -136), new Vector2(1080, 36), 24, Color.white);
            ui.Label(canvas, "Language Title", "language.title", new Vector2(-460, -213), new Vector2(190, 58), 25, new Color(0.7f, 0.82f, 0.86f));
            var names = new[] { "Chinese", "English", "Russian" };
            var keys = new[] { "language.zh", "language.en", "language.ru" };
            for (int i = 0; i < 3; i++)
            {
                var button = ui.Button(canvas, "Language " + names[i], keys[i], new Vector2(-200 + i * 250, -213), new Vector2(226, 64));
                UnityEventTools.AddIntPersistentListener(button.onClick, localization.SetLanguageIndex, i);
            }
            var reset = ui.Button(canvas, "Reset Repair", "repair.reset", new Vector2(0, -303), new Vector2(400, 58));
            UnityEventTools.AddPersistentListener(reset.onClick, task.ResetTask);
            station.AddComponent<RepairPresenter>().Configure(task, status, progress, fill, target.GetComponent<Renderer>());

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("XR UI Event System");
                events.AddComponent<EventSystem>();
                // 官方模块同时支持鼠标与 VR 射线，不需要为两套输入复制按钮逻辑。
                events.AddComponent<XRUIInputModule>();
            }
            PlayerSettings.runInBackground = true;
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(BuildFoundation.ScenePath, true)
            };
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_REPAIR_CREATED " + ScenePath);
        }

        [MenuItem("Lunar Escape/Refresh Multilingual Font")]
        public static void RefreshFont()
        {
            PrepareFont();
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset PrepareFont()
        {
            FontEngine.InitializeFontEngine();
            string sourcePath = Path.Combine(Application.dataPath, "_LunarEscape/Fonts/NotoSansCJKsc-Regular.otf");
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (asset == null)
            {
                asset = TMP_FontAsset.CreateFontAsset(sourcePath, 0, 64, 7, GlyphRenderMode.SDFAA, 2048, 2048);
                if (asset == null) throw new InvalidOperationException("字体无法导入。");
                asset.name = "Station Multilingual SDF";
                AssetDatabase.CreateAsset(asset, FontPath);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Localization/repair-texts.json");
            var table = JsonUtility.FromJson<LocalizationService.Table>(catalog.text);
            string text = string.Concat(table.entries.Select(e => e.zh + e.en + e.ru));
            text += string.Concat(Enumerable.Range(32, 95).Select(n => (char)n)) + "ёЁ";
            text = new string(text.Where(c => !char.IsControl(c)).Distinct().ToArray());
            string pending = new string(text.Where(c => !asset.HasCharacter(c)).ToArray());
            // 仅在编辑器补字，游戏使用静态图集，跨电脑不依赖系统字体或文件路径。
            var settings = new SerializedObject(asset);
            try
            {
                if (pending.Length > 0)
                {
                    asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    settings.Update();
                    settings.FindProperty("m_SourceFontFilePath").stringValue = sourcePath;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    if (!asset.TryAddCharacters(pending, out string missing))
                        throw new InvalidOperationException("字体缺少字符：" + missing);
                }
            }
            finally
            {
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                settings.Update();
                settings.FindProperty("m_SourceFontFilePath").stringValue = string.Empty;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var atlas in asset.atlasTextures)
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");

        private static GameObject Solid(string name, Transform parent, Vector3 position, Vector3 scale, string material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Material(material);
            return obj;
        }
    }
}

using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LunarEscape.Editor
{
    // 可重复安装，范围只限本组件生成的结算窗口和两份专用材质。
    public static class BuildFailureSummary
    {
        private const string AssetsRoot = "Assets/_LunarEscape";
        private const string PanelName = "Failure Summary";

        public static void Install(StationMissionSession session)
        {
            if (session == null || session.Mission == null || session.Player == null
                || session.Player.Camera == null)
                throw new InvalidOperationException("安装结算窗口需要已配置的任务会话与玩家相机。");

            var localization = UnityEngine.Object.FindAnyObjectByType<LocalizationService>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetsRoot + "/Fonts/Station Multilingual SDF.asset");
            if (localization == null || font == null)
                throw new InvalidOperationException("安装结算窗口需要本地化服务和三语字体。");

            var presenter = session.GetComponent<MissionFailurePresenter>();
            if (presenter != null && presenter.Root != null)
                UnityEngine.Object.DestroyImmediate(presenter.Root);
            // 也能恢复上次安装中断后尚未连接到 Presenter 的专属窗口。
            var oldPanel = session.transform.Find(PanelName);
            if (oldPanel != null) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
            if (presenter == null) presenter = session.gameObject.AddComponent<MissionFailurePresenter>();

            var ui = new LocalizedPanelBuilder(font, localization);
            Camera camera = session.Player.Camera;
            var panel = ui.Panel(PanelName, camera.transform.position + camera.transform.forward * 1.8f,
                new Vector2(1360, 820), 0.0012f, camera.transform.rotation);
            panel.SetParent(session.transform, true);
            var canvas = panel.GetComponent<Canvas>();
            canvas.worldCamera = camera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32700;

            // 深色背景为有限大小的面板，不在眼前铺满无限平面。
            var surround = ui.Rectangle(panel, "Failure Dim Surround", Vector2.zero,
                new Vector2(2100, 1400), new Color(0.004f, 0.008f, 0.015f, 0.94f));
            surround.transform.SetAsFirstSibling();
            surround.raycastTarget = true;
            panel.Find("Panel Background").GetComponent<Image>().raycastTarget = true;
            ui.Rectangle(panel, "Failure Accent", new Vector2(0, 390),
                new Vector2(1360, 12), new Color(0.95f, 0.28f, 0.2f));
            ui.Label(panel, "Failure Heading", "failure.title", new Vector2(0, 280),
                new Vector2(1240, 100), 64, new Color(1f, 0.38f, 0.29f));
            ui.Label(panel, "Failure Death", "failure.death", new Vector2(0, 170),
                new Vector2(1240, 70), 40, Color.white);
            ui.Label(panel, "Failure Reason", "failure.reason", new Vector2(0, 58),
                new Vector2(1200, 126), 32, new Color(0.87f, 0.91f, 0.95f));
            var summary = ui.Label(panel, "Failure Repair Summary", "failure.summary.unrepaired",
                new Vector2(0, -60), new Vector2(1200, 70), 29, new Color(0.67f, 0.76f, 0.84f));
            ui.Label(panel, "Failure Restart Instruction", "failure.restart.instruction",
                new Vector2(0, -154), new Vector2(1200, 70), 28, new Color(0.67f, 0.76f, 0.84f));
            var restart = ui.Button(panel, "Restart After Failure", "failure.restart",
                new Vector2(0, -290), new Vector2(600, 104), 36);
            restart.GetComponent<Image>().color = new Color(0.13f, 0.39f, 0.46f);
            UnityEventTools.AddPersistentListener(restart.onClick, session.RetryMission);

            // 靠墙时也必须能看到并点到唯一按钮；普通场景面板仍保留原来的深度规则。
            panel.GetComponent<GraphicRaycaster>().blockingObjects = GraphicRaycaster.BlockingObjects.None;
            var trackedRaycaster = panel.GetComponent<TrackedDeviceGraphicRaycaster>();
            trackedRaycaster.checkFor2DOcclusion = false;
            trackedRaycaster.checkFor3DOcclusion = false;
            var uiMaterial = CreateOverlayMaterial("Failure Summary UI", "UI/NoZTest");
            var textMaterial = CreateOverlayMaterial("Failure Summary Text",
                "TextMeshPro/Mobile/Distance Field Overlay", font.material);
            foreach (var image in panel.GetComponentsInChildren<Image>(true)) image.material = uiMaterial;
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true)) label.fontSharedMaterial = textMaterial;

            presenter.Configure(session.Mission, camera, panel.gameObject, restart, summary);
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private static Material CreateOverlayMaterial(string name, string shaderName, Material source = null)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("缺少结算面板着色器：" + shaderName);
            string path = AssetsRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = source == null ? new Material(shader) : new Material(source);
                material.name = name;
                AssetDatabase.CreateAsset(material, path);
            }
            else if (source != null) material.CopyPropertiesFromMaterial(source);

            // 复制字体材质保留 SDF 图集参数，只修改结算专用副本，避免影响其它三语面板。
            material.shader = shader;
            material.renderQueue = 4000;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

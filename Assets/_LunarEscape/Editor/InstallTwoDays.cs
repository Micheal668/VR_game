using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LunarEscape.Editor
{
    // 一键安装两天的全部新增内容（按依赖顺序）：语音与泄压 → 第二天钢梁与切割器 → 基地打磨 → 场景声音 → 复制出第一天场景。
    // 场景 11 由朋友的安装脚本重建后，执行本菜单即可补齐。
    public static class InstallTwoDays
    {
        [MenuItem("Lunar Escape/Install Both Days (Voice, Cutter, Polish, Audio, Day One Scene)")]
        public static void InstallAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InstallVoiceAndDecompression.Install();
            InstallDayTwoCutter.Install();
            InstallStationPolish.Install();
            InstallStationAudio.Install();
            InstallEarthReturnScene.Install();
            InstallStationPreparation.InstallScene();
            BuildDayOneScene.Build();
            Debug.Log("TWO_DAYS_INSTALLED");
        }
    }
}

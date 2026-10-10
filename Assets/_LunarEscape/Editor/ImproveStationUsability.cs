using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        [MenuItem("Lunar Escape/Improve Station Interaction Usability")]
        public static void ImproveUsability()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Logs/LayoutRevisionBackup");
            string backup="Logs/LayoutRevisionBackup/11-before-usability-fixes.unity";
            if(!File.Exists(backup))File.Copy(BuildLifeSupportScene.ScenePath,backup);
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            session=Object.FindAnyObjectByType<StationMissionSession>();life=session.GetComponent<LifeSupportMission>();
            ConfigureUsability();
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);
            AssetDatabase.SaveAssets();Debug.Log("STATION_USABILITY_IMPROVEMENTS_INSTALLED");
        }
        private static void ConfigureUsability()
        {
            foreach(var tool in Object.FindObjectsByType<RepairTool>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var tip=tool.transform.Find("Open Jaw Working End")??Frame(tool.transform,"Open Jaw Working End",new Vector3(0,0,-.15f));
                tool.ConfigureSecondaryTip(tip);EditorUtility.SetDirty(tool);
            }
            var pack=Object.FindAnyObjectByType<CargoPackZone>(FindObjectsInactive.Include);pack.ConfigureAccessibleOpening();
            var feedback=pack.GetComponent<CargoPackFeedback>();if(feedback==null)feedback=pack.gameObject.AddComponent<CargoPackFeedback>();
            feedback.Configure(pack,pack.GetComponentsInChildren<LocalizedText>(true).First(t=>t.name=="Pouch Label"),
                pack.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Pouch Side").ToArray());
            EditorUtility.SetDirty(pack);EditorUtility.SetDirty(feedback);
            var docking=session.GetComponent<DockingMission>();
            const string path="Assets/_LunarEscape/Settings/Life Support Docking Config.asset";
            var settings=AssetDatabase.LoadAssetAtPath<DockingConfig>(path);
            if(settings==null){settings=Object.Instantiate(docking.Config);AssetDatabase.CreateAsset(settings,path);}
            settings.ConfigureApproachableAssistance();EditorUtility.SetDirty(settings);
            docking.Configure(session.GetComponent<AscentMission>(),settings);EditorUtility.SetDirty(docking);
            var view=session.GetComponent<FlightScenePresenter>().FlightWorld.GetComponent<OrbiterView>();
            var marker=view.Target.Find("Docking Port Guidance")??Frame(view.Target,"Docking Port Guidance",new Vector3(0,0,-.18f));
            var ring=marker.GetComponent<LineRenderer>();if(ring==null)ring=marker.gameObject.AddComponent<LineRenderer>();
            const string matPath="Assets/_LunarEscape/Settings/Docking Guide.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,matPath);}
            material.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(material);ring.sharedMaterial=material;
            ring.useWorldSpace=false;ring.loop=true;ring.positionCount=64;ring.widthMultiplier=.05f;
            for(int i=0;i<64;i++){float a=i*Mathf.PI/32;ring.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*1.05f);}
            var guide=view.GetComponent<DockingApproachGuide>();if(guide==null)guide=view.gameObject.AddComponent<DockingApproachGuide>();
            guide.Configure(docking,ring);EditorUtility.SetDirty(guide);EditorUtility.SetDirty(ring);
        }
    }
}

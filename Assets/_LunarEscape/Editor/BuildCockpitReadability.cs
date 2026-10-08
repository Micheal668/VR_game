using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape.Editor
{
    public static partial class BuildLifeSupportScene
    {
        public static void RefreshCockpitReadability()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var layout=Object.FindAnyObjectByType<LifeSupportSceneLayout>();
            ApplyCockpitReadability(layout.ResourceScreen.transform,layout.NavigationScreen.transform,
                layout.OperationsScreen.transform,layout.OperationsScreen.transform.parent);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            EditorSceneManager.SaveScene(layout.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("COCKPIT_LARGE_TEXT_SAVED");
        }

        private static void ApplyCockpitReadability(Transform resources,Transform navigation,Transform operations,Transform controls)
        {
            foreach(var label in controls.GetComponentsInChildren<TMP_Text>(true))
            {
                label.enableAutoSizing=false;
                label.fontStyle|=FontStyles.Bold;
                label.extraPadding=true;
                // Small grey annotations were unreadable from the seated camera.
                if(label.color==Muted)label.color=new Color(.88f,.94f,1);
                EditorUtility.SetDirty(label);
            }
            foreach(var button in operations.GetComponentsInChildren<Button>(true))
                FontSize(button.GetComponentInChildren<TMP_Text>(true),52);
            var startupNames=new[]{"power","navigation","engine","ignite"};
            for(int i=0;i<startupNames.Length;i++)
            {
                var button=operations.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Startup "+startupNames[i]);
                var rect=button.GetComponent<RectTransform>();var position=rect.anchoredPosition;position.y=245-i/2*195;
                rect.anchoredPosition=position;rect.sizeDelta=new Vector2(470,170);
                button.GetComponentInChildren<TMP_Text>(true).rectTransform.sizeDelta=new Vector2(452,162);
                button.transform.Find("Key underline").GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-82);
                EditorUtility.SetDirty(rect);EditorUtility.SetDirty(button.GetComponentInChildren<TMP_Text>(true).rectTransform);
                EditorUtility.SetDirty(button.transform.Find("Key underline"));
            }
            var circularize=operations.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Circularize Orbit").GetComponent<RectTransform>();
            circularize.anchoredPosition=new Vector2(0,-145);EditorUtility.SetDirty(circularize);
            LabelLayout(operations,"Operations Header",44,400,990,96);
            LabelLayout(operations,"Flight Checklist Feedback",40,-314,955,180);
            FontSize(operations.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Toggle Capture Assist Text"),44);
            foreach(var button in controls.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent.name.StartsWith("Physical Key ")))
                FontSize(button.GetComponentInChildren<TMP_Text>(true),button.name=="Cockpit life.key.crew"?92:112);

            LabelLayout(resources,"Resources Header",44,412,780,65);
            LabelLayout(resources,"Cabin Temperature",44,-413,775,65);
            foreach(var gauge in resources.GetComponentsInChildren<ResourceReadout>(true))
            {
                FontSize(gauge.ValueLabel,80);FontSize(gauge.RemainingLabel,56);
                ResizeLabel(gauge.ValueLabel,-5,gauge.ValueLabel.rectTransform.sizeDelta.x,128);
                var caption=gauge.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name.EndsWith(" Caption")&&!t.name.EndsWith("Time Caption"));
                FontSize(caption,40);ResizeLabel(caption,caption.rectTransform.anchoredPosition.y,740,64);
                var timeCaption=gauge.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name.EndsWith("Time Caption"));
                FontSize(timeCaption,32);ResizeLabel(timeCaption,-57,timeCaption.rectTransform.sizeDelta.x,50);
                var valuePosition=gauge.ValueLabel.rectTransform.anchoredPosition;valuePosition.y=-5;gauge.ValueLabel.rectTransform.anchoredPosition=valuePosition;
                var timePosition=gauge.RemainingLabel.rectTransform.anchoredPosition;timePosition.y=-5;gauge.RemainingLabel.rectTransform.anchoredPosition=timePosition;
                EditorUtility.SetDirty(gauge.ValueLabel.rectTransform);EditorUtility.SetDirty(gauge.RemainingLabel.rectTransform);
            }

            LabelLayout(navigation,"Navigation Header",44,427,1350,64);
            LabelLayout(navigation,"Optical Guidance",38,363,1300,60);
            LabelLayout(navigation,"Dock Telemetry",48,-147,1350,145);
            LabelLayout(navigation,"Dock Alignment",40,-282,1350,120);
            LabelLayout(navigation,"Cabin Crew State",36,-400,1350,110);
            var video=navigation.GetComponentInChildren<RawImage>(true).rectTransform;
            video.anchoredPosition=new Vector2(0,130);video.sizeDelta=new Vector2(914,400);
            var reticle=video.GetComponentInChildren<DockingReticle>(true).rectTransform;
            reticle.sizeDelta=video.sizeDelta;
            EditorUtility.SetDirty(video);EditorUtility.SetDirty(reticle);
        }
        private static void FontSize(TMP_Text label,float size)
        { label.fontSize=size;label.enableAutoSizing=false;EditorUtility.SetDirty(label); }
        private static void LabelLayout(Transform parent,string name,float size,float y,float width,float height)
        {
            var label=parent.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name==name);
            FontSize(label,size);ResizeLabel(label,y,width,height);
        }
        private static void ResizeLabel(TMP_Text label,float y,float width,float height)
        {
            var rect=label.rectTransform;var position=rect.anchoredPosition;position.y=y;
            rect.anchoredPosition=position;rect.sizeDelta=new Vector2(width,height);EditorUtility.SetDirty(rect);
        }
    }
}

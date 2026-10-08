using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class BuildLifeSupportScene
    {
        private static LocalizedPanelBuilder ui;
        private static TMP_FontAsset font;
        private static LocalizationService language;
        private static readonly Color Ink = new(.021f, .033f, .043f, 1);
        private static readonly Color Cyan = new(.40f, .88f, .89f, 1);
        private static readonly Color Muted = new(.59f, .69f, .75f, 1);
        private static readonly Color Amber = new(1, .69f, .30f, 1);
        private static Material RouteMaterial()
        {
            const string path=Root+"/Art/LifeSupport/Materials/LS_RouteChevron.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(.13f,.54f,.52f,1));material.SetFloat("_Cull",0);
            EditorUtility.SetDirty(material);return material;
        }

        private static Transform Display(string name, Transform parent, Vector3 local, Vector2 size, float scale, Quaternion rotation, bool housing = false)
        {
            var panel = ui.Panel(name, parent.TransformPoint(local), size, scale, parent.rotation * rotation); panel.SetParent(parent, true);
            panel.GetComponentInChildren<Image>().color = Ink;
            if (housing) Frame(panel, size * scale);
            return panel;
        }
        private static LocalizedText Text(Transform root, string name, string key, float x, float y, float width, float height, float size, Color? color = null)
            => ui.Label(root, name, key, new Vector2(x, y), new Vector2(width, height), size, color ?? Color.white);
        private static TMP_Text Number(Transform parent, string name, Vector2 point, Vector2 size, float fontSize, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); label.transform.SetParent(parent, false);
            label.rectTransform.anchoredPosition = point; label.rectTransform.sizeDelta = size;
            label.font = font; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.color = color; label.text = "--"; return label;
        }
        private static Button Key(Transform root, string name, string key, float x, float y, float width, float height, float size = 30, bool danger = false)
        {
            var button = ui.Button(root, name, key, new Vector2(x, y), new Vector2(width, height), size);
            button.GetComponent<Image>().color = danger ? new Color(.40f,.095f,.065f) : new Color(.065f,.12f,.16f);
            var colors = button.colors; colors.highlightedColor = new Color(.72f,1,1); colors.pressedColor = new Color(.35f,.72f,.79f);
            colors.disabledColor = new Color(.4f,.44f,.48f,.6f); button.colors = colors;
            ui.Rectangle(button.transform, "Key underline", new Vector2(0,-height/2+3), new Vector2(width,3), danger ? Amber : Cyan);
            return button;
        }
        private static Image Bar(Transform root, string name, float x, float y, float width, float height, Color tint)
        {
            var track = ui.Rectangle(root, name + " Track", new Vector2(x,y), new Vector2(width,height), new Color(.10f,.15f,.18f));
            var fill = ui.Rectangle(track.transform, name + " Fill", Vector2.zero, new Vector2(width,height), tint);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; return fill;
        }
        private static ResourceReadout Gauge(Transform parent, string name, string caption, float x, float y, float width, float height, Color tint)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height);
            float factor = Mathf.Clamp(height / 200, .65f, 1);
            Text(root.transform, name + " Caption", caption, 0, height*.36f, width-30, height*.24f, 28*factor, Muted);
            var number = Number(root.transform, name + " Percent", new Vector2(-width*.24f,0), new Vector2(width*.48f,height*.50f), 58*factor,tint);
            var clock = Number(root.transform, name + " Time", new Vector2(width*.24f,5), new Vector2(width*.44f,height*.43f), 38*factor,Color.white);
            Text(root.transform, name + " Time Caption", "life.remaining", width*.24f, -height*.23f, width*.44f, height*.20f,21*factor,Muted);
            var fill = Bar(root.transform,name,0,-height*.43f,width-35,8,tint);
            var readout = root.AddComponent<ResourceReadout>(); readout.Configure(number,clock,fill,tint); return readout;
        }
        private static void Languages(Transform parent, float y, float spacing, float width, float height, float size)
        {
            for (int i = 0; i < 3; i++)
            { var key = Key(parent, "Language " + i, new[] { "language.zh", "language.en", "language.ru" }[i], (i-1)*spacing,y,width,height,size);
                UnityEventTools.AddIntPersistentListener(key.onClick,language.SetLanguageIndex,i); }
        }
        private static void Frame(Transform panel, Vector2 size)
        {
            var frame = new GameObject(panel.name + " Housing").transform; frame.SetParent(panel.parent, false);
            frame.SetPositionAndRotation(panel.position, panel.rotation);
            Solid(frame,"Instrument enclosure",new Vector3(0,0,.074f),new Vector3(size.x+.10f,size.y+.10f,.13f),"LS_Graphite");
            foreach (float x in new[] { -(size.x+.065f)/2, (size.x+.065f)/2 })
                Solid(frame,"Machined side rail",new Vector3(x,0,.002f),new Vector3(.025f,size.y+.10f,.03f),"LS_BrushedAlloy");
            foreach (float y in new[] { -(size.y+.065f)/2, (size.y+.065f)/2 })
                Solid(frame,"Machined top rail",new Vector3(0,y,.002f),new Vector3(size.x+.10f,.025f,.03f),"LS_BrushedAlloy");
            foreach (float x in new[] { -(size.x+.06f)/2, (size.x+.06f)/2 })
                foreach (float y in new[] { -(size.y+.06f)/2, (size.y+.06f)/2 })
                    Solid(frame,"Captive fastener",new Vector3(x,y,-.022f),new Vector3(.023f,.023f,.009f),"LS_BrushedAlloy");
        }
        private static GameObject Solid(Transform parent, string name, Vector3 local, Vector3 scale, string material, bool collision = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition = local; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Mat(material);
            if (!collision) Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }
        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(ImportLifeSupportArt.Root + "/Materials/" + name + ".mat");
        private static void UiLayer(Transform root) { foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5; }

        private static Transform BuildHabitatDisplay(Transform hardware, StationMissionSession session, out ResourceReadout o2,
            out ResourceReadout power, out LocalizedText temperature, out LocalizedText condition, out Button begin)
        {
            var panel = Display("Habitat Environmental Console",hardware,new Vector3(0,2.15f,3.69f),new Vector2(1800,960),.0014f,Quaternion.identity,true);
            Text(panel,"Habitat Title","life.base.title",0,420,1680,64,44);
            Text(panel,"Habitat Serial","life.base.serial",0,355,1680,40,24,Muted);
            ui.Rectangle(panel,"Header rule",new Vector2(0,322),new Vector2(1680,3),Cyan);
            o2 = Gauge(panel,"Habitat Oxygen","life.oxygen",-430,205,800,205,Cyan);
            power = Gauge(panel,"Habitat Power","life.power",430,205,800,205,Amber);
            temperature = Text(panel,"Habitat Temperature","life.base.temperature",0,55,1640,56,30,Muted);
            condition = Text(panel,"Habitat Pressure State","life.base.sealed",0,0,1640,50,28,Cyan);
            ui.Rectangle(panel,"Mission separator",new Vector2(0,-46),new Vector2(1680,2),new Color(.18f,.27f,.32f));
            var heading = Text(panel,"Mission Heading","mission.briefing.title",-250,-107,1160,62,33);
            var clock = Text(panel,"Mission Clock","mission.clock.ready",650,-107,370,64,34,Amber);
            var instructions = Text(panel,"Life Support Instructions","lifeground.briefing.instructions",-250,-190,1160,106,28);
            var details = new GameObject("Repair Details",typeof(RectTransform)).transform; details.SetParent(panel,false);
            var status = Text(details,"Repair Status","repair.ready",-300,-260,1030,45,24,Muted);
            var fill = Bar(details,"Repair Work",-300,-300,1030,10,Cyan);
            var percentage = Text(details,"Repair Progress","repair.progress",-300,-333,1030,40,22,Muted);
            var summary = Text(panel,"Repair Outcome","mission.summary.pending",-310,-378,1030,44,22,Muted);
            begin = Key(panel,"Begin Mission","mission.start",610,-248,385,105,35);
            var retry = Key(panel,"Retry Mission","mission.retry",610,-248,385,105,35);
            UnityEventTools.AddPersistentListener(begin.onClick,session.BeginMission); UnityEventTools.AddPersistentListener(retry.onClick,session.RetryMission);
            Languages(panel,-432,300,265,46,22);
            panel.gameObject.AddComponent<MissionPresenter>().Configure(session.Mission,heading,instructions,clock,summary,begin,retry,details.gameObject);
            panel.GetComponent<MissionPresenter>().ConfigureInstructionPrefix("lifeground.");
            var repair = session.Mission.RepairTask.GetComponent<RepairPresenter>();
            var indicator = new SerializedObject(repair).FindProperty("indicator").objectReferenceValue as Renderer;
            repair.Configure(session.Mission.RepairTask,status,percentage,fill,indicator);
            UiLayer(panel); return panel;
        }
        private static Transform BuildSupplyDisplay(Transform hardware, StationMissionSession session)
        {
            var panel = Display("Habitat Supply Console",hardware,new Vector3(2.1f,2.15f,3.69f),new Vector2(1150,1000),.0011f,Quaternion.identity,true);
            Text(panel,"Supply Title","life.cargo.title",0,421,1050,76,42);
            var capacity = Text(panel,"Supply Capacity","cargo.capacity",0,330,1040,90,28,Cyan);
            var status = Text(panel,"Supply Guidance","cargo.status.ready",0,-368,1040,120,26,Muted);
            var rows = new LocalizedText[6]; var buttons = new Button[6];
            var presenter = panel.gameObject.AddComponent<CargoPresenter>();
            for (int i = 0; i < 6; i++)
            {
                float y = 219-i*90;
                rows[i]=Text(panel,"Supply Row "+i,"cargo.row",-125,y,760,73,30);
                buttons[i]=Key(panel,"Discard Supply "+i,"cargo.discard",408,y,215,65,25);
                UnityEventTools.AddIntPersistentListener(buttons[i].onClick,presenter.DiscardIndex,i);
                ui.Rectangle(panel,"Supply row separator "+i,new Vector2(0,y-43),new Vector2(1040,1),new Color(.13f,.20f,.24f));
            }
            presenter.Configure(session.GetComponent<CargoInventory>(),session.Mission,language,capacity,status,rows,buttons);
            UiLayer(panel); return panel;
        }
        private static Transform BuildSuitDisplay(Transform rack, SuitInteractionController interactions, out LocalizedText state,
            out LocalizedText help, out Button don, out Image progress)
        {
            var panel = Display("Suit Service Console",rack,new Vector3(.96f,1.39f,-.18f),new Vector2(720,800),.0009f,Quaternion.identity,true);
            Text(panel,"Suit Title","life.suit.title",0,323,650,83,40);
            Text(panel,"Suit Unit","life.suit.serial",0,251,650,45,23,Muted);
            state=Text(panel,"Suit State","life.suit.progress",0,165,650,90,35,Cyan);
            help=Text(panel,"Suit Instructions","life.suit.instructions",0,31,645,165,29);
            progress=Bar(panel,"Suit Seal Progress",0,-92,620,14,Cyan);
            don=Key(panel,"Hold to Don Suit","life.suit.don",0,-225,620,130,35);
            don.gameObject.AddComponent<SuitRackInteractor>().Configure(interactions);
            Text(panel,"Suit Service Footer","life.suit.sync",0,-336,650,56,24,Muted);
            UiLayer(panel); return panel;
        }
        private static Transform BuildAirlockDisplay(Transform hardware, LifeSupportMission life, out LocalizedText state, out Button open)
        {
            var panel=Display("Airlock Pressure Console",hardware,new Vector3(3.47f,1.65f,.24f),new Vector2(700,760),.00085f,Quaternion.Euler(0,90,0),true);
            Text(panel,"Airlock Title","life.hatch.title",0,300,640,80,40);
            Text(panel,"Airlock Serial","life.hatch.serial",0,223,640,50,24,Muted);
            state=Text(panel,"Airlock Pressure Warning","life.hatch.warning",0,52,628,220,34,Amber);
            open=Key(panel,"Open Habitat Hatch","life.hatch.release",0,-225,620,133,32,true);
            UnityEventTools.AddPersistentListener(open.onClick,life.OpenDoor);
            Text(panel,"Airlock Vent Note","life.hatch.vent_note",0,-338,635,48,23,Muted);
            var repairStatus=Text(panel,"Hatch Repair State","repair.ready",0,-102,625,45,23,Cyan);
            var repairFill=Bar(panel,"Hatch Repair",0,-138,600,8,Cyan);
            life.DoorRepairTask.gameObject.AddComponent<RepairPresenter>().Configure(life.DoorRepairTask,repairStatus,null,repairFill,
                life.DoorRepairContact.RepairPoint.GetComponent<Renderer>(),"life.hatch.repair.");
            UiLayer(panel); return panel;
        }
        private static void BuildHelmetUi(StationMissionSession session, LifeSupportMission life, Transform hardware)
        {
            var head=session.Player.Camera.transform;
            RemoveNamed(head,"Life Support Helmet HUD");
            var panel=Display("Life Support Helmet HUD",head,new Vector3(0,-.33f,1.15f),new Vector2(1500,380),.00065f,Quaternion.identity);
            panel.GetComponentInChildren<Image>().color=new Color(.014f,.027f,.038f,.82f);
            var oxygen=Gauge(panel,"Suit Oxygen","life.suit.oxygen",-490,50,480,170,Cyan);
            var power=Gauge(panel,"Suit Power","life.suit.power",0,50,480,170,Amber);
            var thermal=Text(panel,"Body Temperature","life.suit.thermal",488,82,470,90,31);
            var route=Text(panel,"Escape Route","life.hud.route_hatch",485,-4,375,65,25,Cyan);
            var arrow=new GameObject("Route Arrow",typeof(RectTransform)).GetComponent<RectTransform>();arrow.SetParent(panel,false);arrow.anchoredPosition=new Vector2(675,-64);arrow.sizeDelta=new Vector2(45,45);
            foreach(float side in new[]{-1f,1f})
            {var wing=ui.Rectangle(arrow,"Arrow wing",new Vector2(side*10,0),new Vector2(8,34),Cyan);wing.rectTransform.localRotation=Quaternion.Euler(0,0,side*40);}
            var warning=Text(panel,"Suit Warning","life.hud.nominal",-230,-98,970,45,25,Amber);
            var o2=Key(panel,"Use Carried Oxygen","life.use.oxygen",-370,-154,450,57,24);
            var battery=Key(panel,"Use Carried Battery","life.use.battery",140,-154,450,57,24);
            UnityEventTools.AddPersistentListener(o2.onClick,life.UseOxygen);UnityEventTools.AddPersistentListener(battery.onClick,life.UseBattery);
            var left=session.Player.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Left Controller");
            RemoveNamed(left,"Life Support Wrist Controls");
            var wrist=Display("Life Support Wrist Controls",left,new Vector3(0,.035f,-.10f),new Vector2(610,335),.00020f,Quaternion.Euler(65,0,0));
            // A 6 mm watch backing, not the 13 cm wall-console housing.
            var backing=Solid(left,"Wrist slim backing",wrist.localPosition+wrist.localRotation*new Vector3(0,0,.004f),new Vector3(.128f,.073f,.006f),"LS_Graphite");
            backing.transform.localRotation=wrist.localRotation;backing.transform.SetParent(wrist,true);
            wrist.gameObject.AddComponent<LookDownDisplay>().Configure(session.Player.Camera,25,.32f);
            Text(wrist,"Mechanical Service Label","life.wrist.title",0,111,550,65,35,Muted);
            var wristO2=Key(wrist,"Wrist Oxygen","life.wrist.oxygen",-146,-35,270,165,45);
            var wristBattery=Key(wrist,"Wrist Battery","life.wrist.battery",146,-35,270,165,45);
            UnityEventTools.AddPersistentListener(wristO2.onClick,life.UseOxygen);UnityEventTools.AddPersistentListener(wristBattery.onClick,life.UseBattery);
            // 腕部外壳归属同一可见节点，失败或尚未穿戴时不会留下悬空外壳。
            var wristHousing=left.Find("Life Support Wrist Controls Housing");if(wristHousing!=null)wristHousing.SetParent(wrist,true);
            var routeObject=new GameObject("Helmet Escape Route");routeObject.transform.SetParent(hardware,false);
            var line=routeObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.widthMultiplier=.025f;line.numCornerVertices=3;
            line.sharedMaterial=Mat("LS_Light");line.startColor=line.endColor=Cyan;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            routeObject.AddComponent<RouteChevronGuide>().Configure(RouteMaterial());
            panel.gameObject.AddComponent<HelmetHudOverlay>().Configure(Shader.Find("LunarEscape/Visor UI Overlay"),Shader.Find("TextMeshPro/Mobile/Distance Field Overlay"));
            var points=session.GetComponent<GroundCrewController>().Waypoints.ToArray();
            GetOrAdd<SuitHudPresenter>(session.gameObject).Configure(life,session.GetComponent<AscentMission>(),session.Player.Camera,panel.gameObject,wrist.gameObject,
                oxygen,power,thermal,route,warning,arrow,line,points,new[]{o2,wristO2},new[]{battery,wristBattery});
            UiLayer(panel);UiLayer(wrist);
        }
    }
}

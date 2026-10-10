using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        private static StationPatchPuzzle BuildPuzzle(string name, Vector3 position, Quaternion rotation, bool circuit, HabitatBreaker requiredPower)
        {
            var mount = Frame(root, name, position); mount.rotation = rotation;
            var puzzle = mount.gameObject.AddComponent<StationPatchPuzzle>();
            Primitive(mount, "Patch Panel Face", PrimitiveType.Cube, Vector3.zero, new Vector3(1.60f,.96f,.08f), "EX_Blue");
            var readout = Label(mount, "Patch Status", "", new Vector3(0,.32f,.057f), new Vector2(1.48f,.25f), .39f, new Vector3(0,180,0));
            var ports = new StationPatchSocket[3]; var modules = new StationPatchModule[3];
            var colors = new[] { new Color(.95f,.10f,.06f), new Color(.035f,.48f,.98f), new Color(1,.72f,.025f) };
            string[] colorNames = { "RED", "BLUE", "YELLOW" };
            for (int i = 0; i < 3; i++)
            {
                string materialName = "EX_Circuit" + colorNames[i];
                string path = Art + "/Materials/" + materialName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
                material.SetColor("_BaseColor",colors[i]);material.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(material);materials[materialName]=material;
            }
            for (int i = 0; i < 3; i++)
            {
                int id = circuit ? new[] {2,0,1}[i] : new[] {1,2,0}[i];
                float x = (i-1)*.48f;
                string color = circuit ? "EX_Circuit"+colorNames[id] : "EX_White";
                var socketRoot = Frame(mount, "Patch Socket " + id, new Vector3(x,-.01f,.12f));
                Primitive(socketRoot, "Socket Trim", circuit ? PrimitiveType.Cylinder : Shape(id), Vector3.zero, new Vector3(.22f,.16f,.14f), color);
                var socket = socketRoot.gameObject.AddComponent<XRSocketInteractor>();
                var trigger = socketRoot.gameObject.AddComponent<SphereCollider>();trigger.isTrigger=true;trigger.radius=.13f;
                socket.attachTransform = Frame(socketRoot,"Seated Module",new Vector3(0,0,.065f));
                socket.showInteractableHoverMeshes = false;
                var lampObject = Primitive(socketRoot,"Socket Lamp",PrimitiveType.Sphere,new Vector3(.12f,.08f,.035f),Vector3.one*.035f,"EX_Light");
                var lamp=lampObject.AddComponent<CockpitLamp>();lamp.Configure(lampObject.GetComponent<Renderer>());
                var port=socketRoot.gameObject.AddComponent<StationPatchSocket>();port.Configure(puzzle,id,lamp);ports[i]=port;
                Label(mount,"Socket Identifier "+id,(id+1).ToString(),new Vector3(x,-.18f,.08f),new Vector2(.18f,.15f),.5f,new Vector3(0,180,0));

                var moduleRoot = Frame(mount,"Patch Module "+i,new Vector3(x,-.36f,.36f));
                string moduleColor=circuit?"EX_Circuit"+colorNames[i]:"EX_Copper";
                Primitive(moduleRoot,"Module Body",circuit?PrimitiveType.Cube:Shape(i),Vector3.zero,new Vector3(.135f,.135f,.12f),moduleColor);
                Primitive(moduleRoot,"Module Contact",PrimitiveType.Cube,new Vector3(0,0,-.07f),new Vector3(.075f,.08f,.035f),"EX_Trim");
                var collider=moduleRoot.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.15f,.15f,.17f);
                var body=moduleRoot.gameObject.AddComponent<Rigidbody>();body.mass=.12f;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var grab=moduleRoot.gameObject.AddComponent<XRGrabInteractable>();grab.useDynamicAttach=true;grab.throwOnDetach=false;grab.colliders.Add(collider);
                var recovery=moduleRoot.gameObject.AddComponent<ReturnFallenTool>();recovery.ConfigureFloorRecovery(-.08f);
                var module=moduleRoot.gameObject.AddComponent<StationPatchModule>();
                LineRenderer wire=null;Transform cableAnchor=null;
                if(circuit)
                {
                    cableAnchor=Frame(mount,"Cable Terminal "+i,new Vector3(x,-.42f,.08f));
                    wire=Frame(mount,"Colored Circuit Cable "+i,Vector3.zero).gameObject.AddComponent<LineRenderer>();
                    wire.sharedMaterial=materials["EX_Circuit"+colorNames[i]];wire.useWorldSpace=true;wire.startWidth=wire.endWidth=.012f;
                    wire.numCapVertices=4;wire.numCornerVertices=4;
                }
                module.Configure(puzzle,i,cableAnchor,wire);modules[i]=module;
                Label(moduleRoot,"Module Number",(i+1).ToString(),new Vector3(0,0,.071f),new Vector2(.10f,.10f),.42f,new Vector3(0,180,0));
            }
            puzzle.Configure(life,requiredPower,ports,modules,readout,circuit);
            return puzzle;
        }
        private static PrimitiveType Shape(int identity) => identity == 0 ? PrimitiveType.Cube : identity == 1 ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
    }
}

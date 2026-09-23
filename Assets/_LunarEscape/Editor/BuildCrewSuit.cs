using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    // 导入用户的 ACES 静态模型，烘焙成带上身骨骼的 Unity 原生 Prefab；原始 GLB 保留。
    public static class BuildCrewSuit
    {
        public const string Root="Assets/_LunarEscape/Art/CrewEscape";
        public const string PrefabPath=Root+"/Crew Escape Suit.prefab";
        private static readonly string[] BoneNames={"Hips","Chest","Head","Left Upper Arm","Left Forearm","Left Hand","Right Upper Arm","Right Forearm","Right Hand"};
        private static readonly int[] Parents={-1,0,1,1,3,4,1,6,7};
        private static readonly Vector3[] Joints={new(0,.9f,-.06f),new(0,1.26f,-.06f),new(0,1.64f,-.06f),new(-.235f,1.36f,-.06f),new(-.325f,1.15f,.015f),new(-.325f,1.025f,.14f),new(.235f,1.36f,-.06f),new(.325f,1.15f,.015f),new(.325f,1.025f,.14f)};
        private sealed class Part
        { public int material;public Vector3[] vertices,normals;public Vector2[] uv;public int[] indices; }

        public static GameObject Import(bool rebuild = false)
        {
            var cached=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(cached!=null&&!rebuild)return cached;
            var parts=new List<Part>();Material[] materials;
            using(var reader=new BinaryReader(File.OpenRead(Root+"/CrewEscapeMeshes.bytes")))
            {
                if(new string(reader.ReadChars(4))!="LML1")throw new InvalidDataException("无效的航天服模型缓存。");
                int count=reader.ReadInt32();materials=new Material[count];
                for(int i=0;i<count;i++)
                {
                    var colour=new Color(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    float metal=reader.ReadSingle(),rough=reader.ReadSingle();reader.ReadInt32();
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ACES Material "+i};
                    material.SetColor("_BaseColor",colour);material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",1-rough);
                    // 源文件对所有材质都标记了 transmission=1，但这是一套不透明织物/头盔展示模型。
                    // 使用原始基色；面罩采用深色反光材质，避免把整个乘员错误导成透明。
                    if(i==6){material.SetFloat("_Metallic",.35f);material.SetFloat("_Smoothness",.8f);}
                    materials[i]=Save(material,Root+"/Material "+i+".mat");
                }
                int groups=reader.ReadInt32();
                for(int i=0;i<groups;i++)
                {
                    var part=new Part{material=reader.ReadInt32()};int countV=reader.ReadInt32(),countI=reader.ReadInt32();
                    part.vertices=new Vector3[countV];part.normals=new Vector3[countV];part.uv=new Vector2[countV];part.indices=new int[countI];
                    for(int j=0;j<countV;j++)part.vertices[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<countV;j++)part.normals[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<countV;j++)part.uv[j]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<countI;j++)part.indices[j]=reader.ReadInt32();parts.Add(part);
                }
            }
            var bounds=new Bounds(parts[0].vertices[0],Vector3.zero);
            foreach(var p in parts)foreach(var v in p.vertices)bounds.Encapsulate(v);
            var offset=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);float scale=1.8f/bounds.size.y;
            foreach(var p in parts)for(int i=0;i<p.vertices.Length;i++)p.vertices[i]=(p.vertices[i]-offset)*scale;
            var root=new GameObject("Advanced Crew Escape Suit");
            try
            {
                var bones=new Transform[BoneNames.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    bones[i]=new GameObject(BoneNames[i]).transform;bones[i].SetParent(Parents[i]<0?root.transform:bones[Parents[i]],false);
                    bones[i].position=Joints[i];
                }
                var bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
                var body=BuildSkin(root,"Suit Body",false,parts,materials,bones,bindposes);
                var helmet=BuildSkin(root,"Helmet",true,parts,materials,bones,bindposes);
                root.AddComponent<CrewSuitRig>().Configure(bones,body,helmet);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();return prefab;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static SkinnedMeshRenderer BuildSkin(GameObject root,string name,bool head,List<Part> parts,Material[] materials,Transform[] bones,Matrix4x4[] bindposes)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var weights=new List<BoneWeight>();
            var submeshes=Enumerable.Range(0,materials.Length).Select(_=>new List<int>()).ToArray();
            foreach(var part in parts)
            {
                var partWeights=ComponentWeights(part);
                var mapping=new Dictionary<int,int>();
                for(int i=0;i<part.indices.Length;i+=3)
                {
                    int a=part.indices[i],b=part.indices[i+1],c=part.indices[i+2];
                    bool isHead=(part.vertices[a].y+part.vertices[b].y+part.vertices[c].y)/3>1.465f;
                    if(isHead!=head)continue;
                    for(int k=0;k<3;k++)
                    {
                        int source=part.indices[i+k];
                        if(!mapping.TryGetValue(source,out int index))
                        {
                            index=vertices.Count;mapping.Add(source,index);var p=part.vertices[source];
                            vertices.Add(p);normals.Add(part.normals[source]);uvs.Add(part.uv[source]);weights.Add(partWeights[source]);
                        }
                        submeshes[part.material].Add(index);
                    }
                }
            }
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);
            mesh.boneWeights=weights.ToArray();mesh.bindposes=bindposes;mesh.subMeshCount=materials.Length;
            for(int i=0;i<materials.Length;i++)mesh.SetTriangles(submeshes[i],i);mesh.RecalculateBounds();
            mesh=Save(mesh,Root+"/"+name+".asset");
            var renderer=new GameObject(name,typeof(SkinnedMeshRenderer)).GetComponent<SkinnedMeshRenderer>();renderer.transform.SetParent(root.transform,false);
            renderer.sharedMesh=mesh;renderer.sharedMaterials=materials;renderer.bones=bones;renderer.rootBone=bones[0];renderer.updateWhenOffscreen=true;
            renderer.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(3,3,3));return renderer;
        }
        private static BoneWeight[] ComponentWeights(Part part)
        {
            var result=part.vertices.Select(p=>Weight(p,part.material)).ToArray();
            if(part.material==9||part.material==13)return result;
            // 拉链、袖口、铭牌等独立硬件整体绑定，避免同一零件两端落在不同骨骼上。
            var parent=Enumerable.Range(0,part.vertices.Length).ToArray();
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<part.vertices.Length;i++)
            {
                var p=part.vertices[i]*100000;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(welded.TryGetValue(key,out int previous))Join(i,previous);else welded.Add(key,i);
            }
            for(int i=0;i<part.indices.Length;i+=3){Join(part.indices[i],part.indices[i+1]);Join(part.indices[i],part.indices[i+2]);}
            var bounds=new Dictionary<int,Bounds>();
            for(int i=0;i<parent.Length;i++)
            {int root=Find(i);if(!bounds.TryGetValue(root,out var b))b=new Bounds(part.vertices[i],Vector3.zero);else b.Encapsulate(part.vertices[i]);bounds[root]=b;}
            for(int i=0;i<parent.Length;i++)
            {var weight=Weight(bounds[Find(i)].center,part.material);result[i]=new BoneWeight{boneIndex0=weight.boneIndex0,weight0=1};}
            return result;
        }
        private static BoneWeight Weight(Vector3 p,int material)
        {
            if(p.y>1.465f)return new BoneWeight{boneIndex0=2,weight0=1};
            // 手指横跨多个高度和横向位置，但整只手必须刚性跟随手腕。
            if(material==13)return new BoneWeight{boneIndex0=p.x<0?5:8,weight0=1};
            // 两臂在原图里自然下垂，按肩、肘、腕高度分段并平滑混合，避免肘部断开。
            // 腰侧和胸前附件不能分配到手腕：肩部向腰部逐渐收窄手臂影响范围。
            float boundary=Mathf.Lerp(.285f,.18f,Smooth(1.12f,1.39f,p.y));
            float arm=Smooth(boundary,boundary+.035f,Mathf.Abs(p.x))*(1-Smooth(1.38f,1.465f,p.y))*Smooth(.85f,.91f,p.y);
            if(material==10&&p.y<1.15f&&Mathf.Abs(p.x)>.23f)
                return new BoneWeight{boneIndex0=p.x<0?5:8,weight0=1};
            int upper=p.x<0?3:6,fore=upper+1,hand=upper+2;
            float wrist=1-Smooth(.995f,1.065f,p.y),elbow=1-Smooth(1.13f,1.22f,p.y);
            float torso=Smooth(.85f,1.18f,p.y);
            var values=new[]{(index:0,value:(1-arm)*(1-torso)),(index:1,value:(1-arm)*torso),(index:upper,value:arm*(1-elbow)),(index:fore,value:arm*elbow*(1-wrist)),(index:hand,value:arm*wrist)}.OrderByDescending(v=>v.value).Take(4).ToArray();
            float sum=values.Sum(v=>v.value);
            return new BoneWeight{boneIndex0=values[0].index,weight0=values[0].value/sum,boneIndex1=values[1].index,weight1=values[1].value/sum,boneIndex2=values[2].index,weight2=values[2].value/sum,boneIndex3=values[3].index,weight3=values[3].value/sum};
        }
        private static float Smooth(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        private static T Save<T>(T asset,string path)where T:UnityEngine.Object
        {var previous=AssetDatabase.LoadAssetAtPath<T>(path);if(previous==null)AssetDatabase.CreateAsset(asset,path);else{EditorUtility.CopySerialized(asset,previous);UnityEngine.Object.DestroyImmediate(asset);asset=previous;}return asset;}

        public static void ApplyToCurrentScene()
        {
            var prefab=Import();var session=UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            var scene=session.GetComponent<FlightScenePresenter>();var crew=scene.FlightWorld.GetComponent<CrewCabinLayout>();
            if(crew.Companion.GetComponent<CrewSuitRig>()==null)
            {
                var old=crew.Companion;var npc=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                npc.name="Commander - Crew Escape Suit";npc.transform.SetParent(crew.CompanionStation,false);npc.transform.localRotation=Quaternion.Euler(0,200,0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
                crew.Configure(crew.PlayerStation,crew.CompanionStation,npc,crew.CabinSize);EditorUtility.SetDirty(crew);UnityEngine.Object.DestroyImmediate(old);
            }
            var player=session.Player;
            var previous=player.GetComponentInChildren<TrackedCrewSuit>(true);
            if(previous==null)
            {
                var avatar=(GameObject)PrefabUtility.InstantiatePrefab(prefab);avatar.name="Player - Crew Escape Suit";avatar.transform.SetParent(player.transform,false);
                var left=player.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Left Controller");
                var right=player.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Right Controller");
                var model=avatar.GetComponent<CrewSuitRig>();avatar.AddComponent<TrackedCrewSuit>().Configure(player,left,right,model);
                foreach(var controller in new[]{left,right})
                    foreach(var visual in controller.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Left Controller Visual"||t.name=="Right Controller Visual"))
                    {visual.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(visual.gameObject);}
                int layer=EnsureHeadLayer();model.Helmet.gameObject.layer=layer;player.Camera.cullingMask &= ~(1<<layer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.Camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model.Helmet.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);PrefabUtility.RecordPrefabInstancePropertyModifications(avatar.transform);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        private static int EnsureHeadLayer()
        {
            int layer=LayerMask.NameToLayer("Player Head");if(layer>=0)return layer;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
            for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {layers.GetArrayElementAtIndex(i).stringValue="Player Head";tags.ApplyModifiedProperties();return i;}
            throw new InvalidOperationException("需要一个空闲图层来隐藏玩家自己的头盔。");
        }
        [MenuItem("Lunar Escape/Install Crew Escape Suit")]
        public static void Install()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            AssetDatabase.Refresh();EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("CREW_ESCAPE_SUIT_INSTALLED");
        }
        public static void Rebuild()
        { Import(true); Install(); }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    // 将随工程保存的 NASA 静态网格数据转成原生 Unity 资源；Windows 打开不需要 Python 或 glTF 插件。
    public static class ImportApolloModel
    {
        public const string Root="Assets/_LunarEscape/Art/ApolloLM";
        public const string PrefabPath=Root+"/Apollo Lunar Module.prefab";
        public static GameObject Import()=>ImportStatic(Root,PrefabPath,"ApolloMeshes.bytes",7,"Apollo Lunar Module - NASA");
        public static GameObject ImportStatic(string rootPath,string prefabPath,string dataName,float height,string displayName)
        {
            AssetDatabase.Refresh();
            var cached=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); if(cached!=null) return cached;
            var root=new GameObject(displayName);
            using(var reader=new BinaryReader(File.OpenRead(rootPath+"/"+dataName)))
            {
                if(new string(reader.ReadChars(4))!="LML1") throw new InvalidDataException("无效的模型缓存。");
                int materialCount=reader.ReadInt32(); var materials=new Material[materialCount];
                for(int i=0;i<materialCount;i++)
                {
                    var color=new Color(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    float metallic=reader.ReadSingle(),roughness=reader.ReadSingle();int texture=reader.ReadInt32();
                    var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Apollo Material "+i};
                    mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",1-roughness);mat.SetFloat("_Cull",0);
                    if(texture>=0) mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(rootPath+"/texture-"+texture+".png"));
                    AssetDatabase.CreateAsset(mat,rootPath+"/Material "+i+".mat");materials[i]=mat;
                }
                int count=reader.ReadInt32();
                for(int i=0;i<count;i++)
                {
                    int material=reader.ReadInt32(),vertices=reader.ReadInt32(),indices=reader.ReadInt32();
                    var pos=new Vector3[vertices];var normal=new Vector3[vertices];var uv=new Vector2[vertices];var triangles=new int[indices];
                    for(int j=0;j<vertices;j++)pos[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<vertices;j++)normal[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<vertices;j++)uv[j]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<indices;j++)triangles[j]=reader.ReadInt32();
                    var mesh=new Mesh{name="Apollo Group "+i,indexFormat=IndexFormat.UInt32,vertices=pos,normals=normal,uv=uv,triangles=triangles};mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh,rootPath+"/Mesh "+i+".asset");
                    var part=new GameObject("Apollo Material Group "+i,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(root.transform,false);
                    part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=materials[material];
                }
            }
            var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float factor=height/bounds.size.y;
            foreach(Transform child in root.transform) {child.localScale=Vector3.one*factor;child.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*factor;}
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();return prefab;
        }
        public static void ImportAndPreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=Import();var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.42f,.44f);
            var light=new GameObject("Preview Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(38,-32,0);
            var plane=GameObject.CreatePrimitive(PrimitiveType.Cube);plane.transform.position=new Vector3(0,-.08f,0);plane.transform.localScale=new Vector3(22,.15f,22);
            plane.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Materials/Lunar Soil.mat");
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI/2; Vector3 position=new Vector3(Mathf.Sin(angle)*12,5,Mathf.Cos(angle)*12);
                Capture("apollo-view-"+i,position,new Vector3(0,3,0));
            }
        }
        internal static void Capture(string name,Vector3 position,Vector3 target)
        {
            var owner=new GameObject("Preview Camera");var camera=owner.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.02f,.03f);camera.fieldOfView=55;
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            var texture=new RenderTexture(1400,1100,24);var pixels=new Texture2D(1400,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try {texture.Create();camera.targetTexture=texture;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=texture});RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,1400,1100),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath("../.development/"+name+".png"),pixels.EncodeToPNG());}
            finally {RenderTexture.active=previous;camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(owner);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static class ImportReturnCapsule
    {
        public const string Root = "Assets/_LunarEscape/Art/EarthReturn/Capsule";
        public const string Prefab = Root + "/Three Seat Capsule.prefab";
        [Serializable] private class Mat { public string name; public float[] color; public float metallic, roughness; }
        [Serializable] private class Part { public string name, material; }
        [Serializable] private class Manifest { public Mat[] materials; public Part[] meshes; }
        public static GameObject Import()
        {
            Directory.CreateDirectory(Root + "/Meshes"); Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/capsule.json"));
            var mats = data.materials.ToDictionary(m => m.name, m =>
            {
                string path = Root + "/Materials/" + m.name + ".mat";
                var result = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result,path); }
                result.SetColor("_BaseColor",new Color(m.color[0],m.color[1],m.color[2],1).gamma);
                result.SetFloat("_Metallic",m.metallic); result.SetFloat("_Smoothness",1-m.roughness);
                if(m.name=="Light") { result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor",Color.white*2); }
                if(m.name=="Pressure glass")
                {
                    result.SetColor("_BaseColor",new Color(.18f,.30f,.36f,.035f));result.SetFloat("_Surface",1);
                    result.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);result.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    result.SetFloat("_ZWrite",0);result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");result.renderQueue=3000;
                    result.SetShaderPassEnabled("ShadowCaster",false);
                }
                EditorUtility.SetDirty(result); return result;
            });
            var root = new GameObject("Three Seat Bell Capsule - Blender");
            try
            {
                using var reader = new BinaryReader(File.OpenRead(Root + "/capsule-meshes.bytes"));
                int count = reader.ReadInt32();
                if(count!=data.meshes.Length) throw new InvalidDataException("返回舱网格清单不匹配");
                for(int i=0;i<count;i++)
                {
                    int n=reader.ReadInt32(); var v=new Vector3[n]; var normals=new Vector3[n];
                    for(int j=0;j<n;j++)v[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    for(int j=0;j<n;j++)normals[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    var part=data.meshes[i]; var mesh=new Mesh{name=part.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=v,normals=normals,triangles=Enumerable.Range(0,n).ToArray()}; mesh.RecalculateBounds();
                    string path=Root+"/Meshes/"+part.name+".asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(stored==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);mesh=stored;EditorUtility.SetDirty(mesh);}
                    var obj=new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(root.transform,false);
                    obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=mats[part.material];
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

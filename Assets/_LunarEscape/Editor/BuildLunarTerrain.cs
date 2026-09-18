using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    // 编辑时烘焙两级地形网格；运行时直接使用静态网格与近处碰撞，不每帧重建地形。
    public static class BuildLunarTerrain
    {
        private const string Root="Assets/_LunarEscape";
        public static GameObject Build(Transform ground,Transform outside,StationMissionSession session)
        {
            var material=Material("Lunar Regolith",new Color(.48f,.46f,.43f));
            var terrain=new GameObject("Cratered Lunar Surface");terrain.transform.SetParent(ground,false);
            var near=MeshObject(terrain.transform,"Walkable crater terrain",CreateMesh(true),material);
            var collision=near.AddComponent<MeshCollider>();collision.sharedMesh=near.GetComponent<MeshFilter>().sharedMesh;
            MeshObject(terrain.transform,"Distant crater ridges",CreateMesh(false),material);
            var proxy=new GameObject("Flight Cratered Surface");proxy.transform.SetParent(outside,false);
            foreach(var source in terrain.GetComponentsInChildren<MeshFilter>())MeshObject(proxy.transform,source.name,source.sharedMesh,material);
            session.gameObject.AddComponent<LunarTerrainLayout>().Configure(collision,terrain,proxy);
            return proxy;
        }
        private static Mesh CreateMesh(bool near)
        {
            int nx=near?192:200,nz=near?160:200;float cell=near?1:8;
            var vertices=new Vector3[(nx+1)*(nz+1)];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];
            var triangles=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float px=(x-nx*.5f)*cell+25,pz=(z-nz*.5f)*cell;float y=LunarTerrainProfile.Height(px,pz);
                // 近景边界与远景的 8 m 网格共用直线边，避免接缝裂开。
                if(near && (x==0 || x==nx)) {float lo=Mathf.Floor(pz/8)*8;y=Mathf.Lerp(LunarTerrainProfile.Height(px,lo),LunarTerrainProfile.Height(px,lo+8),(pz-lo)/8);}
                if(near && (z==0 || z==nz)) {float lo=Mathf.Floor((px-25)/8)*8+25;y=Mathf.Lerp(LunarTerrainProfile.Height(lo,pz),LunarTerrainProfile.Height(lo+8,pz),(px-lo)/8);}
                int i=z*(nx+1)+x;vertices[i]=new Vector3(px,y,pz);uv[i]=new Vector2(px*.02f,pz*.02f);
                float shade=.85f+Mathf.PerlinNoise(px*.2f+20,pz*.2f+30)*.3f;colors[i]=new Color(shade,shade,shade,1);
                if(x==nx || z==nz)continue;
                float centerX=px-25+cell*.5f,centerZ=pz+cell*.5f;
                if(!near && Mathf.Abs(centerX)<96 && Mathf.Abs(centerZ)<80)continue;
                triangles.Add(i);triangles.Add(i+nx+1);triangles.Add(i+1);
                triangles.Add(i+1);triangles.Add(i+nx+1);triangles.Add(i+nx+2);
            }
            var mesh=new Mesh{name=near?"Lunar Terrain Near":"Lunar Terrain Far",indexFormat=IndexFormat.UInt32,vertices=vertices,colors=colors,uv=uv};
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Root+"/Art/Orbit/"+mesh.name+".asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(cached==null)AssetDatabase.CreateAsset(mesh,path);else {EditorUtility.CopySerialized(mesh,cached);Object.DestroyImmediate(mesh);mesh=cached;EditorUtility.SetDirty(mesh);}
            return mesh;
        }
        public static Mesh CreateMoon()
        {
            const int columns=512,rows=256;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];
            var triangles=new int[columns*rows*6];
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                float lat=Mathf.PI*(float)y/rows,lon=2*Mathf.PI*x/columns;int i=y*(columns+1)+x;
                vertices[i]=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon));normals[i]=vertices[i];uv[i]=new Vector2((float)x/columns,1-(float)y/rows);colors[i]=Color.white;
                if(x==columns || y==rows)continue;int j=(y*columns+x)*6;
                triangles[j]=i;triangles[j+1]=i+1;triangles[j+2]=i+columns+1;triangles[j+3]=i+1;triangles[j+4]=i+columns+2;triangles[j+5]=i+columns+1;
            }
            var mesh=new Mesh{name="Orbital Moon Sphere",indexFormat=IndexFormat.UInt32,vertices=vertices,normals=normals,uv=uv,colors=colors,triangles=triangles};mesh.RecalculateBounds();
            string path=Root+"/Art/Orbit/Orbital Moon Sphere.asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(cached==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,cached);Object.DestroyImmediate(mesh);mesh=cached;EditorUtility.SetDirty(mesh);}return mesh;
        }
        public static GameObject MeshObject(Transform parent,string name,Mesh mesh,Material material)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj;
        }
        public static Material Material(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("LunarEscape/Lunar Regolith"));AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",color);EditorUtility.SetDirty(mat);return mat;
        }
    }
}

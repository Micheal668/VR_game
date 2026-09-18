using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    // 从现有 1.6 km 方形网格的每个边界点向全球逐级延伸，首圈顶点逐一重合。
    public static class BuildContinuousMoon
    {
        private const string Root="Assets/_LunarEscape";
        public static Material BuildMaterial()
        {
            const string path=Root+"/Materials/Continuous Lunar Surface.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("LunarEscape/Continuous Moon"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",new Color(.64f,.64f,.63f));
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Orbit/lroc_color_poles_4k.tif"));
            mat.SetFloat("_SurfaceAngle",0);EditorUtility.SetDirty(mat);return mat;
        }
        public static Mesh BuildMesh()
        {
            const int segments=800,rings=240;
            const double radius=1737400;
            var vertices=new Vector3[(rings+1)*(segments+1)];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];
            var triangles=new List<int>(rings*segments*6);
            double expansion=Math.Exp(rings*.047)-1;
            for(int row=0;row<=rings;row++)for(int col=0;col<=segments;col++)
            {
                int edge=(col%segments)/200;float along=(col%200)*8;
                Vector2 border=edge switch{0=>new Vector2(800,-800+along),1=>new Vector2(800-along,800),2=>new Vector2(-800,800-along),_=>new Vector2(-800+along,-800)};
                double inner=border.magnitude,theta0=Math.Asin(inner/radius);
                double t=(Math.Exp(row*.047)-1)/expansion,theta=theta0+(Math.PI-theta0)*t;
                double horizontal=radius*Math.Sin(theta);Vector2 direction=border.normalized;
                double boundaryCurve=radius*(1-Math.Cos(theta0));
                float px=25+(float)(direction.x*horizontal),pz=(float)(direction.y*horizontal);
                double correction=0;
                if(theta<.1)
                {
                    // 重新采样同一高度场，避免把边缘的一排山脊沿半径拉成长条。
                    float extension=OrbitalTrajectory.Smooth((Mathf.Max(Mathf.Abs(px-25),Mathf.Abs(pz))-800)/600);
                    float farBlend=1-OrbitalTrajectory.Smooth(((float)(theta*radius)-20000)/100000);
                    correction=(LunarTerrainProfile.Height(px,pz)+extension*OuterLandforms(px,pz))*farBlend
                        +boundaryCurve*Math.Exp(-Math.Max(0,theta*radius-theta0*radius)/1700);
                }
                int i=row*(segments+1)+col;
                vertices[i]=new Vector3(25+(float)(direction.x*horizontal),(float)(radius*(Math.Cos(theta)-1)+correction),(float)(direction.y*horizontal));
                // 最内圈精确匹配旧远景边界，避免浮点或曲率造成裂缝。
                if(row==0)vertices[i]=new Vector3(border.x+25,LunarTerrainProfile.Height(border.x+25,border.y),border.y);
                uv[i]=new Vector2((float)col/segments,(float)row/rings);colors[i]=Color.white;
                if(row==rings || col==segments)continue;
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+segments+1);
                triangles.Add(i+1);triangles.Add(i+segments+2);triangles.Add(i+segments+1);
            }
            var mesh=new Mesh{name="Continuous Moon with terrain opening",indexFormat=IndexFormat.UInt32,vertices=vertices,uv=uv,colors=colors};
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            const string path=Root+"/Art/Orbit/Continuous Moon.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(mesh);}
            return mesh;
        }
        private static float OuterLandforms(float x,float z)
        {
            float height=(Mathf.PerlinNoise(x*.0004f+41,z*.0004f+23)-.5f)*120;
            const float cell=2400;int ix=Mathf.FloorToInt(x/cell),iz=Mathf.FloorToInt(z/cell);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int cx=ix+dx,cz=iz+dz;float seed=Hash(cx,cz);if(seed<.45f)continue;
                float radius=180+Hash(cx+91,cz)*650;
                var center=new Vector2((cx+.2f+Hash(cx,cz+71)*.6f)*cell,(cz+.2f+Hash(cx+37,cz)*.6f)*cell);
                float q=Vector2.Distance(new Vector2(x,z),center)/radius;if(q>1.6f)continue;
                height+=q<1?-radius*.11f*Mathf.Pow(1-q*q,2):0;
                height+=radius*.024f*Mathf.Exp(-Mathf.Pow((q-1)/.18f,2));
            }
            return height;
        }
        private static float Hash(int x,int z){float v=Mathf.Sin(x*127.1f+z*311.7f)*43758.5453f;return v-Mathf.Floor(v);}
    }
}

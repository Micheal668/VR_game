using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    public static class ArtemisLandingSiteBuilder
    {
        private const string Detail = "Assets/_LunarEscape/Art/LunarSurfaceDetail/";
        private const string Site = "Assets/_LunarEscape/Art/ArtemisLanding/";
        private static readonly Vector3 StationPosition = new(24, 0, 85);

        // A flat disk extends beyond the required 15 m radius, including the interpolation cells at its edge.
        public static Transform Build(Transform environment)
        {
            if (!AssetDatabase.IsValidFolder(Site.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/_LunarEscape/Art", "ArtemisLanding");
            var terrain = SurfaceMesh();
            var ground = BuildLunarTerrain.MeshObject(environment, "Cratered approach terrain", terrain,
                AssetDatabase.LoadAssetAtPath<Material>(Detail + "Detailed Lunar Surface.mat"));
            ground.AddComponent<MeshCollider>().sharedMesh = terrain;
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddStation(environment);
            AddBoulders(environment);
            var target = new GameObject("Touchdown center").transform;
            target.SetParent(environment, false);
            AddMarkings(target);
            return target;
        }

        private readonly struct Crater
        {
            public readonly float X, Z, Radius, Depth, Aspect, Sin, Cos;
            public Crater(float x, float z, float radius, float depth, float aspect, float angle)
            {
                X=x; Z=z; Radius=radius; Depth=depth; Aspect=aspect;
                Sin=Mathf.Sin(angle); Cos=Mathf.Cos(angle);
            }
        }

        private static Crater[] Craters()
        {
            var list = new List<Crater>
            {
                new(-47,-35,17,3.6f,.84f,-.22f), new(60,26,22,4.5f,.95f,.31f),
                new(88,-138,48,11,.82f,.11f), new(-158,163,74,12,.83f,-.34f),
                new(230,360,135,19,.95f,.23f), new(-205,-300,105,16,.94f,.45f)
            };
            var random = new System.Random(3901);
            for (int i=0;i<100;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2;
                float d=37+Mathf.Pow((float)random.NextDouble(),1.5f)*850;
                float radius=3+Mathf.Pow((float)random.NextDouble(),2f)*Mathf.Min(80,d*.2f);
                list.Add(new Crater(Mathf.Cos(a)*d,Mathf.Sin(a)*d,radius,radius*(.11f+.07f*(float)random.NextDouble()),
                    .76f+.24f*(float)random.NextDouble(),a));
            }
            return list.ToArray();
        }

        private static float Height(float x, float z, Crater[] craters)
        {
            float radius=Mathf.Sqrt(x*x+z*z);
            float padBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,34,radius));
            float stationDistance=Mathf.Max(Mathf.Abs(x-StationPosition.x)-22,Mathf.Abs(z-StationPosition.z)-23);
            float siteBlend=padBlend*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,16,stationDistance));
            if (siteBlend<=0) return 0;
            float land=(Mathf.PerlinNoise(x*.009f+31,z*.009f+57)-.48f)*7+
                (Mathf.PerlinNoise(x*.042f+89,z*.042f+8)-.5f)*1.5f;
            float distant=Mathf.SmoothStep(0,1,Mathf.InverseLerp(170,560,radius));
            // Unity's Perlin implementation can undershoot zero near a local minimum.
            float rimNoise=Mathf.Clamp01(Mathf.PerlinNoise(14+x*.0021f,21+z*.0021f));
            land+=distant*(Mathf.Pow(rimNoise,2.4f)*113-11);
            foreach (var c in craters)
            {
                float dx=x-c.X, dz=z-c.Z;
                if(Mathf.Abs(dx)>c.Radius*1.55f || Mathf.Abs(dz)>c.Radius*1.55f) continue;
                float u=(dx*c.Cos+dz*c.Sin)/c.Radius, v=(-dx*c.Sin+dz*c.Cos)/(c.Radius*c.Aspect);
                float q=Mathf.Sqrt(u*u+v*v);
                float bowl=Mathf.Max(0,1-q*q);
                float ridge=(q-1.0f)/.16f;
                land+=c.Depth*(-bowl*bowl+.31f*Mathf.Exp(-ridge*ridge));
            }
            return land*siteBlend;
        }

        private static Mesh SurfaceMesh()
        {
            var craters=Craters();
            // Metre-scale contact near the target, progressively wider cells toward the mountain horizon.
            var positive=new List<float>{0};
            for (float p=0;p<2100;)
            {
                p+=p<100?2:p<230?4:p<500?8:p<1100?17:32;
                positive.Add(p);
            }
            var axis=new List<float>(positive.Count*2-1);
            for (int i=positive.Count-1;i>0;i--) axis.Add(-positive[i]);
            axis.AddRange(positive);
            int n=axis.Count;
            var v=new Vector3[n*n]; var uv=new Vector2[v.Length]; var colors=new Color32[v.Length];
            var triangles=new int[(n-1)*(n-1)*6]; int k=0;
            for(int z=0;z<n;z++) for(int x=0;x<n;x++)
            {
                int i=z*n+x;
                float px=axis[x], pz=axis[z];
                float height=Height(px,pz,craters);
                if (!float.IsFinite(height)) throw new InvalidOperationException($"Non-finite terrain height at ({px}, {pz}).");
                v[i]=new Vector3(px,height,pz);
                uv[i]=new Vector2(px*.02f,pz*.02f);
                colors[i]=new Color32(255,255,255,255);
                if(x+1==n||z+1==n)continue;
                triangles[k++]=i; triangles[k++]=i+n; triangles[k++]=i+1;
                triangles[k++]=i+1; triangles[k++]=i+n; triangles[k++]=i+n+1;
            }
            var mesh=new Mesh {name="Landing terrain",indexFormat=IndexFormat.UInt32,vertices=v,uv=uv,colors32=colors,triangles=triangles};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return StoreMesh(mesh,Site+"Landing terrain.asset");
        }

        private static void AddStation(Transform environment)
        {
            // The same exterior prefab appears during evacuation from this station.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ImportStationGeometry.ExteriorPrefabPath);
            if (source == null) throw new InvalidOperationException("Missing the station exterior used on the lunar surface.");
            var station = (GameObject)PrefabUtility.InstantiatePrefab(source, environment);
            station.transform.localPosition = StationPosition;
            station.transform.localRotation = Quaternion.Euler(0, 42, 0);
            foreach (var filter in station.GetComponentsInChildren<MeshFilter>(true))
            {
                // Four pressure shells, the level solar arrays and the communications mast have separate mesh bounds.
                if (!filter.name.EndsWith("__LB_ThermalFabric", StringComparison.Ordinal) &&
                    filter.name != "08_PowerAndComms__LB_SolarCells" && filter.name != "08_PowerAndComms__LB_PaintedAlloy")
                    continue;
                var bounds = filter.sharedMesh.bounds;
                var collider = filter.gameObject.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
        }

        private static void AddBoulders(Transform environment)
        {
            var stones=new GameObject("Lunar breccia outcrops").transform;
            stones.SetParent(environment,false);
            var craters=Craters();
            var material=AssetDatabase.LoadAssetAtPath<Material>(Detail+"Lunar Angular Breccia.mat");
            var random=new System.Random(411);
            var meshes=new Mesh[6];
            for(int i=0;i<meshes.Length;i++)meshes[i]=AssetDatabase.LoadAssetAtPath<Mesh>(Detail+"Meshes/Lunar Breccia 0"+(i+1)+" LOD1.asset");
            var pieces=new List<CombineInstance>();
            for(int cluster=0;cluster<34;cluster++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2, distance=23+Mathf.Pow((float)random.NextDouble(),1.7f)*360;
                float cx=Mathf.Cos(a)*distance,cz=Mathf.Sin(a)*distance;
                for(int i=0;i<5;i++)
                {
                    float x=cx+(float)random.NextDouble()*12-6,z=cz+(float)random.NextDouble()*12-6;
                    if(x*x+z*z<21*21 || Mathf.Abs(x-StationPosition.x)<23 && Mathf.Abs(z-StationPosition.z)<25) continue;
                    float width=(.42f+2.1f*Mathf.Pow((float)random.NextDouble(),2.2f))*(i==0?1.8f:1);
                    var size=new Vector3(width,width*(.55f+(float)random.NextDouble()*.3f),width*.81f);
                    var pos=new Vector3(x,Height(x,z,craters)-.1f*width,z);
                    var rot=Quaternion.Euler(8*(float)random.NextDouble(),360*(float)random.NextDouble(),11*(float)random.NextDouble());
                    var mesh=meshes[random.Next(meshes.Length)];
                    pieces.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(pos,rot,size)});
                }
            }
            var rockMesh=new Mesh {name="Landing breccia",indexFormat=IndexFormat.UInt32};
            rockMesh.CombineMeshes(pieces.ToArray());rockMesh.RecalculateBounds();
            var rocks=BuildLunarTerrain.MeshObject(stones,"Breccia field",StoreMesh(rockMesh,Site+"Landing breccia.asset"),material);
            rocks.AddComponent<MeshCollider>().sharedMesh=rocks.GetComponent<MeshFilter>().sharedMesh;
        }

        private static Mesh StoreMesh(Mesh mesh,string path)
        {
            var vertices=mesh.vertices;
            foreach (var v in vertices)
                if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))
                    throw new InvalidOperationException("Non-finite vertices in " + path);
            var bounds=mesh.bounds;
            if (!float.IsFinite(bounds.size.sqrMagnitude) || !float.IsFinite(bounds.center.sqrMagnitude) || bounds.size.sqrMagnitude<=0)
                throw new InvalidOperationException("Invalid bounds in " + path);
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) {AssetDatabase.CreateAsset(mesh,path);return mesh;}
            // Preserve the scene's asset GUID while replacing every vertex and the old native collision data.
            existing.Clear(false);
            existing.indexFormat=mesh.indexFormat;
            existing.vertices=vertices;
            existing.normals=mesh.normals;
            existing.uv=mesh.uv;
            existing.colors32=mesh.colors32;
            existing.triangles=mesh.triangles;
            existing.bounds=bounds;
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void AddMarkings(Transform target)
        {
            var paint=AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Art/StationExpansion/Materials/EX_White.mat");
            var strip=AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Materials/Safety Amber.mat");
            var lamp=AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Art/StationExpansion/Materials/EX_Light.mat");
            var baseMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Art/StationExpansion/Materials/EX_Trim.mat");
            void Block(string name,Vector3 position,Vector3 size,float heading,Material mat)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
                obj.name=name;obj.transform.SetParent(target,false);obj.transform.localPosition=position;
                obj.transform.localScale=size;obj.transform.localRotation=Quaternion.Euler(0,heading,0);
                obj.GetComponent<MeshRenderer>().sharedMaterial=mat;
            }
            // Ground markings are flush and have no colliders. The four short edge lamps sit beyond the clear disk.
            for(int i=0;i<32;i++)
            {
                float angle=i*360f/32;
                var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                Block("Perimeter stripe",direction*(ArtemisLanding.TargetRadius+.15f)+Vector3.up*.018f,new Vector3(1.92f,.015f,.27f),angle,i%4==0?strip:paint);
            }
            foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
            {
                var foot=new Vector3(x*12.5f,0,z*12.5f);
                Block("Beacon foot",foot+Vector3.up*.13f,new Vector3(.68f,.26f,.68f),0,baseMaterial);
                Block("Beacon body",foot+Vector3.up*.47f,new Vector3(.26f,.59f,.26f),0,strip);
                Block("Beacon lens",foot+Vector3.up*.84f,new Vector3(.36f,.21f,.36f),0,lamp);
            }
            foreach(int x in new[]{-1,1})
                Block("Landing H stem",new Vector3(x*2,.018f,0),new Vector3(.57f,.018f,6.3f),0,paint);
            Block("Landing H bridge",new Vector3(0,.018f,0),new Vector3(4.5f,.018f,.57f),0,paint);
        }
    }
}

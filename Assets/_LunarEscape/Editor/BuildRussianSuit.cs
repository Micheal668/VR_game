using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // 俄罗斯舱外服的独立外观资源；沿用原骨骼，旧教学场景与原始模型不受影响。
    public static class BuildRussianSuit
    {
        public const string Root = "Assets/_LunarEscape/Art/RussianSuit";
        public const string PrefabPath = Root + "/Russian Orlan Suit.prefab";
        private const int Shell = 14, Navy = 15, Red = 16, Metal = 17, Crest = 18, Russia = 19, Label = 20, Glass = 21;

        [MenuItem("Lunar Escape/Install Russian Spacesuit and BMSTU Crest (Both Days)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Import(true);
            BuildDayOneScene.Build();
            Debug.Log("RUSSIAN_SUIT_INSTALLED_BOTH_DAYS");
        }

        public static GameObject Import(bool rebuild = false)
        {
            var cached = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (cached != null && !rebuild) return cached;
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var source = PrefabUtility.LoadPrefabContents(BuildCrewSuit.PrefabPath);
            try
            {
                var rig = source.GetComponent<CrewSuitRig>();
                var materials = Materials(rig.Body.sharedMaterials);
                var body = new Skin(rig.Body.sharedMesh, materials.Length);
                var helmet = new Skin(rig.Helmet.sharedMesh, materials.Length);

                // 半硬胸甲、胸前控制盒和背入式生命维持背包构成俄式舱外服轮廓。
                body.Box(new(0, 1.275f, -.077f), new(.39f, .33f, .34f), .042f, Shell, 1);
                body.Box(new(0, 1.245f, -.335f), new(.43f, .49f, .24f), .045f, Shell, 1);
                body.Box(new(0, 1.245f, -.459f), new(.365f, .41f, .014f), .007f, Navy, 1);
                body.Box(new(0, 1.255f, -.472f), new(.335f, .365f, .016f), .008f, Shell, 1);
                foreach (float x in new[] { -.175f, .175f })
                {
                    body.Box(new(x, 1.24f, -.482f), new(.025f, .31f, .018f), .006f, Red, 1);
                    foreach (float y in new[] { 1.1f, 1.39f })
                        body.Box(new(x, y, -.49f), new(.05f, .036f, .022f), .006f, Metal, 1);
                }
                body.Box(new(0, 1.114f, .12f), new(.31f, .12f, .078f), .018f, Navy, 1);
                body.Box(new(0, 1.114f, .163f), new(.28f, .095f, .014f), .007f, Shell, 1);
                body.Quad(new(0, 1.137f, .174f), new(.15f, .032f), Label, 1);
                foreach (float x in new[] { -.103f, .103f })
                {
                    body.Cylinder(new(x, 1.105f, .183f), .027f, .023f, Metal, 1, Quaternion.Euler(90, 0, 0));
                    body.Cylinder(new(x, 1.105f, .197f), .019f, .012f, x < 0 ? Red : Navy, 1, Quaternion.Euler(90, 0, 0));
                }
                body.Box(new(0, 1.099f, .178f), new(.095f, .024f, .012f), .004f, Glass, 1);
                // 校徽单独使用学校原始透明 PNG，保持比例、颜色和完整图案。
                body.Box(new(0, 1.321f, .104f), new(.181f, .184f, .012f), .012f, Navy, 1);
                body.Box(new(0, 1.321f, .112f), new(.17f, .173f, .008f), .008f, Shell, 1);
                body.Quad(new(0, 1.321f, .117f), new(.163f, .163f), Crest, 1);
                body.Quad(new(.13f, 1.351f, .101f), new(.071f, .0426f), Russia, 1);
                body.Quad(new(0, 1.30f, -.483f), new(.16f, .096f), Russia, 1, Quaternion.Euler(0, 180, 0));

                foreach (float side in new[] { -1f, 1f })
                {
                    var axis = new Vector3(side * .09f, -.21f, .05f).normalized;
                    body.SurfaceStripe(new(side * .286f, 1.282f, -.074f), axis, .034f, Red);
                    body.SurfaceStripe(new(side * .146f, .585f, -.091f), Vector3.up, .033f, Red);
                    body.SurfacePatch(new(side * .29f, 1.334f, .026f), new(.078f, .0468f), Russia, Quaternion.Euler(0, 0, side * 17));
                    // 连接软管绑定胸骨，不改变手部追踪或碰撞范围。
                    var hose = new[] { new Vector3(side * .148f, 1.117f, .153f), new Vector3(side * .203f, 1.04f, .145f),
                        new Vector3(side * .225f, .98f, .07f), new Vector3(side * .224f, 1.02f, -.07f), new Vector3(side * .19f, 1.09f, -.245f) };
                    body.Hose(hose, .012f, Navy, 1);
                }
                helmet.Torus(new(0, 1.473f, -.076f), .146f, .011f, Metal, 2);
                foreach (float x in new[] { -.108f, .108f })
                {
                    helmet.Box(new(x, 1.737f, .032f), new(.056f, .039f, .048f), .012f, Navy, 2);
                    helmet.Box(new(x, 1.739f, .059f), new(.041f, .022f, .01f), .006f, Glass, 2);
                }

                rig.Body.sharedMesh = Save(body.Build("Russian Suit Body"), Root + "/Russian Suit Body.asset");
                rig.Helmet.sharedMesh = Save(helmet.Build("Russian Helmet"), Root + "/Russian Helmet.asset");
                rig.Body.sharedMaterials = rig.Helmet.sharedMaterials = materials;
                source.name = "Russian Orlan Spacesuit - BMSTU";
                var result = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
                AssetDatabase.SaveAssets(); return result;
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
        }

        // 在复制第一天之前应用；挂架、玩家、地面/船内指挥官和返航乘员使用同一套外观。
        public static void ApplyToCurrentScene()
        {
            var rig = Import().GetComponent<CrewSuitRig>();
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(BuildCrewSuit.PrefabPath).GetComponent<CrewSuitRig>();
            foreach (var current in Object.FindObjectsByType<CrewSuitRig>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (current.Body.sharedMesh != original.Body.sharedMesh && current.Body.sharedMesh != rig.Body.sharedMesh) continue;
                CopySkin(current.Body, rig.Body); CopySkin(current.Helmet, rig.Helmet);
                if (current.TryGetComponent<TrackedCrewSuit>(out var tracked))
                {
                    tracked.ConfigureChestClearance(.14f); EditorUtility.SetDirty(tracked);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tracked);
                }
                if (current.name == "Hanging Emergency Suit" || current.name == "Hanging Commander Suit")
                {
                    // 新背包比原软服更厚；给挂架背板留出实际空间，重复安装保持相同位置。
                    var position = current.transform.localPosition; position.z = -.39f;
                    current.transform.localPosition = position; EditorUtility.SetDirty(current.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(current.transform);
                }
            }
            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                bool body = path.EndsWith("/Seated Suit Body.asset") || path.EndsWith("/Seated Russian Suit Body.asset");
                bool head = path.EndsWith("/Seated Helmet.asset") || path.EndsWith("/Seated Russian Helmet.asset");
                if (!body && !head) continue;
                var skin = body ? rig.Body : rig.Helmet;
                filter.sharedMesh = Seated(skin.sharedMesh);
                filter.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(filter.GetComponent<MeshRenderer>());
            }
            AssetDatabase.SaveAssets();
        }
        private static void CopySkin(SkinnedMeshRenderer target, SkinnedMeshRenderer source)
        {
            target.sharedMesh = source.sharedMesh; target.sharedMaterials = source.sharedMaterials;
            EditorUtility.SetDirty(target); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private static Mesh Seated(Mesh source)
        {
            var mesh = Object.Instantiate(source); mesh.name = "Seated " + source.name;
            var vertices = mesh.vertices; var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i]; var n = normals[i]; float y = v.y;
                if (y >= .9f) { v.y = .60f + (y - .9f) * .91f; v.z -= (y - .9f) * .41f; n = Quaternion.Euler(-24, 0, 0) * n; }
                else if (y >= .45f) { v.y = .60f - (.9f - y) * .15f; v.z += .9f - y; n = Quaternion.Euler(-81, 0, 0) * n; }
                else { v.y = .5325f - (.45f - y); v.z += .45f; }
                vertices[i] = v; normals[i] = n;
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
            return Save(mesh, Root + "/" + mesh.name + ".asset");
        }

        private static Material[] Materials(Material[] original)
        {
            var colors = new[] { new Color(.72f,.74f,.71f), new Color(.03f,.055f,.08f), new Color(.36f,.40f,.43f), new Color(.3f,.33f,.35f),
                new Color(.87f,.87f,.81f), new Color(.035f,.065f,.11f), new Color(.30f,.20f,.075f), new Color(.48f,.52f,.55f),
                new Color(.59f,.035f,.035f), new Color(.83f,.84f,.80f), new Color(.026f,.065f,.12f), new Color(.035f,.052f,.075f),
                new Color(.09f,.11f,.13f), new Color(.78f,.80f,.76f) };
            var list = new List<Material>();
            for (int i = 0; i < original.Length; i++)
            {
                var material = new Material(original[i]) { name = "Orlan Base " + i };
                material.SetColor("_BaseColor", colors[i]);
                material.SetFloat("_Smoothness", i == 6 ? .91f : (i == 2 || i == 7 ? .52f : .17f));
                material.SetFloat("_Metallic", i == 6 ? .82f : (i == 2 || i == 7 ? .65f : 0));
                list.Add(Save(material, Root + "/" + material.name + ".mat"));
            }
            list.Add(Mat("Orlan Thermal Shell", new(.88f,.89f,.84f), .18f));
            list.Add(Mat("Orlan Navy Trim", new(.024f,.055f,.10f), .27f));
            list.Add(Mat("Orlan Red Identification", new(.67f,.035f,.045f), .2f));
            list.Add(Mat("Orlan Metal Fittings", new(.47f,.51f,.55f), .58f, .7f));
            list.Add(Decal("BMSTU Chest Crest", "BMSTU-Crest.png"));
            list.Add(Decal("Russia Identification", "Russia-Identification.png"));
            list.Add(Decal("Orlan Control Label", "Orlan-Control-Label.png"));
            list.Add(Mat("Orlan Instrument Glass", new(.16f,.32f,.34f), .7f, .18f));
            return list.ToArray();
        }
        private static Material Mat(string name, Color color, float smooth, float metal = 0)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            return Save(m, Root + "/" + name + ".mat");
        }
        private static Material Decal(string name, string file)
        {
            string path = Root + "/" + file;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var m = Mat(name, Color.white, .17f); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .25f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450; EditorUtility.SetDirty(m); return m;
        }
        private static T Save<T>(T asset, string path) where T : Object
        {
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous == null) AssetDatabase.CreateAsset(asset, path);
            else { EditorUtility.CopySerialized(asset, previous); Object.DestroyImmediate(asset); asset = previous; EditorUtility.SetDirty(asset); }
            return asset;
        }

        private sealed class Skin
        {
            private readonly List<Vector3> vertices, normals;
            private readonly List<Vector2> uv;
            private readonly List<BoneWeight> weights;
            private readonly List<int>[] triangles;
            private readonly Matrix4x4[] bindposes;
            public Skin(Mesh source, int count)
            {
                vertices = source.vertices.ToList(); normals = source.normals.ToList(); uv = source.uv.ToList();
                weights = source.boneWeights.ToList(); bindposes = source.bindposes;
                triangles = Enumerable.Range(0, count).Select(i => i < source.subMeshCount ? source.GetTriangles(i).ToList() : new List<int>()).ToArray();
            }
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, subMeshCount = triangles.Length };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.boneWeights = weights.ToArray(); mesh.bindposes = bindposes;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds(); return mesh;
            }
            private int Vertex(Vector3 p, Vector3 n, Vector2 tex, int bone)
            {
                int index = vertices.Count; vertices.Add(p); normals.Add(n); uv.Add(tex);
                weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 }); return index;
            }
            public void Quad(Vector3 center, Vector2 size, int material, int bone, Quaternion rotation = default)
            {
                if (rotation == default) rotation = Quaternion.identity;
                int first = vertices.Count;
                foreach (var p in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) })
                    Vertex(center + rotation * new Vector3((p.x - .5f) * size.x, (p.y - .5f) * size.y, 0), rotation * Vector3.forward, new Vector2(1 - p.x, p.y), bone);
                triangles[material].AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            private readonly struct SurfacePoint
            {
                public readonly Vector3 position, normal;
                public readonly BoneWeight weight;
                public SurfacePoint(Vector3 p, Vector3 n, BoneWeight w) { position = p; normal = n; weight = w; }
            }
            private static BoneWeight Blend(BoneWeight a, BoneWeight b, float blend)
            {
                var values = new Dictionary<int, float>();
                void Add(BoneWeight w, float scale)
                {
                    foreach (var (bone, amount) in new[] { (w.boneIndex0,w.weight0), (w.boneIndex1,w.weight1), (w.boneIndex2,w.weight2), (w.boneIndex3,w.weight3) })
                        if (amount > 0) { values.TryGetValue(bone, out float value); values[bone] = value + amount * scale; }
                }
                Add(a, 1 - blend); Add(b, blend);
                var pairs = values.OrderByDescending(p => p.Value).Take(4).ToArray(); float sum = pairs.Sum(p => p.Value);
                var result = new BoneWeight();
                if (pairs.Length > 0) { result.boneIndex0 = pairs[0].Key; result.weight0 = pairs[0].Value / sum; }
                if (pairs.Length > 1) { result.boneIndex1 = pairs[1].Key; result.weight1 = pairs[1].Value / sum; }
                if (pairs.Length > 2) { result.boneIndex2 = pairs[2].Key; result.weight2 = pairs[2].Value / sum; }
                if (pairs.Length > 3) { result.boneIndex3 = pairs[3].Key; result.weight3 = pairs[3].Value / sum; }
                return result;
            }
            private static SurfacePoint Lerp(SurfacePoint a, SurfacePoint b, float value) => new(Vector3.Lerp(a.position,b.position,value), Vector3.Lerp(a.normal,b.normal,value).normalized, Blend(a.weight,b.weight,value));
            private SurfacePoint Point(int index) => new(vertices[index], normals[index], weights[index]);
            private void SurfaceVertex(SurfacePoint point, Vector2 tex)
            {
                vertices.Add(point.position + point.normal * .0014f); normals.Add(point.normal); uv.Add(tex); weights.Add(point.weight);
            }
            // 识别条裁切并贴合原软服表面，同时插值原骨骼权重，弯肘时不会变成悬浮硬环。
            public void SurfaceStripe(Vector3 center, Vector3 axis, float width, int material)
            {
                List<SurfacePoint> Clip(List<SurfacePoint> input, float side)
                {
                    var output = new List<SurfacePoint>();
                    for (int i = 0; i < input.Count; i++)
                    {
                        var a = input[i]; var b = input[(i + 1) % input.Count];
                        float da = Vector3.Dot(a.position - center,axis) * side - width * .5f;
                        float db = Vector3.Dot(b.position - center,axis) * side - width * .5f;
                        if (da <= 0) output.Add(a);
                        if ((da <= 0) != (db <= 0)) output.Add(Lerp(a,b,da / (da - db)));
                    }
                    return output;
                }
                var cloth = triangles[9];
                for (int i = 0; i < cloth.Count; i += 3)
                {
                    var a = Point(cloth[i]); var b = Point(cloth[i+1]); var c = Point(cloth[i+2]);
                    var polygon = Clip(Clip(new List<SurfacePoint> { a,b,c },1),-1);
                    float side = Mathf.Sign(center.x), edge = center.y > 1.2f ? .215f : .005f;
                    var clipped = new List<SurfacePoint>();
                    for (int j=0;j<polygon.Count;j++)
                    {
                        var p=polygon[j];var q=polygon[(j+1)%polygon.Count];
                        float dp=p.position.x*side-edge,dq=q.position.x*side-edge;
                        if(dp>=0)clipped.Add(p);
                        if((dp>=0)!=(dq>=0))clipped.Add(Lerp(p,q,dp/(dp-dq)));
                    }
                    polygon=clipped;
                    if (polygon.Count < 3) continue;
                    int first = vertices.Count;
                    foreach (var p in polygon) SurfaceVertex(p,Vector2.zero);
                    for (int j = 1; j < polygon.Count - 1; j++) triangles[material].AddRange(new[] { first,first+j,first+j+1 });
                }
            }
            // 袖章沿正面投影到服装三角面，保留织物褶皱和原有手臂蒙皮。
            public void SurfacePatch(Vector3 center, Vector2 size, int material, Quaternion rotation)
            {
                var inverse = Quaternion.Inverse(rotation);
                List<SurfacePoint> Clip(List<SurfacePoint> input, Vector3 axis, float edge)
                {
                    var output = new List<SurfacePoint>();
                    for (int i=0;i<input.Count;i++)
                    {
                        var a=input[i];var b=input[(i+1)%input.Count];
                        float da=Vector3.Dot(inverse*(a.position-center),axis)-edge;
                        float db=Vector3.Dot(inverse*(b.position-center),axis)-edge;
                        if(da<=0)output.Add(a);
                        if((da<=0)!=(db<=0))output.Add(Lerp(a,b,da/(da-db)));
                    }
                    return output;
                }
                var cloth=triangles[9];
                for(int i=0;i<cloth.Count;i+=3)
                {
                    var a=Point(cloth[i]);var b=Point(cloth[i+1]);var c=Point(cloth[i+2]);
                    if((a.normal.z+b.normal.z+c.normal.z)/3<.08f)continue;
                    var polygon=new List<SurfacePoint>{a,b,c};
                    polygon=Clip(Clip(polygon,Vector3.right,size.x*.5f),Vector3.left,size.x*.5f);
                    polygon=Clip(Clip(polygon,Vector3.up,size.y*.5f),Vector3.down,size.y*.5f);
                    if(polygon.Count<3)continue;
                    int first=vertices.Count;
                    foreach(var p in polygon)
                    {var local=inverse*(p.position-center);SurfaceVertex(p,new Vector2(.5f-local.x/size.x,.5f+local.y/size.y));}
                    for(int j=1;j<polygon.Count-1;j++)triangles[material].AddRange(new[]{first,first+j,first+j+1});
                }
            }
            public void Box(Vector3 center, Vector3 size, float radius, int material, int bone)
            {
                var half = size * .5f; radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
                var inner = half - Vector3.one * radius;
                var faces = new[] { (Vector3.right, Vector3.back, Vector3.up), (Vector3.left, Vector3.forward, Vector3.up),
                    (Vector3.up, Vector3.right, Vector3.back), (Vector3.down, Vector3.right, Vector3.forward),
                    (Vector3.forward, Vector3.right, Vector3.up), (Vector3.back, Vector3.left, Vector3.up) };
                const int count = 8;
                foreach (var (normal, u, v) in faces)
                {
                    int first = vertices.Count;
                    for (int y = 0; y <= count; y++) for (int x = 0; x <= count; x++)
                    {
                        var p = Vector3.Scale(normal + u * (x * 2f / count - 1) + v * (y * 2f / count - 1), half);
                        var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var n = (p - q).normalized; Vertex(center + q + n * radius, n, new Vector2((float)x / count, (float)y / count), bone);
                    }
                    for (int y = 0; y < count; y++) for (int x = 0; x < count; x++)
                    { int a = first + y * (count + 1) + x, b = a + count + 1; triangles[material].AddRange(new[] { a, a + 1, b + 1, a, b + 1, b }); }
                }
            }
            public void Cylinder(Vector3 center, float radius, float height, int material, int bone, Quaternion rotation)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var mesh = obj.GetComponent<MeshFilter>().sharedMesh; int first = vertices.Count;
                var points = mesh.vertices; var norms = mesh.normals; var tex = mesh.uv;
                for (int i = 0; i < points.Length; i++) Vertex(center + rotation * Vector3.Scale(points[i], new Vector3(radius * 2, height * .5f, radius * 2)), rotation * norms[i], tex[i], bone);
                triangles[material].AddRange(mesh.triangles.Select(i => i + first)); Object.DestroyImmediate(obj);
            }
            public void Hose(Vector3[] points, float radius, int material, int bone)
            {
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var axis = points[i + 1] - points[i]; var rotation = Quaternion.FromToRotation(Vector3.up, axis);
                    Cylinder((points[i] + points[i + 1]) * .5f, radius, axis.magnitude + .012f, material, bone, rotation);
                    for (int j = 0; j <= 5; j++) Cylinder(Vector3.Lerp(points[i], points[i + 1], j / 5f), radius * 1.22f, .004f, Metal, bone, rotation);
                }
            }
            public void Torus(Vector3 center, float major, float minor, int material, int bone)
            {
                const int rings = 40, sides = 8; int first = vertices.Count;
                for (int i = 0; i <= rings; i++) for (int j = 0; j <= sides; j++)
                {
                    float a = i * Mathf.PI * 2 / rings, b = j * Mathf.PI * 2 / sides;
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    Vertex(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * major + n * minor, n, new Vector2((float)i / rings, (float)j / sides), bone);
                }
                for (int i = 0; i < rings; i++) for (int j = 0; j < sides; j++)
                { int a = first + i * (sides + 1) + j, b = a + sides + 1; triangles[material].AddRange(new[] { a, a + 1, b + 1, a, b + 1, b }); }
            }
        }
    }
}

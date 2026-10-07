using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // All textures, topology, stone instances and LODs are baked in the editor. No runtime generator.
    public static class BuildRealisticLunarSurface
    {
        public const string Root = "Assets/_LunarEscape/Art/LunarSurfaceDetail";
        public const string ModelRoot = "Assets/_LunarEscape/Models/LunarSurface";
        public const string GroundRocksName = "Realistic Lunar Rocks";
        public const string FlightRocksName = "Flight Realistic Lunar Rocks";
        private const int TextureSize = 1024;
        private const string AlbedoPath = Root + "/regolith_albedo_roughness.png";
        private const string NormalPath = Root + "/regolith_normal.png";
        private static readonly List<Mesh> GeneratedMeshes = new();

        [MenuItem("Lunar Escape/Install Realistic Lunar Surface")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            ArtResourceAudit.ReportAfter();
            Debug.Log("REALISTIC_LUNAR_SURFACE_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            var session = Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            if (session == null) throw new InvalidOperationException("The station session is missing.");
            // A full main-scene rebuild starts from 09 and saves as 10 after adding DockingMission.
            if (session.GetComponent<DockingMission>() == null)
                throw new InvalidOperationException("The detailed lunar surface requires the main docking scene.");
            var layout = session.GetComponent<LunarTerrainLayout>();
            var view = session.GetComponent<LunarViewLayout>();
            var presenter = session.GetComponent<FlightScenePresenter>();
            if (layout == null || layout.GroundVisual == null || layout.FlightVisual == null)
                throw new InvalidOperationException("The continuous surface must be built first.");

            Directory.CreateDirectory(Root + "/Meshes");
            Directory.CreateDirectory(ModelRoot);
            AssetDatabase.Refresh();
            GeneratedMeshes.Clear();
            BakeTextures();
            var material = SurfaceMaterial();
            var rocksMaterial = RockMaterial();
            var near = NearMesh();
            var far = FarMesh();
            foreach (var terrain in new[] { layout.GroundVisual, layout.FlightVisual })
            {
                Find(terrain.transform, "Walkable crater terrain").GetComponent<MeshFilter>().sharedMesh = near;
                Find(terrain.transform, "Distant crater ridges").GetComponent<MeshFilter>().sharedMesh = far;
                foreach (var renderer in terrain.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }
            }
            // Keep a single shared near mesh for rendering and collision, so there is no height mismatch.
            layout.WalkableSurface.sharedMesh = null;
            layout.WalkableSurface.sharedMesh = near;
            foreach (var collider in layout.FlightVisual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            RemoveNamed(presenter.GroundRoot.transform, GroundRocksName);
            RemoveNamed(view.OutsideWorld, FlightRocksName);
            foreach (var old in presenter.GroundRoot.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("Moon Boulder ", StringComparison.Ordinal)).ToArray())
                Object.DestroyImmediate(old.gameObject);

            var stoneMeshes = new Mesh[6, 2];
            for (int variant = 0; variant < 6; variant++)
                for (int lod = 0; lod < 2; lod++) stoneMeshes[variant, lod] = RockMesh(variant, lod);
            var placements = StonePlacements();
            BuildRocks(presenter.GroundRoot.transform, GroundRocksName, placements, stoneMeshes, rocksMaterial, true);
            BuildRocks(view.OutsideWorld, FlightRocksName, placements, stoneMeshes, rocksMaterial, false);
            var continuousView = presenter.FlightWorld.GetComponent<ContinuousMoonView>();
            if (continuousView != null)
                continuousView.Configure(session.GetComponent<AscentMission>(), view.OutsideWorld,
                    layout.FlightVisual.GetComponentsInChildren<Renderer>(true));
            WriteManifest(near, far, placements, material, rocksMaterial);
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            AssetDatabase.SaveAssets();
            ReloadAndValidateSurfaceMaterial();
            Debug.Log($"REALISTIC_LUNAR_SURFACE_READY near={near.triangles.Length / 3} far={far.triangles.Length / 3} stones={placements.Count} textureBCMipMiB=2.667");
        }

        private static Mesh NearMesh()
        {
            const int nx = 192, nz = 160;
            var vertices = new Vector3[(nx + 1) * (nz + 1)];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var indices = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                float px = x - 71, pz = z - 80;
                float height = NearVertexHeight(px, pz);
                int i = z * (nx + 1) + x;
                vertices[i] = new Vector3(px, height, pz);
                uv[i] = new Vector2(px * .02f, pz * .02f);
                colors[i] = new Color32(255, 255, 255, 255);
                if (x == nx || z == nz) continue;
                int j = (z * nx + x) * 6;
                indices[j] = i; indices[j + 1] = i + nx + 1; indices[j + 2] = i + 1;
                indices[j + 3] = i + 1; indices[j + 4] = i + nx + 1; indices[j + 5] = i + nx + 2;
            }
            var mesh = new Mesh { name = "Detailed Lunar Terrain Near", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors32 = colors; mesh.triangles = indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MatchBoundaryNormals(mesh, true);
            return SaveMesh(mesh);
        }

        private static float NearVertexHeight(float x, float z)
        {
            float y = LunarSurfaceProfile.Height(x, z);
            if (x == -71 || x == 121)
            {
                float lo = Mathf.Floor(z / 8) * 8;
                y = Mathf.Lerp(LunarTerrainProfile.Height(x, lo), LunarTerrainProfile.Height(x, lo + 8), (z - lo) / 8);
            }
            if (z == -80 || z == 80)
            {
                float lo = Mathf.Floor((x - 25) / 8) * 8 + 25;
                y = Mathf.Lerp(LunarTerrainProfile.Height(lo, z), LunarTerrainProfile.Height(lo + 8, z), (x - lo) / 8);
            }
            return y;
        }

        private static Mesh FarMesh()
        {
            // 16 m interior grid; insert the original 8 m edge vertices on BOTH rectangular borders.
            // A centre fan in only those edge cells stitches fine/coarse topology without skirts or cracks.
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var vertexIndices = new Dictionary<long, int>();
            int Vertex(int x, int z)
            {
                long key = ((long)x << 32) ^ (uint)z;
                if (vertexIndices.TryGetValue(key, out int cached)) return cached;
                int index = vertices.Count; vertexIndices.Add(key, index);
                vertices.Add(new Vector3(x, LunarTerrainProfile.Height(x, z), z)); uv.Add(new Vector2(x * .02f, z * .02f));
                return index;
            }
            bool Border(int x0, int z0, int x1, int z1)
            {
                if (x0 == x1)
                    return x0 == -775 || x0 == 825 || ((x0 == -71 || x0 == 121) && z0 >= -80 && z0 <= 80 && z1 >= -80 && z1 <= 80);
                return z0 == -800 || z0 == 800 || ((z0 == -80 || z0 == 80) && x0 >= -71 && x0 <= 121 && x1 >= -71 && x1 <= 121);
            }
            for (int iz = 0; iz < 100; iz++) for (int ix = 0; ix < 100; ix++)
            {
                int x = -775 + ix * 16, z = -800 + iz * 16;
                if (Mathf.Abs(x + 8 - 25) < 96 && Mathf.Abs(z + 8) < 80) continue;
                var corners = new[] { new Vector2Int(x, z), new Vector2Int(x, z + 16), new Vector2Int(x + 16, z + 16), new Vector2Int(x + 16, z) };
                var polygon = new List<int>(8);
                for (int j = 0; j < 4; j++)
                {
                    var a = corners[j]; var b = corners[(j + 1) % 4];
                    polygon.Add(Vertex(a.x, a.y));
                    if (Border(a.x, a.y, b.x, b.y)) polygon.Add(Vertex((a.x + b.x) / 2, (a.y + b.y) / 2));
                }
                if (polygon.Count == 4)
                {
                    triangles.Add(polygon[0]); triangles.Add(polygon[1]); triangles.Add(polygon[3]);
                    triangles.Add(polygon[3]); triangles.Add(polygon[1]); triangles.Add(polygon[2]);
                }
                else
                {
                    int centre = Vertex(x + 8, z + 8);
                    for (int j = 0; j < polygon.Count; j++)
                    { triangles.Add(centre); triangles.Add(polygon[j]); triangles.Add(polygon[(j + 1) % polygon.Count]); }
                }
            }
            var mesh = new Mesh { name = "Detailed Lunar Terrain Far", indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(Enumerable.Repeat(new Color32(255, 255, 255, 255), vertices.Count).ToArray());
            mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MatchBoundaryNormals(mesh, false);
            return SaveMesh(mesh);
        }

        private static void MatchBoundaryNormals(Mesh mesh, bool near)
        {
            var vertices = mesh.vertices; var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                bool seam = (Mathf.Abs(Mathf.Abs(p.x - 25) - 96) < .01f && Mathf.Abs(p.z) <= 80) ||
                            (Mathf.Abs(Mathf.Abs(p.z) - 80) < .01f && Mathf.Abs(p.x - 25) <= 96);
                if (!seam) continue;
                // Share the original profile gradient as well as its edge positions.
                normals[i] = new Vector3(LunarTerrainProfile.Height(p.x - .5f, p.z) - LunarTerrainProfile.Height(p.x + .5f, p.z),
                    1, LunarTerrainProfile.Height(p.x, p.z - .5f) - LunarTerrainProfile.Height(p.x, p.z + .5f)).normalized;
            }
            mesh.normals = normals;
        }

        private static Material SurfaceMaterial()
        {
            var shader = Shader.Find("LunarEscape/Detailed Continuous Moon");
            if (shader == null) throw new InvalidOperationException("The detailed continuous moon shader has not imported.");
            string path = Root + "/Detailed Lunar Surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(shader);
            material.shader = shader;
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Materials/Continuous Lunar Surface.mat");
            if (original == null) throw new InvalidOperationException("The original continuous moon material is missing.");
            // Copy only the shared properties; do not replace this shader's serialized property table.
            material.SetColor("_BaseColor", original.GetColor("_BaseColor"));
            material.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
            material.SetTextureScale("_BaseMap", original.GetTextureScale("_BaseMap"));
            material.SetTextureOffset("_BaseMap", original.GetTextureOffset("_BaseMap"));
            foreach (string property in new[] { "_MoonRadius", "_UseMap", "_NoiseScale", "_Visibility", "_InvertFade", "_SrcBlend", "_DstBlend", "_ZWrite" })
                material.SetFloat(property, original.GetFloat(property));
            material.renderQueue = original.renderQueue;
            material.SetTexture("_DetailAlbedo", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
            material.SetTexture("_DetailNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
            material.SetFloat("_DetailScale", .37f); material.SetFloat("_NormalStrength", .7f);
            material.SetFloat("_SurfaceAngle", 0); material.SetFloat("_RenderScale", 1);
            material.enableInstancing = true;
            ValidateSurfaceBindings(material);
            if (create) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            // Later mesh creation/manifest imports must never see an unsaved material change.
            AssetDatabase.SaveAssetIfDirty(material);
            return ReloadAndValidateSurfaceMaterial();
        }

        private static Material ReloadAndValidateSurfaceMaterial()
        {
            string path = Root + "/Detailed Lunar Surface.mat";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            ValidateSurfaceBindings(material);
            return material;
        }

        private static void ValidateSurfaceBindings(Material material)
        {
            if (material == null || material.shader == null || material.shader.name != "LunarEscape/Detailed Continuous Moon")
                throw new InvalidOperationException("The saved detailed surface material has an invalid shader.");
            foreach (string property in new[] { "_DetailAlbedo", "_DetailNormal", "_DetailScale", "_NormalStrength" })
                if (!material.HasProperty(property)) throw new InvalidOperationException("The detailed surface shader is missing " + property);
            if (AssetDatabase.GetAssetPath(material.GetTexture("_DetailAlbedo")) != AlbedoPath ||
                AssetDatabase.GetAssetPath(material.GetTexture("_DetailNormal")) != NormalPath ||
                Mathf.Abs(material.GetFloat("_DetailScale") - .37f) > .00001f ||
                Mathf.Abs(material.GetFloat("_NormalStrength") - .7f) > .00001f)
                throw new InvalidOperationException("Detailed surface texture/scale bindings were not preserved after save/import.");
        }

        private static Material RockMaterial()
        {
            string path = Root + "/Lunar Angular Breccia.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", new Color(.63f, .62f, .6f));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
            material.SetFloat("_BumpScale", .65f); material.SetFloat("_Smoothness", .055f); material.SetFloat("_Metallic", 0);
            material.SetFloat("_SmoothnessTextureChannel", 0); material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void BakeTextures()
        {
            if (!File.Exists(AlbedoPath) || !File.Exists(NormalPath))
            {
                int count = TextureSize * TextureSize;
                var height = new float[count]; var tint = new float[count];
                for (int y = 0; y < TextureSize; y++) for (int x = 0; x < TextureSize; x++)
                {
                    float u = (float)x / TextureSize, v = (float)y / TextureSize;
                    float fine = PeriodicNoise(u, v, 256, 7) - .5f;
                    float sand = PeriodicNoise(u, v, 96, 3) - .5f;
                    float broad = PeriodicNoise(u, v, 8, 11) - .5f;
                    int i = y * TextureSize + x;
                    height[i] = fine * .16f + sand * .21f + broad * .14f;
                    tint[i] = .62f + fine * .11f + sand * .07f + broad * .04f;
                }
                var random = new System.Random(73291);
                // Tiny irregular pits and embedded angular grains, baked rather than extra mesh vertices.
                for (int grain = 0; grain < 6400; grain++)
                {
                    int cx = random.Next(TextureSize), cy = random.Next(TextureSize);
                    float radius = Mathf.Lerp(1.1f, 8.5f, Mathf.Pow((float)random.NextDouble(), 2.8f));
                    float squash = Mathf.Lerp(.5f, 1, (float)random.NextDouble());
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                    bool pit = grain % 4 == 0;
                    float value = pit ? -.08f : Mathf.Lerp(-.04f, .045f, (float)random.NextDouble());
                    int bounds = Mathf.CeilToInt(radius * 1.4f);
                    for (int dy = -bounds; dy <= bounds; dy++) for (int dx = -bounds; dx <= bounds; dx++)
                    {
                        float rx = (dx * cosine + dy * sine) / radius;
                        float ry = (-dx * sine + dy * cosine) / (radius * squash);
                        float q = pit ? Mathf.Sqrt(rx * rx + ry * ry) : Mathf.Max(Mathf.Abs(rx) * .85f + Mathf.Abs(ry) * .45f, Mathf.Abs(ry));
                        if (q >= 1.15f) continue;
                        int index = ((cy + dy + TextureSize) % TextureSize) * TextureSize + ((cx + dx + TextureSize) % TextureSize);
                        if (pit)
                        { height[index] -= .2f * Mathf.Max(0, 1 - q * q); height[index] += .07f * Mathf.Exp(-Mathf.Pow((q - 1) / .15f, 2)); }
                        else height[index] += .18f * Mathf.Max(0, 1 - q) + (q < .85f ? .07f : 0);
                        tint[index] += value * Mathf.Clamp01((1.15f - q) * 4);
                    }
                }
                var albedo = new Color32[count]; var normals = new Color32[count];
                for (int y = 0; y < TextureSize; y++) for (int x = 0; x < TextureSize; x++)
                {
                    int i = y * TextureSize + x;
                    float dx = height[y * TextureSize + (x + 1) % TextureSize] - height[y * TextureSize + (x + TextureSize - 1) % TextureSize];
                    float dy = height[((y + 1) % TextureSize) * TextureSize + x] - height[((y + TextureSize - 1) % TextureSize) * TextureSize + x];
                    var n = new Vector3(-dx * 4.5f, -dy * 4.5f, 1).normalized;
                    byte shade = (byte)Mathf.Clamp(Mathf.RoundToInt(tint[i] * 255), 0, 255);
                    albedo[i] = new Color32(shade, shade, (byte)Mathf.Max(0, shade - 1), (byte)Mathf.Clamp(Mathf.RoundToInt((.91f - height[i] * .09f) * 255), 218, 249));
                    normals[i] = new Color32((byte)((n.x * .5f + .5f) * 255), (byte)((n.y * .5f + .5f) * 255), (byte)((n.z * .5f + .5f) * 255), 255);
                }
                WriteTexture(AlbedoPath, albedo); WriteTexture(NormalPath, normals);
                AssetDatabase.Refresh();
            }
            ImportTexture(AlbedoPath, false); ImportTexture(NormalPath, true);
        }

        private static void WriteTexture(string path, Color32[] colors)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(colors); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void ImportTexture(string path, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.isReadable = false; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.maxTextureSize = TextureSize; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            var settings = importer.GetPlatformTextureSettings("Standalone");
            settings.overridden = true; settings.maxTextureSize = TextureSize;
            settings.format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            importer.SetPlatformTextureSettings(settings);
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = TextureSize; android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static float PeriodicNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix % period, iy % period, seed), Hash((ix + 1) % period, iy % period, seed), fx),
                Mathf.Lerp(Hash(ix % period, (iy + 1) % period, seed), Hash((ix + 1) % period, (iy + 1) % period, seed), fx), fy);
        }
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                value = (value ^ (value >> 13)) * 1274126177;
                return (value ^ (value >> 16)) / (float)uint.MaxValue;
            }
        }

        private static Mesh RockMesh(int variant, int lod)
        {
            float phi = (1 + Mathf.Sqrt(5)) * .5f;
            var positions = new List<Vector3>
            {
                new(-1,phi,0),new(1,phi,0),new(-1,-phi,0),new(1,-phi,0),new(0,-1,phi),new(0,1,phi),
                new(0,-1,-phi),new(0,1,-phi),new(phi,0,-1),new(phi,0,1),new(-phi,0,-1),new(-phi,0,1)
            };
            var faces = new List<int> { 0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
                3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1 };
            for (int i = 0; i < positions.Count; i++) positions[i] = positions[i].normalized;
            if (lod == 0)
            {
                var mids = new Dictionary<ulong, int>();
                int Mid(int a, int b)
                {
                    ulong key = ((ulong)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                    if (mids.TryGetValue(key, out int old)) return old;
                    int next = positions.Count; positions.Add((positions[a] + positions[b]).normalized); mids.Add(key, next); return next;
                }
                var subdivided = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    subdivided.AddRange(new[] { a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca });
                }
                faces = subdivided;
            }
            var rotation = Quaternion.Euler(17 + variant * 31, variant * 59, variant * 13);
            for (int i = 0; i < positions.Count; i++)
            {
                var p = rotation * positions[i];
                float shape = .45f + .075f * Mathf.Sin(p.x * 5 + variant * 1.7f) * Mathf.Cos(p.z * 4 - variant) + .035f * Mathf.Sin(p.y * 9 + variant);
                p *= shape;
                p.y = Mathf.Clamp(p.y, -.29f, .36f) * (variant % 2 == 0 ? .88f : 1.05f);
                p.x += p.y * (.16f - variant * .05f);
                positions[i] = p;
            }
            var vertices = new Vector3[faces.Count]; var normals = new Vector3[faces.Count]; var uv = new Vector2[faces.Count];
            for (int face = 0; face < faces.Count; face += 3)
            {
                var a = positions[faces[face]]; var b = positions[faces[face + 1]]; var c = positions[faces[face + 2]];
                var normal = Vector3.Cross(b - a, c - a).normalized;
                int axis = Mathf.Abs(normal.y) > .55f ? 1 : Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? 0 : 2;
                for (int corner = 0; corner < 3; corner++)
                {
                    int i = face + corner; var p = positions[faces[i]]; vertices[i] = p;
                    normals[i] = Vector3.Lerp(normal, new Vector3(p.x, p.y * 1.7f, p.z).normalized, .3f).normalized;
                    uv[i] = (axis == 1 ? new Vector2(p.x, p.z) : axis == 0 ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) * 1.8f + Vector2.one * .5f;
                }
            }
            var mesh = new Mesh { name = $"Lunar Breccia {variant + 1:00} LOD{lod}", vertices = vertices, normals = normals, uv = uv, triangles = Enumerable.Range(0, faces.Count).ToArray() };
            mesh.RecalculateTangents(); mesh.RecalculateBounds(); return SaveMesh(mesh);
        }

        private readonly struct Stone
        {
            public readonly Vector3 Position, Scale; public readonly Quaternion Rotation; public readonly int Variant, Size;
            public Stone(Vector3 position, Vector3 scale, Quaternion rotation, int variant, int size)
            { Position = position; Scale = scale; Rotation = rotation; Variant = variant; Size = size; }
        }

        private static List<Stone> StonePlacements()
        {
            var result = new List<Stone>(); var random = new System.Random(7841);
            var centres = new[] { new Vector2(25,16), new Vector2(44,-18), new Vector2(68,15), new Vector2(10,-17),
                new Vector2(-25,27),new Vector2(18,42),new Vector2(51,47),new Vector2(21,8.5f),new Vector2(18,-7),new Vector2(58,-11) };
            var radii = new[] { 8f, 11, 12, 6, 14, 19, 10, 2.4f, 2.6f, 2.7f };
            float Next() => (float)random.NextDouble();
            for (int size = 2; size >= 0; size--)
            {
                int target = size == 2 ? 8 : size == 1 ? 36 : 180;
                int accepted = 0;
                for (int attempt = 0; accepted < target && attempt < 10000; attempt++)
                {
                    float diameter = size == 2 ? Mathf.Lerp(.85f, 1.35f, Next()) : size == 1 ? Mathf.Lerp(.32f, .68f, Next()) : Mathf.Lerp(.08f, .28f, Mathf.Pow(Next(), 1.4f));
                    Vector2 p;
                    if (Next() < .8f)
                    {
                        int cluster = random.Next(centres.Length);
                        float angle = Next() * Mathf.PI * 2;
                        float radius = radii[cluster] * Mathf.Lerp(.86f, 1.8f, Mathf.Pow(Next(), 1.7f));
                        p = centres[cluster] + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    }
                    else p = new Vector2(Mathf.Lerp(-46, 91, Next()), Mathf.Lerp(-46, 55, Next()));
                    if (!LunarSurfaceProfile.IsClearForRock(p.x, p.y, diameter * .8f)) continue;
                    if (result.Any(s => Vector2.Distance(new Vector2(s.Position.x, s.Position.z), p) < (s.Scale.x + diameter) * .48f + .1f)) continue;
                    float slopeX = (LunarSurfaceProfile.Height(p.x + .4f, p.y) - LunarSurfaceProfile.Height(p.x - .4f, p.y)) / .8f;
                    float slopeZ = (LunarSurfaceProfile.Height(p.x, p.y + .4f) - LunarSurfaceProfile.Height(p.x, p.y - .4f)) / .8f;
                    if (slopeX * slopeX + slopeZ * slopeZ > 1.1f) continue;
                    var scale = new Vector3(diameter * Mathf.Lerp(.9f, 1.15f, Next()), diameter * Mathf.Lerp(.76f, 1.04f, Next()), diameter * Mathf.Lerp(.8f, 1.1f, Next()));
                    var rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-slopeX, 1, -slopeZ).normalized) * Quaternion.Euler(0, Next() * 360, 0);
                    // Sample the baked triangles, rather than float profile detail between mesh vertices.
                    var position = new Vector3(p.x, WalkableTriangleHeight(p.x, p.y) + diameter * .1f, p.y);
                    result.Add(new Stone(position, scale, rotation, random.Next(6), size)); accepted++;
                }
                if (accepted != target) throw new InvalidOperationException("Not enough safe, separated lunar stone positions.");
            }
            return result;
        }

        private static float WalkableTriangleHeight(float x, float z)
        {
            float x0 = Mathf.Floor(x), z0 = Mathf.Floor(z), fx = x - x0, fz = z - z0;
            float h00 = NearVertexHeight(x0, z0), h10 = NearVertexHeight(x0 + 1, z0), h01 = NearVertexHeight(x0, z0 + 1), h11 = NearVertexHeight(x0 + 1, z0 + 1);
            return fx + fz <= 1 ? h00 + fx * (h10 - h00) + fz * (h01 - h00) : h11 + (1 - fx) * (h01 - h11) + (1 - fz) * (h10 - h11);
        }

        private static void BuildRocks(Transform parent, string name, List<Stone> placements, Mesh[,] meshes, Material material, bool physical)
        {
            var root = new GameObject(name).transform; root.SetParent(parent, false);
            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var rock = new GameObject($"Lunar Breccia {i + 1:000}").transform; rock.SetParent(root, false);
                rock.SetLocalPositionAndRotation(placement.Position, placement.Rotation); rock.localScale = placement.Scale;
                var renderers = new Renderer[2];
                for (int lod = 0; lod < 2; lod++)
                {
                    var child = BuildLunarTerrain.MeshObject(rock, "Rock LOD" + lod, meshes[placement.Variant, lod], material);
                    var renderer = child.GetComponent<MeshRenderer>(); renderers[lod] = renderer;
                    renderer.shadowCastingMode = placement.Size == 0 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.receiveShadows = true; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                var group = rock.gameObject.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[] { new LOD(.025f, new[] { renderers[0] }), new LOD(placement.Size == 2 ? .001f : placement.Size == 1 ? .0025f : .004f, new[] { renderers[1] }) });
                group.RecalculateBounds();
                if (physical && placement.Size == 2)
                {
                    var collider = rock.gameObject.AddComponent<SphereCollider>(); collider.radius = .43f; collider.center = new Vector3(0, .02f, 0);
                }
            }
        }

        [Serializable] private sealed class ResourceManifest
        {
            public string generator = nameof(BuildRealisticLunarSurface), scope = "10_LunarStation_Docking only";
            public int nearVertices, nearTriangles, farVertices, farTriangles, previousNearFarTriangles = 140480;
            public int stoneInstancesPerView, largeStoneColliders, smallAndMediumStoneColliders = 0, flightColliders = 0;
            public int highestDetailStoneTrianglesPerView, lowestDetailStoneTrianglesPerView, uniqueMeshVertices, uniqueMeshTriangles;
            public long meshBuffersBytes, conservativeMeshCpuPlusGpuBytes;
            public int textureWidth = TextureSize, textureHeight = TextureSize, newTextureCount = 2;
            public double newTexturesBcGpuMiBWithMips = 2.667;
            public string textureReadWrite = "false", textureEncoding = "sRGB RGB albedo + linear roughness alpha, BC7; tangent-space normal, BC5; mips; Android ASTC6x6";
            public string[] materialPaths, meshPaths;
            public string globalMesh = "Original Continuous Moon.asset reused unchanged; not counted as new mesh memory.";
            public string physics = "Near render/collider share the exact same baked 1m triangles. Eight simple sphere colliders on large rocks; no small-rock or flight-proxy colliders.";
            public string meshCpuCopies = "Mesh assets retain CPU data for editor validation / collider cooking. Conservative byte estimate counts both CPU and GPU buffers; runtime generation is absent.";
        }

        private static void WriteManifest(Mesh near, Mesh far, List<Stone> placements, params Material[] materials)
        {
            var meshes = GeneratedMeshes.Distinct().ToArray();
            long bufferBytes = meshes.Sum(mesh => (long)mesh.vertexCount * mesh.GetVertexBufferStride(0) + (long)mesh.triangles.Length * (mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4));
            var report = new ResourceManifest
            {
                nearVertices = near.vertexCount, nearTriangles = near.triangles.Length / 3, farVertices = far.vertexCount, farTriangles = far.triangles.Length / 3,
                stoneInstancesPerView = placements.Count, largeStoneColliders = placements.Count(s => s.Size == 2),
                highestDetailStoneTrianglesPerView = placements.Count * 80, lowestDetailStoneTrianglesPerView = placements.Count * 20,
                uniqueMeshVertices = meshes.Sum(mesh => mesh.vertexCount), uniqueMeshTriangles = meshes.Sum(mesh => mesh.triangles.Length / 3),
                meshBuffersBytes = bufferBytes, conservativeMeshCpuPlusGpuBytes = bufferBytes * 2,
                materialPaths = materials.Select(AssetDatabase.GetAssetPath).ToArray(), meshPaths = meshes.Select(AssetDatabase.GetAssetPath).ToArray()
            };
            File.WriteAllText(ModelRoot + "/resource-manifest.json", JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ModelRoot + "/resource-manifest.json");
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = Root + "/Meshes/" + mesh.name + ".asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, cached); Object.DestroyImmediate(mesh); mesh = cached; EditorUtility.SetDirty(mesh); }
            GeneratedMeshes.Add(mesh); return mesh;
        }
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static void RemoveNamed(Transform root, string name)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray()) Object.DestroyImmediate(item.gameObject);
        }
    }
}

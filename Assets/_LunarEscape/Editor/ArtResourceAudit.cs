using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    // Read-only scene inventory. Asset identity, not renderer count, determines unique resources.
    public static class ArtResourceAudit
    {
        private const string DefaultPath = "Logs/orbiter-surface-resource-audit.json";

        [Serializable] private sealed class Report
        {
            public string createdUtc, scenePath, measurementNotes;
            public int uniqueMeshCount, uniqueMaterialCount, uniqueTextureCount;
            public long uniqueMeshVertices, uniqueMeshTriangles, meshEditorRuntimeBytes, textureEditorRuntimeBytes;
            public ScopeRecord[] scopes;
            public SharingRecord groundFlightSharing, rockGroundFlightSharing;
            public MeshRecord[] meshes;
            public MaterialRecord[] materials;
            public TextureRecord[] textures;
            public LodRecord[] lodGroups;
        }
        [Serializable] private sealed class ScopeRecord
        {
            public string name, hierarchyPath;
            public int rendererCount, activeRendererCount, colliderCount, lodGroupCount;
            public long rendererInstanceTrianglesAllLods, rendererInstanceVerticesAllLods;
            public string[] meshIds, materialIds, textureIds;
        }
        [Serializable] private sealed class MeshRecord
        {
            public string id, name, assetPath, indexFormat;
            public int vertices, subMeshes, vertexBuffers;
            public long triangles, editorRuntimeBytes, estimatedVertexBufferBytes, estimatedIndexBufferBytes;
            public bool readable;
            public Vector3 boundsCenter, boundsSize;
        }
        [Serializable] private sealed class MaterialRecord
        {
            public string id, name, assetPath, shader;
            public int renderQueue;
            public TextureSlot[] textureSlots;
        }
        [Serializable] private sealed class TextureSlot { public string property, textureId; }
        [Serializable] private sealed class TextureRecord
        {
            public string id, name, assetPath, dimension, graphicsFormat, textureFormat;
            public int width, height, mipmapCount;
            public bool readable, importedAsSrgb;
            public long editorRuntimeBytes, sourceFileBytes;
        }
        [Serializable] private sealed class LodRecord
        {
            public string hierarchyPath, fadeMode;
            public bool enabled, animateCrossFading;
            public float size;
            public Vector3 localReferencePoint;
            public LodLevel[] levels;
        }
        [Serializable] private sealed class LodLevel
        {
            public int level, renderers;
            public float screenRelativeTransitionHeight, fadeTransitionWidth;
            public long rendererInstanceTriangles, rendererInstanceVertices;
            public string[] meshIds;
        }
        [Serializable] private sealed class SharingRecord
        {
            public string groundPath, flightPath;
            public int groundMeshCount, flightMeshCount, sharedMeshCount, sharedMaterialCount, sharedTextureCount;
            public int flightColliderCount;
            public string[] sharedMeshIds, groundOnlyMeshIds, flightOnlyMeshIds;
        }

        [MenuItem("Lunar Escape/Audit Orbiter and Surface Resources")]
        public static void ReportCurrentScene() => WriteReport(DefaultPath);

        public static void ReportBefore() => WriteReport("Logs/orbiter-surface-resource-before.json");
        public static void ReportAfter() => WriteReport("Logs/orbiter-surface-resource-after.json");

        private static void WriteReport(string relativePath)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the scene to audit first.");
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var session = all.Select(child => child.GetComponent<StationMissionSession>()).FirstOrDefault(value => value != null);
            var presentation = session != null ? session.GetComponent<FlightScenePresenter>() : null;
            var terrain = session != null ? session.GetComponent<LunarTerrainLayout>() : null;
            var orbiter = all.Select(child => child.GetComponent<OrbiterView>()).FirstOrDefault(value => value != null);
            var groundRocks = all.FirstOrDefault(child => child.name == "Realistic Lunar Rocks");
            var flightRocks = all.FirstOrDefault(child => child.name == "Flight Realistic Lunar Rocks");
            var scopes = new List<(string name, Transform root)>();
            if (presentation != null && presentation.GroundRoot != null) scopes.Add(("groundEnvironment", presentation.GroundRoot.transform));
            if (terrain != null && terrain.GroundVisual != null) scopes.Add(("lunarSurfaceGround", terrain.GroundVisual.transform));
            if (terrain != null && terrain.FlightVisual != null) scopes.Add(("lunarSurfaceFlight", terrain.FlightVisual.transform));
            if (groundRocks != null) scopes.Add(("lunarRocksGround", groundRocks));
            if (flightRocks != null) scopes.Add(("lunarRocksFlight", flightRocks));
            if (orbiter != null && orbiter.Target != null) scopes.Add(("orbiter", orbiter.Target));
            if (scopes.Count == 0)
                foreach (var root in scene.GetRootGameObjects()) scopes.Add(("sceneRoot", root.transform));

            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            var groups = new HashSet<LODGroup>();
            var scopeRecords = new List<ScopeRecord>();
            foreach (var scope in scopes)
            {
                var renderers = scope.root.GetComponentsInChildren<Renderer>(true);
                var localMeshes = RenderMeshes(renderers).ToHashSet();
                foreach (var collider in scope.root.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.sharedMesh != null) localMeshes.Add(collider.sharedMesh);
                var localMaterials = Materials(renderers);
                var localTextures = Textures(localMaterials);
                var localGroups = scope.root.GetComponentsInChildren<LODGroup>(true);
                meshes.UnionWith(localMeshes); materials.UnionWith(localMaterials); textures.UnionWith(localTextures); groups.UnionWith(localGroups);
                scopeRecords.Add(new ScopeRecord
                {
                    name = scope.name, hierarchyPath = Hierarchy(scope.root), rendererCount = renderers.Length,
                    activeRendererCount = renderers.Count(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy),
                    colliderCount = scope.root.GetComponentsInChildren<Collider>(true).Length, lodGroupCount = localGroups.Length,
                    rendererInstanceTrianglesAllLods = RenderMeshes(renderers).Sum(Triangles),
                    rendererInstanceVerticesAllLods = RenderMeshes(renderers).Sum(mesh => (long)mesh.vertexCount),
                    meshIds = Ids(localMeshes), materialIds = Ids(localMaterials), textureIds = Ids(localTextures)
                });
            }

            var meshRecords = meshes.OrderBy(Id).Select(MeshInfo).ToArray();
            var textureRecords = textures.OrderBy(Id).Select(TextureInfo).ToArray();
            var report = new Report
            {
                createdUtc = DateTime.UtcNow.ToString("O"), scenePath = scene.path,
                measurementNotes = "Read-only inventory of referenced resources, including inactive objects and every LOD. " +
                    "Unique resources are deduplicated by Unity object/asset identity across overlapping scopes. " +
                    "Renderer-instance totals include all LODs simultaneously for inventory, not actual frame submissions. " +
                    "Profiler.GetRuntimeMemorySizeLong reports the currently loaded Editor resource allocation, not standalone-player or whole-process memory. " +
                    "Vertex/index buffer estimates count vertex strides and index widths only, excluding driver overhead and extra skinning/collision buffers. " +
                    "Texture Editor memory may include CPU copies and importer/Editor overhead; source file bytes are compressed disk bytes, not GPU residency. " +
                    "graphicsFormat, mipmapCount, readable and import settings are recorded to support platform-specific GPU estimates; no total GPU or VR frame-rate claim is made.",
                uniqueMeshCount = meshRecords.Length, uniqueMaterialCount = materials.Count, uniqueTextureCount = textureRecords.Length,
                uniqueMeshVertices = meshRecords.Sum(mesh => (long)mesh.vertices), uniqueMeshTriangles = meshRecords.Sum(mesh => mesh.triangles),
                meshEditorRuntimeBytes = meshRecords.Sum(mesh => mesh.editorRuntimeBytes), textureEditorRuntimeBytes = textureRecords.Sum(texture => texture.editorRuntimeBytes),
                scopes = scopeRecords.ToArray(), meshes = meshRecords, textures = textureRecords,
                materials = materials.OrderBy(Id).Select(MaterialInfo).ToArray(),
                lodGroups = groups.OrderBy(group => Hierarchy(group.transform)).Select(LodInfo).ToArray(),
                groundFlightSharing = terrain != null ? Sharing(terrain.GroundVisual?.transform, terrain.FlightVisual?.transform) : null,
                rockGroundFlightSharing = Sharing(groundRocks, flightRocks)
            };
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log($"ART_RESOURCE_AUDIT {path}; meshes={report.uniqueMeshCount}; vertices={report.uniqueMeshVertices}; " +
                $"triangles={report.uniqueMeshTriangles}; materials={report.uniqueMaterialCount}; textures={report.uniqueTextureCount}; " +
                $"Editor mesh bytes={report.meshEditorRuntimeBytes}; Editor texture bytes={report.textureEditorRuntimeBytes}; " +
                $"surface shared meshes={report.groundFlightSharing?.sharedMeshCount ?? 0}; flight colliders={report.groundFlightSharing?.flightColliderCount ?? 0}");
        }

        private static MeshRecord MeshInfo(Mesh mesh)
        {
            long vertexBytes = 0, indexCount = 0;
            for (int buffer = 0; buffer < mesh.vertexBufferCount; buffer++) vertexBytes += (long)mesh.GetVertexBufferStride(buffer) * mesh.vertexCount;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++) indexCount += (long)mesh.GetIndexCount(submesh);
            return new MeshRecord
            {
                id = Id(mesh), name = mesh.name, assetPath = AssetDatabase.GetAssetPath(mesh), vertices = mesh.vertexCount,
                subMeshes = mesh.subMeshCount, vertexBuffers = mesh.vertexBufferCount, triangles = Triangles(mesh), readable = mesh.isReadable,
                indexFormat = mesh.indexFormat.ToString(), estimatedVertexBufferBytes = vertexBytes,
                estimatedIndexBufferBytes = indexCount * (mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4),
                editorRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(mesh), boundsCenter = mesh.bounds.center, boundsSize = mesh.bounds.size
            };
        }

        private static MaterialRecord MaterialInfo(Material material) => new()
        {
            id = Id(material), name = material.name, assetPath = AssetDatabase.GetAssetPath(material),
            shader = material.shader != null ? material.shader.name : "<missing>", renderQueue = material.renderQueue,
            textureSlots = material.GetTexturePropertyNames().Where(property => material.GetTexture(property) != null)
                .Select(property => new TextureSlot { property = property, textureId = Id(material.GetTexture(property)) }).ToArray()
        };

        private static TextureRecord TextureInfo(Texture texture)
        {
            var assetPath = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            var image = texture as Texture2D;
            var cube = texture as Cubemap;
            return new TextureRecord
            {
                id = Id(texture), name = texture.name, assetPath = assetPath, dimension = texture.dimension.ToString(),
                graphicsFormat = texture.graphicsFormat.ToString(), textureFormat = image != null ? image.format.ToString() : cube != null ? cube.format.ToString() : texture.GetType().Name,
                width = texture.width, height = texture.height, mipmapCount = image != null ? image.mipmapCount : cube != null ? cube.mipmapCount : 1,
                readable = image != null ? image.isReadable : importer != null && importer.isReadable,
                importedAsSrgb = importer != null && importer.sRGBTexture, editorRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(texture),
                sourceFileBytes = File.Exists(sourcePath) ? new FileInfo(sourcePath).Length : 0
            };
        }

        private static LodRecord LodInfo(LODGroup group) => new()
        {
            hierarchyPath = Hierarchy(group.transform), enabled = group.enabled, size = group.size,
            localReferencePoint = group.localReferencePoint, fadeMode = group.fadeMode.ToString(), animateCrossFading = group.animateCrossFading,
            levels = group.GetLODs().Select((level, index) => new LodLevel
            {
                level = index, renderers = level.renderers.Count(renderer => renderer != null),
                screenRelativeTransitionHeight = level.screenRelativeTransitionHeight, fadeTransitionWidth = level.fadeTransitionWidth,
                rendererInstanceTriangles = RenderMeshes(level.renderers).Sum(Triangles),
                rendererInstanceVertices = RenderMeshes(level.renderers).Sum(mesh => (long)mesh.vertexCount), meshIds = Ids(RenderMeshes(level.renderers).Distinct())
            }).ToArray()
        };

        private static SharingRecord Sharing(Transform groundRoot, Transform flightRoot)
        {
            if (groundRoot == null || flightRoot == null) return null;
            var groundRenderers = groundRoot.GetComponentsInChildren<Renderer>(true);
            var flightRenderers = flightRoot.GetComponentsInChildren<Renderer>(true);
            var ground = RenderMeshes(groundRenderers).ToHashSet(); var flight = RenderMeshes(flightRenderers).ToHashSet();
            var groundMaterials = Materials(groundRenderers); var flightMaterials = Materials(flightRenderers);
            return new SharingRecord
            {
                groundPath = Hierarchy(groundRoot), flightPath = Hierarchy(flightRoot),
                groundMeshCount = ground.Count, flightMeshCount = flight.Count, sharedMeshCount = ground.Intersect(flight).Count(),
                sharedMaterialCount = groundMaterials.Intersect(flightMaterials).Count(),
                sharedTextureCount = Textures(groundMaterials).Intersect(Textures(flightMaterials)).Count(),
                sharedMeshIds = Ids(ground.Intersect(flight)), groundOnlyMeshIds = Ids(ground.Except(flight)), flightOnlyMeshIds = Ids(flight.Except(ground)),
                flightColliderCount = flightRoot.GetComponentsInChildren<Collider>(true).Length
            };
        }

        private static IEnumerable<Mesh> RenderMeshes(IEnumerable<Renderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) yield return mesh;
            }
        }
        private static HashSet<Material> Materials(IEnumerable<Renderer> renderers) => renderers.Where(renderer => renderer != null)
            .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).ToHashSet();
        private static HashSet<Texture> Textures(IEnumerable<Material> materials) => materials
            .SelectMany(material => material.GetTexturePropertyNames().Select(material.GetTexture)).Where(texture => texture != null).ToHashSet();
        private static long Triangles(Mesh mesh)
        {
            long count = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                if (mesh.GetTopology(sub) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(sub) / 3;
            return count;
        }
        private static string[] Ids<T>(IEnumerable<T> resources) where T : UnityEngine.Object => resources.Select(Id).OrderBy(value => value).ToArray();
        private static string Id(UnityEngine.Object resource)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(resource, out string guid, out long localId) && !string.IsNullOrEmpty(guid))
                return guid + ":" + localId;
            return "runtime:" + resource.GetEntityId().ToString() + ":" + resource.name;
        }
        private static string Hierarchy(Transform transform) => transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
    }
}

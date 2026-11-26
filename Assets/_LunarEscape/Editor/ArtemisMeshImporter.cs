using System;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    [ScriptedImporter(1, "slsmesh")]
    public sealed class ArtemisMeshImporter : ScriptedImporter
    {
        static readonly (string name, string color, float metal, float gloss)[] Finishes = {
            ("Ceramic white", "#E9E9E4", .09f, .38f), ("Insulating foam", "#C8753A", 0f, .18f),
            ("Ribbed insulation", "#B66131", .04f, .2f), ("Nozzle alloy", "#45414A", .58f, .38f),
            ("Brushed metal", "#898F94", .42f, .39f), ("Heat blanket", "#B4BDC4", .5f, .43f),
            ("Roll marking", "#24272D", .03f, .32f), ("Gold thermal wrap", "#C59345", .58f, .39f),
            ("Cabin glazing", "#304753", .52f, .66f), ("Rescue orange", "#CA5233", .10f, .39f)
        };

        public override void OnImportAsset(AssetImportContext context)
        {
            using var reader = new BinaryReader(File.OpenRead(context.assetPath));
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "SLS1")
                throw new InvalidDataException("Unrecognized Artemis mesh version.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var materials = new Material[Finishes.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var finish = Finishes[i];
                ColorUtility.TryParseHtmlString(finish.color, out var color);
                var material = new Material(shader) { name = finish.name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", finish.metal);
                material.SetFloat("_Smoothness", finish.gloss);
                context.AddObjectToAsset(finish.name, material);
                materials[i] = material;
            }
            var root = new GameObject("Artemis SLS Block 1");
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = Vector(reader);
                int vertexCount = reader.ReadInt32();
                int subMeshCount = reader.ReadInt32();
                var mesh = new Mesh { name = name, indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = Vectors(reader, vertexCount);
                mesh.normals = Vectors(reader, vertexCount);
                mesh.subMeshCount = subMeshCount;
                var slots = new Material[subMeshCount];
                for (int j = 0; j < subMeshCount; j++)
                {
                    slots[j] = materials[reader.ReadInt32()];
                    var triangles = new int[reader.ReadInt32()];
                    for (int t = 0; t < triangles.Length; t++) triangles[t] = reader.ReadInt32();
                    mesh.SetTriangles(triangles, j, false);
                }
                mesh.RecalculateBounds();
                context.AddObjectToAsset(name, mesh);
                piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                piece.GetComponent<MeshRenderer>().sharedMaterials = slots;
            }
            context.AddObjectToAsset("rocket", root);
            context.SetMainObject(root);
        }

        private static Vector3 Vector(BinaryReader reader) =>
            new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static Vector3[] Vectors(BinaryReader reader, int count)
        {
            var values = new Vector3[count];
            for (int i = 0; i < count; i++) values[i] = Vector(reader);
            return values;
        }
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static class LaunchCinematicBuilder
    {
        private const string Root = "Assets/_LunarEscape";
        private const string Art = Root + "/Art/ArtemisFilm";

        public static LaunchCinematic Build(Transform root, ArtemisRocketVisual rocket, Transform pad)
        {
            if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(Root + "/Art", "ArtemisFilm");
            var film = Child(root, "Artemis flight sequence");
            var start = Required(pad, "Crew Bridge Start");
            var end = Required(pad, "Crew Bridge End");
            var pivot = Required(pad, "Crew Access Arm Pivot");
            var eye = Child(film, "Astronaut eye");
            var mounted = Child(film, "Vehicle camera boom");
            var actors = new LaunchCrewActor[3];
            for (int i = 0; i < actors.Length; i++) actors[i] = CreateCrew(film, rocket, i);

            var sphere = Asset<Mesh>(Root + "/Art/EarthReturn/Earth globe.asset");
            var earth = Globe(film, "Earth", sphere, Root + "/Art/EarthReturn/NASA Earth Equirectangular.jpg", 1);
            var moon = Globe(film, "Moon", sphere, Root + "/Art/Orbit/lroc_color_poles_4k.tif", 0);

            var sky = Material("Atlantic and starfield", "LunarEscape/Earth Sky");
            sky.SetTexture("_StarPhoto", Asset<Texture2D>(Root + "/Art/Orbit/Starfield.png"));
            sky.SetFloat("_HideEarth", 1); sky.SetFloat("_AtmosphereBlend", 1); sky.SetFloat("_StarExposure", .6f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            LaunchCinematic.Stage Stage(Transform part, float release, Vector3 drift, Vector3 tumble)
            {
                return new LaunchCinematic.Stage { part = part, release = release,
                    home = rocket.transform.InverseTransformPoint(part.position),
                    facing = Quaternion.Inverse(rocket.transform.rotation) * part.rotation, drift = drift, tumble = tumble };
            }
            var stages = new[] {
                Stage(rocket.LeftBooster, 30, new Vector3(-2.3f,-2.1f,-.4f), new Vector3(1,4,6)),
                Stage(rocket.RightBooster, 30, new Vector3(2.3f,-2.1f,-.4f), new Vector3(-2,-3,-6)),
                Stage(rocket.Core, 36.5f, new Vector3(-.35f,-3.4f,-.24f), new Vector3(1.2f,3,2)),
                Stage(rocket.EscapeTower, 40, new Vector3(1,4.9f,0), new Vector3(2,0,-4)),
                Stage(rocket.UpperStage, 57, new Vector3(.4f,-2.2f,-.35f), new Vector3(1,1.6f,-2))
            };
            var timeline = film.gameObject.AddComponent<LaunchCinematic>();
            timeline.Configure(rocket, pad, start, end, pivot, eye, mounted, actors, stages, earth, moon,
                CreateExhaust(film, rocket), sky);
            timeline.Evaluate(0);
            return timeline;
        }

        private static T Asset<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing film asset " + path);
        private static Transform Required(Transform parent, string name) =>
            parent.Find(name) ?? throw new InvalidOperationException("Pad is missing " + name);

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Material Material(string name, string shaderName)
        {
            string path = Art + "/" + name + ".mat";
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Missing shader " + shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Transform Globe(Transform root, string name, Mesh sphere, string image, float air)
        {
            var mat = Material(name + " from space", "LunarEscape/Launch Globe");
            mat.SetTexture("_BaseMap", Asset<Texture2D>(image)); mat.SetFloat("_Air", air);
            EditorUtility.SetDirty(mat);
            var body = Child(root, name);
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
            var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return body;
        }

        private static LaunchCrewActor CreateCrew(Transform film, ArtemisRocketVisual vehicle, int index)
        {
            var prefab = Asset<GameObject>(BuildCrewSuit.PrefabPath);
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, film);
            PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            actor.name = "Boarding astronaut " + (index + 1);
            var rig = actor.GetComponent<CrewSuitRig>();
            var legs = AddLegRig(rig);
            var seated = Child(vehicle.Orion, "Seated astronaut " + (index + 1));
            var eye = vehicle.Seat;
            float lateral = index == 0 ? -.74f : index == 1 ? .74f : 0;
            seated.SetPositionAndRotation(eye.position + eye.right*lateral - eye.up*1.29f
                + eye.forward*(index == 2 ? .28f : .05f), eye.rotation);
            MeshRenderer SeatedPart(string name, Material[] materials)
            {
                var part = Child(seated, name);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = Asset<Mesh>(Root + "/Art/EarthReturn/Seated " + name + ".asset");
                var view = part.gameObject.AddComponent<MeshRenderer>(); view.sharedMaterials = materials;
                return view;
            }
            var body = SeatedPart("Suit Body", rig.Body.sharedMaterials);
            var helmet = SeatedPart("Helmet", rig.Helmet.sharedMaterials);
            Renderer lap = null;
            if (index == 2)
            {
                var lower = Child(seated, "First-person lap");
                lower.gameObject.AddComponent<MeshFilter>().sharedMesh = FirstPersonLap(body.GetComponent<MeshFilter>().sharedMesh);
                lap = lower.gameObject.AddComponent<MeshRenderer>();
                lap.sharedMaterials = body.sharedMaterials;
            }
            var performer = actor.AddComponent<LaunchCrewActor>();
            performer.Configure(rig, legs, seated, body, helmet, lap);
            return performer;
        }

        // Keep the commander's legs and resting forearms visible when the wearer looks down.
        // The helmet and collar have no inner surface and would intersect a tracked eye when leaning.
        private static Mesh FirstPersonLap(Mesh suitedBody)
        {
            string path = Art + "/Seated ACES lap.asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored != null) return stored;
            var lap = Object.Instantiate(suitedBody);
            lap.name = "Seated ACES lap";
            var vertices = lap.vertices;
            for (int sub = 0; sub < lap.subMeshCount; sub++)
            {
                var source = lap.GetTriangles(sub);
                var kept = new System.Collections.Generic.List<int>();
                for (int i = 0; i < source.Length; i += 3)
                {
                    if (vertices[source[i]].y > .86f || vertices[source[i + 1]].y > .86f || vertices[source[i + 2]].y > .86f) continue;
                    kept.Add(source[i]);
                    kept.Add(source[i + 1]);
                    kept.Add(source[i + 2]);
                }
                lap.SetTriangles(kept, sub);
            }
            MeshUtility.Optimize(lap);
            AssetDatabase.CreateAsset(lap, path);
            return lap;
        }

        // The original ACES mesh has an articulated upper body. Give its legs four
        // additional joints on a separate mesh used only by the three boarding costumes.
        private static Transform[] AddLegRig(CrewSuitRig rig)
        {
            var limbs = new Transform[4];
            for (int i=0; i<2; i++)
            {
                string side = i == 0 ? "Left" : "Right";
                float x = i == 0 ? -.145f : .145f;
                var hip = Child(rig.Bones[0], side+" thigh");
                hip.position = rig.transform.TransformPoint(new Vector3(x,.89f,-.06f));
                var knee = Child(hip, side+" knee");
                knee.position = rig.transform.TransformPoint(new Vector3(x,.46f,-.07f));
                limbs[2*i] = hip; limbs[2*i+1] = knee;
            }
            string path = Art + "/Boarding ACES body.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            int originalCount = rig.Body.bones.Length;
            var bones = rig.Body.bones.Concat(limbs).ToArray();
            if (mesh == null)
            {
                mesh = Object.Instantiate(rig.Body.sharedMesh);
                mesh.name = "Boarding ACES body";
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                for (int i=0; i<vertices.Length; i++)
                {
                    Vector3 p = vertices[i];
                    if (p.y >= .88f || Mathf.Abs(p.x) > .40f) continue;
                    int hip = originalCount + (p.x < 0 ? 0 : 2);
                    float leg = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f,.74f,p.y));
                    float shin = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.53f,.38f,p.y));
                    weights[i] = new BoneWeight { boneIndex0 = hip, weight0 = leg*(1-shin),
                        boneIndex1 = hip+1, weight1 = leg*shin, boneIndex2 = 0, weight2 = 1-leg };
                }
                mesh.bindposes = bones.Select(b => b.worldToLocalMatrix*rig.Body.transform.localToWorldMatrix).ToArray();
                mesh.boneWeights = weights;
                AssetDatabase.CreateAsset(mesh, path);
            }
            rig.Body.sharedMesh = mesh; rig.Body.bones = bones;
            return limbs;
        }

        private static LaunchExhaust CreateExhaust(Transform director, ArtemisRocketVisual rocket)
        {
            var root = Child(director, "Launch plume and pad steam");
            root.SetPositionAndRotation(rocket.transform.position, rocket.transform.rotation);
            var solid = Material("Booster flame", "LunarEscape/Launch Exhaust");
            var core = Material("Core flame", "LunarEscape/Launch Exhaust");
            solid.SetColor("_Hot", new Color(1,.94f,.68f)); solid.SetColor("_Tail", new Color(1,.26f,.045f));
            core.SetColor("_Hot", new Color(.86f,.89f,1)); core.SetColor("_Tail", new Color(.36f,.47f,.98f));
            EditorUtility.SetDirty(solid); EditorUtility.SetDirty(core);
            var mesh = ExhaustMesh();
            LaunchExhaust.Jet Jet(Transform parent, Vector3 relative, Vector3 size, Material mat, float start, float stop)
            {
                var holder = Child(parent, "Exhaust glow");
                holder.SetPositionAndRotation(rocket.transform.TransformPoint(relative), rocket.transform.rotation);
                holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = holder.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                return new LaunchExhaust.Jet { flame = renderer, size = size, start = start, stop = stop };
            }
            var jets = new System.Collections.Generic.List<LaunchExhaust.Jet>();
            foreach (float x in new[]{-2f,2f}) foreach (float z in new[]{-2f,2f})
                jets.Add(Jet(rocket.Core, new(x,.25f,z), new(1.5f,22,1.5f), core,17.5f,36.5f));
            jets.Add(Jet(rocket.LeftBooster, new(-6.3f,.25f,0),new(2.1f,32,2.1f),solid,18.5f,30));
            jets.Add(Jet(rocket.RightBooster, new(6.3f,.25f,0),new(2.1f,32,2.1f),solid,18.5f,30));
            jets.Add(Jet(rocket.UpperStage, new(0,66.5f,0),new(1.6f,17,1.6f),core,37,54));
            var smoke = new Renderer[26];
            var vapour = Material("Launch steam", "LunarEscape/Launch Vapour");
            for (int i=0; i<smoke.Length; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puff.name = "Condensation puff"; puff.transform.SetParent(root, false);
                Object.DestroyImmediate(puff.GetComponent<Collider>());
                smoke[i] = puff.GetComponent<Renderer>();
                smoke[i].sharedMaterial = vapour; smoke[i].shadowCastingMode = ShadowCastingMode.Off;
                smoke[i].receiveShadows = false;
            }
            var effects = root.gameObject.AddComponent<LaunchExhaust>();
            effects.Configure(jets.ToArray(), smoke);
            return effects;
        }

        private static Mesh ExhaustMesh()
        {
            string path = Art + "/Exhaust envelope.asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored != null) return stored;
            const int sides=32;
            float[] radii = { .65f,.80f,.90f,.76f,.50f,.18f,.012f };
            var vertices = new Vector3[(sides+1)*radii.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new System.Collections.Generic.List<int>();
            for (int ring=0; ring<radii.Length; ring++)
                for (int s=0; s<=sides; s++)
                {
                    float y=ring/(radii.Length-1f), x=(float)s/sides, a=2*Mathf.PI*x;
                    int i=ring*(sides+1)+s;
                    vertices[i]=new Vector3(Mathf.Cos(a)*radii[ring], -y, Mathf.Sin(a)*radii[ring]);
                    uv[i]=new Vector2(x,y);
                    if (ring==radii.Length-1 || s==sides) continue;
                    int below=i+sides+1;
                    indices.AddRange(new[]{i,i+1,below,i+1,below+1,below});
                }
            var mesh=new Mesh { name="Exhaust envelope", vertices=vertices, uv=uv, triangles=indices.ToArray() };
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }
    }
}

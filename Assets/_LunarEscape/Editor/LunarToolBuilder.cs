using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Editor
{
    // 扳机工具与工具架的模型（用基本体拼出），供第一天（氮气枪、取芯钻）和第二天（激光切割器）的安装脚本共用。
    // 每件工具：抓取点在握把上（固定姿态，不随抓取位置变化），抓取方式为“瞬时跟手”，远距离抓取时吸到手上。
    public static class LunarToolBuilder
    {
        private const string Root = "Assets/_LunarEscape";
        private const string LaserMaterialPath = Root + "/Materials/Laser Beam.mat";
        private const string PanelDustPath = Root + "/Materials/Panel Dust.mat";

        public static Material Mat(string name)
        {
            foreach (var path in new[] { Root + "/Materials/" + name + ".mat", Root + "/Art/StationExpansion/Materials/" + name + ".mat" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) return material;
            }
            throw new InvalidOperationException("缺少材质 " + name);
        }

        public static Transform Frame(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.SetLocalPositionAndRotation(position, rotation);
            return frame;
        }

        public static Transform Frame(Transform parent, string name, Vector3 position) => Frame(parent, name, position, Quaternion.identity);

        public static GameObject Prim(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, string material, Quaternion? rotation = null, bool collider = false)
        {
            var o = GameObject.CreatePrimitive(shape);
            o.name = name;
            o.transform.SetParent(parent, false);
            o.transform.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
            o.transform.localScale = scale;
            if (!collider) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            o.GetComponent<Renderer>().sharedMaterial = Mat(material);
            return o;
        }

        public static CockpitLamp Lamp(Transform parent, string name, Vector3 position, float size = 0.022f)
        {
            var bulb = Prim(parent, name, PrimitiveType.Sphere, position, Vector3.one * size, "Cockpit Lamp");
            var lamp = bulb.AddComponent<CockpitLamp>();
            lamp.Configure(bulb.GetComponent<Renderer>());
            return lamp;
        }

        public static TextMeshPro Text(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, float fontSize, Color color)
        {
            var text = new GameObject(name).AddComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.SetLocalPositionAndRotation(position, rotation);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset");
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize; text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        // 抓取设置：握把处固定抓点、瞬时跟手（瞄准准确）、远距离抓取吸到手上。
        private static XRGrabInteractable Grabbable(GameObject tool, Transform attach, float mass)
        {
            var body = tool.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var grab = tool.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = false;
            grab.attachTransform = attach;
            grab.farAttachMode = InteractableFarAttachMode.Near;
            grab.throwOnDetach = false;
            tool.AddComponent<ReturnFallenTool>();
            tool.AddComponent<GrabFeedback>();
            return grab;
        }

        // —— 压缩氮气除尘枪：手枪式，白色枪身、蓝色气瓶、橙色喷嘴 ——
        public static NitrogenBlower BuildBlower(Transform parent, Vector3 position, Quaternion rotation)
        {
            var tool = new GameObject("Nitrogen Blower");
            tool.transform.SetParent(parent, false);
            tool.transform.SetLocalPositionAndRotation(position, rotation);
            var t = tool.transform;
            var sideways = Quaternion.Euler(90f, 0f, 0f);
            Prim(t, "Blower Body", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.03f), new Vector3(0.05f, 0.11f, 0.05f), "Panel White", sideways);
            Prim(t, "Blower Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.19f), new Vector3(0.022f, 0.06f, 0.022f), "Station Graphite", sideways);
            Prim(t, "Blower Nozzle", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.255f), new Vector3(0.032f, 0.012f, 0.032f), "Safety Amber", sideways);
            Prim(t, "Nitrogen Tank", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0.06f), new Vector3(0.045f, 0.08f, 0.045f), "Guide Teal", sideways);
            Prim(t, "Blower Grip", PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.04f), new Vector3(0.035f, 0.11f, 0.045f), "Station Graphite", Quaternion.Euler(-15f, 0f, 0f));
            Prim(t, "Blower Trigger", PrimitiveType.Cube, new Vector3(0f, -0.025f, 0.0f), new Vector3(0.012f, 0.03f, 0.012f), "Safety Amber");
            var collider = tool.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.01f, 0.09f); collider.size = new Vector3(0.07f, 0.16f, 0.36f);
            var attach = Frame(t, "Grip Attach", new Vector3(0f, -0.05f, -0.04f));
            var nozzle = Frame(t, "Nozzle", new Vector3(0f, 0.02f, 0.27f));
            var gauge = Text(t, "Tank Gauge", new Vector3(0f, 0.05f, -0.02f), Quaternion.Euler(60f, 0f, 0f), new Vector2(0.12f, 0.03f), 0.18f, new Color(0.6f, 0.95f, 1f));
            gauge.text = "N2 100%";
            var jet = Particles(nozzle, "Nitrogen Jet", 240, new Vector2(0.18f, 0.35f), new Vector2(0.02f, 0.06f), new Color(0.92f, 0.96f, 1f, 0.35f), ParticleSystemSimulationSpace.World);
            var shape = jet.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = 0.005f; shape.angle = 6f;
            var grow = jet.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3f));
            var emission = jet.emission; emission.rateOverTime = 0f;
            var main = jet.main; main.loop = true; main.playOnAwake = true;
            var puff = Particles(t, "Dust Puff", 300, new Vector2(0.5f, 1.1f), new Vector2(0.02f, 0.07f), new Color(0.55f, 0.52f, 0.48f, 0.55f), ParticleSystemSimulationSpace.World);
            var puffShape = puff.shape; puffShape.enabled = true; puffShape.shapeType = ParticleSystemShapeType.Sphere; puffShape.radius = 0.04f;
            var puffMain = puff.main; puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f); puffMain.gravityModifier = 0.15f;
            var grab = Grabbable(tool, attach, 1.2f);
            grab.colliders.Add(collider);
            var blower = tool.AddComponent<NitrogenBlower>();
            blower.Configure(nozzle, jet, puff, gauge);
            return blower;
        }

        // —— 手持取芯钻：顶部 T 形把手，橙色电机，长钻杆，底部钻头 ——
        public static CoreDrill BuildDrill(Transform parent, Vector3 position, Quaternion rotation, float length = 1.0f)
        {
            var tool = new GameObject("Core Drill");
            tool.transform.SetParent(parent, false);
            tool.transform.SetLocalPositionAndRotation(position, rotation);
            var t = tool.transform;
            Prim(t, "T Handle", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.035f, 0.11f, 0.035f), "Station Graphite", Quaternion.Euler(0f, 0f, 90f));
            Prim(t, "Handle Stem", PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0f), new Vector3(0.03f, 0.05f, 0.03f), "Station Graphite");
            Prim(t, "Drill Motor", PrimitiveType.Cylinder, new Vector3(0f, -0.17f, 0f), new Vector3(0.09f, 0.09f, 0.09f), "Safety Amber");
            Prim(t, "Motor Vent", PrimitiveType.Cube, new Vector3(0f, -0.17f, 0.046f), new Vector3(0.06f, 0.1f, 0.006f), "Station Graphite");
            Prim(t, "Drill Shaft", PrimitiveType.Cylinder, new Vector3(0f, -0.26f - (length - 0.32f) * 0.5f, 0f), new Vector3(0.028f, (length - 0.32f) * 0.5f, 0.028f), "Station Shell");
            // 钻头绕竖直轴（局部 Y）旋转。
            var bit = Frame(t, "Drill Bit", new Vector3(0f, -length + 0.04f, 0f));
            Prim(bit, "Bit Crown", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.055f, 0.04f, 0.055f), "Station Graphite");
            for (int i = 0; i < 4; i++)
                Prim(bit, "Bit Tooth", PrimitiveType.Cube, Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0.024f, -0.042f, 0f), new Vector3(0.012f, 0.02f, 0.012f), "Panel White");
            var collider = tool.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -length * 0.5f + 0.02f, 0f); collider.size = new Vector3(0.12f, length + 0.05f, 0.1f);
            var attach = Frame(t, "Grip Attach", Vector3.zero);
            var tip = Frame(t, "Drill Tip", new Vector3(0f, -length, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            // 读数朝上、偏向持握者一侧：低头即可看到。
            var lamp = Lamp(t, "Zone Lamp", new Vector3(0.035f, -0.12f, -0.045f), 0.02f);
            var bar = Prim(t, "RPM Bar", PrimitiveType.Cube, new Vector3(-0.03f, -0.17f, -0.047f), new Vector3(0.012f, 0.001f, 0.004f), "Cockpit Lamp");
            var readout = Text(t, "Depth Readout", new Vector3(0f, -0.21f, -0.05f), Quaternion.Euler(20f, 0f, 0f), new Vector2(0.14f, 0.03f), 0.18f, Color.white);
            readout.text = "-- cm";
            var grab = Grabbable(tool, attach, 2.5f);
            grab.colliders.Add(collider);
            var drill = tool.AddComponent<CoreDrill>();
            drill.Configure(tip, bit, lamp, bar.GetComponent<Renderer>(), readout);
            return drill;
        }

        // —— 激光切割器：深灰机身、散热片、红色镜头 ——
        public static LaserCutter BuildCutter(Transform parent, Vector3 position, Quaternion rotation)
        {
            var tool = new GameObject("Laser Cutter");
            tool.transform.SetParent(parent, false);
            tool.transform.SetLocalPositionAndRotation(position, rotation);
            var t = tool.transform;
            Prim(t, "Cutter Body", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.055f, 0.065f, 0.24f), "Station Graphite");
            for (int i = 0; i < 4; i++)
                Prim(t, "Heat Fin", PrimitiveType.Cube, new Vector3(0f, 0.058f, 0.01f + i * 0.035f), new Vector3(0.06f, 0.012f, 0.008f), "Station Shell");
            Prim(t, "Emitter Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.19f), new Vector3(0.04f, 0.012f, 0.04f), "Safety Amber", Quaternion.Euler(90f, 0f, 0f));
            Prim(t, "Emitter Lens", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.199f), new Vector3(0.025f, 0.004f, 0.025f), "Cockpit Red", Quaternion.Euler(90f, 0f, 0f));
            Prim(t, "Cutter Grip", PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.03f), new Vector3(0.035f, 0.11f, 0.045f), "Station Shell", Quaternion.Euler(-15f, 0f, 0f));
            Prim(t, "Cutter Trigger", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.01f), new Vector3(0.012f, 0.03f, 0.012f), "Cockpit Red");
            Prim(t, "Warning Stripe", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.155f), new Vector3(0.057f, 0.067f, 0.01f), "Safety Amber");
            var collider = tool.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0.07f); collider.size = new Vector3(0.07f, 0.16f, 0.3f);
            var attach = Frame(t, "Grip Attach", new Vector3(0f, -0.05f, -0.03f));
            var emitter = Frame(t, "Emitter", new Vector3(0f, 0.02f, 0.205f));
            var lamp = Lamp(t, "Heat Lamp", new Vector3(0f, 0.06f, -0.05f), 0.02f);
            var beam = new GameObject("Laser Beam").AddComponent<LineRenderer>();
            beam.transform.SetParent(t, false);
            beam.useWorldSpace = true; beam.positionCount = 2; beam.widthMultiplier = 0.006f;
            beam.numCapVertices = 3;
            beam.sharedMaterial = LaserMaterial();
            beam.startColor = beam.endColor = new Color(1f, 0.35f, 0.12f, 1f);
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.enabled = false;
            var sparks = Particles(t, "Cut Sparks", 400, new Vector2(0.25f, 0.6f), new Vector2(0.006f, 0.018f), new Color(1f, 0.6f, 0.2f, 1f), ParticleSystemSimulationSpace.World);
            var sparkShape = sparks.shape; sparkShape.enabled = true; sparkShape.shapeType = ParticleSystemShapeType.Cone; sparkShape.angle = 40f; sparkShape.radius = 0.005f;
            var sparkMain = sparks.main; sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f); sparkMain.gravityModifier = 0.5f;
            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>(); sparkRenderer.sharedMaterial = LaserMaterial();
            var glow = new GameObject("Cut Hot Spot").AddComponent<Light>();
            glow.transform.SetParent(t, false);
            glow.type = LightType.Point; glow.range = 0.7f; glow.color = new Color(1f, 0.5f, 0.15f); glow.intensity = 1f; glow.shadows = LightShadows.None; glow.enabled = false;
            var grab = Grabbable(tool, attach, 1.4f);
            grab.colliders.Add(collider);
            var cutter = tool.AddComponent<LaserCutter>();
            cutter.Configure(emitter, beam, sparks, glow, lamp);
            return cutter;
        }

        // 工具架卡槽：插座吸附位置即工具在架上的姿态；只接受指定工具，开局工具就挂在上面。
        public static ToolHolster BuildHolster(Transform parent, string name, Vector3 position, Quaternion rotation, TriggerTool tool)
        {
            var root = Frame(parent, name, position, rotation);
            var trigger = root.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = 0.12f;
            var socket = root.gameObject.AddComponent<XRSocketInteractor>();
            var attach = Frame(root, "Holster Attach", Vector3.zero);
            socket.attachTransform = attach;
            socket.showInteractableHoverMeshes = true;
            var holster = root.gameObject.AddComponent<ToolHolster>();
            holster.Configure(tool);
            var grab = tool.GetComponent<XRGrabInteractable>(); // 编辑器里工具还没 Awake，不能用 tool.Grab
            tool.transform.SetPositionAndRotation(attach.position, attach.rotation * Quaternion.Inverse(grab.attachTransform.localRotation));
            tool.transform.position += attach.position - grab.attachTransform.position;
            var serialized = new SerializedObject(socket);
            serialized.FindProperty("m_StartingSelectedInteractable").objectReferenceValue = grab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return holster;
        }

        public static ParticleSystem Particles(Transform parent, string name, int max, Vector2 lifetime, Vector2 size, Color color, ParticleSystemSimulationSpace space)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color; main.startSpeed = 0f; main.simulationSpace = space; main.gravityModifier = 0f;
            var emission = system.emission; emission.rateOverTime = 0f;
            var shape = system.shape; shape.enabled = false;
            var fade = system.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Decompression Dust.mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        // 激光与火花：URP 粒子无光照、加色混合，亮度不受环境光影响。
        public static Material LaserMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LaserMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, LaserMaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Effects/Soft Particle.png"));
            material.SetColor("_BaseColor", new Color(2.2f, 0.8f, 0.3f, 1f));
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // 太阳能板尘层材质：URP Lit 透明，贴图在运行时生成。
        public static Material PanelDustMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PanelDustPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, PanelDustPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
            material.SetFloat("_Smoothness", 0.05f); material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static TMP_FontAsset StationFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset");
    }
}

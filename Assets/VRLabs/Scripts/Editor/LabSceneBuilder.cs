using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

// Сохранены названия компонентов из методички. Эти legacy-компоненты есть в XRI 3.6.0.
#pragma warning disable CS0618
namespace VRLabs.Editor
{
    [InitializeOnLoad]
    public static class LabSceneBuilder
    {
        public const string Root = "Assets/VRLabs";
        public const string Scenes = Root + "/Scenes";
        const int SharedMask = 1 << 1;
        const int RayMask = 1 << 2;
        const int DirectMask = 1 << 3;
        static readonly int[] Variants = { 1, 2, 20 };
        static readonly string[] Names = { "Артемьев Григорий", "Чэнь Хунмин", "Мухин Даниил" };
        static bool building;
        static Font font;
        static Material floor, blue, gold, orange, white, dark;

        static LabSceneBuilder() { EditorApplication.delayCall += AutoBuild; }

        static void AutoBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoBuild;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || building) return;
            if (Variants.All(v => File.Exists(PathFor(1, v)) && File.Exists(PathFor(2, v)))) return;
            try { BuildMissing(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static string PathFor(int lab, int variant) => Scenes + "/LR" + lab + "_Variant" + variant.ToString("00") + ".unity";

        [MenuItem("VR Labs/Prepare missing scenes")]
        public static void BuildMissing()
        {
            if (building || EditorApplication.isPlayingOrWillChangePlaymode) return;
            building = true;
            Scene originalActive = SceneManager.GetActiveScene();
            try
            {
                Directory.CreateDirectory(Scenes);
                Directory.CreateDirectory(Root + "/Materials");
                Directory.CreateDirectory(Root + "/Prefabs");
                AssetDatabase.Refresh();
                font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                if (font == null) throw new InvalidOperationException("Missing LiberationSans.ttf");
                floor = MaterialAsset("Floor", new Color(.12f, .25f, .28f));
                blue = MaterialAsset("Blue", new Color(.08f, .55f, .85f));
                gold = MaterialAsset("Gold", new Color(1f, .68f, .16f));
                orange = MaterialAsset("Orange", new Color(.95f, .25f, .12f));
                white = MaterialAsset("White", new Color(.82f, .9f, .95f));
                dark = MaterialAsset("Dark", new Color(.06f, .09f, .13f));
                SetupInteractionLayers();
                for (int index = 0; index < Variants.Length; index++)
                    for (int lab = 1; lab <= 2; lab++)
                    {
                        string path = PathFor(lab, Variants[index]);
                        if (File.Exists(path)) continue;
                        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                        SceneManager.SetActiveScene(scene);
                        try
                        {
                            BuildScene(lab, Variants[index], Names[index]);
                            ValidateScene(scene, lab);
                            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
                        }
                        finally { EditorSceneManager.CloseScene(scene, true); }
                    }
                // Все шесть сцен нужны в Build Settings для кнопки повторного запуска.
                var entries = EditorBuildSettings.scenes.ToList();
                foreach (int v in Variants)
                    for (int lab = 1; lab <= 2; lab++)
                    {
                        string path = PathFor(lab, v);
                        entries.RemoveAll(s => s.path == path);
                        entries.Add(new EditorBuildSettingsScene(path, true));
                    }
                EditorBuildSettings.scenes = entries.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("VR Labs: готовы 6 сцен. Откройте VR Labs > Open scenes или Assets/VRLabs/Scenes.");
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                building = false;
                EditorUtility.ClearProgressBar();
            }
        }

        static void SetupInteractionLayers()
        {
            Object settings = AssetDatabase.LoadAssetAtPath<Object>("Assets/XRI/Settings/Resources/InteractionLayerSettings.asset");
            if (settings == null) throw new InvalidOperationException("InteractionLayerSettings asset missing.");
            var serialized = new SerializedObject(settings);
            var layers = serialized.FindProperty("m_LayerNames");
            if (layers == null || layers.arraySize < 4) throw new InvalidOperationException("Interaction layer layout differs from XRI 3.6.0.");
            layers.GetArrayElementAtIndex(1).stringValue = "Interactable";
            layers.GetArrayElementAtIndex(2).stringValue = "Only Ray";
            layers.GetArrayElementAtIndex(3).stringValue = "Only Direct";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material MaterialAsset(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader missing. Wait for package import.");
            material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", .25f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Empty(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static TextMesh Label(string name, string text, Vector3 position, float characterSize = .025f)
        {
            var go = Empty(name);
            go.transform.position = position;
            var label = go.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            label.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return label;
        }

        static InputActionProperty Action(string name, string controlType, params string[] bindings)
        {
            var action = new InputAction(name, controlType == "Button" ? InputActionType.Button : InputActionType.Value,
                expectedControlType: controlType);
            foreach (string binding in bindings) action.AddBinding(binding);
            return new InputActionProperty(action);
        }

        static ActionBasedController Controller(Transform parent, string hand, Material color)
        {
            var go = Empty(hand + " Controller", parent);
            string device = "<XRController>{" + hand + "Hand}/";
            var controller = go.AddComponent<ActionBasedController>();
            controller.positionAction = Action("Position", "Vector3", device + "devicePosition");
            controller.rotationAction = Action("Rotation", "Quaternion", device + "deviceRotation");
            controller.isTrackedAction = Action("IsTracked", "Button", device + "isTracked");
            controller.trackingStateAction = Action("TrackingState", "Integer", device + "trackingState");
            string desktop = hand == "Left" ? "<Keyboard>/space" : "<Mouse>/leftButton";
            controller.selectAction = Action("Select", "Button", device + "gripPressed", desktop);
            controller.selectActionValue = Action("SelectValue", "Axis", device + "grip", desktop);
            controller.activateAction = Action("Activate", "Button", device + "triggerPressed");
            controller.activateActionValue = Action("ActivateValue", "Axis", device + "trigger");
            controller.uiPressAction = Action("UIPress", "Button", device + "triggerPressed", "<Mouse>/leftButton");
            controller.uiPressActionValue = Action("UIPressValue", "Axis", device + "trigger", "<Mouse>/leftButton");
            controller.rotateAnchorAction = Action("RotateAnchor", "Vector2", device + "primary2DAxis");
            controller.translateAnchorAction = Action("TranslateAnchor", "Vector2", device + "primary2DAxis");
            controller.enableInputActions = true;
            var sphere = Shape("Controller Sphere", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .1f, color, go.transform);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            return controller;
        }

        static XRGrabInteractable Grabbable(GameObject go, XRInteractionManager manager, int mask = SharedMask)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = .5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            grab.interactionLayers = mask;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = true;
            // Коллайдер предмета обычный: пол должен останавливать падающий предмет.
            return grab;
        }

        static GameObject Prefab(string name, PrimitiveType type, float size, Material material)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = Shape(name, type, Vector3.zero, Vector3.one * size, material);
            Grabbable(go, null);
            if (size < .2f) go.GetComponent<Rigidbody>().mass = .12f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildScene(int lab, int variant, string student)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .6f, .68f);
            var lamp = Empty("Directional Light").AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.intensity = 1.1f;
            lamp.shadows = LightShadows.Soft;
            lamp.transform.rotation = Quaternion.Euler(45, -25, 0);
            var manager = Empty("XR Interaction Manager").AddComponent<XRInteractionManager>();
            Shape("Colored Floor", PrimitiveType.Cube, new Vector3(0, -.1f, 3), new Vector3(12, .2f, 12), floor);
            Shape("Back Wall", PrimitiveType.Cube, new Vector3(0, 1.5f, 6), new Vector3(12, 3, .15f), dark);
            Shape("Left Wall", PrimitiveType.Cube, new Vector3(-6, 1.5f, 3), new Vector3(.15f, 3, 6), dark);
            Shape("Right Wall", PrimitiveType.Cube, new Vector3(6, 1.5f, 3), new Vector3(.15f, 3, 6), dark);
            Shape("Cube", PrimitiveType.Cube, new Vector3(-1.4f, .4f, 3.8f), Vector3.one * .8f, blue);
            Shape("Sphere", PrimitiveType.Sphere, new Vector3(0, .45f, 4.3f), Vector3.one * .9f, gold);
            Shape("Cylinder", PrimitiveType.Cylinder, new Vector3(1.4f, .6f, 3.8f), new Vector3(.65f, .6f, .65f), orange);
            var identity = Empty("Lab Settings - Student Name").AddComponent<LabIdentity>();
            identity.laboratory = lab;
            identity.variant = variant;
            identity.studentName = student;
            identity.board = Label("Student Board", "ЛР " + lab + "\n" + student + " · ИУ6-71Б\nВариант " + variant, new Vector3(0, 2.5f, 5.6f), .035f);

            var rig = Empty("XR Origin (XR Rig)");
            var offset = Empty("Camera Offset", rig.transform);
            offset.transform.localPosition = new Vector3(0, 1.65f, 0);
            var headGO = Empty("Main Camera", offset.transform);
            headGO.tag = "MainCamera";
            var camera = headGO.AddComponent<Camera>();
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .09f, .13f);
            headGO.AddComponent<AudioListener>();
            headGO.AddComponent<UniversalAdditionalCameraData>();
            var tracked = headGO.AddComponent<TrackedPoseDriver>();
            tracked.positionInput = Action("HeadPosition", "Vector3", "<XRHMD>/centerEyePosition");
            tracked.rotationInput = Action("HeadRotation", "Quaternion", "<XRHMD>/centerEyeRotation");
            tracked.trackingStateInput = Action("HeadTracking", "Integer", "<XRHMD>/trackingState");
            var origin = rig.AddComponent<XROrigin>();
            origin.Origin = rig;
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 1.65f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var left = Controller(offset.transform, "Left", blue);
            var right = Controller(offset.transform, "Right", gold);
            var character = rig.AddComponent<CharacterController>();
            character.radius = .22f;
            character.height = 1.65f;
            character.center = new Vector3(0, .85f, 0);
            character.skinWidth = .02f;
            character.minMoveDistance = 0;
            var preview = rig.AddComponent<LabDesktopPreview>();
            preview.head = camera;
            preview.leftHand = left.transform;
            preview.rightHand = right.transform;
            preview.leftController = left;
            preview.rightController = right;
            preview.character = character;
            if (lab == 1) return;

            var locomotionGO = Empty("Locomotion System", rig.transform);
            var system = locomotionGO.AddComponent<LocomotionSystem>();
            system.xrOrigin = origin;
            var move = Empty("Move", locomotionGO.transform).AddComponent<ActionBasedContinuousMoveProvider>();
            move.system = system;
            move.moveSpeed = 1.5f;
            move.forwardSource = camera.transform;
            move.useGravity = true;
            move.gravityApplicationMode = ContinuousMoveProviderBase.GravityApplicationMode.Immediately;
            move.leftHandMoveAction = Action("Move", "Vector2", "<XRController>{LeftHand}/primary2DAxis");
            var snap = Empty("Snap Turn", locomotionGO.transform).AddComponent<ActionBasedSnapTurnProvider>();
            snap.system = system;
            snap.turnAmount = 30;
            snap.rightHandSnapTurnAction = Action("SnapTurn", "Vector2", "<XRController>{RightHand}/primary2DAxis");
            var continuous = Empty("Continuous Turn", locomotionGO.transform).AddComponent<ActionBasedContinuousTurnProvider>();
            continuous.system = system;
            continuous.turnSpeed = 45;
            continuous.rightHandTurnAction = Action("Turn", "Vector2", "<XRController>{RightHand}/primary2DAxis");
            continuous.enabled = false;
            var driver = rig.AddComponent<CharacterControllerDriver>();
            driver.locomotionProvider = move;
            preview.moveProvider = move;
            preview.snapProvider = snap;
            preview.continuousProvider = continuous;

            var directGO = Empty("Direct Interactor", left.transform);
            directGO.layer = 2;
            var trigger = directGO.AddComponent<SphereCollider>();
            trigger.radius = .12f;
            trigger.isTrigger = true;
            var directBody = directGO.AddComponent<Rigidbody>();
            directBody.isKinematic = true;
            directBody.useGravity = false;
            var direct = directGO.AddComponent<XRDirectInteractor>();
            direct.interactionManager = manager;
            direct.interactionLayers = SharedMask | DirectMask;
            direct.xrController = left;
            direct.inputCompatibilityMode = XRBaseInputInteractor.InputCompatibilityMode.ForceDeprecatedInput;
            direct.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;

            var rayGO = Empty("Ray Interactor", right.transform);
            var ray = rayGO.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            ray.interactionLayers = SharedMask | RayMask;
            ray.xrController = right;
            ray.inputCompatibilityMode = XRBaseInputInteractor.InputCompatibilityMode.ForceDeprecatedInput;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
            ray.maxRaycastDistance = 12;
            ray.raycastMask = ~(1 << 2);
            ray.enableUIInteraction = true;
            ray.manipulateAttachTransform = true;
            ray.translateSpeed = 1.5f;
            var line = rayGO.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            var visual = rayGO.AddComponent<XRInteractorLineVisual>();
            visual.lineWidth = .008f;

            var eventGO = Empty("EventSystem");
            eventGO.AddComponent<EventSystem>();
            var inputModule = eventGO.AddComponent<XRUIInputModule>();
            inputModule.enableXRInput = true;
            inputModule.enableMouseInput = false; // Мышь уже управляет правым XR-лучом в desktop preview.
            inputModule.enableJoystickInput = false;

            Shape("Grab Table", PrimitiveType.Cube, new Vector3(-.6f, .38f, 1.4f), new Vector3(.8f, .76f, .8f), dark);
            var grabCube = Shape("Grab Cube - Direct and Ray", PrimitiveType.Cube, new Vector3(-.6f, .94f, 1.4f), Vector3.one * .28f, blue);
            Grabbable(grabCube, manager);
            Label("Grab Instructions", "Куб: взять и бросить\nЛевая рука или правый луч", new Vector3(-.6f, 1.5f, 1.5f), .013f);

            var controls = Empty("Lab Controls").AddComponent<LabControls>();
            controls.snapTurn = snap;
            controls.continuousTurn = continuous;
            controls.roomLight = lamp;
            controls.ray = ray;
            controls.turnLabel = Label("Turn Mode", "Поворот: ступенчатый", new Vector3(-2.1f, 2.2f, 3), .017f);
            BuildUI(controls, camera);
            BuildDoor(manager);
            BuildTask(variant, manager);
            BuildSpawner(manager);
        }

        static void BuildTask(int variant, XRInteractionManager manager)
        {
            Shape("Task Pedestal", PrimitiveType.Cube, new Vector3(.65f, .4f, 1.55f), new Vector3(.85f, .8f, .85f), dark);
            var target = Shape("Variant " + variant + " - Individual Task", variant == 1 ? PrimitiveType.Cube : PrimitiveType.Sphere,
                new Vector3(.65f, 1.16f, 1.55f), Vector3.one * .52f, gold);
            var simple = target.AddComponent<XRSimpleInteractable>();
            simple.interactionManager = manager;
            simple.interactionLayers = variant == 20 ? DirectMask : RayMask;
            var action = target.AddComponent<LabVariantAction>();
            action.variant = variant;
            if (variant == 1)
            {
                action.spawnedPrefab = Prefab("SpawnedSphere", PrimitiveType.Sphere, .22f, blue);
                action.spawnPoint = Empty("Sphere Spawn Point").transform;
                action.spawnPoint.position = new Vector3(1.35f, 1.5f, 1.55f);
            }
            if (variant == 20) action.spawnedPrefab = Prefab("SmallSphere", PrimitiveType.Sphere, .13f, gold);
            UnityEventTools.AddPersistentListener(simple.selectEntered, action.OnSelected);
            string text = variant == 1 ? "Вариант 1: куб создаёт шар\nТолько правый луч + Grip" :
                variant == 2 ? "Вариант 2: шар исчезает\nТолько правый луч + Grip" :
                "Вариант 20: шар распадается\nТолько левая рука + Grip";
            Label("Task Instructions", text, new Vector3(.65f, 1.95f, 1.65f), .015f);
        }

        static void BuildSpawner(XRInteractionManager manager)
        {
            var capsule = Shape("Ray Capsule - Spawn Cylinder", PrimitiveType.Capsule, new Vector3(2.7f, 1.1f, 3), new Vector3(.35f, .4f, .35f), orange);
            capsule.transform.rotation = Quaternion.Euler(0, 0, 90);
            var simple = capsule.AddComponent<XRSimpleInteractable>();
            simple.interactionManager = manager;
            simple.interactionLayers = RayMask;
            var action = capsule.AddComponent<LabVariantAction>();
            action.variant = 0;
            action.spawnedPrefab = Prefab("SpawnedCylinder", PrimitiveType.Cylinder, .25f, orange);
            action.spawnPoint = Empty("Cylinder Spawn Point").transform;
            action.spawnPoint.position = new Vector3(3.3f, 1.4f, 3);
            UnityEventTools.AddPersistentListener(simple.selectEntered, action.OnSelected);
            Label("Capsule Instructions", "Общий пример\nКапсула создаёт цилиндр", new Vector3(2.7f, 1.85f, 3), .017f);
        }

        static void BuildDoor(XRInteractionManager manager)
        {
            var root = Empty("Interactable Door");
            root.transform.position = new Vector3(-3.6f, 0, 4.6f);
            Shape("Left Frame", PrimitiveType.Cube, new Vector3(-.6f, 1.1f, 0), new Vector3(.15f, 2.2f, .2f), white, root.transform);
            Shape("Right Frame", PrimitiveType.Cube, new Vector3(.6f, 1.1f, 0), new Vector3(.15f, 2.2f, .2f), white, root.transform);
            Shape("Top Frame", PrimitiveType.Cube, new Vector3(0, 2.2f, 0), new Vector3(1.35f, .15f, .2f), white, root.transform);
            var door = Empty("Door - Hinge Joint", root.transform);
            door.transform.localPosition = new Vector3(0, 1.05f, 0);
            Shape("Door Leaf", PrimitiveType.Cube, Vector3.zero, new Vector3(1.02f, 2.02f, .08f), blue, door.transform);
            var handle = Shape("Door Handle", PrimitiveType.Cube, new Vector3(.36f, 0, -.1f), new Vector3(.18f, .1f, .16f), gold, door.transform);
            var body = door.AddComponent<Rigidbody>();
            body.mass = 3;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var hinge = door.AddComponent<HingeJoint>();
            hinge.anchor = new Vector3(-.51f, 0, 0);
            hinge.axis = Vector3.up;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.connectedAnchor = door.transform.TransformPoint(hinge.anchor);
            hinge.useLimits = true;
            hinge.limits = new JointLimits { min = -120, max = 0 };
            var grab = door.AddComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            grab.interactionLayers = DirectMask;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.trackRotation = false;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            grab.attachTransform = handle.transform;
            grab.colliders.Clear();
            grab.colliders.Add(handle.GetComponent<Collider>());
            Label("Door Instructions", "Дверь: возьмите ручку\nлевой рукой и потяните", new Vector3(-3.6f, 2.65f, 4.6f), .02f);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Text UIText(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, pos, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static Button Button(string name, Transform parent, string text, Vector2 pos)
        {
            var rect = Rect(name, parent, pos, new Vector2(340, 65));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.12f, .4f, .56f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UIText("Text", rect, text, Vector2.zero, new Vector2(330, 60), 25);
            return button;
        }

        static void BuildUI(LabControls controls, Camera camera)
        {
            var go = new GameObject("XR UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            go.transform.position = new Vector3(-2.1f, 1.55f, 3);
            go.transform.localScale = Vector3.one * .0022f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var root = go.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(800, 400);
            var background = Rect("Panel", root, Vector2.zero, root.sizeDelta).gameObject.AddComponent<Image>();
            background.color = new Color(.035f, .07f, .1f, .97f);
            UIText("Title", root, "ПАНЕЛЬ УПРАВЛЕНИЯ", new Vector2(0, 145), new Vector2(760, 55), 30);
            UIText("Slider Label", root, "Яркость: луч + триггер", new Vector2(0, 75), new Vector2(760, 45), 27);
            var sliderRect = Rect("Brightness Slider", root, new Vector2(0, 15), new Vector2(680, 50));
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            var track = Rect("Track", sliderRect, Vector2.zero, new Vector2(680, 12)).gameObject.AddComponent<Image>();
            track.color = new Color(.25f, .35f, .4f);
            track.raycastTarget = true;
            var handleArea = Rect("Handle Area", sliderRect, Vector2.zero, new Vector2(640, 50));
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(34, 50));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(1, .7f, .2f);
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = .63f;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controls.SetBrightness);
            var turn = Button("Turn Mode Button", root, "Сменить поворот", new Vector2(-185, -90));
            var reset = Button("Reset Button", root, "Начать заново", new Vector2(185, -90));
            UnityEventTools.AddPersistentListener(turn.onClick, controls.ToggleTurn);
            UnityEventTools.AddPersistentListener(reset.onClick, controls.ResetScene);
            UIText("Hint", root, "Кнопки: правый луч + триггер", new Vector2(0, -160), new Vector2(760, 35), 23);
        }

        public static void ValidateScene(Scene scene, int lab)
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            if (components.Any(c => c == null)) throw new InvalidOperationException("Missing script in scene.");
            if (components.OfType<Camera>().Count() != 1) throw new InvalidOperationException("Scene must have exactly one camera.");
            if (components.OfType<AudioListener>().Count() != 1) throw new InvalidOperationException("Scene must have one AudioListener.");
            if (components.OfType<ActionBasedController>().Count() != 2) throw new InvalidOperationException("Two controllers are required.");
            if (lab == 2)
            {
                if (components.OfType<XRRayInteractor>().Count() != 1 || components.OfType<XRDirectInteractor>().Count() != 1)
                    throw new InvalidOperationException("Direct and ray interactors are required.");
                foreach (var task in components.OfType<LabVariantAction>())
                {
                    var simple = task.GetComponent<XRSimpleInteractable>();
                    int expectedMask = task.variant == 20 ? DirectMask : RayMask;
                    if (simple.interactionLayers.value != expectedMask || simple.selectEntered.GetPersistentEventCount() != 1)
                        throw new InvalidOperationException("Task mask or Select Entered event is wrong.");
                    if (task.variant != 2 && task.spawnedPrefab == null) throw new InvalidOperationException("Missing spawn prefab.");
                }
                if (components.OfType<ActionBasedContinuousTurnProvider>().Single().enabled)
                    throw new InvalidOperationException("Continuous turn must start disabled.");
                if (!components.OfType<ActionBasedSnapTurnProvider>().Single().enabled)
                    throw new InvalidOperationException("Snap turn must start enabled.");
                if (components.OfType<Slider>().Count() != 1 || components.OfType<HingeJoint>().Count() != 1)
                    throw new InvalidOperationException("Missing UI slider or door hinge.");
            }
        }

        [MenuItem("VR Labs/Open scenes/1 Artemyev - LR1")]
        static void Open11() { Open(1, 1); }
        [MenuItem("VR Labs/Open scenes/1 Artemyev - LR2")]
        static void Open21() { Open(2, 1); }
        [MenuItem("VR Labs/Open scenes/2 Chen - LR1")]
        static void Open12() { Open(1, 2); }
        [MenuItem("VR Labs/Open scenes/2 Chen - LR2")]
        static void Open22() { Open(2, 2); }
        [MenuItem("VR Labs/Open scenes/20 Mukhin - LR1")]
        static void Open120() { Open(1, 20); }
        [MenuItem("VR Labs/Open scenes/20 Mukhin - LR2")]
        static void Open220() { Open(2, 20); }
        static void Open(int lab, int variant)
        {
            if (EditorApplication.isPlaying) return;
            BuildMissing();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(PathFor(lab, variant));
        }

        [MenuItem("VR Labs/Validate current scene")]
        static void ValidateCurrent()
        {
            var identity = Object.FindFirstObjectByType<LabIdentity>();
            if (identity == null) throw new InvalidOperationException("Open one of Assets/VRLabs/Scenes first.");
            ValidateScene(SceneManager.GetActiveScene(), identity.laboratory);
            Debug.Log("VR Labs: scene structure checked. Test interaction in Play Mode and in the headset.");
        }
    }
}
#pragma warning restore CS0618

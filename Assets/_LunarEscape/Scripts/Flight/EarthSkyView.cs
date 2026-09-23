using UnityEngine;

namespace LunarEscape
{
    // 地球属于无限远天空，不跟随头部转向，也不参与月面模型的缩放。
    // 飞行时采用现有外景的旋转，使月面、升空、对接共享同一个天体方向。
    [DefaultExecutionOrder(10004)]
    public sealed class EarthSkyView : MonoBehaviour
    {
        [SerializeField] private Material skyMaterial;
        [SerializeField] private FlightScenePresenter flightScene;
        [SerializeField] private Transform flightOutsideWorld;
        [SerializeField] private Vector3 groundDirection = new(0, .32f, 1);
        [SerializeField, Range(1, 12)] private float angularDiameter = 6;
        private Material instance, previousSky;
        public Vector3 Direction { get; private set; }
        public float AngularDiameter => angularDiameter;

        public void Configure(Material material, FlightScenePresenter scene, Transform outside)
        { skyMaterial = material; flightScene = scene; flightOutsideWorld = outside; }

        private void OnEnable()
        {
            if (skyMaterial == null) return;
            previousSky = RenderSettings.skybox;
            instance = new Material(skyMaterial) { name = "Earth Sky (Runtime)" };
            RenderSettings.skybox = instance;
            Apply();
        }
        private void LateUpdate() => Apply();
        private void Apply()
        {
            if (instance == null) return;
            var rotation = flightScene != null && flightScene.FlightWorld.activeInHierarchy && flightOutsideWorld != null
                ? flightOutsideWorld.rotation : Quaternion.identity;
            var forward = groundDirection.sqrMagnitude > .001f ? groundDirection.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < .001f) right = Vector3.right;
            right.Normalize();
            Direction = rotation * forward;
            instance.SetVector("_EarthDirection", Direction);
            instance.SetVector("_EarthRight", rotation * right);
            instance.SetVector("_EarthUp", rotation * Vector3.Cross(forward, right));
            // 星空与地球共用外景坐标，不随头部转动或月表展示滚动而漂移。
            instance.SetVector("_SkyRight", rotation * Vector3.right);
            instance.SetVector("_SkyUp", rotation * Vector3.up);
            instance.SetVector("_SkyForward", rotation * Vector3.forward);
            // 照片选区宽 170 像素，地球直径约 148 像素；参数表达地球本身的视直径。
            instance.SetFloat("_EarthHalfSize", Mathf.Tan(angularDiameter * .5f * Mathf.Deg2Rad) * 170f / 148f);
        }
        private void OnDisable()
        {
            if (instance == null) return;
            if (RenderSettings.skybox == instance) RenderSettings.skybox = previousSky;
            Destroy(instance);
            instance = null;
        }
    }
}

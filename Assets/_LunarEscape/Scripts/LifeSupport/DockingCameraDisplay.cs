using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 真实三维外部摄像机送往舱内屏幕；不以预录视频替代轨道器位置和姿态。
    public sealed class DockingCameraDisplay : MonoBehaviour
    {
        [SerializeField] private Camera optics;
        [SerializeField] private RawImage screen;
        private RenderTexture texture;
        public Camera Optics => optics;
        public RawImage Screen => screen;
        public RenderTexture Feed => texture;
        public void Configure(Camera source, RawImage output) { optics = source; screen = output; }
        private void OnEnable()
        {
            if (!Application.isPlaying || optics == null || screen == null) return;
            if (texture == null)
            {
                texture = new RenderTexture(1024, 448, 24) { name = "Live Docking Optics", antiAliasing = 1 };
                texture.Create();
            }
            optics.targetTexture = texture; optics.aspect = 1024f / 448;
            screen.texture = texture; optics.enabled = true;
        }
        private void OnDisable() { if (optics != null) optics.enabled = false; }
        private void OnDestroy()
        {
            if (optics != null) optics.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}

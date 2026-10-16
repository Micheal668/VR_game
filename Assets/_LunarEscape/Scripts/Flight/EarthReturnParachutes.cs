using UnityEngine;

namespace LunarEscape
{
    // 伞包位于外壁肩部。分别抽出伞绳、拉直伞衣、充气，绝不缩放整个伞组到舱内。
    public sealed class EarthReturnParachutes : MonoBehaviour
    {
        [SerializeField] private EarthReturnMission mission;
        [SerializeField] private Transform[] canopies, anchors, covers;
        [SerializeField] private LineRenderer[] lines;
        public Transform[] Canopies => canopies;
        public Transform[] Anchors => anchors;
        public LineRenderer[] Lines => lines;
        public void Configure(EarthReturnMission task, Transform[] fabric, Transform[] mounts, Transform[] lids, LineRenderer[] ropes)
        {
            if (mission != null) mission.Changed -= Refresh;
            mission = task; canopies = fabric; anchors = mounts; covers = lids; lines = ropes;
            if (isActiveAndEnabled) mission.Changed += Refresh;
            Refresh();
        }
        private void OnEnable() { if (mission != null) { mission.Changed += Refresh; Refresh(); } }
        private void OnDisable() { if (mission != null) mission.Changed -= Refresh; }
        private void Refresh()
        {
            if (mission == null || canopies == null) return;
            float age = mission.ParachuteAge;
            bool deployed = mission.ParachutesDeployed;
            float extension = Mathf.SmoothStep(0, 1, age / 1.2f);
            float inflation = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 2, age));
            for (int k = 0; k < canopies.Length; k++)
            {
                var mount = anchors[k].localPosition;
                var radial = new Vector3(mount.x, 0, mount.z).normalized;
                var packed = mount + radial * .35f + Vector3.up * .32f;
                var opened = radial * 5.2f + Vector3.up * 14;
                canopies[k].gameObject.SetActive(deployed);
                canopies[k].localPosition = Vector3.Lerp(packed, opened, extension);
                canopies[k].localScale = new Vector3(Mathf.Lerp(.07f, 3.4f, inflation), Mathf.Lerp(.65f, 3.4f, inflation), Mathf.Lerp(.07f, 3.4f, inflation));
                covers[k].localRotation = Quaternion.Euler(-110 * Mathf.SmoothStep(0, 1, age / .35f), 0, 0);
                for (int i = 0; i < 8; i++)
                {
                    var rope = lines[k * 8 + i]; rope.enabled = deployed;
                    float a = i * Mathf.PI / 4;
                    var rim = canopies[k].localPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * canopies[k].localScale.x;
                    rope.SetPosition(0, mount); rope.SetPosition(1, rim);
                }
            }
        }
    }
}

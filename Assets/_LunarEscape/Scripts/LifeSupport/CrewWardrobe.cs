using UnityEngine;

namespace LunarEscape
{
    // 只切换服装渲染，不触碰玩家追踪、NPC 跟随根节点、交互或身体碰撞。
    public sealed class CrewWardrobe : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private Renderer[] suited, casual;
        [SerializeField] private GameObject hangingSuit;
        [SerializeField] private GameObject commanderHangingSuit;
        public GameObject CommanderHangingSuit => commanderHangingSuit;
        public void ConfigureCommanderRack(GameObject model) { commanderHangingSuit=model; Refresh(); }
        public Renderer[] SuitedRenderers => suited;
        public Renderer[] CasualRenderers => casual;
        public GameObject HangingSuit => hangingSuit;
        public void Configure(LifeSupportMission source, Renderer[] spacesuits, Renderer[] uniforms, GameObject rackModel)
        {
            if (life != null) life.Changed -= Refresh;
            life = source; suited = spacesuits; casual = uniforms; hangingSuit = rackModel;
            if (isActiveAndEnabled) life.Changed += Refresh; Refresh();
        }
        private void OnEnable() { if (life != null) { life.Changed += Refresh; Refresh(); } }
        private void OnDisable() { if (life != null) life.Changed -= Refresh; }
        private void Refresh()
        {
            if (life == null) return;
            foreach (var renderer in suited) if (renderer != null) renderer.enabled = life.SuitWorn;
            foreach (var renderer in casual) if (renderer != null) renderer.enabled = !life.SuitWorn;
            if (hangingSuit != null) hangingSuit.SetActive(!life.SuitWorn);
            if (commanderHangingSuit != null) commanderHangingSuit.SetActive(!life.SuitWorn);
        }
    }
}

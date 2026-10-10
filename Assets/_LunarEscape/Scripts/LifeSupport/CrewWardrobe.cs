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
        [SerializeField] private StationExpansionMission expansion;
        [SerializeField] private Transform commanderRoot;
        public void ConfigureIndependentCrew(StationExpansionMission mission, Transform commander)
        { expansion = mission; commanderRoot = commander; }
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
        private void LateUpdate() { if (expansion != null) Refresh(); }
        private void Refresh()
        {
            if (life == null) return;
            bool Dressed(Renderer r) => expansion != null && commanderRoot != null && r.transform.IsChildOf(commanderRoot)
                ? expansion.CommanderSuited : life.SuitWorn;
            foreach (var renderer in suited) if (renderer != null) renderer.enabled = Dressed(renderer);
            foreach (var renderer in casual) if (renderer != null) renderer.enabled = !Dressed(renderer);
            if (hangingSuit != null) hangingSuit.SetActive(!life.SuitWorn);
            if (commanderHangingSuit != null) commanderHangingSuit.SetActive(expansion != null ? !expansion.CommanderSuited : !life.SuitWorn);
        }
    }
}

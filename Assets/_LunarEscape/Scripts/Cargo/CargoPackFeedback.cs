using UnityEngine;

namespace LunarEscape
{
    public sealed class CargoPackFeedback : MonoBehaviour
    {
        [SerializeField] private CargoPackZone zone;
        [SerializeField] private LocalizedText label;
        [SerializeField] private Renderer[] rims;
        private CargoItem previous;
        private MaterialPropertyBlock block;
        public void Configure(CargoPackZone area, LocalizedText text, Renderer[] sides)
        { zone=area; label=text; rims=sides; }
        private void LateUpdate()
        {
            if(zone==null)return;
            var item=zone.ReadyItem;
            if(item!=null && item!=previous)HandHaptics.Pulse(HandHaptics.FromGrab(item.Grab),.25f,.06f);
            previous=item;
            if(label!=null)label.SetKey(item!=null ? "cargo.pack.ready" : "cargo.pack");
            block??=new MaterialPropertyBlock();
            Color color=item!=null ? new Color(.2f,1,.45f) : new Color(.1f,.7f,.85f);
            foreach(var rim in rims)
            {
                if(rim==null)continue;
                rim.GetPropertyBlock(block);block.SetColor("_BaseColor",color);block.SetColor("_EmissionColor",color*.5f);rim.SetPropertyBlock(block);
            }
        }
    }
}

using TMPro;
using UnityEngine;

namespace LunarEscape
{
    public sealed class ExpansionResourcePanel : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private TMP_Text label;
        [SerializeField] private bool oxygen;
        private LocalizationService language;
        public void Configure(LifeSupportMission source, TMP_Text text, bool oxygenPanel) { life=source; label=text; oxygen=oxygenPanel; }
        private void Awake() => language=FindAnyObjectByType<LocalizationService>();
        private string Text(string zh,string en,string ru)=>language!=null&&language.CurrentLanguage==GameLanguage.English?en:language!=null&&language.CurrentLanguage==GameLanguage.Russian?ru:zh;
        private void Update()
        {
            if(life==null||label==null)return;
            if(oxygen)label.text=life.Station.RepairRestored?Text("制氧机已修复 · 耗氧减半","O2 REPAIRED · HALF DRAIN","O2 ИСПРАВЕН · РАСХОД ВДВОЕ МЕНЬШЕ")
                :Text("扳手保持 4 秒 · 进度 ","HOLD WRENCH 4 s · ","УДЕРЖИВАЙТЕ КЛЮЧ 4 с · ")+Mathf.RoundToInt(life.Station.RepairTask.Progress*100)+"%";
            else label.text=Text("基地电量 ","BASE POWER ","ЗАРЯД БАЗЫ ")+life.BasePower.ToString("0.0")+"%\n"
                +Text("应急照明共用 · 接错 −5%","SHARED EMERGENCY POWER · ERROR −5%","ОБЩЕЕ АВАРИЙНОЕ ПИТАНИЕ · ОШИБКА −5%");
        }
    }
}

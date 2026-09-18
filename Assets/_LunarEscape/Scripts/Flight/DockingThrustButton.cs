using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LunarEscape
{
    [RequireComponent(typeof(Button))]
    public sealed class DockingThrustButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        [SerializeField] private DockingMission mission;
        [SerializeField] private DockCommand command;
        public DockCommand Command=>command;
        public void Configure(DockingMission task,DockCommand control){mission=task;command=control;}
        public void OnPointerDown(PointerEventData data){if(GetComponent<Button>().IsInteractable())mission.SetCommand(command,true);}
        public void OnPointerUp(PointerEventData data)=>Release();
        public void OnPointerExit(PointerEventData data)=>Release();
        private void OnDisable()=>Release();
        private void Release(){if(mission!=null)mission.SetCommand(command,false);}
    }
}

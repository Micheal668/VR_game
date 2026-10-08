using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    [RequireComponent(typeof(Button))]
    public sealed class CircularizeKeyPresenter : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        public void Configure(AscentMission source) { flight=source; }
        private void Update() { if(flight!=null)GetComponent<Button>().interactable=flight.CanCircularize; }
    }
}

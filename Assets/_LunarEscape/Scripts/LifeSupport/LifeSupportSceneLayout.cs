using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 可检查的场景引用，供验收和后续美术迭代使用，不参与规则判断。
    public sealed class LifeSupportSceneLayout : MonoBehaviour
    {
        [SerializeField] private GameObject habitatConsole, supplyConsole, suitRack, airlockConsole, cockpit;
        [SerializeField] private GameObject resourceScreen, navigationScreen, operationsScreen;
        [SerializeField] private Button beginButton;
        [SerializeField] private SuitRackInteractor suitHandle;
        public GameObject HabitatConsole => habitatConsole;
        public GameObject SupplyConsole => supplyConsole;
        public GameObject SuitRack => suitRack;
        public GameObject AirlockConsole => airlockConsole;
        public GameObject Cockpit => cockpit;
        public GameObject ResourceScreen => resourceScreen;
        public GameObject NavigationScreen => navigationScreen;
        public GameObject OperationsScreen => operationsScreen;
        public Button BeginButton => beginButton;
        public SuitRackInteractor SuitHandle => suitHandle;
        public void Configure(GameObject habitat, GameObject supplies, GameObject rack, GameObject hatch, GameObject cabin,
            GameObject resources, GameObject navigation, GameObject operations, Button begin, SuitRackInteractor handle)
        { habitatConsole = habitat; supplyConsole = supplies; suitRack = rack; airlockConsole = hatch; cockpit = cabin;
            resourceScreen = resources; navigationScreen = navigation; operationsScreen = operations; beginButton = begin; suitHandle = handle; }
    }
}

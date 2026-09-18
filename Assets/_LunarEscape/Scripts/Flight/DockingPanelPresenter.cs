using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    public sealed class DockingPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        [SerializeField] private DockingMission docking;
        [SerializeField] private GameObject ascentPanel,dockingPanel;
        [SerializeField] private LocalizedText telemetry,status,help,assistLabel;
        [SerializeField] private LocalizedText alignment;
        [SerializeField] private Button[] supplyButtons;
        private static readonly CargoKind[] SupplyKinds={CargoKind.Oxygen,CargoKind.RepairKit,CargoKind.MedicalKit,CargoKind.Battery};
        [SerializeField] private Button[] thrustButtons;
        [SerializeField] private Button assist,retry;
        [SerializeField] private GameObject commandRoot;
        public GameObject Panel=>dockingPanel;
        public Button RetryButton=>retry;
        public void Configure(AscentMission task,DockingMission controller,GameObject oldPanel,GameObject newPanel,
            LocalizedText instruments,LocalizedText state,LocalizedText instructions,LocalizedText assistText,
            Button[] controls,Button assistance,Button restart,GameObject controlRoot,LocalizedText axes,Button[] supplies)
        {
            flight=task;docking=controller;ascentPanel=oldPanel;dockingPanel=newPanel;telemetry=instruments;status=state;help=instructions;assistLabel=assistText;
            thrustButtons=controls;assist=assistance;retry=restart;commandRoot=controlRoot;
            alignment=axes;supplyButtons=supplies;
        }
        private void OnEnable(){if(flight!=null){flight.Changed+=Refresh;docking.Changed+=Refresh;Refresh();}}
        private void OnDisable(){if(flight!=null){flight.Changed-=Refresh;docking.Changed-=Refresh;}docking?.ReleaseControls();}
        private void Refresh()
        {
            bool visible=flight.Phase==AscentPhase.Rendezvous||flight.Phase==AscentPhase.Docking||flight.Phase==AscentPhase.Docked;
            ascentPanel.SetActive(!visible);dockingPanel.SetActive(visible);
            if(!visible)return;
            telemetry.SetKey("dock.telemetry",docking.Distance.ToString("0.0"),docking.ClosingSpeed.ToString("+0.00;-0.00;0.00"),docking.LateralError.ToString("0.00"),docking.AlignmentError.ToString("0.0"),flight.MainFuel.ToString("0.0"),docking.RcsFuel.ToString("0.0"),Mathf.CeilToInt(flight.OxygenSupportSeconds));
            status.SetKey(docking.State==DockingState.Docked?"dock.status.complete":docking.State==DockingState.Capturing?"dock.status.capture":docking.AssistanceActive?"dock.status.assisting":docking.CanAssist?"dock.status.ready":"dock.status.manual");
            help.SetKey(docking.State==DockingState.Docked?"dock.result":"dock.help",flight.SuppliesUsed,flight.Inventory.GetCount(CargoKind.DataCore),flight.Inventory.GetCount(CargoKind.LunarSample));
            commandRoot.SetActive(docking.State!=DockingState.Docked);retry.gameObject.SetActive(docking.State==DockingState.Docked);
            foreach(var button in thrustButtons)
            {
                var controller=button.GetComponent<DockingThrustButton>();
                button.interactable=docking.Active&&(controller.Command!=DockCommand.MainBoost||docking.CanBoost);
            }
            assist.interactable=docking.Active;assistLabel.SetKey(docking.AssistanceEnabled?"dock.assist.on":"dock.assist.off");
            var euler=docking.Attitude.eulerAngles;
            alignment.SetKey("dock.alignment",docking.Position.x.ToString("+0.00;-0.00;0.00"),docking.Position.y.ToString("+0.00;-0.00;0.00"),Mathf.DeltaAngle(0,euler.x).ToString("+0.0;-0.0;0.0"),Mathf.DeltaAngle(0,euler.y).ToString("+0.0;-0.0;0.0"),Mathf.DeltaAngle(0,euler.z).ToString("+0.0;-0.0;0.0"));
            for(int i=0;i<supplyButtons.Length;i++)
            {supplyButtons[i].interactable=flight.CanUse(SupplyKinds[i]);supplyButtons[i].GetComponentInChildren<LocalizedText>().SetKey("flight.use."+SupplyKinds[i],flight.Inventory.GetCount(SupplyKinds[i]));}
        }
    }
}

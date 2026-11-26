using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

#pragma warning disable CS0618
namespace VRLabs
{
    public class LabControls : MonoBehaviour
    {
        public ActionBasedSnapTurnProvider snapTurn;
        public ActionBasedContinuousTurnProvider continuousTurn;
        public Light roomLight;
        public TextMesh turnLabel;
        public XRRayInteractor ray;
        bool useContinuous;

        void Update()
        {
            // При удержании предмета трекпад управляет расстоянием, а не поворотом игрока.
            bool allowTurn = ray == null || !ray.hasSelection;
            continuousTurn.enabled = allowTurn && useContinuous;
            snapTurn.enabled = allowTurn && !useContinuous;
        }

        public void ToggleTurn()
        {
            useContinuous = !useContinuous;
            continuousTurn.enabled = useContinuous;
            snapTurn.enabled = !useContinuous;
            if (turnLabel != null) turnLabel.text = useContinuous ? "Поворот: плавный" : "Поворот: ступенчатый";
        }

        public void SetBrightness(float value)
        {
            if (roomLight != null) roomLight.intensity = Mathf.Lerp(.25f, 1.6f, value);
        }

        public void ResetScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
    }
}
#pragma warning restore CS0618

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRLabs
{
    public class LabVariantAction : MonoBehaviour
    {
        [Tooltip("0 — общий пример: капсула создаёт цилиндр; 1, 2, 20 — индивидуальные задания.")]
        public int variant;
        public GameObject spawnedPrefab;
        public Transform spawnPoint;
        [Range(4, 20)] public int fragmentCount = 8;
        public float fragmentImpulse = 1.2f;
        bool consumed;

        // Привязано к Select Entered в Inspector. Проверка типа дополняет маски XRI.
        public void OnSelected(SelectEnterEventArgs args)
        {
            if (consumed || args == null) return;
            if (variant == 20)
            {
                if (!(args.interactorObject is XRDirectInteractor)) return;
                BreakSphere();
            }
            else
            {
                if (!(args.interactorObject is XRRayInteractor)) return;
                if (variant == 2)
                {
                    consumed = true;
                    gameObject.SetActive(false);
                }
                else if ((variant == 0 || variant == 1) && spawnedPrefab != null && spawnPoint != null)
                {
                    Instantiate(spawnedPrefab, spawnPoint.position, spawnPoint.rotation);
                }
            }
        }

        void BreakSphere()
        {
            if (spawnedPrefab == null) return;
            consumed = true;
            Vector3 center = transform.position;
            // Равномерное распределение направлений по сфере (золотой угол).
            for (int i = 0; i < fragmentCount; i++)
            {
                float y = 1f - 2f * (i + .5f) / fragmentCount;
                float radius = Mathf.Sqrt(1f - y * y);
                float angle = i * 2.39996323f;
                Vector3 direction = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                GameObject fragment = Instantiate(spawnedPrefab, center + direction * .24f, Quaternion.identity);
                Rigidbody body = fragment.GetComponent<Rigidbody>();
                if (body != null) body.AddForce(direction * fragmentImpulse + Vector3.up * .4f, ForceMode.Impulse);
            }
            // Деактивация сразу убирает коллайдер исходного шара, чтобы он не толкал осколки.
            gameObject.SetActive(false);
        }
    }
}

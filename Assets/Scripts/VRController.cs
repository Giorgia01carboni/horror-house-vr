using UnityEngine;

namespace HorrorHouse.Player.VR
{
    /// <summary>
    /// Sincronizza dinamicamente il CharacterController con i movimenti spaziali del visore Meta.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class VRColliderSync : MonoBehaviour
    {
        [Header("Tracking References")]
        [Tooltip("Assegna qui l'oggetto CenterEyeAnchor che si trova dentro il Camera Rig")]
        [SerializeField] private Transform centerEyeAnchor;

        private CharacterController _characterController;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            UpdateColliderToMatchHeadset();
        }

        private void UpdateColliderToMatchHeadset()
        {
            if (centerEyeAnchor == null)
            {
                Debug.LogWarning("VRColliderSync: CenterEyeAnchor mancante.", this);
                return;
            }

            // 1. L'altezza del collisore diventa l'altezza reale della testa dal pavimento
            _characterController.height = centerEyeAnchor.localPosition.y;

            // 2. Centra il collisore sotto la testa (assi X e Z)
            // L'asse Y viene posizionato a metà altezza per allinearsi alla logica di Unity
            Vector3 newCenter = Vector3.zero;
            newCenter.x = centerEyeAnchor.localPosition.x;
            newCenter.y = _characterController.height / 2f;
            newCenter.z = centerEyeAnchor.localPosition.z;

            _characterController.center = newCenter;
        }
    }
}
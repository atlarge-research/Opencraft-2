using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using Unity.Entities;
using Unity.RenderStreaming;
using UnityEngine.UI;

namespace Opencraft.Player.Multiplay
{
    // Collects input either from local devices or a remote input stream using InputActions
    public class MultiplayPlayerController : MonoBehaviour
    {
        [SerializeField] InputReceiver playerInput;
        public int username;

        public Vector2 inputMovement;
        public Vector2 inputLook;
        public bool inputJump;
        public bool inputStart;
        public bool inputPrimaryAction;
        public bool inputSecondaryAction;
        public bool playerEntityExists;
        public bool playerEntityRequestSent;
        public Entity playerEntity;

        public Text debugText;
        public Text tooltipText;

        protected void Awake()
        {
            playerInput.onDeviceChange += OnDeviceChange;
            username = Random.Range(0, 99999);
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                {
                    playerInput.PerformPairingWithDevice(device);
                    CheckPairedDevices();
                    return;
                }
                case InputDeviceChange.Removed:
                {
                    playerInput.UnpairDevices(device);
                    CheckPairedDevices();
                    return;
                }
            }
        }

        public void CheckPairedDevices()
        {
            if (!playerInput.user.valid)
                return;

            // todo: touchscreen support
            bool hasTouchscreenDevice =
                playerInput.user.pairedDevices.Count(_ => _.path.Contains("Touchscreen")) > 0;
        }

        private void Update()
        {
            debugText.text = $"NumAreas: {DebugStats.numAreas}\n";
            tooltipText.enabled = !playerEntityRequestSent && !playerEntityExists;
        }


        public void OnControlsChanged()
        {
        }

        public void OnDeviceLost()
        {
        }

        public void OnDeviceRegained()
        {
        }

        public void OnMovement(InputAction.CallbackContext value)
        {
            inputMovement = value.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext value)
        {
            inputLook = value.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputJump = true;
            }
        }

        public void OnStart(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputStart = true;
            }
        }
        
        public void OnPrimaryAction(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputPrimaryAction = true;
            }
        }
        
        public void OnSecondaryAction(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputSecondaryAction = true;
            }
        }
    }
}


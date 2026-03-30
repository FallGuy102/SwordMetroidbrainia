using UnityEngine;
using UnityEngine.InputSystem;

namespace SwordMetroidbrainia
{
    // Centralizes gameplay input so movement and abilities do not depend on concrete device bindings.
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const float DirectionThreshold = 0.5f;

        private InputAction _moveAction;
        private InputAction _primaryAbilityAction;
        private InputAction _secondaryAbilityAction;
        private InputAction _openMapAction;

        public Vector2 Move => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        // Converts both keyboard and analog stick input into a strict digital direction for precision movement.
        public Vector2 DigitalMove => new(GetDigitalAxis(Move.x), GetDigitalAxis(Move.y));
        public bool PrimaryAbilityTriggered => _primaryAbilityAction != null && _primaryAbilityAction.WasPressedThisFrame();
        public bool SecondaryAbilityTriggered => _secondaryAbilityAction != null && _secondaryAbilityAction.WasPressedThisFrame();
        public bool SecondaryAbilityReleased => _secondaryAbilityAction != null && _secondaryAbilityAction.WasReleasedThisFrame();
        public bool SecondaryAbilityHeld => _secondaryAbilityAction != null && _secondaryAbilityAction.IsPressed();
        public bool OpenMapTriggered => _openMapAction != null && _openMapAction.WasPressedThisFrame();

        private void Awake()
        {
            _moveAction = new InputAction(name: "Move", type: InputActionType.Value);
            // WASD and gamepad movement intentionally funnel into the same action so downstream code stays device-agnostic.
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");
            _moveAction.AddBinding("<Gamepad>/dpad");

            _primaryAbilityAction = new InputAction(name: "PrimaryAbility", type: InputActionType.Button);
            _primaryAbilityAction.AddBinding("<Mouse>/leftButton");
            _primaryAbilityAction.AddBinding("<Keyboard>/space");
            _primaryAbilityAction.AddBinding("<Gamepad>/buttonSouth");

            _secondaryAbilityAction = new InputAction(name: "SecondaryAbility", type: InputActionType.Button);
            _secondaryAbilityAction.AddBinding("<Mouse>/rightButton");
            _secondaryAbilityAction.AddBinding("<Gamepad>/buttonWest");

            _openMapAction = new InputAction(name: "OpenMap", type: InputActionType.Button);
            _openMapAction.AddBinding("<Keyboard>/tab");
            _openMapAction.AddBinding("<Gamepad>/leftShoulder");
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _primaryAbilityAction?.Enable();
            _secondaryAbilityAction?.Enable();
            _openMapAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _primaryAbilityAction?.Disable();
            _secondaryAbilityAction?.Disable();
            _openMapAction?.Disable();
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
            _primaryAbilityAction?.Dispose();
            _secondaryAbilityAction?.Dispose();
            _openMapAction?.Dispose();
        }

        private static float GetDigitalAxis(float value)
        {
            if (value > DirectionThreshold)
            {
                return 1f;
            }

            if (value < -DirectionThreshold)
            {
                return -1f;
            }

            return 0f;
        }
    }
}

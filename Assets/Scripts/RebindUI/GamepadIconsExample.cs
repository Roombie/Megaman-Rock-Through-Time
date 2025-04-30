using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace UnityEngine.InputSystem.Samples.RebindUI
{
    public class GamepadIconsExample : MonoBehaviour
    {
        public bool allowKeyboardBindings = true;
        public bool allowGamepadBindings = true;

        public GamepadIcons xbox;
        public GamepadIcons ps4;
        public GamepadIcons nintendo;
        public KeyboardIcons keyboard;

        public enum ControlScheme
        {
            Xbox,
            PlayStation,
            Nintendo
        }

        public ControlScheme currentScheme = ControlScheme.Xbox;

        protected void OnEnable()
        {
            keyboard?.Initialize();

            var rebindUIComponents = transform.GetComponentsInChildren<RebindActionUI>(true);
            foreach (var component in rebindUIComponents)
            {
                component.updateBindingUIEvent.AddListener(OnUpdateBindingDisplay);
                component.UpdateBindingDisplay();
            }
        }

        public void SetControlScheme(ControlScheme scheme)
        {
            currentScheme = scheme;
        }

        public void RefreshAllIcons()
        {
            var rebindUIComponents = GetComponentsInChildren<RebindActionUI>(true);
            foreach (var component in rebindUIComponents)
            {
                component.UpdateBindingDisplay();
            }
        }

        protected void OnUpdateBindingDisplay(RebindActionUI component, string bindingDisplayString, string deviceLayoutName, string controlPath)
        {
            
            if (component == null || string.IsNullOrEmpty(controlPath))
                return;

            var icon = default(Sprite);
            var isKeyboard = InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Keyboard");
            var isGamepad = InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "Gamepad");

            if (isKeyboard && !allowKeyboardBindings)
            return;

            if (isGamepad && !allowGamepadBindings)
                return;

            if (isKeyboard)
            {
                icon = keyboard.GetSprite(controlPath);
            }
            else if (isGamepad)
            {
                switch (currentScheme)
                {
                    case ControlScheme.Xbox:
                        icon = xbox.GetSprite(controlPath);
                        break;
                    case ControlScheme.PlayStation:
                        icon = ps4.GetSprite(controlPath);
                        break;
                    case ControlScheme.Nintendo:
                        icon = nintendo.GetSprite(controlPath);
                        break;
                }
            }

            var textComponent = component.bindingText;
            if (textComponent == null || textComponent.transform == null)
                return;

            var imageGO = textComponent.transform.parent.Find("ActionBindingIcon");
            if (imageGO == null)
            {
                Debug.LogWarning("GamepadIconsExample: 'ActionBindingIcon' GameObject not found under bindingText's parent.");
                return;
            }

            var imageComponent = imageGO.GetComponent<Image>();
            if (imageComponent == null)
                return;

            if (icon != null)
            {
                textComponent.gameObject.SetActive(false);
                imageComponent.sprite = icon;
                imageComponent.gameObject.SetActive(true);
            }
            else
            {
                string controlName = controlPath.Replace("<Keyboard>/", "").ToUpperInvariant();
                textComponent.text = controlName;
                textComponent.gameObject.SetActive(true);
                imageComponent.gameObject.SetActive(false);
            }
        }

        [Serializable]
        public class GamepadIcons
        {
            public Sprite buttonSouth;
            public Sprite buttonNorth;
            public Sprite buttonEast;
            public Sprite buttonWest;
            public Sprite startButton;
            public Sprite selectButton;
            public Sprite leftTrigger;
            public Sprite rightTrigger;
            public Sprite leftShoulder;
            public Sprite rightShoulder;
            public Sprite dpad;
            public Sprite dpadUp;
            public Sprite dpadDown;
            public Sprite dpadLeft;
            public Sprite dpadRight;
            public Sprite leftStick;
            public Sprite rightStick;
            public Sprite leftStickPress;
            public Sprite rightStickPress;

            public Sprite GetSprite(string controlPath)
            {
                switch (controlPath)
                {
                    case "buttonSouth": return buttonSouth;
                    case "buttonNorth": return buttonNorth;
                    case "buttonEast": return buttonEast;
                    case "buttonWest": return buttonWest;
                    case "start": return startButton;
                    case "select": return selectButton;
                    case "leftTrigger": return leftTrigger;
                    case "rightTrigger": return rightTrigger;
                    case "leftShoulder": return leftShoulder;
                    case "rightShoulder": return rightShoulder;
                    case "dpad": return dpad;
                    case "dpad/up": return dpadUp;
                    case "dpad/down": return dpadDown;
                    case "dpad/left": return dpadLeft;
                    case "dpad/right": return dpadRight;
                    case "leftStick": return leftStick;
                    case "rightStick": return rightStick;
                    case "leftStickPress": return leftStickPress;
                    case "rightStickPress": return rightStickPress;
                }
                return null;
            }
        }

        [Serializable]
        public class KeyboardIcons
        {
            public List<KeyIcon> icons = new();

            private Dictionary<string, Sprite> _iconMap;

            public void Initialize()
            {
                _iconMap = new Dictionary<string, Sprite>();
                foreach (var icon in icons)
                {
                    if (!string.IsNullOrEmpty(icon.key) && icon.icon != null)
                        _iconMap[icon.key.ToLower()] = icon.icon;
                }
            }

            public Sprite GetSprite(string controlPath)
            {
                if (string.IsNullOrEmpty(controlPath)) return null;

                if (controlPath.StartsWith("<Keyboard>/"))
                    controlPath = controlPath.Replace("<Keyboard>/", "");

                if (_iconMap == null)
                    Initialize();

                return _iconMap.TryGetValue(controlPath.ToLower(), out var sprite) ? sprite : null;
            }

            [Serializable]
            public struct KeyIcon
            {
                public string key;
                public Sprite icon;
            }
        }
    }
}
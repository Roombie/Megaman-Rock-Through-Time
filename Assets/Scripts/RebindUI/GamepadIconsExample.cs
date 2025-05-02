using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.InputSystem.LowLevel;
using System.IO;

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
        public GamepadIcons custom;

        public enum ControlScheme
        {
            Auto,
            Xbox,
            PlayStation,
            Nintendo,
            Custom
        }

        public ControlScheme userScheme = ControlScheme.Auto; // set via menu
        private ControlScheme currentScheme = ControlScheme.Xbox; // updated based on input

        protected void OnEnable()
        {
            keyboard?.Initialize();

            var rebindUIComponents = transform.GetComponentsInChildren<RebindActionUI>(true);
            foreach (var component in rebindUIComponents)
            {
                component.updateBindingUIEvent.AddListener(OnUpdateBindingDisplay);
                component.UpdateBindingDisplay();
            }

            InputSystem.onEvent += OnInputEvent;
        }

        protected void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;

            var input = GetComponent<PlayerInput>();
            if (input != null && input.actions != null)
            {
                input.actions.Disable(); // Helps to avoid leaks or performance issues on scene exit
            }
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (userScheme != ControlScheme.Auto)
                return;

            if (device is not Gamepad gamepad)
                return;

            // Detectar el tipo de control
            var layout = device.layout;

            if (layout.Contains("Xbox") || layout.Contains("XInput"))
                currentScheme = ControlScheme.Xbox;
            else if (layout.Contains("DualShock") || layout.Contains("PlayStation"))
                currentScheme = ControlScheme.PlayStation;
            else if (layout.Contains("Switch") || layout.Contains("Nintendo"))
                currentScheme = ControlScheme.Nintendo;

            RefreshAllIcons();
        }


        public ControlScheme GetCurrentScheme()
        {
            return userScheme == ControlScheme.Auto ? currentScheme : userScheme;
        }

        public void SetControlScheme(ControlScheme scheme)
        {
            userScheme = scheme;
            if (scheme != ControlScheme.Auto)
            {
                currentScheme = scheme;
            }

            RefreshAllIcons();
        }

        public void SetAutoScheme()
        {
            userScheme = ControlScheme.Auto;
            RefreshAllIcons();
        }

        public void SetCustomSprites()
        {
            userScheme = ControlScheme.Custom;
            RefreshAllIcons();
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
                /*string controlName = controlPath.Replace("<Keyboard>/", "").ToUpperInvariant();
                textComponent.text = controlName;
                textComponent.gameObject.SetActive(true);
                imageComponent.gameObject.SetActive(false);*/
                string controlName = controlPath.Replace("<Keyboard>/", "").Replace("<Gamepad>/", "");

                string fallbackText = isGamepad
                    ? controlName switch
                    {
                        "buttonSouth" => "A",
                        "buttonNorth" => "Y",
                        "buttonEast" => "B",
                        "buttonWest" => "X",
                        "start" => "START",
                        "select" => "SELECT",
                        "leftTrigger" => "LT",
                        "rightTrigger" => "RT",
                        "leftShoulder" => "LB",
                        "rightShoulder" => "RB",
                        "dpad" => "DPAD",
                        "dpad/up" => "↑",
                        "dpad/down" => "↓",
                        "dpad/left" => "←",
                        "dpad/right" => "→",
                        "leftStick" => "L-STICK",
                        "rightStick" => "R-STICK",
                        "leftStickPress" => "L-CLICK",
                        "rightStickPress" => "R-CLICK",
                        _ => controlName.ToUpperInvariant()
                    }
                    : controlName.ToUpperInvariant();

                textComponent.text = fallbackText;
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

        private void LoadCustomSprites()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "CustomIcons");

            if (!Directory.Exists(path))
            {
                Debug.LogWarning("CustomIcons folder not found.");
                return;
            }

            Dictionary<string, string> iconFileMap = new()
            {
                { "buttonSouth", "buttonSouth.png" },
                { "buttonNorth", "buttonNorth.png" },
                { "buttonEast", "buttonEast.png" },
                { "buttonWest", "buttonWest.png" },
                { "start", "startButton.png" },
                { "select", "selectButton.png" },
                { "leftTrigger", "leftTrigger.png" },
                { "rightTrigger", "rightTrigger.png" },
                { "leftShoulder", "leftShoulder.png" },
                { "rightShoulder", "rightShoulder.png" },
                { "dpad", "dpad.png" },
                { "dpad/up", "dpadUp.png" },
                { "dpad/down", "dpadDown.png" },
                { "dpad/left", "dpadLeft.png" },
                { "dpad/right", "dpadRight.png" },
                { "leftStick", "leftStick.png" },
                { "rightStick", "rightStick.png" },
                { "leftStickPress", "leftStickPress.png" },
                { "rightStickPress", "rightStickPress.png" }
            };

            foreach (var pair in iconFileMap)
            {
                string filePath = Path.Combine(path, pair.Value);
                if (!File.Exists(filePath)) continue;

                byte[] data = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(data))
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    AssignSpriteToCustom(pair.Key, sprite);
                }
            }
        }

        private void AssignSpriteToCustom(string controlPath, Sprite sprite)
        {
            switch (controlPath)
            {
                case "buttonSouth": custom.buttonSouth = sprite; break;
                case "buttonNorth": custom.buttonNorth = sprite; break;
                case "buttonEast": custom.buttonEast = sprite; break;
                case "buttonWest": custom.buttonWest = sprite; break;
                case "start": custom.startButton = sprite; break;
                case "select": custom.selectButton = sprite; break;
                case "leftTrigger": custom.leftTrigger = sprite; break;
                case "rightTrigger": custom.rightTrigger = sprite; break;
                case "leftShoulder": custom.leftShoulder = sprite; break;
                case "rightShoulder": custom.rightShoulder = sprite; break;
                case "dpad": custom.dpad = sprite; break;
                case "dpad/up": custom.dpadUp = sprite; break;
                case "dpad/down": custom.dpadDown = sprite; break;
                case "dpad/left": custom.dpadLeft = sprite; break;
                case "dpad/right": custom.dpadRight = sprite; break;
                case "leftStick": custom.leftStick = sprite; break;
                case "rightStick": custom.rightStick = sprite; break;
                case "leftStickPress": custom.leftStickPress = sprite; break;
                case "rightStickPress": custom.rightStickPress = sprite; break;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;

////TODO: localization support

////TODO: deal with composites that have parts bound in different control schemes

namespace UnityEngine.InputSystem.Samples.RebindUI
{
    /// <summary>
    /// A reusable component with a self-contained UI for rebinding a single action.
    /// </summary>
    public class RebindActionUI : MonoBehaviour
    {
        /// <summary>
        /// Reference to the action that is to be rebound.
        /// </summary>
        public InputActionReference actionReference
        {
            get => m_Action;
            set
            {
                m_Action = value;
                UpdateActionLabel();
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// ID (in string form) of the binding that is to be rebound on the action.
        /// </summary>
        /// <seealso cref="InputBinding.id"/>
        public string bindingId
        {
            get => m_BindingId;
            set
            {
                m_BindingId = value;
                UpdateBindingDisplay();
            }
        }

        public InputBinding.DisplayStringOptions displayStringOptions
        {
            get => m_DisplayStringOptions;
            set
            {
                m_DisplayStringOptions = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Text component that receives the name of the action. Optional.
        /// </summary>
        public TMP_Text actionLabel
        {
            get => m_ActionLabel;
            set
            {
                m_ActionLabel = value;
                UpdateActionLabel();
            }
        }

        /// <summary>
        /// Text component that receives the display string of the binding. Can be <c>null</c> in which
        /// case the component entirely relies on <see cref="updateBindingUIEvent"/>.
        /// </summary>
        public TMP_Text bindingText
        {
            get => m_BindingText;
            set
            {
                m_BindingText = value;
                UpdateBindingDisplay();
            }
        }

        /// <summary>
        /// Optional text component that receives a text prompt when waiting for a control to be actuated.
        /// </summary>
        /// <seealso cref="startRebindEvent"/>
        /// <seealso cref="rebindOverlay"/>
        public TMP_Text rebindPrompt
        {
            get => m_RebindText;
            set => m_RebindText = value;
        }

        /// <summary>
        /// Optional UI that is activated when an interactive rebind is started and deactivated when the rebind
        /// is finished. This is normally used to display an overlay over the current UI while the system is
        /// waiting for a control to be actuated.
        /// </summary>
        /// <remarks>
        /// If neither <see cref="rebindPrompt"/> nor <c>rebindOverlay</c> is set, the component will temporarily
        /// replaced the <see cref="bindingText"/> (if not <c>null</c>) with <c>"Waiting..."</c>.
        /// </remarks>
        /// <seealso cref="startRebindEvent"/>
        /// <seealso cref="rebindPrompt"/>
        public GameObject rebindOverlay
        {
            get => m_RebindOverlay;
            set => m_RebindOverlay = value;
        }

        /// <summary>
        /// Event that is triggered every time the UI updates to reflect the current binding.
        /// This can be used to tie custom visualizations to bindings.
        /// </summary>
        public UpdateBindingUIEvent updateBindingUIEvent
        {
            get
            {
                if (m_UpdateBindingUIEvent == null)
                    m_UpdateBindingUIEvent = new UpdateBindingUIEvent();
                return m_UpdateBindingUIEvent;
            }
        }

        /// <summary>
        /// Event that is triggered when an interactive rebind is started on the action.
        /// </summary>
        public InteractiveRebindEvent startRebindEvent
        {
            get
            {
                if (m_RebindStartEvent == null)
                    m_RebindStartEvent = new InteractiveRebindEvent();
                return m_RebindStartEvent;
            }
        }

        /// <summary>
        /// Event that is triggered when an interactive rebind has been completed or canceled.
        /// </summary>
        public InteractiveRebindEvent stopRebindEvent
        {
            get
            {
                if (m_RebindStopEvent == null)
                    m_RebindStopEvent = new InteractiveRebindEvent();
                return m_RebindStopEvent;
            }
        }

        /// <summary>
        /// When an interactive rebind is in progress, this is the rebind operation controller.
        /// Otherwise, it is <c>null</c>.
        /// </summary>
        public InputActionRebindingExtensions.RebindingOperation ongoingRebind => m_RebindOperation;

        /// <summary>
        /// Return the action and binding index for the binding that is targeted by the component
        /// according to
        /// </summary>
        /// <param name="action"></param>
        /// <param name="bindingIndex"></param>
        /// <returns></returns>
        public bool ResolveActionAndBinding(out InputAction action, out int bindingIndex)
        {
            bindingIndex = -1;

            action = m_Action?.action;
            if (action == null)
                return false;

            if (string.IsNullOrEmpty(m_BindingId))
                return false;

            // Look up binding index.
            var bindingId = new Guid(m_BindingId);
            bindingIndex = action.bindings.IndexOf(x => x.id == bindingId);
            if (bindingIndex == -1)
            {
                Debug.LogError($"Cannot find binding with ID '{bindingId}' on '{action}'", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Trigger a refresh of the currently displayed binding.
        /// </summary>
        public void UpdateBindingDisplay()
        {
            if (m_Action == null || m_Action.action == null)
            {
                Debug.LogWarning("UpdateBindingDisplay: Action reference is null.");
                return;
            }

            if (string.IsNullOrEmpty(m_BindingId))
            {
                Debug.LogWarning("UpdateBindingDisplay: Binding ID is null or empty.");
                return;
            }

            var action = m_Action.action;
            int bindingIndex = -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id.ToString() == m_BindingId)
                {
                    bindingIndex = i;
                    break;
                }
            }

            if (bindingIndex == -1)
            {
                Debug.LogWarning($"UpdateBindingDisplay: Binding ID {m_BindingId} not found in action {action.name}.");
                return;
            }

            // Retrieve the display string safely
            string displayString = action.GetBindingDisplayString(bindingIndex, out string deviceLayoutName, out string controlPath, displayStringOptions);

            if (m_BindingText != null)
            {
                m_BindingText.text = displayString;
            }
            else
            {
                Debug.LogWarning("UpdateBindingDisplay: m_BindingText is null, cannot update binding text.");
            }

            if (m_BindingIcon != null)
            {
                m_BindingIcon.gameObject.SetActive(!string.IsNullOrEmpty(displayString));
            }

            // Safely invoke the event
            m_UpdateBindingUIEvent?.Invoke(this, displayString, deviceLayoutName, controlPath);
        }

        /// <summary>
        /// Remove currently applied binding overrides.
        /// </summary>
        public void ResetToDefault()
        {
            if (!ResolveActionAndBinding(out var action, out var bindingIndex))
                return;

            ResetBinding(action, bindingIndex);

            if (action.bindings[bindingIndex].isComposite)
            {
                for (var i = bindingIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; ++i)
                    action.RemoveBindingOverride(i);
            }

            InputActionAsset bindingAsset = action.actionMap?.asset;
            if (bindingAsset != null)
            {
                bindingAsset.SaveBindingOverridesAsJson();
            }

            UpdateBindingDisplay();
        }

        private void ResetBinding(InputAction action, int bindingIndex)
        {
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return;

            InputBinding newBinding = action.bindings[bindingIndex];
            string oldOverridePath = newBinding.overridePath;

            action.RemoveBindingOverride(bindingIndex);

            foreach (InputAction otherAction in action.actionMap.actions)
            {
                foreach (var binding in otherAction.bindings)
                {
                    if (binding.overridePath == newBinding.path)
                    {
                        otherAction.ApplyBindingOverride(binding.id.ToString(), oldOverridePath);
                    }
                }
            }

            InputActionAsset bindingAsset = action.actionMap?.asset;
            if (bindingAsset != null)
            {
                bindingAsset.SaveBindingOverridesAsJson();
            }

            UpdateBindingDisplay();
        }

        /// <summary>
        /// Initiate an interactive rebind that lets the player actuate a control to choose a new binding
        /// for the action.
        /// </summary>
        public void StartInteractiveRebind()
        {
            if (m_rebindStartClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOnce(m_rebindStartClip);
            }

            m_Action.action.Disable();

            if (!ResolveActionAndBinding(out var action, out var bindingIndex))
                return;

            // If the binding is a composite, we need to rebind each part in turn.
            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;
                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
                    PerformInteractiveRebind(action, firstPartIndex, allCompositeParts: true);
            }
            else
            {
                PerformInteractiveRebind(action, bindingIndex);
            }
        }

        private void PerformInteractiveRebind(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                Debug.LogError("Invalid PerformInteractiveRebind parameters");
                return;
            }

            m_RebindOperation?.Cancel();
            action.Disable();

            void CleanUp()
            {
                m_RebindOperation?.Dispose();
                m_RebindOperation = null;
                m_Action.action.Enable();
            }

            // Save the previous overridePath in case we need to restore it
            string previousPath = action.bindings[bindingIndex].effectivePath;
            string previousOverridePath = action.bindings[bindingIndex].overridePath;
            
            isRebindError = false;
            m_RebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Mouse>/leftButton")
                .WithControlsExcluding("<Mouse>/rightButton")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(operation =>
                {
                    if (!isRebindError && m_rebindCancelClip != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayOnce(m_rebindCancelClip);
                    }
                    m_RebindStopEvent?.Invoke(this, operation);
                    m_RebindOverlay?.SetActive(false);
                    UpdateBindingDisplay();
                    CleanUp();
                })
                .OnPotentialMatch(operation =>
                {
                    var control = operation.selectedControl;
                    var layout = control?.device?.layout;

                    bool isKeyboard = layout == "Keyboard" || layout == "Mouse";
                    bool isGamepad = layout == "Gamepad" || layout == "DualShockGamepad" || layout == "XInputController" || layout == "SwitchProControllerHID";

                    if ((isKeyboard && !allowKeyboardBindings) || (isGamepad && !allowGamepadBindings))
                    {
                        Debug.LogWarning("Forbidden device detected during rebind.");
                        ShowError("ButtonNotAllowedInScheme");

                        action.Disable();

                        isRebindError = true;
                        PerformInteractiveRebind(action, bindingIndex, allCompositeParts);
                        return;
                    }
                })
                .OnComplete(operation =>
                {
                    if (CheckDuplicatesBinding(action, bindingIndex, allCompositeParts))
                    {
                        // Restore the previous override path instead of removing it entirely
                        if (!string.IsNullOrEmpty(previousOverridePath))
                        {
                            action.ApplyBindingOverride(bindingIndex, previousOverridePath);
                        }
                        else
                        {
                            action.ApplyBindingOverride(bindingIndex, previousPath);
                        }

                        ShowError("ButtonAlreadyAssigned");

                        action.Disable();

                        isRebindError = true;
                        PerformInteractiveRebind(action, bindingIndex, allCompositeParts);
                        return;
                    }

                    UpdateBindingDisplay();
                    FindFirstObjectByType<RebindSaveLoad>()?.SaveBindings();
                    m_RebindOverlay?.SetActive(false);
                    m_RebindStopEvent?.Invoke(this, operation);

                    if (m_rebindSuccessClip != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayOnce(m_rebindSuccessClip);
                    }

                    CleanUp();

                    if (allCompositeParts)
                    {
                        var nextBindingIndex = bindingIndex + 1;
                        if (nextBindingIndex < action.bindings.Count && action.bindings[nextBindingIndex].isPartOfComposite)
                            PerformInteractiveRebind(action, nextBindingIndex, true);
                    }

                    isRebindError = false;
                });

            m_RebindOverlay?.SetActive(true);
            m_ErrorText.gameObject.SetActive(false);
            /*if (m_RebindText != null)
            {
                m_RebindText.text = "Waiting for input...";
            }*/

            m_RebindOperation.Start();
        }

        private bool CheckDuplicatesBinding(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            InputBinding newBinding = action.bindings[bindingIndex];

            var actionMap = action.actionMap;
            if (actionMap == null)
                return false;

            foreach (var otherAction in actionMap.actions)
            {
                foreach (var binding in otherAction.bindings)
                {
                    // Skip the binding that we are currently changing
                    if (binding.id == newBinding.id)
                        continue;

                    if (binding.effectivePath == newBinding.overridePath || binding.effectivePath == newBinding.path)
                    {
                        Debug.Log("Duplicate binding detected with action: " + otherAction.name + " at path: " + binding.effectivePath);
                        return true;
                    }
                }
            }

            if (allCompositeParts)
            {
                for (int i = 1; i < bindingIndex; i++)
                {
                    if (action.bindings[i].effectivePath == newBinding.overridePath)
                    {
                        Debug.Log("Duplicate composite binding found: " + newBinding.effectivePath);
                        return true;
                    }
                }
            }

            return false;
        }

        public void ShowError(string entryId)
        {
            if (m_ErrorText == null)
                return;

            _localizedErrorString.TableEntryReference = entryId;

        #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync("GameText");
                tableOperation.Completed += (handle) =>
                {
                    var table = handle.Result;
                    if (table != null)
                    {
                        var entry = table.GetEntry(entryId);
                        if (entry != null)
                        {
                            m_ErrorText.text = entry.GetLocalizedString();
                        }
                        else
                        {
                            m_ErrorText.text = $"[No entry: {entryId}]";
                            Debug.LogWarning($"Entry ID '{entryId}' not found in GameText table.");
                        }
                        m_ErrorText.gameObject.SetActive(true);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to load GameText table in Editor.");
                    }
                };
            }
            else
        #endif
            {
                var localizedStringOperation = _localizedErrorString.GetLocalizedStringAsync();
                localizedStringOperation.Completed += (handle) =>
                {
                    if (handle.Status == ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        m_ErrorText.text = handle.Result;
                    }
                    else
                    {
                        m_ErrorText.text = $"[No localized string: {entryId}]";
                    }
                    m_ErrorText.gameObject.SetActive(true);
                };
            }

            if (m_rebindFailedClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOnce(m_rebindFailedClip);
            }
        }

        private void UpdateErrorText(string localizedText)
        {
            if (m_ErrorText == null)
                return;

            // m_ErrorText.text = ""; // just in case
            m_ErrorText.text = localizedText;
            m_ErrorText.gameObject.SetActive(true);
        }

        protected void OnEnable()
        {
            if (s_RebindActionUIs == null)
                s_RebindActionUIs = new List<RebindActionUI>();

            if (!s_RebindActionUIs.Contains(this))
                s_RebindActionUIs.Add(this);

            if (s_RebindActionUIs.Count == 1)
                InputSystem.onActionChange += OnActionChange;

            RebindSaveLoad.OnBindingsReset += UpdateBindingDisplay;
        }

        protected void OnDisable()
        {
            m_RebindOperation?.Dispose();
            m_RebindOperation = null;

            if (m_Action != null && m_Action.action != null)
                m_Action.action.Disable();

            if (s_RebindActionUIs != null && s_RebindActionUIs.Contains(this))
                s_RebindActionUIs.Remove(this);

            if (s_RebindActionUIs != null && s_RebindActionUIs.Count == 0)
            {
                s_RebindActionUIs = null;
                InputSystem.onActionChange -= OnActionChange;
            }

            RebindSaveLoad.OnBindingsReset -= UpdateBindingDisplay;
        }

        
        private void Awake()
        {
            if (m_ErrorText == null)
            Debug.LogWarning("ErrorText is not assigned in RebindActionUI.");

            _localizedErrorString = new LocalizedString
            {
                TableReference = "GameText"
            };

            _localizedErrorString.StringChanged += UpdateErrorText;
        }

        private void OnDestroy()
        {
            _localizedErrorString.StringChanged -= UpdateErrorText;

            if (m_Action != null && m_Action.action != null)
                m_Action.action.Disable();
        }

        // When the action system re-resolves bindings, we want to update our UI in response. While this will
        // also trigger from changes we made ourselves, it ensures that we react to changes made elsewhere. If
        // the user changes keyboard layout, for example, we will get a BoundControlsChanged notification and
        // will update our UI to reflect the current keyboard layout.
        private static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged)
                return;

            var action = obj as InputAction;
            var actionMap = action?.actionMap ?? obj as InputActionMap;
            var actionAsset = actionMap?.asset ?? obj as InputActionAsset;

            for (var i = 0; i < s_RebindActionUIs.Count; ++i)
            {
                var component = s_RebindActionUIs[i];
                var referencedAction = component.actionReference?.action;
                if (referencedAction == null)
                    continue;

                if (referencedAction == action ||
                    referencedAction.actionMap == actionMap ||
                    referencedAction.actionMap?.asset == actionAsset)
                    component.UpdateBindingDisplay();
            }
        }

        [Tooltip("Reference to action that is to be rebound from the UI.")]
        [SerializeField]
        private InputActionReference m_Action;

        [SerializeField]
        private string m_BindingId;

        [SerializeField]
        private InputBinding.DisplayStringOptions m_DisplayStringOptions;

        [Tooltip("Text label that will receive the name of the action. Optional. Set to None to have the "
            + "rebind UI not show a label for the action.")]
        [SerializeField]
        private TMP_Text m_ActionLabel;

        [Tooltip("Text label that will receive the current, formatted binding string.")]
        [SerializeField]
        private TMP_Text m_BindingText;

        [Tooltip("Optional UI that will be shown while a rebind is in progress.")]
        [SerializeField]
        private GameObject m_RebindOverlay;

        [Tooltip("Text label that will show current error when performing the rebind")]
        [SerializeField]
        private TMP_Text m_ErrorText;
        [SerializeField]
        private LocalizedString _localizedErrorString;

        [Tooltip("Optional text label that will be updated with prompt for user input.")]
        [SerializeField]
        private TMP_Text m_RebindText;

        [Tooltip("Image component that will show the binding icon.")]
        [SerializeField] private Image m_BindingIcon;
        public Image BindingIcon => m_BindingIcon;

        public bool allowKeyboardBindings = true;
        public bool allowGamepadBindings = true;

        [SerializeField]
        private AudioClip m_rebindStartClip;
        [SerializeField]
        private AudioClip m_rebindSuccessClip;
        [SerializeField]
        private AudioClip m_rebindFailedClip;
        [SerializeField]
        private AudioClip m_rebindCancelClip;
        private bool isRebindError = false;

        [Tooltip("Event that is triggered when the way the binding is display should be updated. This allows displaying "
            + "bindings in custom ways, e.g. using images instead of text.")]
        [SerializeField]
        private UpdateBindingUIEvent m_UpdateBindingUIEvent;

        [Tooltip("Event that is triggered when an interactive rebind is being initiated. This can be used, for example, "
            + "to implement custom UI behavior while a rebind is in progress. It can also be used to further "
            + "customize the rebind.")]
        [SerializeField]
        private InteractiveRebindEvent m_RebindStartEvent;

        [Tooltip("Event that is triggered when an interactive rebind is complete or has been aborted.")]
        [SerializeField]
        private InteractiveRebindEvent m_RebindStopEvent;

        private InputActionRebindingExtensions.RebindingOperation m_RebindOperation;

        private static List<RebindActionUI> s_RebindActionUIs;

        // We want the label for the action name to update in edit mode, too, so
        // we kick that off from here.
        #if UNITY_EDITOR
        protected void OnValidate()
        {
            if (_localizedErrorString == null)
            {
                _localizedErrorString = new LocalizedString
                {
                    TableReference = "GameText"
                };
            }

            UpdateActionLabel();
            UpdateBindingDisplay();
        }

        #endif

        private void Start() {
            UpdateActionLabel();
            UpdateBindingDisplay();
        }

        private void UpdateActionLabel()
        {
            if (m_ActionLabel != null)
            {
                var action = m_Action?.action;
                m_ActionLabel.text = action != null ? action.name : string.Empty;
            }
        }

        [Serializable]
        public class UpdateBindingUIEvent : UnityEvent<RebindActionUI, string, string, string>
        {
        }

        [Serializable]
        public class InteractiveRebindEvent : UnityEvent<RebindActionUI, InputActionRebindingExtensions.RebindingOperation>
        {
        }
    }
}
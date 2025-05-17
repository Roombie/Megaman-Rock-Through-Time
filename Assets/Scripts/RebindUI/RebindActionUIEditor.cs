#if UNITY_EDITOR
using System.Linq;
using UnityEditor;

////TODO: support multi-object editing

namespace UnityEngine.InputSystem.Samples.RebindUI
{
    /// <summary>
    /// A custom inspector for <see cref="RebindActionUI"/> which provides a more convenient way for
    /// picking the binding which to rebind.
    /// </summary>
    [CustomEditor(typeof(RebindActionUI))]
    [CanEditMultipleObjects]
    public class RebindActionUIEditor : UnityEditor.Editor
    {

        protected void OnEnable()
        {
            m_ActionProperty = serializedObject.FindProperty("m_Action");
            m_BindingIdProperty = serializedObject.FindProperty("m_BindingId");
            m_ActionLabelProperty = serializedObject.FindProperty("m_ActionLabel");
            m_BindingTextProperty = serializedObject.FindProperty("m_BindingText");
            m_RebindOverlayProperty = serializedObject.FindProperty("m_RebindOverlay");
            m_RebindTextProperty = serializedObject.FindProperty("m_RebindText");
            m_BindingIconProperty = serializedObject.FindProperty("m_BindingIcon");
            m_RebindStartEventProperty = serializedObject.FindProperty("m_RebindStartEvent");
            m_RebindStopEventProperty = serializedObject.FindProperty("m_RebindStopEvent");
            m_UpdateBindingUIEventProperty = serializedObject.FindProperty("m_UpdateBindingUIEvent");
            m_DisplayStringOptionsProperty = serializedObject.FindProperty("m_DisplayStringOptions");
            m_AllowKeyboardBindingsProperty = serializedObject.FindProperty("allowKeyboardBindings");
            m_AllowGamepadBindingsProperty = serializedObject.FindProperty("allowGamepadBindings");
            m_ErrorTextProperty = serializedObject.FindProperty("m_ErrorText");
            m_RebindStartClipProperty = serializedObject.FindProperty("m_rebindStartClip");
            m_RebindSuccessClipProperty = serializedObject.FindProperty("m_rebindSuccessClip");
            m_RebindFailedClipProperty = serializedObject.FindProperty("m_rebindFailedClip");
            m_RebindCancelClipProperty = serializedObject.FindProperty("m_rebindCancelClip");

            RefreshBindingOptions();
        }
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            // Binding section.
            EditorGUILayout.LabelField(m_BindingLabel, Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_ActionProperty);

                if (!serializedObject.isEditingMultipleObjects && m_BindingOptions != null)
                {
                    var newSelectedBinding = EditorGUILayout.Popup(m_BindingLabel, m_SelectedBindingOption, m_BindingOptions);
                    if (newSelectedBinding != m_SelectedBindingOption)
                    {
                        var bindingId = m_BindingOptionValues[newSelectedBinding];
                        m_BindingIdProperty.stringValue = bindingId;
                        m_SelectedBindingOption = newSelectedBinding;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Binding selection not supported for multi-object editing.", MessageType.Info);
                }

                var optionsOld = (InputBinding.DisplayStringOptions)m_DisplayStringOptionsProperty.intValue;
                var optionsNew = (InputBinding.DisplayStringOptions)EditorGUILayout.EnumFlagsField(m_DisplayOptionsLabel, optionsOld);
                if (optionsOld != optionsNew)
                    m_DisplayStringOptionsProperty.intValue = (int)optionsNew;
            }

            // UI section.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(m_UILabel, Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_ActionLabelProperty);
                EditorGUILayout.PropertyField(m_BindingTextProperty);
                EditorGUILayout.PropertyField(m_RebindOverlayProperty);
                EditorGUILayout.PropertyField(m_RebindTextProperty);
                EditorGUILayout.PropertyField(m_BindingIconProperty);
                EditorGUILayout.PropertyField(m_ErrorTextProperty);
            }

             EditorGUILayout.Space();
            EditorGUILayout.LabelField("Allowed Devices", Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_AllowKeyboardBindingsProperty);
                EditorGUILayout.PropertyField(m_AllowGamepadBindingsProperty);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feedback Audio", Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_RebindStartClipProperty);
                EditorGUILayout.PropertyField(m_RebindSuccessClipProperty);
                EditorGUILayout.PropertyField(m_RebindFailedClipProperty);
                EditorGUILayout.PropertyField(m_RebindCancelClipProperty);
            }

            // Events section.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(m_EventsLabel, Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_RebindStartEventProperty);
                EditorGUILayout.PropertyField(m_RebindStopEventProperty);
                EditorGUILayout.PropertyField(m_UpdateBindingUIEventProperty);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                RefreshBindingOptions();
            }
        }

        protected void RefreshBindingOptions()
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                m_BindingOptions = new GUIContent[] { new GUIContent("[Multiple Objects Selected]") };
                m_BindingOptionValues = new string[] { "" };
                m_SelectedBindingOption = -1;
                return;
            }

            var actionReference = (InputActionReference)m_ActionProperty.objectReferenceValue;
            var action = actionReference?.action;

            if (action == null)
            {
                m_BindingOptions = new GUIContent[0];
                m_BindingOptionValues = new string[0];
                m_SelectedBindingOption = -1;
                return;
            }

            var bindings = action.bindings;
            var bindingCount = bindings.Count;

            m_BindingOptions = new GUIContent[bindingCount];
            m_BindingOptionValues = new string[bindingCount];
            m_SelectedBindingOption = -1;

            var currentBindingId = m_BindingIdProperty.stringValue;
            for (var i = 0; i < bindingCount; ++i)
            {
                var binding = bindings[i];
                var bindingId = binding.id.ToString();
                var displayOptions = InputBinding.DisplayStringOptions.DontUseShortDisplayNames |
                                      InputBinding.DisplayStringOptions.IgnoreBindingOverrides;

                if (string.IsNullOrEmpty(binding.groups))
                    displayOptions |= InputBinding.DisplayStringOptions.DontOmitDevice;

                // Create display string.
                var displayString = action.GetBindingDisplayString(i, displayOptions);

                // If binding is part of a composite, include the part name.
                if (binding.isPartOfComposite)
                    displayString = $"{ObjectNames.NicifyVariableName(binding.name)}: {displayString}";

                // Some composites use '/' as a separator. When used in popup, this will lead to to submenus. Prevent
                // by instead using a backlash.
                displayString = displayString.Replace('/', '\\');

                // If the binding is part of control schemes, mention them.
                m_BindingOptions[i] = new GUIContent(displayString);
                m_BindingOptionValues[i] = bindingId;

                if (currentBindingId == bindingId)
                    m_SelectedBindingOption = i;
            }
        }

        private SerializedProperty m_ActionProperty;
        private SerializedProperty m_BindingIdProperty;
        private SerializedProperty m_ActionLabelProperty;
        private SerializedProperty m_BindingTextProperty;
        private SerializedProperty m_RebindOverlayProperty;
        private SerializedProperty m_RebindTextProperty;
        private SerializedProperty m_BindingIconProperty;
        private SerializedProperty m_RebindStartEventProperty;
        private SerializedProperty m_RebindStopEventProperty;
        private SerializedProperty m_UpdateBindingUIEventProperty;
        private SerializedProperty m_DisplayStringOptionsProperty;
        private SerializedProperty m_AllowKeyboardBindingsProperty;
        private SerializedProperty m_AllowGamepadBindingsProperty;
        private SerializedProperty m_ErrorTextProperty;
        private SerializedProperty m_RebindStartClipProperty;
        private SerializedProperty m_RebindSuccessClipProperty;
        private SerializedProperty m_RebindFailedClipProperty;
        private SerializedProperty m_RebindCancelClipProperty;
        
        private GUIContent m_BindingLabel = new GUIContent("Binding");
        private GUIContent m_DisplayOptionsLabel = new GUIContent("Display Options");
        private GUIContent m_UILabel = new GUIContent("UI");
        private GUIContent m_EventsLabel = new GUIContent("Events");
        private GUIContent[] m_BindingOptions;
        private string[] m_BindingOptionValues;
        private int m_SelectedBindingOption;

        private static class Styles
        {
            public static GUIStyle boldLabel = new GUIStyle("MiniBoldLabel");
        }
    }
}
#endif
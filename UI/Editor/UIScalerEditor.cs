using UnityEditor;
using UnityEngine;

namespace XD.UI
{
    [CustomEditor(typeof(UIScaler))]
    public class UIScalerEditor : Editor
    {
        private static RuntimePlatform platformSwitchPopUp;
        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script");
            
            EditorGUILayout.LabelField("Принудительное переключение");
            platformSwitchPopUp = (RuntimePlatform)EditorGUILayout.EnumPopup(platformSwitchPopUp);
            if (GUILayout.Button("Переключить"))
                ((UIScaler)target).ForceScale(platformSwitchPopUp);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
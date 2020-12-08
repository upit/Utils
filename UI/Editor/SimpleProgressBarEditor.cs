using UnityEditor;

namespace XD.UI
{
    /// <summary> Класс для рисования ползунка прогрессбара в инспекторе. </summary>
    [CustomEditor(typeof(SimpleProgressBar))]
    public class SimpleProgressBarEditor: Editor
    {
        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            SimpleProgressBar progressBar = (SimpleProgressBar) target;
            if (!progressBar.enabled)
                return;
            progressBar.Progress = EditorGUILayout.Slider(progressBar.Progress, 0.0f, 1.0f);
        }
    }
}
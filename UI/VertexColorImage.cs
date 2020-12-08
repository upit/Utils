using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif

using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Класс для задания цвета каждой вершине спрайта. (@Upit: сыровато, кто хочет доработать - милости прошу.)
    /// </summary>
    public class VertexColorImage : Image
    {
        [SerializeField] private Color[] vertexColors;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            UIVertex vert = new UIVertex();
            for (int i = 0; i < vertexColors.Length; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.color = vertexColors[i];
                vh.SetUIVertex(vert, i);
            }
        }

        #region Custom editor
#if UNITY_EDITOR
        [CustomEditor(typeof(VertexColorImage))]
        private class VertexColorImageEditor : ImageEditor
        {
            private SerializedProperty colors;

            protected override void OnEnable()
            {
                base.OnEnable();
                colors = serializedObject.FindProperty("vertexColors");
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                SpriteGUI();
                EditorGUILayout.PropertyField(colors);
                EditorGUILayout.PropertyField(m_Material);
                RaycastControlsGUI();
                MaskableControlsGUI();
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
        #endregion
        
    }
}
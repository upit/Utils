#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary> Визуальное отображение отсечения (frustum culling) в редакторе. </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class FrustumCullingVisualizer : MonoBehaviour
{
    [SerializeField] private bool previewInEditMode;
    private Camera sceneCamera;
    private Camera cullingCamera;

    private void OnEnable()
    {
        bool show = EditorApplication.isPlaying || previewInEditMode;
        if (!show || (sceneCamera = GetSceneCamera()) == null)
            return;
        
        cullingCamera = GetComponent<Camera>();
        
        // Если не LWRP, то заменить на Camera.onPreRender += OnBeginRendering, не забыть отписаться в OnDisable. 
        RenderPipelineManager.beginCameraRendering += OnBeginRendering;
    }

    private void OnBeginRendering(ScriptableRenderContext context, Camera currentCam)
    {
        if (currentCam == sceneCamera)
            currentCam.cullingMatrix = cullingCamera.cullingMatrix;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginRendering;
        Camera cam = GetSceneCamera();
        if (cam != null)
            cam.ResetCullingMatrix();
    }

    private static Camera GetSceneCamera()
    {
        Camera[] sceneCameras = SceneView.GetAllSceneCameras();
        for (int i = 0; i < sceneCameras.Length; i++)
        {
            if (sceneCameras[i].name == "SceneCamera")
                return sceneCameras[i];
        }

        return null;
    }
    
    [CustomEditor(typeof(FrustumCullingVisualizer))]
    public class FrustumCullingVisualizerEditor : Editor
    {
        private bool previewInEditMode;
        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            // Проверяем флаг previewInEditMode.
            FrustumCullingVisualizer cullingVisualizer = (FrustumCullingVisualizer) target;
            bool previewFlag = cullingVisualizer.previewInEditMode;
            if (previewInEditMode != previewFlag)
            {
                previewInEditMode = previewFlag;
                cullingVisualizer.enabled = false;
                // ReSharper disable once Unity.InefficientPropertyAccess
                cullingVisualizer.enabled = true;
            }
        }
    }
}
#endif
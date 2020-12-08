using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utils.FastShadows
{
    [CustomEditor(typeof(FastShadowProjector))]
    public class FastShadowInspector : Editor
    {
        private LayerMask recieversLayerMask;
        /// <summary> Custom inspector. </summary>
        public override void OnInspectorGUI() {
            
            DrawDefaultInspector();
            
            /*FastShadowProjector projector = (FastShadowProjector) target;
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Минимальный Quality Level");
                
                EditorGUI.BeginChangeCheck();
                int qualityLevel = EditorGUILayout.Popup(projector.MinQualityLevel, QualitySettings.names,
                    GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "Changed FastShadow quality level");
                    projector.MinQualityLevel = qualityLevel;
                }
            }
            EditorGUILayout.EndHorizontal();*/

            GUILayout.Space(50);
            
            recieversLayerMask = EditorGUILayout.LayerField("Слой для получателей:", recieversLayerMask);
            if (GUILayout.Button("Применить к получателям"))
                SetRecievers(recieversLayerMask);
            
            if (GUILayout.Button("Сбросить получателей"))
                ResetRecievers();
            
            if (!Application.isPlaying)
                return;

            GUILayout.Space(30);
            GUILayout.Box("PLAY MODE!", GUILayout.ExpandWidth(true));
            
            if (GUILayout.Button("Обновить"))
            {
                ((FastShadowProjector) target).Refresh();
                return;
            }
            
            if (GUILayout.Button("Источников : " + FastShadowProjector.ShadowSources.Count))
                ConsolePrint("Источники: ", FastShadowProjector.ShadowSources);
            
            if (GUILayout.Button("Получателей: " + FastShadowProjector.ShadowReceivers.Count))
                ConsolePrint("Получатели: ", FastShadowProjector.ShadowReceivers);
        }

        private static void ConsolePrint(string header, IReadOnlyList<Object> obj)
        {
            Debug.LogWarning(header + obj.Count);
            for (int i = 0; i < obj.Count; i++)
                Debug.LogWarning(obj[i].name, obj[i]);
        }

        private static void ResetRecievers()
        {
            FastShadowReceiver[] receivers = FindObjectsOfType<FastShadowReceiver>();
            for (int i = 0; i < receivers.Length; i++)
                DestroyImmediate(receivers[i]);
            Debug.LogWarning($"Удалено {receivers.Length} получателей теней.");
        }

        private static void SetRecievers(LayerMask layerMask)
        {
            ResetRecievers();
            
            MeshRenderer[] renderer = FindObjectsOfType<MeshRenderer>();
            for (int i = 0; i < renderer.Length; i++)
            {
                if (!renderer[i].enabled)
                    continue;
                GameObject go = renderer[i].gameObject; 
                if (go.layer == layerMask)
                {
                    MeshFilter meshFilter = go.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        continue;
                    
                    go.AddComponent<FastShadowReceiver>().SetMesh(meshFilter.sharedMesh);
                    Debug.LogWarning(renderer[i].name, renderer[i]);
                }
            }
        }
    }
}
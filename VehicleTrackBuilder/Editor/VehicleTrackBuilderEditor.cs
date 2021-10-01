using System;
using UnityEditor;
using UnityEngine;

namespace XD.Utils.VehicleTrackBuilder
{
    [CustomEditor(typeof(VehicleTrackBuilder))]
    public class VehicleTrackBuilderEditor : Editor
    {
        private VehicleTrackBuilder vehicleTrackBuilder;
        
        private Mesh segmentMesh;

        private GameObject buildedTrack;
        private Mesh buildedMesh;
        
        private string meshName;
        private Vector3 meshSize;
        
        private void OnEnable()
        {
            vehicleTrackBuilder = (VehicleTrackBuilder) target;
            Refresh();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.LabelField("Mesh name: " + meshName);
            EditorGUILayout.LabelField("Mesh size: " + meshSize);
            
            if (buildedMesh != null)
                EditorGUILayout.LabelField($"Build result: verts {buildedMesh.vertexCount} polys {buildedMesh.GetIndexCount(0) / 3}");

            if (!Application.isPlaying)
            {

                if (Button(Refresh))
                    return;
                
                if (Button(FitCollider))
                    return;

                if (Button(Build))
                    return;

                if (Button(Clear))
                    return;
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rig:");
                if (Button(BuildRig))
                    return;
                
                if (Button(WrapAround))
                    return;
                
                if (Button(ExportMesh))
                    return;
            }
        }

        private void Refresh()
        {
            segmentMesh = vehicleTrackBuilder.GetComponent<MeshFilter>().sharedMesh;
            meshName = segmentMesh.name;
            meshSize = segmentMesh != null ? segmentMesh.bounds.size : Vector3.zero;
            buildedTrack = vehicleTrackBuilder.GetBuildedTrack();
            buildedMesh = vehicleTrackBuilder.GetMesh();
        }

        private void Build()
        {
            vehicleTrackBuilder.BuildTrack();
            Refresh();
        }

        private void FitCollider()
        {
            vehicleTrackBuilder.FitCollider();
        }
        
        private void BuildRig()
        {
            if(Application.isPlaying)
                Time.fixedDeltaTime = 0.001f;    // Для лучшего физона.
            
            vehicleTrackBuilder.BuildRig();
        }
        
        private void WrapAround()
        {
            vehicleTrackBuilder.WrapAround();
        }

        private void ExportMesh()
        {
            MeshTools.BakeSkinnedMesh(vehicleTrackBuilder.GetBuildedTrack().GetComponent<SkinnedMeshRenderer>(),vehicleTrackBuilder.GetBuildData());
        }
        
        private void Clear()
        {
            vehicleTrackBuilder.Clear();
        }

        private static bool Button(Action action)
        {
            EditorGUILayout.Space();
            string actionName = action.Method.Name;
            if (GUILayout.Button(actionName))
            {
                action.Invoke();
                return true;
            }

            return false;
        }
    }
}
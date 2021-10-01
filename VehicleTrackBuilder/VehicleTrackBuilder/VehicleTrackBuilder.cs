#if UNITY_EDITOR
using UnityEngine;

namespace XD.Utils.VehicleTrackBuilder
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(HingeJoint)), ExecuteAlways]
    public class VehicleTrackBuilder : MonoBehaviour
    {
        private enum BuildAxisOffset { X,Y,Z }
        
        [SerializeField] private string buildedTrackName = "BuildedTrack";
        [SerializeField] private int segmentsAmount = 10;
        [SerializeField] private BuildAxisOffset buildAxisOffset = BuildAxisOffset.Z;
        [SerializeField] private Vector2 uvOffset;
        [SerializeField] private bool weldVertsAndUVs = true;
        [SerializeField] private float weldSqrDist = 0.01f;

        [SerializeField, HideInInspector] private GameObject buildedTrack;
         
        public void BuildTrack()
        {
            Clear();
            
            
            VehicleTrackMesh vehicleTrackMesh = new VehicleTrackMesh(sourceMesh: GetComponent<MeshFilter>().sharedMesh,
                segmentsAmount, GetAxisOffset(buildAxisOffset), uvOffset, weldSqrDist);

            vehicleTrackMesh.BuildMesh(transform.localScale, weldVertsAndUVs);
            Mesh buildedMesh = vehicleTrackMesh.GetMesh();
            InstantiateTrack(buildedTrackName, buildedMesh, GetComponent<MeshRenderer>().sharedMaterial);
        }

        public void BuildRig()
        {
            Transform rigParent = GetBuildedTrack().transform;
            for (int i = 0; i < rigParent.childCount; i++) 
                DestroyImmediate(rigParent.GetChild(i--).gameObject);
            
            Vector3 size = GetComponent<Collider>().bounds.size;
            Vector3 offset = Vector3.Scale(GetAxisOffset(buildAxisOffset),  size);
            
            VehicleTrackJoints.InitJoints("TrackJoint", rigParent, size, GetComponent<HingeJoint>(), segmentsAmount,
                offset, GetComponent<Rigidbody>());
            VehicleTrackJoints.FillJoints();

            Skin(rigParent.GetComponent<SkinnedMeshRenderer>(), VehicleTrackJoints.Transforms, rigParent.localToWorldMatrix);

            // Extra bones.
            //rootBone.worldToLocalMatrix * localToWorldMatrix;
        }

        private static void Skin(SkinnedMeshRenderer skin, Transform[] bones, Matrix4x4 localToWorldMatrix)
        {
            var bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) 
                bindPoses[i] = bones[i].worldToLocalMatrix * localToWorldMatrix;

            skin.bones = bones;
            skin.localBounds = skin.sharedMesh.bounds;
            skin.sharedMesh.bindposes = bindPoses;
        }

        public void FitCollider()
        {
            GetComponent<BoxCollider>().size = GetComponent<MeshFilter>().sharedMesh.bounds.size;
        }

        public void WrapAround()
        {
            VehicleTrackJoints.WrapAround();
        }
        
        public void Clear()
        {
            Clear(GetBuildedTrack()); // Clear last builded track.
        }

        public GameObject GetBuildedTrack()
        {
            if (buildedTrack == null)
                buildedTrack = GameObject.Find(buildedTrackName);
            
            return buildedTrack;
        }

        public Mesh GetMesh()
        {
            GameObject trackGO = GetBuildedTrack();
            return trackGO != null ? trackGO.GetComponent<SkinnedMeshRenderer>().sharedMesh : null;
        }

        public string GetBuildData()
        {
            Mesh mesh = GetBuildedTrack().GetComponent<SkinnedMeshRenderer>().sharedMesh;
            Vector3 meshSize = mesh.bounds.size;
            
            VehicleTrackData buildedTrackData = new VehicleTrackData
            {
                segments = segmentsAmount,
                segmentName = GetComponent<MeshFilter>().sharedMesh.name,
                segmentScale = transform.localScale,
                length = Mathf.Max(meshSize.x, meshSize.y, meshSize.z),
                uvOffset = Mathf.Max(uvOffset.x, uvOffset.y)
            };
            
            return JsonUtility.ToJson(buildedTrackData);
            
            // var values = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string,string>>>(json);
        }

        #region Private section
        
        private static void InstantiateTrack(string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name, typeof(SkinnedMeshRenderer));
            SkinnedMeshRenderer skinnedMesh = go.GetComponent<SkinnedMeshRenderer>();
            skinnedMesh.sharedMesh = mesh;
            skinnedMesh.sharedMaterial = material;
            skinnedMesh.updateWhenOffscreen = true;
        }

        private static void Clear(Object go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private static Vector3 GetAxisOffset(BuildAxisOffset axisOffset)
        {
            float[] offset = {0f, 0f, 0f};
            offset[(int) axisOffset] = 1f;

            return new Vector3(offset[0], offset[1], offset[2]);
        }
        
        #endregion
    }
}
#endif
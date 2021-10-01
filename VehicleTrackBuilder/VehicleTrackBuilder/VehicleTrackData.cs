using UnityEngine;

#if UNITY_EDITOR
namespace XD.Utils.VehicleTrackBuilder
{
    [System.Serializable]
    public class VehicleTrackData
    {
        public int segments;
        public string segmentName;
        public Vector3 segmentScale;
        public float length;
        public float uvOffset;
        public float boneBaseScale = 0.3f;
        public float boneExtraScale = 2.0f;
    }
}
#endif
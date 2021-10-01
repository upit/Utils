#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XD.Utils.VehicleTrackBuilder
{
    public static class VehicleTrackJoints
    {
        private static string jointsName;
        private static VehicleTrackJoint initialJoint;
        private static Transform jointsParent;
        private static Vector3 jointsOffset;

        private static List<VehicleTrackJoint> jointsList;
        
        public static Transform[] Transforms { get; private set; }

        public static void InitJoints(string name, Transform parent, Vector3 colliderSize, HingeJoint hingeJoint,
            int jointsCount, Vector3 offset, Rigidbody rb)
        {
            jointsName = name;
            jointsParent = parent;
            jointsOffset = offset;
            jointsList = new List<VehicleTrackJoint>(jointsCount);
            Transforms = new Transform[jointsCount];

            GameObject initialJointGO = new GameObject(name + "0(Initial)", typeof(BoxCollider), typeof(HingeJoint));
            initialJointGO.GetComponent<BoxCollider>().size = colliderSize;
            
            // Initial joint.
            initialJoint = initialJointGO.AddComponent<VehicleTrackJoint>();
            initialJoint.Init(parent, rb, hingeJoint);
            
            AddToList(initialJoint);
        }

        public static void FillJoints()
        {
            int jointsCount = jointsList.Count;
            int jointsCapacity = jointsList.Capacity;
            VehicleTrackJoint prevJoint = initialJoint;
            
            for (int i = jointsCount; i < jointsCapacity; i++)
            {
                VehicleTrackJoint joint =
                    Object.Instantiate(initialJoint, jointsOffset * i, Quaternion.identity, jointsParent);
                joint.name = jointsName + i;
                joint.Connect(prevJoint);
                AddToList(joint);
                
                prevJoint = joint;
            }

            initialJoint.Fix(true);
            
            VehicleTrackJoint lastJoint = jointsList[jointsList.Count - 1];
            lastJoint.name += "(Last)";
            lastJoint.CachedTransform.SetSiblingIndex(1);
            lastJoint.Fix(true);
        }

        public static void WrapAround()
        {
            var transforms = Transforms;
            if (transforms == null || transforms.Length == 0)
                return;

            int count = transforms.Length;
            Vector3 initialPos = transforms[0].localPosition;
            float circleLength = count * jointsOffset.magnitude;
            float radius = circleLength / (2 * Mathf.PI);
            
            Vector3 center = new Vector3(0.0f, -radius, 0.0f);
            float deltaAngle = 360.0f / count;
            
            count--;    // Minus last.
            float angle = deltaAngle;
            for (int i = 1; i < count; i++)
            {
                transforms[i].localPosition = initialPos;
                transforms[i].RotateAround(center,Vector3.right, angle);
                angle += deltaAngle;
            }
            
            // Last one.
            transforms[count].localPosition = initialPos - jointsOffset;
            VehicleTrackJoint lastJoint = jointsList[jointsList.Count - 1];
            initialJoint.Connect(lastJoint);
            lastJoint.FreezeRotation(true);
            lastJoint.Fix(false);
        }

        private static void AddToList(VehicleTrackJoint joint)
        {
            Transforms[jointsList.Count] = joint.CachedTransform;
            jointsList.Add(joint);
        }
    }
}
#endif
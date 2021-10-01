#if UNITY_EDITOR
using UnityEngine;

namespace XD.Utils.VehicleTrackBuilder
{
    public class VehicleTrackJoint : MonoBehaviour
    {
        [SerializeField] private Transform cachedTransform;
        [SerializeField] private HingeJoint joint;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private BoxCollider col;

        public Transform CachedTransform => cachedTransform;

        public void Init(Transform jointsParent, Rigidbody initialRb, HingeJoint initialJoint)
        {
            cachedTransform = transform;
            cachedTransform.SetParent(jointsParent);

            rb = GetComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = initialRb.constraints;
            rb.drag = initialRb.drag;
            
            joint = GetComponent<HingeJoint>();
            joint.useLimits = initialJoint.useLimits;
            joint.limits = initialJoint.limits;
            
            
            col = GetComponent<BoxCollider>();
        }

        public void Connect(VehicleTrackJoint target)
        {
            joint.connectedBody = target.rb;
        }

        public void FreezeRotation(bool freeze)
        {
            rb.freezeRotation = freeze;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void Fix(bool fix)
        {
            CollisionDetectionMode colMode = fix ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;

            // Так надо. (Ругается ворнингами иначе).
            if (fix)
            {
                rb.collisionDetectionMode = colMode;
                rb.isKinematic = true;    
            }
            else
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = colMode;
            }
        }

        private void OnDrawGizmos()
        {
            if (!rb.isKinematic)
                return;
            
            Gizmos.color = Color.yellow;
            Gizmos.matrix = cachedTransform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, col.size);
            
            // Up.
            Vector3 pos = new Vector3(0.0f, 0.75f, 0.0f);
            Gizmos.DrawLine(Vector3.zero, pos);
            Gizmos.DrawSphere(pos, 0.2f);
        }
    }
    
    
}
#endif
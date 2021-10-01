#if UNITY_EDITOR
using UnityEngine;

namespace XD.Utils.VehicleTrackBuilder
{
    public class VehicleTrackMesh
    {
        private const float DEFAULT_WELD_TOLLERANCE = 0.01f;
        
        private readonly Mesh sourceMesh;
        private readonly int segments;
        private readonly Vector3 buildOffset;
        private readonly Vector3 uvOffset;
        private readonly float weldTolerance;

        private Mesh buildedMesh;
        public VehicleTrackMesh(Mesh sourceMesh, int segments, Vector3 buildOffset, Vector3 uvOffset, float weldTolerance = DEFAULT_WELD_TOLLERANCE)
        {
            this.sourceMesh = sourceMesh;
            this.segments = segments;
            this.buildOffset = Vector3.Scale(buildOffset, sourceMesh.bounds.size);
            this.uvOffset = uvOffset;
            this.weldTolerance = weldTolerance;
        }
        
        public void BuildMesh(Vector3 scale, bool weld = true, bool optimize = true)
        {
            var sourceVerts = sourceMesh.vertices;
            int sourceVertsCount = sourceVerts.Length;
            var sourceInds = sourceMesh.triangles;
            int sourceIndsCount = sourceInds.Length;
            var sourceNorms = sourceMesh.normals;
            var sourceUVs = sourceMesh.uv;

            int buildedVertsCount = sourceVertsCount * segments;
            var buildedVerts = new Vector3[buildedVertsCount];
            var buildedInds = new int[sourceIndsCount * segments];
            var buildedNorms = new Vector3[buildedVertsCount];
            var buildedUVs = new Vector2[buildedVertsCount];
            
            var boneWeights = new BoneWeight[buildedVertsCount];
            
            // BuildPass
            for (int i = 0; i < segments; i++)
            {
                int vertIndexStart = i * sourceVertsCount;
                int vertIndexEnd = vertIndexStart + sourceVertsCount;
                for (int j = vertIndexStart; j < vertIndexEnd; j++)
                {
                    int sourceIndex = j - vertIndexStart;
                    buildedVerts[j] = sourceVerts[sourceIndex];
                    buildedNorms[j] = sourceNorms[sourceIndex];
                    buildedUVs[j] = sourceUVs[sourceIndex];
                    // boneWeights[j] = new BoneWeight();
                    boneWeights[j].weight0 = 100.0f;
                    boneWeights[j].boneIndex0 = i;
                }

                int indStart = i * sourceIndsCount;
                int indEnd = indStart + sourceIndsCount;
                for (int j = indStart; j < indEnd; j++)
                {
                    int sourceIndex = j - indStart;
                    buildedInds[j] = sourceInds[sourceIndex];
                }
                
                MeshTools.OffsetVertices(sourceVerts, buildOffset);
                MeshTools.OffsetUVs(sourceUVs, uvOffset);
                MeshTools.OffsetIndices(sourceInds, sourceVertsCount);
            }

            if (weld)
                MeshTools.WeldVertices(buildedVerts, buildedNorms, buildedUVs, boneWeights, buildedInds, weldTolerance,
                    out buildedVerts, out buildedNorms, out buildedUVs, out boneWeights);
            
            if (!Mathf.Approximately(scale.x, 1f) || !Mathf.Approximately(scale.y, 1f) || !Mathf.Approximately(scale.z, 1f))
                MeshTools.ScaleVertices(buildedVerts, scale);

            buildedMesh = new Mesh
            {
                name = "TrackMesh",
                vertices = buildedVerts,
                triangles = buildedInds,
                normals = buildedNorms,
                uv = buildedUVs,
                boneWeights = boneWeights
            };

            buildedMesh.RecalculateBounds();
            buildedMesh.RecalculateNormals();
        }

        public Mesh GetMesh()
        {
            return buildedMesh;
        }
    }
}
#endif
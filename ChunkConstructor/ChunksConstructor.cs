using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;
using UnityEditor;
#endif
using System.Collections.Generic;


namespace Utils
{
    /// <summary>
    /// Утилита разбиения в рантайме меша и его коллайдеров на чанки с последующим батчингом.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class ChunksConstructor : MonoBehaviour
    {
        private const string MSG_MESH_ERROR = "Mesh is null or not readable. Check Import Settings->Read/Write Enabled";

        [SerializeField, Range(10, 1000)] private int chunkSize = 500;
        [SerializeField] private Mesh mesh;
        [SerializeField] private int rebuildStep = 5;

        private void Awake()
        {
            InitMesh();
            if (!IsMeshAccessible(true))
                return;

            StopAllCoroutines();
            StartCoroutine(RebuildChunksRoutine());
        }

        private void InitMesh()
        {
            if (mesh == null)
                mesh = GetComponent<MeshFilter>().sharedMesh;
        }

        private bool IsMeshAccessible(bool log = false)
        {
            bool result = mesh != null && mesh.isReadable;
            if (!result && log)
                Debug.LogError($"{nameof(ChunksConstructor)}: {name} {MSG_MESH_ERROR}", this);
            return result;
        }

        private Vector2Int GetChunksCount(Bounds meshBounds)
        {
            Vector3 meshSize = meshBounds.size;
            return new Vector2Int((int) (meshSize.x / chunkSize + 1.0f), (int) (meshSize.z / chunkSize + 1.0f));
        }

        private Vector3 GetFirstChunkPosition(Bounds meshBounds)
        {
            Vector3 chunkPos = meshBounds.min;
            chunkPos.y = meshBounds.center.y;
            float chunkHalfSize = chunkSize * 0.5f;
            chunkPos.x += chunkHalfSize;
            chunkPos.z -= chunkHalfSize;
            return chunkPos;
        }

        private static GameObject BuildChunk(string chunkName, Vector3 pos, Vector3 size, List<int> tris,
            Vector3[] verts, Vector3[] normals, Vector2[] uv, Vector2[] uv2, Renderer renderer, bool createColider)
        {
            // TODO@Upit получать индекс из координат вершины, не пробегаясь каждый раз по оставшимся полигонам.
            Bounds bounds = new Bounds(pos, size);

            List<int> newTris = new List<int>();
            List<Vector3> newVerts = new List<Vector3>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector2> newUV = new List<Vector2>();
            List<Vector2> newUV2 = new List<Vector2>();

            List<int> removeTris = new List<int>();
            bool isEmpty = true;
            for (int i = 0; i < tris.Count; i += 3)
            {
                int ind0 = tris[i];
                int ind1 = tris[i + 1];
                int ind2 = tris[i + 2];
                Vector3[] triVerts = {verts[ind0], verts[ind1], verts[ind2]};
                for (int j = 0; j < triVerts.Length; j++)
                {
                    if (bounds.Contains(triVerts[j]))
                    {
                        newVerts.AddRange(triVerts);
                        newNormals.AddRange(new[] {normals[ind0], normals[ind1], normals[ind2]});
                        newUV.AddRange(new[] {uv[ind0], uv[ind1], uv[ind2]});
                        newUV2.AddRange(new[] {uv2[ind0], uv2[ind1], uv2[ind2]});

                        int vertsCount = newVerts.Count;
                        newTris.AddRange(new[] {vertsCount - 3, vertsCount - 2, vertsCount - 1});
                        removeTris.Add(i);
                        isEmpty = false;
                        break;
                    }
                }
            }

            if (isEmpty)
                return null;


            int removeOffset = 0;
            for (int i = 0; i < removeTris.Count; i++)
            {
                tris.RemoveRange(removeTris[i] + removeOffset, 3);
                removeOffset -= 3;
            }

            Mesh chunkMesh = new Mesh();
            chunkMesh.SetVertices(newVerts);
            chunkMesh.SetNormals(newNormals);
            chunkMesh.SetUVs(0, newUV);
            chunkMesh.SetUVs(1, newUV2);
            chunkMesh.SetTriangles(newTris, 0);
            GameObject result = new GameObject(chunkName, typeof(MeshFilter), typeof(MeshRenderer));
            result.SetActive(false);
            result.GetComponent<MeshFilter>().mesh = chunkMesh;
            MeshRenderer chunkRenderer = result.GetComponent<MeshRenderer>();
            chunkRenderer.material = renderer.material;
            chunkRenderer.lightmapIndex = renderer.lightmapIndex;
            chunkRenderer.lightmapScaleOffset = renderer.lightmapScaleOffset;
            if (createColider)
                result.AddComponent<MeshCollider>().sharedMesh = chunkMesh;

            return result;
        }


        private IEnumerator RebuildChunksRoutine()
        {
            Bounds meshBounds = mesh.bounds;
            Vector3 chunkPos = GetFirstChunkPosition(meshBounds);
            float chunkInitialZPos = chunkPos.z;

            Vector2Int chunksCount = GetChunksCount(meshBounds);

            Vector3 chunk3DSize = new Vector3(chunkSize, meshBounds.size.y, chunkSize);
            Vector3[] verts = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            Vector2[] uv2 = mesh.uv2;
            List<int> tris = new List<int>(mesh.triangles);

            int step = 0;
            List<GameObject> chunks = new List<GameObject>(chunksCount.x * chunksCount.y);
            string namePrefix = name + "_chunk_";
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            bool hasCollider = GetComponent<Collider>() != null;
            for (int x = 0; x < chunksCount.x; x++)
            {
                for (int z = 0; z < chunksCount.y; z++)
                {
                    chunkPos.z += chunkSize;
                    GameObject chunk = BuildChunk(namePrefix + chunks.Count, chunkPos, chunk3DSize,
                        tris, verts, normals, uv, uv2, meshRenderer, hasCollider);
                    if (chunk != null)
                        chunks.Add(chunk);

                    if (step++ >= rebuildStep)
                    {
                        step = 0;
                        yield return null;
                    }
                }

                chunkPos.x += chunkSize;
                chunkPos.z = chunkInitialZPos;
            }

            GameObject go = gameObject;
            int layer = go.layer;
            bool isStatic = go.isStatic;
            Transform parentTransform = transform;
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].transform.SetParent(parentTransform);
                chunks[i].layer = layer;
                chunks[i].isStatic = isStatic;
                chunks[i].SetActive(true);
            }


            MeshFilter meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = null;

            if (isStatic)
            {
                StaticBatchingUtility.Combine(go);
                yield return null;
            }

            Destroy(this);
            Destroy(meshRenderer);
            Destroy(meshFilter);
        }


        #region Custom editor

#if UNITY_EDITOR
        [CustomEditor(typeof(ChunksConstructor))]
        public class ChunksConstructorEditor : Editor
        {
            private int prevChunkSize;
            private ChunksConstructor chunkConstructor;
            private static List<Vector3> chunks;
            private GUIStyle errorGuiStyle;
            private EditorCoroutine rebuildRoutine;

            private void OnEnable()
            {
                if (Application.isPlaying)
                    return;

                chunkConstructor = (ChunksConstructor) target;
                chunkConstructor.InitMesh();
                errorGuiStyle = new GUIStyle(EditorStyles.label)
                {
                    richText = true, fontSize = 12, fontStyle = FontStyle.Bold, normal = {textColor = Color.red}
                };
            }

            private void OnDisable()
            {
                if (Application.isPlaying)
                    return;

                chunks.Clear();
                StopRebuild();
            }

            private void StopRebuild()
            {
                if (rebuildRoutine != null)
                    EditorCoroutineUtility.StopCoroutine(rebuildRoutine);
            }

            public override void OnInspectorGUI()
            {
                if (Application.isPlaying)
                    return;

                DrawPropertiesExcluding(serializedObject, "m_Script");
                serializedObject.ApplyModifiedProperties();

                if (!chunkConstructor.IsMeshAccessible())
                {
                    GUILayout.Label(MSG_MESH_ERROR, errorGuiStyle);
                    return;
                }


                int rebuildStep = chunkConstructor.rebuildStep;
                if (chunks != null && rebuildStep > 0)
                {
                    int chunksCount = chunks.Count;
                    GUILayout.Label("Chunks count: " + chunksCount);
                    int frames = chunksCount / rebuildStep;
                    GUILayout.Label($"Runtime build time: {frames} frames ≈ {frames / 60.0f}s (60fps)");
                }

                int chunkSize = chunkConstructor.chunkSize;
                if (prevChunkSize != chunkSize)
                {
                    prevChunkSize = chunkSize;
                    RebuildChunks();
                }
            }

            private void RebuildChunks()
            {
                StopRebuild();
                rebuildRoutine = EditorCoroutineUtility.StartCoroutine(RebuildChunksRoutine(), this);
            }

            private static bool IsEmpty(Vector3 pos, Vector3 size, IReadOnlyList<Vector3> verts)
            {
                Bounds bounds = new Bounds(pos, size);
                for (int i = 0; i < verts.Count; i++)
                {
                    if (bounds.Contains(verts[i]))
                        return false;
                }

                return true;
            }

            private IEnumerator RebuildChunksRoutine()
            {
                Bounds meshBounds = chunkConstructor.mesh.bounds;
                Vector3 chunkPos = chunkConstructor.GetFirstChunkPosition(meshBounds);
                float chunkInitialZPos = chunkPos.z;

                Vector2Int chunksCount = chunkConstructor.GetChunksCount(meshBounds);
                chunks = new List<Vector3>(chunksCount.x * chunksCount.y);

                bool isEditMode = !Application.isPlaying;

                int chunkSize = chunkConstructor.chunkSize;
                Vector3 chunk3DSize = new Vector3(chunkSize, meshBounds.size.y, chunkSize);
                Vector3[] verts = chunkConstructor.mesh.vertices;
                int rebuildStep = chunkConstructor.rebuildStep;


                int step = 0;
                for (int x = 0; x < chunksCount.x; x++)
                {
                    for (int z = 0; z < chunksCount.y; z++)
                    {
                        chunkPos.z += chunkSize;
                        if (!IsEmpty(chunkPos, chunk3DSize, verts))
                            chunks.Add(chunkPos);

                        if (step++ >= rebuildStep)
                        {
                            step = 0;
                            if (isEditMode)
                                SceneView.RepaintAll();
                            yield return null;
                        }
                    }

                    chunkPos.x += chunkSize;
                    chunkPos.z = chunkInitialZPos;
                }

                if (step > 0 && isEditMode)
                    SceneView.RepaintAll();
            }

            [DrawGizmo(GizmoType.Selected)]
            private static void DrawGizmos(ChunksConstructor constructor, GizmoType gizmoType)
            {
                if (Application.isPlaying)
                    return;

                if (chunks == null)
                    return;

                Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.2f);
                Vector3 chunkSize = new Vector3(constructor.chunkSize, constructor.mesh.bounds.size.y,
                    constructor.chunkSize);

                Vector3 offset = constructor.transform.position;
                for (int i = 0; i < chunks.Count; i++)
                {
                    Vector3 pos = chunks[i] + offset;
                    Gizmos.DrawCube(pos, chunkSize);
                    Gizmos.DrawWireCube(pos, chunkSize);
                }
            }
        }
#endif

        #endregion
    }
}
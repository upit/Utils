using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utils.NavMesh
{
    /// <summary> Класс-помощник для работы с навмешем. Имопорт, экспорт и т.д. </summary>
    public class NavMeshHelper : EditorWindow
    {
        // Размеры окна редактора.
        private const int WINDOW_WIDTH = 250;
        private const int WINDOW_HEIGHT = 250;

        [MenuItem("HelpTools/" + nameof(NavMeshHelper))]
        public static void OpenEditorWindow()
        {
            NavMeshHelper window = (NavMeshHelper) GetWindow(typeof(NavMeshHelper), false, nameof(NavMeshHelper));
            window.maxSize = window.minSize = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
        }

        private void OnGUI()
        {
            GUILayout.Space(25);
            if (GUILayout.Button("Построить NavMesh", GUILayout.ExpandWidth(true)))
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            if (GUILayout.Button("Очистить NavMesh", GUILayout.ExpandWidth(true)))
                UnityEngine.AI.NavMesh.RemoveAllNavMeshData();

            GUILayout.Space(50);

            if (GUILayout.Button("Экспортировать NavMesh в OBJ", GUILayout.ExpandWidth(true)))
                ExportNavMesh();
        }

        private static void ExportNavMesh()
        {
            UnityEngine.AI.NavMeshTriangulation data = UnityEngine.AI.NavMesh.CalculateTriangulation();
            if (data.vertices.Length == 0)
            {
                Debug.LogError(nameof(NavMeshHelper) + ": NavMesh not found or empty.");
                return;
            }

            Mesh mesh = new Mesh {vertices = data.vertices, triangles = data.indices};
            string expFile = EditorUtility.SaveFilePanel("Export NavMesh", "", "NavMesh","obj");
            if (expFile.Length > 0)
            {
                FileInfo fi = new FileInfo(expFile);
                EditorPrefs.SetString("a4_OBJExport_lastFile", fi.Name);
                if (fi.Directory != null) EditorPrefs.SetString("a4_OBJExport_lastPath", fi.Directory.FullName);
                ExportMeshToObj(expFile, mesh);
            }
        }

        /// <summary> Экспортирует mesh в файл OBJ. </summary>
        private static void ExportMeshToObj(string exportPath, Mesh mesh)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Export {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}  NavMesh");
            sb.AppendLine("# XD OBJ Exporter ver. 1.0");
            
            foreach (Vector3 vx in mesh.vertices)
            {
                Vector3 v = vx;
                v.x = -v.x;
                sb.AppendLine("v " + v.x + " " + v.y + " " + v.z);
            }

            foreach (Vector3 vx in mesh.normals)
            {
                Vector3 v = vx;
                v.x = -v.x;
                sb.AppendLine("vn " + v.x + " " + v.y + " " + v.z);
            }

            foreach (Vector2 v in mesh.uv)
            {
                sb.AppendLine("vt " + v.x + " " + v.y);
            }

            string meshName = mesh.name;
            for (int j = 0; j < mesh.subMeshCount; j++)
            {
                sb.AppendLine("usemtl " + meshName + "_sm" + j);
                int[] tris = mesh.GetTriangles(j);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int[] ind = {tris[t + 2] + 1, tris[t + 1] + 1, tris[t] + 1};
                    sb.AppendLine(
                        $"f {GetObjFacesString(ind[0])} {GetObjFacesString(ind[1])} {GetObjFacesString(ind[2])}");
                }
            }

            File.WriteAllText(exportPath, sb.ToString());
        }
     
        /// <summary> Формат записи индексов в OBJ </summary>
        private static string GetObjFacesString(int ind)
        {
            string indStr = ind.ToString();
            return $"{indStr}/{indStr}/{indStr}";
        }
    }
}
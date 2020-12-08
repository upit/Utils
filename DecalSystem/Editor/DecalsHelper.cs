using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Utils.DecalSystem
{
    /// <summary> Хелпер для редактирования декалей в редакторе. </summary>
    [CustomEditor(typeof(Decal))]
    public class DecalsHelper : Editor
    {
        // В этом списке хранятся поставленные в редакторе декали.
        private static readonly List<GameObject> decals = new List<GameObject>();
        
        /// <summary> Custom inspector для декалей. </summary>
        public override void OnInspectorGUI() {
            GUILayout.Box("Ctrl + Mouse - Поставить декаль", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Очистить декали"))
            {
                for (int i = 0; i < decals.Count; i++)
                    DestroyImmediate(decals[i]);
                
                decals.Clear();
            }
            DrawDefaultInspector();
        }
        
        /// <summary> Выставление декалей в редакторе. </summary>
        private void OnSceneGUI()
        {
            if (!Event.current.control)            // Если не нажат ctrl, то вообще ничего не делаем.
                return;

            // Чтоб не сбрасывал фокус с объекта.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            // Если кликаем мышкой - строим луч от мышки в мир и пробуем поставить в месте пересечения декаль.
            // Если получилось, то добавляем в лист, чтобы потом была возможность очистить. 
            if (Event.current.type == EventType.MouseDown)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                Decal decal = (Decal) target;
                if (Physics.Raycast(ray, out RaycastHit hit, 500, decal.GetLayerMask()))
                {
                    GameObject go = decal.Build(hit.point, ray.direction.normalized);
                    if (go!=null)
                        decals.Add(go);
                }
            }
        }
    }    

}

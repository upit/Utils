using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Utils
{
    public class AdvancedColliderImporter : AssetPostprocessor
    {
        private const int NAME_START_INDEX = 4;

        private void OnPostprocessModel(GameObject go)
        {
            Dictionary<string, Type> collidersDic = new Dictionary<string, Type>
            {
                {"UBX_", typeof(BoxCollider)},
                {"UCP_", typeof(CapsuleCollider)},
                {"USP_", typeof(SphereCollider)},
                {"UMX_", typeof(MeshCollider)},
                {"UCX_", typeof(MeshCollider)}
            };

            Transform[] childs = go.GetComponentsInChildren<Transform>();
            foreach (Transform child in childs)
                ProcessCollider(child.gameObject, collidersDic);
        }

        private static void ProcessCollider(GameObject colliderGameObject, IReadOnlyDictionary<string, Type> dic)
        {
            string colName = colliderGameObject.name;
            if (colName.Length <= NAME_START_INDEX + 1)
                return;
            
            string colNamePrefix = colName.Remove(NAME_START_INDEX);
            if (!dic.TryGetValue(colNamePrefix, out Type type))
                return;
            
            Component component = colliderGameObject.AddComponent(type);
            if (colNamePrefix == "UCX_")
                colliderGameObject.GetComponent<MeshCollider>().convex = true;

            // Удаляем лишние компоненты.
            RemoveComponents(colliderGameObject);
            
            // Если существует объект с таким же именем, то копируем коллайдер ему, а наш удаляем и прерываем.
            string parentName = colName.Remove(0, NAME_START_INDEX);
            Transform parent;
            if ((parent = FindChildrenByName(colliderGameObject.transform.root, parentName)) != null)
            {
                ComponentUtility.CopyComponent(component);
                ComponentUtility.PasteComponentAsNew(parent.gameObject);
                UnityEngine.Object.DestroyImmediate(colliderGameObject);
            }
            else
                colliderGameObject.name = parentName; // Если нет меша с таким же именем - убираем префикс.
        }

        private static void RemoveComponents(GameObject go)
        {
            MeshFilter meshFilter;
            if ((meshFilter = go.GetComponent<MeshFilter>()) != null)
            {
                UnityEngine.Object.DestroyImmediate(meshFilter.sharedMesh);
                UnityEngine.Object.DestroyImmediate(meshFilter);
            }
                
            MeshRenderer meshRenderer;
            if ((meshRenderer = go.GetComponent<MeshRenderer>()) != null)
            {
                //UnityEngine.Object.DestroyImmediate(meshRenderer.material);
                UnityEngine.Object.DestroyImmediate(meshRenderer);    
            }
        }

        private static Transform FindChildrenByName(Component root, string childName)
        {
            Transform[] childs = root.GetComponentsInChildren<Transform>();
            for (int i = 0; i < childs.Length; i++)
                if (childs[i].name == childName)
                    return childs[i];
               
            return null;
        }
    }
}
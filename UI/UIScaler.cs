using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [Serializable]
    internal class PlatformScale
    {
        [SerializeField] private string name;  // Имя группы.
        [SerializeField] internal RuntimePlatform[] platforms;    // Платформы, к которым применяется масштаб.
        [SerializeField] internal Vector2Int scale;               // Масштаб.
        [SerializeField] internal float pixelsPerUnit = 100;
    }

    [RequireComponent(typeof(CanvasScaler))]
    public class UIScaler : MonoBehaviour
    {
        [SerializeField] private PlatformScale[] platformScale;

#if UNITY_EDITOR
        public void ForceScale(RuntimePlatform platform)
        {
            ApplyScale(GetPlatformScale(platform));
            
            // Для ребилда канваса в редакторе.
            CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
            canvasScaler.enabled = false;
            // ReSharper disable once Unity.InefficientPropertyAccess
            canvasScaler.enabled = true;
        }
#else
        private void Start()
        {
            ApplyScale(GetPlatformScale(Application.platform));
        }
#endif

        private void ApplyScale(PlatformScale platform)
        {
            if (platform == null)
                return;
            
            CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
            canvasScaler.referenceResolution = platform.scale;
            canvasScaler.referencePixelsPerUnit = platform.pixelsPerUnit;
        }

        private PlatformScale GetPlatformScale(RuntimePlatform platform)
        {
            for (int i = 0; i < platformScale.Length; i++)
            {
                for (int j = 0; j < platformScale[i].platforms.Length; j++)
                {
                    if (platformScale[i].platforms[j] == platform)
                        return platformScale[i];
                }
            }

            return null;
        }
    }
}
#if USE_BUNDLES
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UniRx;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace AssetDelivery
{
    public abstract class AssetManager : MonoBehaviour, IAssetManager
    {
        public enum LogMode { All, JustErrors };
        public enum LogType { Info, Warning, Error };

        protected static string mBaseDownloadingURL;
        protected static string mStreamingAssetsPath;
        protected static string[] mActiveVariants =  {};

#if UNITY_EDITOR
        private static int mSimulateAssetDeliveryInEditor = -1;
        private const string SIMULATE_ASSET_BUNDLES = "SimulateAssetBundles";
#endif

        protected static readonly List<AssetLoadOperation> mInProgressOperations = new List<AssetLoadOperation>();

        public static LogMode LOGMode { get; set; } = LogMode.All;

        /// <summary>
        /// Variants which is used to define the active variants.
        /// </summary>
        public static string[] ActiveVariants
        {
            get { return mActiveVariants; }
            set { mActiveVariants = value; }
        }
        
        protected static void Log(LogType logType, string text)
        {
            if (logType == LogType.Error)
                Debug.LogError("[AssetBundleManager] " + text);
            else if (LOGMode == LogMode.All && logType == LogType.Warning)
                Debug.LogWarning("[AssetBundleManager] " + text);
            else if (LOGMode == LogMode.All)
                Debug.Log("[AssetBundleManager] " + text);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Flag to indicate if we want to simulate assetBundles in Editor without building them actually.
        /// </summary>
        public static bool SimulateAssetDeliveryInEditor
        {
            get
            {
                if (mSimulateAssetDeliveryInEditor == -1)
                    mSimulateAssetDeliveryInEditor = EditorPrefs.GetBool(SIMULATE_ASSET_BUNDLES, true) ? 1 : 0;

                return mSimulateAssetDeliveryInEditor != 0;
            }
            set
            {
                int newValue = value ? 1 : 0;
                if (newValue != mSimulateAssetDeliveryInEditor)
                {
                    mSimulateAssetDeliveryInEditor = newValue;
                    EditorPrefs.SetBool(SIMULATE_ASSET_BUNDLES, value);
                }
            }
        }
#endif

        public abstract void SetSourceAssetsAbsoluteDirectory(string absolutePath);

        public abstract bool IsAssetBundleDownloaded(string assetsBundleName);

        public abstract IEnumerator Initialize();

        // Returns base downloading URL for the given asset bundle.
        // This URL may be overridden on per-bundle basis via overrideBaseDownloadingURL event.
        protected static string GetAssetBaseDownloadingURL(string assetName)
        {
            // Если такой ассет есть в streaming assets, то возвращаем нужный путь.
            return File.Exists(mStreamingAssetsPath + assetName) ? mStreamingAssetsPath : mBaseDownloadingURL;
        }

        protected void Update()
        {
            // Update all in progress operations
            for (int i = 0; i < mInProgressOperations.Count;)
            {
                var operation = mInProgressOperations[i];
                if (operation.Update())
                {
                    i++;
                }
                else
                {
                    mInProgressOperations.RemoveAt(i);
                    ProcessFinishedOperation(operation);
                }
            }
        }

        protected abstract void ProcessFinishedOperation(AssetLoadOperation operation);

        public abstract IEnumerator LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, Action<DiContainer> extraBindings = null);

        public abstract Task<T> LoadAssetAsync<T>(string assetBundleName, string assetName) where T : UnityEngine.Object;

        public abstract Task<T> LoadGameAssetAsync<T> (string assetBundleName, string assetName) where T : UnityEngine.Object;

        public abstract Task<T[]> LoadAllGameAssetsAsync<T>(string assetBundleName) where T : UnityEngine.Object;
    } 
}
#endif
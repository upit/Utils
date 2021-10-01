#if USE_BUNDLES
/*  The AssetBundle Manager */

// [downloading method selection]
// AssetBundleManager(ABM) internally uses either WWW or UnityWebRequest to download AssetBundles.
// By default, ABM will automatically select either one based on the version of the Unity runtime.
//
// - WWW
//   For Unity5.3 and earlier PLUS Unity5.5.
// - UnityWebRequest
//   For Unity5.4 and later versions EXCEPT Unity5.5.
//   UnityWebRequest class is officialy introduced since Unity5.4, it is intended to replace WWW.
//   The primary advantage of UnityWebRequest is memory efficiency. It does not load entire
//   AssetBundle into the memory while WWW does.
//
// For Unity5.5 we let ABM to use WWW since we observed a download failure case.
// (https://bitbucket.org/Unity-Technologies/assetbundledemo/pull-requests/25)
//
// Or you can force ABM to use either method by setting one of the following symbols in
// [Player Settings]-[Other Settings]-[Scripting Define Symbols] of each platform.
//
// - ABM_USE_WWW    (to use WWW)
// - ABM_USE_UWREQ  (to use UnityWebRequest)

#if !ABM_USE_WWW && !ABM_USE_UWREQ
#if UNITY_5_4_OR_NEWER && !UNITY_5_5
#define ABM_USE_UWREQ
#else
#define ABM_USE_WWW
#endif
#endif
using UnityEngine;
#if ABM_USE_UWREQ
using UnityEngine.Networking;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using Zenject;

/*  The AssetBundle Manager provides a High-Level API for working with AssetBundles. 
    The AssetBundle Manager will take care of loading AssetBundles and their associated 
    Asset Dependencies.
        Initialize()
            Initializes the AssetBundle manifest object.
        LoadAssetAsync()
            Loads a given asset from a given AssetBundle and handles all the dependencies.
        LoadLevelAsync()
            Loads a given scene from a given AssetBundle and handles all the dependencies.
        LoadDependencies()
            Loads all the dependent AssetBundles for a given AssetBundle.
        BaseDownloadingURL
            Sets the base downloading url which is used for automatic downloading dependencies.
        SimulateAssetBundleInEditor
            Sets Simulation Mode in the Editor.
        Variants
            Sets the active variant.
        RemapVariantName()
            Resolves the correct AssetBundle according to the active variant.
*/


namespace AssetDelivery
{
    
    /// <summary>
    /// Loaded assetBundle contains the references count which can be used to
    /// unload dependent assetBundles automatically.
    /// </summary>
    public class LoadedAssetBundle
    {
        public AssetBundle AssetBundle { get; }
        public int m_ReferencedCount;

        internal event Action unload;

        internal void OnUnload()
        {
            AssetBundle.Unload(false);
            unload?.Invoke();
        }

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            AssetBundle = assetBundle;
            m_ReferencedCount = 1;
        }
    }
    
     /// <summary>
    /// Class takes care of loading assetBundle and its dependencies
    /// automatically, loading variants automatically.
    /// </summary>
    public class AssetBundleManager : AssetManager
    {
        static AssetBundleManifest m_AssetBundleManifest = null;
#if UNITY_EDITOR
        const string kSimulateAssetBundles = "SimulateAssetBundles";
#endif

        static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
        static List<string> m_DownloadingBundles = new List<string>();
        static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

#if UNITY_EDITOR
        [Inject] readonly AssetBundleLoadLevelSimulationOperation.Factory m_assetBundleLoadLevelSimulationOperationFactory;
#endif
        [Inject] readonly AssetBundleLoadLevelOperation.Factory m_AssetBundleLoadLevelOperationFactory;

        
        /// <summary>
        /// AssetBundleManifest object which can be used to load the dependecies
        /// and check suitable assetBundle variants.
        /// </summary>
        public static AssetBundleManifest AssetBundleManifestObject
        {
            get { return m_AssetBundleManifest; }
            set { m_AssetBundleManifest = value; }
        }
        
        private static string GetStreamingAssetsPath()
        {
#if UNITY_EDITOR
            if (Application.isEditor)
                return "file://" +  System.Environment.CurrentDirectory.Replace("\\", "/"); // Use the build output folder directly.
            else
#endif
            if (Application.isMobilePlatform || Application.isConsolePlatform)
                return Application.streamingAssetsPath;
            else // For standalone player.
                return "file://" +  Application.streamingAssetsPath;
        }

        /// <summary>
        /// Retrieves an asset bundle that has previously been requested via LoadAssetBundle.
        /// Returns null if the asset bundle or one of its dependencies have not been downloaded yet.
        /// </summary>
        public static LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                return null;

            m_LoadedAssetBundles.TryGetValue(assetBundleName, out LoadedAssetBundle bundle);
            if (bundle == null)
                return null;

            // No dependencies are recorded, only the bundle itself is required.
            if (!m_Dependencies.TryGetValue(assetBundleName, out string[] dependencies))
                return bundle;

            // Make sure all dependencies are loaded
            foreach (var dependency in dependencies)
            {
                if (m_DownloadingErrors.TryGetValue(dependency, out error))
                    return null;

                // Wait all the dependent assetBundles being loaded.
                m_LoadedAssetBundles.TryGetValue(dependency, out LoadedAssetBundle dependentBundle);
                if (dependentBundle == null)
                    return null;
            }

            return bundle;
        }

        public override void SetSourceAssetsAbsoluteDirectory(string absolutePath)
        {
            if (!absolutePath.EndsWith("/"))
                absolutePath += "/";
            
            mBaseDownloadingURL = absolutePath;
        }

        /// <summary>
        /// Returns true if certain asset bundle has been downloaded without checking
        /// whether the dependencies have been loaded.
        /// </summary>
        public override bool IsAssetBundleDownloaded(string assetsBundleName)
        {
            return m_LoadedAssetBundles.ContainsKey(assetsBundleName);
        }

        /// <summary>
        /// Initializes asset bundle namager and starts download of manifest asset bundle.
        /// Returns the manifest asset bundle downolad operation object.
        /// </summary>
        public override IEnumerator Initialize()
        {
#if UNITY_EDITOR
            Log(LogType.Info, "Simulation Mode: " + (SimulateAssetDeliveryInEditor ? "Enabled" : "Disabled"));
#endif

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't need the manifest assetBundle.
            if (SimulateAssetDeliveryInEditor)
                return null;
#endif
            if (string.IsNullOrEmpty(mBaseDownloadingURL))
                Debug.LogError($"Base downloading URL is empty! Use {nameof(SetSourceAssetsAbsoluteDirectory)} method before init");

            string platformName = Utility.PlatformName;
            mStreamingAssetsPath = $"{Application.streamingAssetsPath}/{platformName}/";

            LoadAssetBundle(platformName, true);
            AssetLoadOperation operation = new AssetBundleLoadManifestOperation(platformName, "AssetBundleManifest", typeof(AssetBundleManifest));
            mInProgressOperations.Add(operation);
            return operation;
        }

//         private IEnumerator CachingBundles ()
//         {
// #if UNITY_EDITOR
//             // If we're in Editor simulation mode, we don't need the manifest assetBundle.
//             if (SimulateAssetDeliveryInEditor)
//                 yield break;
// #endif
//             Debug.LogFormat("Caching:\n"+
//                             "enabled: {0}, ready: {4}, compressionEnabled: {5}\n"+
//                             "expirationDelay: {1}\n"+
//                             "spaceOccupied: {2}\n"+
//                             "spaceFree: {3}\n"+
//                             "maximumAvailableDiskSpace: {6}",
//                 Caching.defaultCache.valid, Caching.defaultCache.expirationDelay, Caching.defaultCache.spaceOccupied, Caching.defaultCache.spaceFree, Caching.ready,
//                 Caching.compressionEnabled, Caching.defaultCache.maximumAvailableStorageSpace);
//
//             var bundles = m_AssetBundleManifest.GetAllAssetBundles();
//             foreach (var bundleName in bundles) {
//                 string bundleBaseDownloadingURL = GetAssetBaseDownloadingURL(bundleName);
//                 if (!bundleBaseDownloadingURL.EndsWith("/")) {
//                     bundleBaseDownloadingURL += "/";
//                 }
//
//                 string url = bundleBaseDownloadingURL + bundleName;
//
//                 bool isCached = Caching.IsVersionCached(url, m_AssetBundleManifest.GetAssetBundleHash(bundleName));
//
//                 if (!isCached) {
//                     Debug.LogFormat("Load bundle: {0}, isCached: {1}", bundleName, isCached);
//                     LoadAssetBundle(bundleName);
//                 }
//             }
//
//             bool isDone = false;
//
//             while (!isDone) {
//                 while (mInProgressOperations.Count > 0) {
//                     yield return new WaitForSecondsRealtime(1f);
//                 }
//
//                 isDone = m_DownloadingErrors.Count == 0;
//
//                 yield return null;
//             }
//         }

        // Starts the download of the asset bundle identified by the given name, and asset bundles
        // that this asset bundle depends on.
        private void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest = false)
        {
            Log(LogType.Info, "Loading Asset Bundle " + (isLoadingAssetBundleManifest ? "Manifest: " : ": ") + assetBundleName);

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
            if (SimulateAssetDeliveryInEditor)
                return;
#endif

            if (!isLoadingAssetBundleManifest && m_AssetBundleManifest == null)
            {
                Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Check if the assetBundle has already been processed.
            bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest);

            // Load dependencies.
            if (!isAlreadyProcessed && !isLoadingAssetBundleManifest)
                LoadDependencies(assetBundleName);
        }

        // Checks who is responsible for determination of the correct asset bundle variant
        // that should be loaded on this platform. 
        //
        // On most platforms, this is done by the AssetBundleManager itself. However, on
        // certain platforms (iOS at the moment) it's possible that an external asset bundle
        // variant resolution mechanism is used. In these cases, we use base asset bundle 
        // name (without the variant tag) as the bundle identifier. The platform-specific 
        // code is responsible for correctly loading the bundle.
        protected static bool UsesExternalBundleVariantResolutionMechanism(string baseAssetBundleName)
        {
#if ENABLE_IOS_APP_SLICING
            var url = GetAssetBaseDownloadingURL(baseAssetBundleName);
            if (url.ToLower().StartsWith("res://") ||
                url.ToLower().StartsWith("odr://"))
                return true;
#endif
            return false;
        }

        // Remaps the asset bundle name to the best fitting asset bundle variant.
        private static string RemapVariantName(string assetName)
        {
            string[] bundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();

            // Get base bundle name
            string baseName = assetName.Split('.')[0];

            if (UsesExternalBundleVariantResolutionMechanism(baseName))
                return baseName;

            int bestFit = int.MaxValue;
            int bestFitIndex = -1;
            // Loop all the assetBundles with variant to find the best fit variant assetBundle.
            for (int i = 0; i < bundlesWithVariant.Length; i++)
            {
                string[] curSplit = bundlesWithVariant[i].Split('.');
                string curBaseName = curSplit[0];
                string curVariant = curSplit[1];

                if (curBaseName != baseName)
                    continue;

                int found = System.Array.IndexOf(mActiveVariants, curVariant);

                // If there is no active variant found. We still want to use the first
                if (found == -1)
                    found = int.MaxValue - 1;

                if (found < bestFit)
                {
                    bestFit = found;
                    bestFitIndex = i;
                }
            }

            if (bestFit == int.MaxValue - 1)
            {
                Log(LogType.Warning, "Ambigious asset bundle variant chosen because there was no matching active variant: " + bundlesWithVariant[bestFitIndex]);
            }

            if (bestFitIndex != -1)
            {
                return bundlesWithVariant[bestFitIndex];
            }
            else
            {
                return assetName;
            }
        }

        // Sets up download operation for the given asset bundle if it's not downloaded already.
        private bool LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest)
        {
            // Already loaded.
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out LoadedAssetBundle bundle);
            if (bundle != null)
            {
                bundle.m_ReferencedCount++;
                return true;
            }

            // @TODO: Do we need to consider the referenced count of WWWs?
            // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
            // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.
            if (m_DownloadingBundles.Contains(assetBundleName))
                return true;

            string bundleBaseDownloadingURL = GetAssetBaseDownloadingURL(assetBundleName);

            if (bundleBaseDownloadingURL.ToLower().StartsWith("odr://"))
            {
#if ENABLE_IOS_ON_DEMAND_RESOURCES
                Log(LogType.Info, "Requesting bundle " + assetBundleName + " through ODR");
                mInProgressOperations.Add(new AssetBundleDownloadFromODROperation(assetBundleName));
#else
                throw new ApplicationException("Can't load bundle " + assetBundleName + " through ODR: this Unity version or build target doesn't support it.");
#endif
            }
            else if (bundleBaseDownloadingURL.ToLower().StartsWith("res://"))
            {
#if ENABLE_IOS_APP_SLICING
                Log(LogType.Info, "Requesting bundle " + assetBundleName + " through asset catalog");
                mInProgressOperations.Add(new AssetBundleOpenFromAssetCatalogOperation(assetBundleName));
#else
                throw new ApplicationException("Can't load bundle " + assetBundleName + " through asset catalog: this Unity version or build target doesn't support it.");
#endif
            }
            else
            {
                string url = bundleBaseDownloadingURL + assetBundleName;

#if UNITY_WEBGL
                if (!isLoadingAssetBundleManifest)
                    url += $"?={m_AssetBundleManifest.GetAssetBundleHash(assetBundleName)}";
#endif

#if !UNITY_EDITOR && UNITY_ANDROID && !NO_GPGS   // Play Asset Delivery.
                mInProgressOperations.Add(new AssetBundleDownloadFromPADOperation(assetBundleName));
#elif ABM_USE_UWREQ
                // If url refers to a file in StreamingAssets, use AssetBundle.LoadFromFileAsync to load.
                // UnityWebRequest also is able to load from there, but we use the former API because:
                // - UnityWebRequest under Android OS fails to load StreamingAssets files (at least Unity5.50 or less)
                // - or UnityWebRequest anyway internally calls AssetBundle.LoadFromFileAsync for StreamingAssets files
                // if (url.StartsWith(Application.persistentDataPath) || url.StartsWith(Application.streamingAssetsPath))
                // {
                //     Debug.LogError(url);
                //     Debug.LogError(assetBundleName);
                
                mInProgressOperations.Add(new AssetBundleDownloadFileOperation(assetBundleName, url));
                
                // }
                // else
                // {
                //     // For manifest assetbundle, always download it as we don't have hash for it.
                //     UnityWebRequest request = isLoadingAssetBundleManifest ? UnityWebRequestAssetBundle.GetAssetBundle(url)
                //         : UnityWebRequestAssetBundle.GetAssetBundle(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0);
                //     mInProgressOperations.Add(new AssetBundleDownloadWebRequestOperation(assetBundleName, request));
                // }

#else
                WWW download = null;
                if (isLoadingAssetBundleManifest) {
                    // For manifest assetbundle, always download it as we don't have hash for it.
                    download = new WWW(url);
                } 
                else {
                    download = WWW.LoadFromCacheOrDownload(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0);
                }
                mInProgressOperations.Add(new AssetBundleDownloadFromWebOperation(assetBundleName, download));
#endif
            }
            m_DownloadingBundles.Add(assetBundleName);

            return false;
        }

        // Where we get all the dependencies and load them all.
        private void LoadDependencies(string assetBundleName)
        {
            if (m_AssetBundleManifest == null)
            {
                Log(LogType.Error, "Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Get dependecies from the AssetBundleManifest object..
            string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
                return;

            for (int i = 0; i < dependencies.Length; i++)
                dependencies[i] = RemapVariantName(dependencies[i]);

            // Record and load all dependencies.
            m_Dependencies.Add(assetBundleName, dependencies);
            for (int i = 0; i < dependencies.Length; i++)
                LoadAssetBundleInternal(dependencies[i], false);
        }

        /// <summary>
        /// Unloads assetbundle and its dependencies.
        /// </summary>
        private void UnloadAssetBundle(string assetBundleName)
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
            if (SimulateAssetDeliveryInEditor)
                return;
#endif
            assetBundleName = RemapVariantName(assetBundleName);

            UnloadAssetBundleInternal(assetBundleName);
            UnloadDependencies(assetBundleName);
        }

        private void UnloadDependencies(string assetBundleName)
        {
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return;

            // Loop dependencies.
            foreach (var dependency in dependencies)
            {
                UnloadAssetBundleInternal(dependency);
            }

            m_Dependencies.Remove(assetBundleName);
        }

        private static void UnloadAssetBundleInternal(string assetBundleName)
        {
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out string error);
            if (bundle == null)
                return;

            if (--bundle.m_ReferencedCount == 0)
            {
                bundle.OnUnload();
                m_LoadedAssetBundles.Remove(assetBundleName);

                Log(LogType.Info, assetBundleName + " has been unloaded successfully");
            }
        }

        protected override void ProcessFinishedOperation(AssetLoadOperation operation)
        {
            if (!(operation is AssetBundleDownloadOperation download))
                return;

            if (string.IsNullOrEmpty(download.error))
                m_LoadedAssetBundles.Add(download.AssetBundleName, download.assetBundle);
            else
            {
                string msg = $"Failed downloading bundle {download.AssetBundleName} from {download.GetSourceURL()}: {download.error}";
                m_DownloadingErrors.Add(download.AssetBundleName, msg);
            }

            m_DownloadingBundles.Remove(download.AssetBundleName);
        }

        /// <summary>
        /// Starts a load operation for an asset from the given asset bundle.
        /// </summary>
        private IEnumerator LoadAssetAsync(string assetBundleName, string assetName, System.Type type)
        {
            assetBundleName = FindAssetBundle(assetBundleName);
            Log(LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");

            AssetBundleLoadAssetOperation operation = null;
#if UNITY_EDITOR
            if (SimulateAssetDeliveryInEditor)
            {
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
                if (assetPaths.Length == 0)
                {
                    Log(LogType.Error, "There is no asset with name \"" + assetName + "\" in " + assetBundleName);
                    return null;
                }

                // @TODO: Now we only get the main object from the first asset. Should consider type also.
                UnityEngine.Object target = AssetDatabase.LoadMainAssetAtPath(assetPaths[0]);
                operation = new AssetBundleLoadAssetOperationSimulation(target);
            }
            else
#endif
            {
                assetBundleName = RemapVariantName(assetBundleName);
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type);

                mInProgressOperations.Add(operation);
            }

            return operation;
        }

        /// <summary>
        /// Starts a load operation for an asset from the given asset bundle.
        /// </summary>
        private AssetLoadOperation LoadAllAssetsAsync(string assetBundleName, System.Type type)
        {
            assetBundleName = FindAssetBundle(assetBundleName);
            Log(LogType.Info, $"Loading all assets of type {type} from {assetBundleName} bundle");

            AssetBundleLoadAllAssetsOperation operation = null;
#if UNITY_EDITOR
            if (SimulateAssetDeliveryInEditor)
            {
                string[] assetsPaths = AssetDatabase.GetAssetPathsFromAssetBundle (assetBundleName);

                List<UnityEngine.Object> lst = new List<UnityEngine.Object>();
                foreach (string assetPath in assetsPaths)
                {
                    Type t = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (type.IsAssignableFrom(t))
                    {
                        lst.Add(AssetDatabase.LoadMainAssetAtPath(assetPath));
                    }
                }

                operation = new AssetBundleLoadAllAssetsOperationSimulation(lst);
            }
            else
#endif
            {
                assetBundleName = RemapVariantName(assetBundleName);
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadAllAssetsOperationFull(assetBundleName, type);

                mInProgressOperations.Add(operation);
            }

            return operation;
        }

        /// <summary>
        /// Starts a load operation for a level from the given asset bundle.
        /// </summary>
        public override IEnumerator LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, Action<DiContainer> extraBindings = null)
        {
            assetBundleName = FindAssetBundle(assetBundleName);
            Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");

            AssetLoadOperation operation = null;
#if UNITY_EDITOR
            if (SimulateAssetDeliveryInEditor)
            {
                operation = m_assetBundleLoadLevelSimulationOperationFactory.Create(assetBundleName, levelName, isAdditive, extraBindings);
            }
            else
#endif
            {
                assetBundleName = RemapVariantName(assetBundleName);
                LoadAssetBundle(assetBundleName);
                operation = m_AssetBundleLoadLevelOperationFactory.Create(assetBundleName, levelName, isAdditive, extraBindings);

                mInProgressOperations.Add(operation);
            }

            return operation;
        }

        private string FindAssetBundle (string startsWith)
        {
            string[] bundles;
#if UNITY_EDITOR
            if (SimulateAssetDeliveryInEditor)
            {
                bundles = AssetDatabase.GetAllAssetBundleNames();
            }
            else
#endif
            {
                bundles = m_AssetBundleManifest.GetAllAssetBundles();
            }
#if !UNITY_EDITOR && (UNITY_ANDROID && !NO_GPGS || ENABLE_IOS_ON_DEMAND_RESOURCES)   // Play Asset Delivery & iOS On Demand Resources.
            startsWith = startsWith.Replace('/', '_');
#endif
            
            foreach (var b in bundles)
            {
                if (b.StartsWith (startsWith, StringComparison.Ordinal))
                {
                    return b;
                }
            }
            Debug.LogErrorFormat("No assets bundle found with name starts with '{0}'", startsWith);
            return "";
        }

        public override async Task<T> LoadAssetAsync<T>(string assetBundleName, string assetName)
        {
            AssetBundleLoadAssetOperation request = (AssetBundleLoadAssetOperation)LoadAssetAsync(assetBundleName, assetName, typeof(T));

            if (request == null)
                return null;

            await request;
            return request.GetAsset<T>();
        }

        public override async Task<T[]> LoadAllAssetsAsync<T>(string assetBundleName)
        {
            AssetBundleLoadAllAssetsOperation request = (AssetBundleLoadAllAssetsOperation)LoadAllAssetsAsync(HelpTools.GameBundle(assetBundleName), typeof(T));

            if (request == null)
                return null;

            await request;
            return request.GetAssets<T>();
        }

    } // End of AssetBundleManager.
}
#endif
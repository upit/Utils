#if USE_BUNDLES
#if !UNITY_EDITOR && (ENABLE_IOS_ON_DEMAND_RESOURCES || UNITY_ANDROID && !NO_GPGS)
#define USE_OWN_ASSET_DELIVERY
#endif

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AssetDelivery
{
#if !(UNITY_WSA && !UNITY_WSA_10_0)
    public class AssetBundleDownloader : AssetDownloaderAbstract
    {
        public string AssetsPath { get { return m_assetsPath; } }

        private const string ASSETS_DIRECTORY_NAME = @"assets";

        protected string m_assetsPath;
        protected string m_basePath;

        protected bool m_isLocalAssetsGood = false;

        protected List<Bundleinfo> m_localBundles = new List<Bundleinfo>();

        protected override void Awake()
        {
            base.Awake();
            m_assetsPath = Path.Combine(Application.persistentDataPath, ASSETS_DIRECTORY_NAME);
            m_basePath = Path.Combine(m_assetsPath, Utility.PlatformName);

            if (!Directory.Exists(m_basePath))
                Directory.CreateDirectory(m_basePath);
        }

        protected override IEnumerator Start()
        {
            // Loading local manifest
            string localManifestPath = Path.Combine(m_basePath, Utility.PlatformName);
            yield return LoadLocalManifest(localManifestPath);

            // Loading remote manifest
            yield return LoadRemoteManifest();

            // Find expired bundles and remove them
            if (m_isRemoteManifestLoaded)
            {
                foreach (var localBundle in m_localBundles)
                {
                    if (m_remoteBundles.Find(b => b.hash == localBundle.hash) == null)
                    {
                        File.Delete(localBundle.pathOrUri);
                    }
                }
            }

            yield return null;
            
#if UNITY_EDITOR || !USE_OWN_ASSET_DELIVERY // On Demand Resources && Play Asset Delivery.
            // Add new manifest to download first
            string remoteManifest = m_baseRemoteUri + Utility.PlatformName;
            m_tasksQueue.Enqueue(new DownloadFileTask(remoteManifest + cacheInvalidationRandomQueryString, localManifestPath, -1L, true));
            m_tasksQueue.Enqueue(new DownloadFileTask(remoteManifest + ".json" + cacheInvalidationRandomQueryString, localManifestPath + ".json", -1L, false));
#endif

            yield return new WaitUntil(() => m_isRemoteManifestLoaded);
            m_initDone = true;
        }

        // public override void DownloadAllRemoteAssets(string[] exclude = null)
        // {
        //     m_loadingFullBytes = 0L;
        //     m_progressBytes = 0L;
        //     // m_currentProgressBytes = 0L;
        //     
        //     // Find new bundles and queue them for downloading
        //     if (exclude == null || exclude.Length == 0)
        //         foreach (Bundleinfo remoteBundle in m_remoteBundles) 
        //             DownloadRemoteBundle(remoteBundle);
        //     else
        //     {
        //         var excludeList = new List<string>(exclude);
        //         foreach (Bundleinfo remoteBundle in m_remoteBundles)
        //         {
        //             if (!excludeList.Contains(remoteBundle.bundleName))
        //                 DownloadRemoteBundle(remoteBundle);
        //         }
        //     }
        //
        //     m_isSizeEstimated = true;
        //
        //     bool isLoading = IsLoading;
        //     if (!isLoading && !m_isLocalAssetsGood)
        //     {
        //         MessageBox.Show(MessageBox.Type.Critical, "Can't download game assets.", (m) => GameData.QuitGame());
        //         return;
        //     }
        //
        //     m_isDone = m_isLocalAssetsGood && !isLoading;
        // }

        public override void DownloadRemoteAssets(string[] bundleNames, bool criticalAssets = true, int threads = DEFAULT_MAX_DOWNLOADS)
        {
            if (bundleNames == null || bundleNames.Length == 0)
                return;

            // Если предыдущая загрузка завершена - скидываем прогресс.
            if (m_isDone)
            {
                m_loadingFullBytes = 0L;
                m_progressBytes = 0L;    
            }
            
            foreach (string bundleName in bundleNames) 
                DownloadRemoteBundle(bundleName);

            m_isSizeEstimated = true;
            base.DownloadRemoteAssets(bundleNames, criticalAssets, threads);
        }

        private void DownloadRemoteBundle(Bundleinfo remoteBundle)
        {
            if (m_localBundles.Find(b => b.hash == remoteBundle.hash) == null)
            {
                m_loadingFullBytes += remoteBundle.size;
                string bundleName = remoteBundle.bundleName;

                DownloadTaskAbstract downloadTask = new
#if ENABLE_IOS_ON_DEMAND_RESOURCES
                    DownloadODRFileTask(remoteBundle);
#elif UNITY_ANDROID && !NO_GPGS
                    DownloadPADFileTask(remoteBundle);
#else
                    DownloadFileTask(remoteBundle, Path.Combine(m_basePath, bundleName));
#endif
                m_tasksQueue.Enqueue(downloadTask);
                
                RaiseOnStartBundleLoadingEvent(bundleName);
                OnBundleLoaded += uri =>
                {
                    if (uri.Contains(bundleName))
                        m_localBundles.Add(remoteBundle);
                };
            }
        }

        private void DownloadRemoteBundle(string bundleName)
        {
#if USE_OWN_ASSET_DELIVERY
            bundleName = bundleName.Replace('/', '_');
#endif
            foreach (Bundleinfo remoteBundle in m_remoteBundles)
            {
                if (remoteBundle.bundleName.Equals(bundleName))
                {
                    DownloadRemoteBundle(remoteBundle);
                    
                    // Подгружаем зависимости.
                    // if (downloadDependent && remoteBundle.dependencies != null)
                    // {
                    //     foreach (string dependency in remoteBundle.dependencies) 
                    //         DownloadRemoteBundle(dependency);
                    // }

                    return;
                }
            }
        }

        /// <summary>
        /// Загружает манифест и наш JSON-компаньон локальных, скачанных бандлов,
        /// проверяет их валидность.
        /// </summary>
        /// <remarks>
        /// Устанавливает <see cref="m_isLocalAssetsGood" /> в <see langword="true" />,
        /// если все бандлы валидны и загружаются.<br/>
        /// 
        /// Добавляет все валидные бандлы в <see cref="m_localBundles" />.
        /// </remarks>
        private IEnumerator LoadLocalManifest(string localManifestPath)
        {
            if (!File.Exists(localManifestPath))
                yield break;

            var opLoadLocal = AssetBundle.LoadFromFileAsync(localManifestPath);
            yield return opLoadLocal;

            if (opLoadLocal.assetBundle == null)
                yield break;

            var opLoadAssetLocal = opLoadLocal.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
            yield return opLoadAssetLocal;
            AssetBundleManifest localAssetBundleManifest = opLoadAssetLocal.asset as AssetBundleManifest;
            if (localAssetBundleManifest == null)
            {
                opLoadLocal.assetBundle.Unload(true);
                yield break;
            }

            string jsonLocalPath = localManifestPath + ".json";
            Dictionary<string, JsonPrefs> jsonLocal = new Dictionary<string, JsonPrefs>();
            if (File.Exists(jsonLocalPath))
            {
                var jsonLocalDict = MiniJSON.Json.Deserialize(
                    File.ReadAllText(jsonLocalPath)) as Dictionary<string, object>;

                if (jsonLocalDict != null)
                {
                    jsonLocal = jsonLocalDict
                        .ToDictionary(key => key.Key, val => new JsonPrefs(val.Value));
                }
                else
                    Debug.LogWarning("JSON manifest is null. Skipping bundles size checking.");
            }
            else
            {
                Debug.LogWarning("JSON manifest does not exist. Skipping bundles size checking.");
            }

            bool allBundlesOk = true;
            foreach (string bundleName in localAssetBundleManifest.GetAllAssetBundles())
            {
                string fileName = Path.Combine(m_basePath, bundleName);
                
                string tempFileName = fileName + ".tmp";
                if(File.Exists(tempFileName)) 
                    File.Delete(tempFileName);

                if (!File.Exists(fileName))
                {
                    allBundlesOk = false;
                    Debug.LogWarningFormat("Local bundle does not exist: '{0}'", bundleName);
                    continue;
                }

                // Пока убрал проверку размера, потому что бандлы пережимаются после получения.
                // long requiredSize = jsonLocal.ContainsKey(bundleName) ? jsonLocal[bundleName].ValueLong("size") : -1;
                // if (requiredSize > 0)
                // {
                //     var fi = new FileInfo(fileName);
                //     if (fi.Length != requiredSize)
                //     {
                //         allBundlesOk = false;
                //         Debug.LogWarningFormat("Loaded bundle size not equal required in JSON manifest: '{0}' ({1}), requred size: {2}", bundleName, fi.Length, requiredSize);
                //         continue;
                //     }
                // }
                var testBundle = AssetBundle.LoadFromFileAsync(fileName);
                yield return testBundle;
                if (testBundle.assetBundle == null)
                {
                    allBundlesOk = false;
                    Debug.LogWarningFormat("Test load local bundle failed! '{0}'", bundleName);
                    continue;
                }
                testBundle.assetBundle.Unload(true);

                m_localBundles.Add(Bundleinfo.FromLocalPath(bundleName, fileName, localAssetBundleManifest.GetAssetBundleHash(bundleName)));
            }
            m_isLocalAssetsGood = allBundlesOk;
            opLoadLocal.assetBundle.Unload(true);
        }

        protected override bool IsBundleOk(DownloadTaskAbstract task)
        {
#if USE_OWN_ASSET_DELIVERY  // Подумать есть ли смысл проверять целостность и реализовать.
            return true;
#endif
            var bundle = AssetBundle.LoadFromFile(((DownloadFileTask)task).FileName);
            if (bundle == null)
            {
                return false;
            }
            bundle.Unload(true);
            return true;
        }

        public override bool IsRemoteAssetDownloaded(string assetName)
        {
#if USE_OWN_ASSET_DELIVERY
            assetName = assetName.Replace('/', '_');
#endif
            return base.IsRemoteAssetDownloaded(assetName) || m_localBundles.Any(bundle => bundle.bundleName == assetName);
        }
    }
#endif
}
#endif
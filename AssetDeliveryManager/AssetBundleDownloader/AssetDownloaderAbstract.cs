#if USE_BUNDLES
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.Networking;

#if !UNITY_EDITOR && UNITY_ANDROID && !NO_GPGS    // Play Asset Delivery.
using Google.Play.AssetDelivery;
#endif

namespace AssetDelivery
{
    public abstract class AssetDownloaderAbstract : MonoBehaviour, IAssetDownloader
    {
        protected const float DELAY_BETWEEN_DOWNLOADS = 1f; // sec
        protected const int DEFAULT_MAX_DOWNLOADS =
#if UNITY_WEBGL
        1;
#else
        3;
#endif
        
        public bool IsSizeEstimated { get { return m_isSizeEstimated; } }
        public bool WaitBeforeBundlesDownload { get; set; }

        public bool IsInit { get { return m_initDone; } }
        public bool IsDone { get { return m_isDone; } }
        public bool IsLoading { get { return m_tasksQueue.Count > 0; } }
        public long LoadingBytesFull { get { return m_loadingFullBytes; } }
        public long LoadingBytes { get { return m_progressBytes + m_currentProgressBytes; } }
        public event Action<string> OnStartBundleLoading;
        protected void RaiseOnStartBundleLoadingEvent(string str) => OnStartBundleLoading?.Invoke(str);
        public event Action<string> OnBundleLoaded;
        public event Action OnDownloadFinished;
        public bool IsDownloadingCriticalAssets { get; private set; }

        protected List<Bundleinfo> m_remoteBundles = new List<Bundleinfo>();
        // protected AssetBundleManifest remoteAssetBundleManifest;
        protected bool m_isRemoteManifestLoaded;

        protected bool m_isSizeEstimated;

        protected string m_baseRemoteUri;
        
        protected string streamingAssetsPath;
        protected bool m_isDone;
        protected bool m_initDone;
        protected int m_processingBundlesCount;
        // protected bool m_isLoading = false;
        protected long m_loadingFullBytes;
        protected long m_progressBytes;
        protected long m_currentProgressBytes;

        protected int m_maxConcurentDownloads = DEFAULT_MAX_DOWNLOADS;
        protected float delayStartTime;

        protected string cacheInvalidationRandomQueryString = string.Empty;

        protected readonly Queue<DownloadTaskAbstract> m_tasksQueue = new Queue<DownloadTaskAbstract>();
        protected readonly List<DownloadTaskAbstract> m_workingTasks = new List<DownloadTaskAbstract>();
        protected readonly List<DownloadTaskAbstract> m_waitingTasks = new List<DownloadTaskAbstract>();

        protected virtual void Awake()
        {
            cacheInvalidationRandomQueryString = $"?={MiscTools.random.Next():X}";
        }

        protected abstract IEnumerator Start();

        public virtual void Init(string game, string version)
        {
            
            m_baseRemoteUri =
#if UNITY_EDITOR
                $"http://scifi-tanks.com/files/assets/{game}/{version}";    //"file:///AssetBundles";
#elif DEVELOPMENT_BUILD || TEST_BUILD
                $"http://scifi-tanks.com/files/assets/{game}/{version}";
#else
                $"https://cdn.extreme-developers.ru/assets/{game}/{version}";
#endif
            string platformName = Utility.PlatformName; 
            m_baseRemoteUri += $"/{platformName}/";

            // На WebGL используем https.
#if UNITY_WEBGL
            m_baseRemoteUri = m_baseRemoteUri.Replace("http", "https");
#elif ENABLE_IOS_ON_DEMAND_RESOURCES
            if (UnityEngine.iOS.OnDemandResources.enabled) 
                m_baseRemoteUri = "res://";
#endif

            streamingAssetsPath = $"{Application.streamingAssetsPath}/{platformName}/";
        }

        /// <summary>
        /// Загружает манифест и наш JSON-компаньон удалённых бандлов.
        /// </summary>
        /// <remarks>
        /// Устанавливает <see cref="m_isRemoteManifestLoaded" /> в <see langword="true" />,
        /// когда манифест успешно загружен.<br />
        /// 
        /// Добавляет все бандлы из скачанного манифеста в список <see cref="m_remoteBundles" />.
        /// </remarks>
        protected virtual IEnumerator LoadRemoteManifest()
        {
            bool isDone = false;
            int tries = 0;
            AssetBundle bundle = null;
            AssetBundleManifest remoteAssetBundleManifest = null;
            m_isRemoteManifestLoaded = false;
            string platformName = Utility.PlatformName;
            while (!isDone)
            {
#if !UNITY_EDITOR && ENABLE_IOS_ON_DEMAND_RESOURCES  // On Demand Resources.
                var requestODR = UnityEngine.iOS.OnDemandResources.PreloadAsync(new[] {platformName});
                yield return requestODR;
                
                // Check for errors
                if (!string.IsNullOrEmpty(requestODR.error))
                    throw new Exception("ODR manifest request failed: " + requestODR.error);
                bundle = AssetBundle.LoadFromFile(m_baseRemoteUri + platformName);
                // requestODR.Dispose();
#elif !UNITY_EDITOR && UNITY_ANDROID && !NO_GPGS    // Play Asset Delivery.
                PlayAssetBundleRequest request = PlayAssetDelivery.RetrieveAssetBundleAsync(platformName);
                yield return request;
                // yield return new WaitUntil(() => request.IsDone);
                
                // Check for errors
                AssetDeliveryErrorCode errorCode = request.Error;
                if (errorCode != AssetDeliveryErrorCode.NoError)
                    throw new Exception("PAD manifest request failed: " + errorCode);

                bundle = request.AssetBundle;
#else
                tries++;
                UnityWebRequest remoteRequest = UnityWebRequestAssetBundle.GetAssetBundle(
                        m_baseRemoteUri + platformName + cacheInvalidationRandomQueryString);
                yield return remoteRequest.SendWebRequest();

                if (remoteRequest.isNetworkError)
                {
                    if (tries < 4 && CanTryRedownload(remoteRequest))
                    {
                        Debug.LogWarning($"[AssetDownloader] Error while loading {remoteRequest.url}: {remoteRequest.error}");
                        Debug.LogWarning("[AssetDownloader] Try to redownload...");
                        yield return new WaitForSecondsRealtime(1f);
                        continue;
                    }
                    else
                    {
                        Debug.LogError($"[AssetDownloader] Can't load {remoteRequest.url}: {remoteRequest.error}");
                        yield break;
                    }
                }

                bundle = (remoteRequest.downloadHandler as DownloadHandlerAssetBundle)?.assetBundle;
#endif
                if (bundle == null)
                {
                    Debug.LogError($"[AssetDownloader] {platformName} is not a valid asset bundle.");
                    yield break;
                }

                AssetBundleRequest opLoadAssetRemote = bundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
                yield return opLoadAssetRemote;
                remoteAssetBundleManifest = opLoadAssetRemote.asset as AssetBundleManifest;
                if (remoteAssetBundleManifest == null)
                {
                    Debug.LogError("[AssetDownloader] remote manifest processing failed.");
                    bundle.Unload(true);
                    yield break;
                }

                isDone = true;
            }
            m_isRemoteManifestLoaded = true;
            
            // Loading remote bundles sizes
            isDone = false;
            tries = 0;
            Dictionary<string, JsonPrefs> json = new Dictionary<string, JsonPrefs>();
            string jsonName = platformName + ".json";
            
            // ReSharper disable once RedundantAssignment
            string cacheString = string.Empty;
            while (!isDone)
            {
#if !UNITY_EDITOR && ENABLE_IOS_ON_DEMAND_RESOURCES
                // Засовываем json manifest на iOS в streaming assets.
                string jsonData = System.IO.File.ReadAllText(streamingAssetsPath + jsonName);
#else
                tries++;
                string url = m_baseRemoteUri + jsonName + cacheInvalidationRandomQueryString;

// Для Play Asset Delivery файлы из streaming assets получаются get запросом по url
#if !UNITY_EDITOR &&  UNITY_ANDROID && !NO_GPGS
                url = streamingAssetsPath + jsonName;                
#endif
                UnityWebRequest remoteJsonRequest = UnityWebRequest.Get(url);
                yield return remoteJsonRequest.SendWebRequest();

                if (remoteJsonRequest.isNetworkError)
                {
                    if (tries < 4 && CanTryRedownload(remoteJsonRequest))
                    {
                        Debug.LogWarning($"[AssetDownloader] Error while loading {remoteJsonRequest.url}: {remoteJsonRequest.error}");
                        Debug.LogWarning("[AssetDownloader] Try to redownload...");
                        continue;
                    }
                    else
                    {
                        Debug.LogError($"[AssetDownloader] Can't load {remoteJsonRequest.url}: {remoteJsonRequest.error}");
                        break;
                    }
                }
                string jsonData = remoteJsonRequest.downloadHandler.text;
#endif
                
                var remoteJsonManifestDict = MiniJSON.Json.Deserialize(jsonData) as Dictionary<string, object>;

                if (remoteJsonManifestDict == null)
                {
                    Debug.LogError($"[AssetDownloader] deserialized {jsonName} is null");
                    break;
                }

                json = remoteJsonManifestDict
                    .ToDictionary(key => key.Key, val => new JsonPrefs(val.Value));
                isDone = true;
            }
            yield return null;
            foreach (string bundleName in remoteAssetBundleManifest.GetAllAssetBundles())
            {
                Hash128 hash = remoteAssetBundleManifest.GetAssetBundleHash(bundleName);
                string uri = $"{m_baseRemoteUri}{bundleName}?={hash}";
                m_remoteBundles.Add(Bundleinfo.FromRemotePath(bundleName, uri, hash,
                    json.TryGetValue(bundleName, out JsonPrefs jsonPrefs) ? jsonPrefs.ValueLong("size") : 0L)); // Bundle size.
            }
            yield return null;
            bundle.Unload(true);
        }

        public virtual void DownloadRemoteAssets(string[] bundleNames, bool criticalAssets = true, int threads = DEFAULT_MAX_DOWNLOADS)
        {
            IsDownloadingCriticalAssets = criticalAssets;
            m_maxConcurentDownloads = threads;
            m_isDone = false;
            enabled = true;
        }

        public virtual void ClearAllDownloadsExclude(string exclude)
        {
            m_loadingFullBytes = 0L;
            m_progressBytes = 0L;
            
            for (int i = 0; i < m_workingTasks.Count; i++)
            {
                DownloadTaskAbstract task = m_workingTasks[i];
                if (task.Uri.Contains(exclude))
                {
                    m_loadingFullBytes = task.Size;
                    IsDownloadingCriticalAssets = true;
                    continue;
                }

                task.End();
                
                // Удаляем недогруженное.
                if (task is DownloadFileTask fileTask)
                    System.IO.File.Delete(fileTask.FileName);
                
                m_workingTasks.RemoveAt(i--);
            }
                    
            m_waitingTasks.Clear();
            m_tasksQueue.Clear();
        }
        
        public bool IsRemoteAssetDownloadingInProgress(string assetName)
        {
            return m_tasksQueue.Any(task => task.Uri.Contains(assetName)) ||
                   m_workingTasks.Any(task => task.Uri.Contains(assetName)) ||
                   m_waitingTasks.Any(task => task.Uri.Contains(assetName));
        }

        public virtual bool IsRemoteAssetDownloaded(string assetName)
        {
            return System.IO.File.Exists(streamingAssetsPath + assetName);
        }

        protected virtual void Update()
        {
            if (WaitBeforeBundlesDownload || !m_initDone)
                return;

            for (int i = m_waitingTasks.Count - 1; i >= 0; i--)
            {
                var task = m_waitingTasks[i];
                task.WaitForSeconds -= Time.unscaledDeltaTime;
                if (task.WaitForSeconds <= 0f)
                {
                    m_waitingTasks.RemoveAt(i);
                    m_tasksQueue.Enqueue(task);
                }
            }

            // Check for available job slots
#if UNITY_WEBGL
            if (Time.realtimeSinceStartup - delayStartTime >= DELAY_BETWEEN_DOWNLOADS)
#endif
            {
                while (m_tasksQueue.Count > 0 && m_workingTasks.Count < m_maxConcurentDownloads)
                {
                    DownloadTaskAbstract task = m_tasksQueue.Dequeue();
                    if (task.Start())
                    {
                        m_workingTasks.Add(task);
                    }
                    else
                    {
                        task.WaitForSeconds = 1f;
                        m_waitingTasks.Add(task);
                    }
                }
            }

            // Update current tasks
            m_currentProgressBytes = 0L;
            for (int i = m_workingTasks.Count - 1; i >= 0; i--)
            {
                var task = m_workingTasks[i];
                if (!task.Update())
                {
                    m_workingTasks.RemoveAt(i);
                    ProcessFinishedOperation(task);
                }
                else if (task.Size > 0)
                {
                    m_currentProgressBytes += task.Downloaded;
                }
            }

            m_isDone = m_tasksQueue.Count == 0 && m_workingTasks.Count == 0 && m_waitingTasks.Count == 0 && m_processingBundlesCount == 0;
            if (m_isDone)
            {
                OnDownloadFinished?.Invoke();
                OnDownloadFinished = null;
                enabled = false;
            }
        }

        protected async void ProcessFinishedOperation(DownloadTaskAbstract task)
        {
            UnityWebRequest request = task.Request;
            if (request != null && request.isNetworkError)
            {
                TaskLoadError(task);
                return;
            }

            if (task.Size > 0) 
                m_progressBytes += task.Size;

            await ProcessBundle(task);
            
            // Check loaded file for open
            if (task.IsBundle && !IsBundleOk(task))
            {
                TaskLoadError(task);
                return;
            }

            task.End();

            delayStartTime = Time.realtimeSinceStartup;
            OnBundleLoaded?.Invoke(task.Uri);
        }

        protected abstract bool IsBundleOk(DownloadTaskAbstract task);

        protected virtual async Task ProcessBundle(DownloadTaskAbstract task)
        {
            if (!task.IsBundle || !(task is DownloadFileTask downloadFileTask))
                return;
            
            m_processingBundlesCount++;
            string fileName = downloadFileTask.FileName;
            AssetBundleRecompressOperation operation = AssetBundle.RecompressAssetBundleAsync(fileName, fileName, BuildCompression.LZ4Runtime);
            await operation;
            m_processingBundlesCount--;
        }

        private void TaskLoadError(DownloadTaskAbstract task)
        {
            task.WaitForSeconds = 1f;
            task.End();
            m_waitingTasks.Add(task);
        }

        protected bool CanTryRedownload(UnityWebRequest request)
        {
            return request.responseCode == -1 || request.responseCode >= 500;
        }
    }
}
#endif
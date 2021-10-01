#if USE_BUNDLES
using Kongregate;
using System.Collections;
using UnityEngine.Networking;

#if DEVELOPMENT_BUILD || TEST_BUILD
using UnityEngine;
#endif

namespace AssetDelivery
{
    public class AssetBundleCacher : AssetDownloaderAbstract
    {
        protected override IEnumerator Start()
        {
            // Loading remote manifest
            yield return LoadRemoteManifest();

            // Find not cached bundles and queue them for downloading
            foreach (var remoteBundle in m_remoteBundles)
            {
                var cacheEntry = $"{remoteBundle.pathOrUri}?={remoteBundle.hash}";
                var query = new CacheEntryQuery(cacheEntry);
                yield return query;

                if (!query.IsCached)
                {
#if DEVELOPMENT_BUILD || TEST_BUILD
                    Debug.Log($"[AssetBundleCacher] NOT CACHED remoteBundle.pathOrUri: {remoteBundle.pathOrUri}, remoteBundle.hash: {remoteBundle.hash}");
#endif

                    m_loadingFullBytes += remoteBundle.size;
                    m_tasksQueue.Enqueue(new CacheBundleTask(remoteBundle));
                }
#if DEVELOPMENT_BUILD || TEST_BUILD
                else
                {
                    Debug.Log($"[AssetBundleCacher] CACHED remoteBundle.pathOrUri: {remoteBundle.pathOrUri}, remoteBundle.hash: {remoteBundle.hash}");
                }
#endif
            }

            m_isSizeEstimated = true;

            bool isLoading = IsLoading;
            if (!isLoading && !m_isRemoteManifestLoaded)
            {
                // MessageBox.Show(MessageBox.Type.Critical, "Can't download game assets.", (m) => GameData.QuitGame());
                yield break;
            }

            m_isDone = !isLoading;
            m_initDone = true;
        }

        protected override bool IsBundleOk(DownloadTaskAbstract task)
        {
            var bundle = (task.Request.downloadHandler as DownloadHandlerAssetBundle).assetBundle;

            if (bundle == null)
            {
                //Debug.LogError($"Bundle {task.Uri} is NOT OK!");
                return false;
            }

            //Debug.LogError($"Bundle {task.Uri} is OK!");

            bundle.Unload(true);
            return true;
        }
    }
}
#endif
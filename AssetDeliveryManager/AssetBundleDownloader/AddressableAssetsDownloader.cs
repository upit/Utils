using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetDelivery
{
    public class AddressableAssetsDownloader : MonoBehaviour, IAssetDownloader
    {
        public bool IsInit { get; private set; }
        public bool IsDone { get => downloadOperation.IsValid() && downloadOperation.IsDone; }

        public long LoadingBytesFull { get => downloadOperation.IsValid() ? downloadOperation.GetDownloadStatus().TotalBytes : 0L; }
        public long LoadingBytes { get => downloadOperation.IsValid() ? downloadOperation.GetDownloadStatus().DownloadedBytes : 0L; }
        public bool IsDownloadingCriticalAssets { get; private set; }
        public event Action<string> OnStartBundleLoading;
        public event Action<string> OnBundleLoaded;

        private AsyncOperationHandle downloadOperation;
        private List<string> downloadedBundles;
        private List<string> downloadingBundles;
        
        public void Init(string game, string version)
        {
            Addressables.InitializeAsync().Completed += _ => IsInit = true;
            downloadedBundles = new List<string>();
            downloadingBundles = new List<string>();
        }

        public void DownloadRemoteAssets(string[] bundleNames, bool criticalAssets = true, int threads = 3)
        {
            IsDownloadingCriticalAssets = criticalAssets;
            var keysList = new List<string>(bundleNames);
#if TEST_BUILD
            var locOp = Addressables.LoadResourceLocationsAsync(keysList, Addressables.MergeMode.None);
            locOp.Completed+= delegate(AsyncOperationHandle<IList<IResourceLocation>> handle)
            {
                string logResult = "Addressables dependencies: \n";
                foreach (IResourceLocation location in handle.Result)
                {
                    logResult += $"\t{location.InternalId}:\n";
                    IList<IResourceLocation> deps = location.Dependencies;
                    foreach (IResourceLocation dep in deps)
                        logResult += $"\t\t{dep.InternalId}\n";
                }
                
                Debug.LogError(logResult);
            };
#endif
            // AsyncOperationHandle<long> sizeOperation = Addressables.GetDownloadSizeAsync(keysList);
            // sizeOperation.Completed += delegate(AsyncOperationHandle<long> handle)
            // {
            //     Debug.LogError(" download size" + handle.Result);
            // };
            //     
            
            //     // if (handle.Result > 0)
            //     // {
            //     // Debug.LogError("downloading " + bundleName);
            //         
            downloadingBundles.AddRange(keysList);
            downloadOperation = Addressables.DownloadDependenciesAsync(keysList,Addressables.MergeMode.None);
            downloadOperation.Completed += delegate(AsyncOperationHandle operationHandle)
            {
                foreach (string key in keysList) 
                    downloadingBundles.Remove(key);
                
                downloadedBundles.AddRange(keysList);
                // Addressables.Release(downloadOperation);
                Debug.LogError("download complete " +keysList.Aggregate( (current, key) => $"{current}\n{key}"));
            };
        }

        public bool IsRemoteAssetDownloaded(string assetName)
        {
            return downloadedBundles.Contains(assetName);
        }

        public void ClearAllDownloadsExclude(string exclude)
        {
            throw new NotImplementedException();
        }

        public bool IsRemoteAssetDownloadingInProgress(string assetName)
        {
            return downloadingBundles.Contains(assetName);
        }
    }
}
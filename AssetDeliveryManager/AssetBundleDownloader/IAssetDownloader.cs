using System;

namespace AssetDelivery
{
    public interface IAssetDownloader
    {
        bool IsInit { get; }
        bool IsDone { get; }
        long LoadingBytesFull { get; }
        long LoadingBytes { get; }
        bool IsDownloadingCriticalAssets { get; }
        
        event Action<string> OnStartBundleLoading;
        event Action<string> OnBundleLoaded;

        void Init(string game, string version);
        
        void DownloadRemoteAssets(string[] bundleNames, bool criticalAssets = true, int threads = 3);

        bool IsRemoteAssetDownloaded(string assetName);

        void ClearAllDownloadsExclude(string exclude);

        bool IsRemoteAssetDownloadingInProgress(string assetName);
    }
}
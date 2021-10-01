#if ENABLE_IOS_ON_DEMAND_RESOURCES && USE_BUNDLES

using UnityEngine;
using UnityEngine.iOS;

namespace AssetDelivery
{

    // Read asset bundle asynchronously from iOS / tvOS asset catalog that is downloaded
    // using on demand resources functionality.
    public class AssetBundleDownloadFromODROperation : AssetBundleDownloadOperation
    {
        private OnDemandResourcesRequest request;

        public AssetBundleDownloadFromODROperation(string assetBundleName)
            : base(assetBundleName)
        {
            // Work around Xcode crash when opening Resources tab when a 
            // resource name contains slash character
            request = OnDemandResources.PreloadAsync(new[] { assetBundleName.Replace('/', '_') });
        }

        protected override bool downloadIsDone { get { return request == null || request.isDone; } }

        public override string GetSourceURL()
        {
            return "odr://" + AssetBundleName;
        }

        protected override void FinishDownload()
        {
            error = request.error;
            if (error != null)
                return;

            string path = "res://" + AssetBundleName;

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                error = $"Failed to load {path}";
                request.Dispose();
            }
            else
            {
                assetBundle = new LoadedAssetBundle(bundle);
                // At the time of unload request is already set to null, so capture it to local variable.
                OnDemandResourcesRequest localRequest = request;
                // Dispose of request only when bundle is unloaded to keep the ODR pin alive.
                assetBundle.unload += () => localRequest.Dispose();
            }

            request = null;
        }
    }
}
#endif
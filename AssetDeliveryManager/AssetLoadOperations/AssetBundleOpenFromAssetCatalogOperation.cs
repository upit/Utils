#if ENABLE_IOS_APP_SLICING && USE_BUNDLES

using UnityEngine;

namespace AssetDelivery
{
    // Read asset bundle synchronously from an iOS / tvOS asset catalog
    public class AssetBundleOpenFromAssetCatalogOperation : AssetBundleDownloadOperation
    {
        public AssetBundleOpenFromAssetCatalogOperation(string assetBundleName)
            : base(assetBundleName)
        {
            string path = "res://" + assetBundleName;
            AssetBundle bundle = AssetBundle.LoadFromFile(path);

            if (bundle == null)
                error = $"Failed to load {path}";
            else
                assetBundle = new LoadedAssetBundle(bundle);
        }

        protected override bool downloadIsDone { get { return true; } }

        protected override void FinishDownload() {}

        public override string GetSourceURL()
        {
            return "res://" + AssetBundleName;
        }
    }

}
#endif
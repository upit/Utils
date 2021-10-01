#if UNITY_5_4_OR_NEWER && USE_BUNDLES

using UnityEngine;

namespace AssetDelivery
{

    public class AssetBundleDownloadFileOperation : AssetBundleDownloadOperation
    {
        AssetBundleCreateRequest m_Operation;
        string m_Url;

        public AssetBundleDownloadFileOperation(string assetBundleName, string url, uint crc = 0, ulong offset = 0)
            : base(assetBundleName)
        {
            m_Operation = AssetBundle.LoadFromFileAsync(url, crc, offset);
            m_Url = url;
        }

        protected override bool downloadIsDone { get { return (m_Operation == null) || m_Operation.isDone; } }

        protected override void FinishDownload()
        {
            AssetBundle bundle = m_Operation.assetBundle;
            if (bundle == null) {
                error = $"failed to load assetBundle {AssetBundleName}.";
                return;
            }

            if (bundle == null)
                error = $"{AssetBundleName} is not a valid asset bundle.";
            else
                assetBundle = new LoadedAssetBundle(bundle);
            m_Operation = null;
        }

        public override string GetSourceURL()
        {
            return m_Url;
        }
    }
}
#endif
#if UNITY_5_4_OR_NEWER && USE_BUNDLES
using UnityEngine;
using UnityEngine.Networking;

namespace AssetDelivery
{
    public class AssetBundleDownloadWebRequestOperation : AssetBundleDownloadOperation
    {
        UnityWebRequest m_request;
        AsyncOperation m_Operation;
        string m_Url;

        public AssetBundleDownloadWebRequestOperation(string assetBundleName, UnityWebRequest request)
            : base(assetBundleName)
        {
            if (request == null || !(request.downloadHandler is DownloadHandlerAssetBundle))
                throw new System.ArgumentNullException("request");
            m_Url = request.url;
            m_request = request;
            m_Operation = request.Send();
        }

        protected override bool downloadIsDone { get { return (m_Operation == null) || m_Operation.isDone; } }

        protected override void FinishDownload()
        {
            error = m_request.error;
            if (!string.IsNullOrEmpty(error))
                return;

            var handler = m_request.downloadHandler as DownloadHandlerAssetBundle;
            AssetBundle bundle = handler.assetBundle;
            if (bundle == null)
                error = string.Format("{0} is not a valid asset bundle.", AssetBundleName);
            else
                assetBundle = new LoadedAssetBundle(bundle);

            m_request.Dispose();
            m_request = null;
            m_Operation = null;
        }

        public override string GetSourceURL()
        {
            return m_Url;
        }
    }
}
#endif
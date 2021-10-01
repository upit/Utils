#if !UNITY_EDITOR && UNITY_ANDROID && !NO_GPGS && USE_BUNDLES

using AssetDelivery;
using Google.Play.AssetDelivery;
using UnityEngine;

/// <summary> Загрузка с (Google) Play Asset Delivery. </summary>
public class AssetBundleDownloadFromPADOperation : AssetBundleDownloadOperation
{
    private PlayAssetBundleRequest request;
    private bool waitingForWifi;
    
    public AssetBundleDownloadFromPADOperation(string assetBundleName) : base(assetBundleName)
    {
        request = PlayAssetDelivery.RetrieveAssetBundleAsync(assetBundleName.Replace("/", "_"));
    }

    public override bool Update()
    {
        if (!waitingForWifi && request.Status == AssetDeliveryStatus.WaitingForWifi)
        {
            PlayAssetDelivery.ShowCellularDataConfirmation();
            waitingForWifi = true;
            return false;
        }
        
        return base.Update();
    }

    protected override bool downloadIsDone { get { return request != null && request.IsDone; } }
    
    protected override void FinishDownload()
    {
        AssetDeliveryErrorCode errorCode = request.Error;
        if (errorCode != AssetDeliveryErrorCode.NoError)
        {
            error = errorCode.ToString();
            return;
        }
        
        AssetBundle bundle = request.AssetBundle;
        if (bundle == null)
            error = $"{AssetBundleName} is not a valid asset bundle.";
        else
            assetBundle = new LoadedAssetBundle(bundle);

        request = null;
        waitingForWifi = false;
        // operation = null;
    }

    public override string GetSourceURL()
    {
        return "Play Asset Delivery URL";
    }
}

#endif
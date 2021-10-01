using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID && !NO_GPGS
using Google.Play.AssetDelivery;
#endif

namespace AssetDelivery
{
    public abstract class DownloadTaskAbstract
    {
        public string Uri { get { return m_uri; } }
        public bool IsBundle { get { return m_isBundle; } }
        public long Size { get { return m_sizeToDownload; } }
        public virtual long Downloaded { get { return m_request == null ? 0L : (long)m_request.downloadedBytes; } }
        public UnityWebRequest Request { get { return m_request; } }
        public bool IsDone
        {
            get { return m_request == null || m_request.isDone; }
        }
        public int Tries { get { return m_tries; } }
        public float WaitForSeconds { get { return m_waitForSeconds; } set { m_waitForSeconds = value; } }

        protected string m_uri;
        protected bool m_isBundle = false;
        protected long m_sizeToDownload;
        protected int m_tries = 0;
        protected float m_waitForSeconds = 0f;

        protected UnityWebRequest m_request = null;
        protected AsyncOperation m_operation = null;

        public DownloadTaskAbstract(string uri, long size, bool isBundle = false)
        {
            m_uri = uri;
            m_sizeToDownload = size;
            m_isBundle = isBundle;
        }

        public virtual bool Start()
        {
            End();
            m_request = CreateRequest();
            if (m_request == null)
            {
                return false;
            }
            m_operation = m_request.SendWebRequest();
            m_tries++;
            return true;
        }

        public virtual void End()
        {
            if (m_request != null)
            {
                m_request.Dispose();
                m_request = null;
            }
        }

        public virtual bool Update()
        {
            if (m_operation == null)
            {
                return false;
            }

            if (m_operation.isDone)
            {
                m_operation = null;
                return false;
            }
            return true;
        }

        protected abstract UnityWebRequest CreateRequest();
    }

    public class DownloadFileTask : DownloadTaskAbstract
    {
        public string FileName { get { return m_fileName; } }
        protected string m_fileName;

        public DownloadFileTask(string uri, string fileName, long size, bool isBundle = false) : base(uri, size, isBundle)
        {
            m_fileName = fileName;
        }

        public DownloadFileTask(Bundleinfo bundle, string fileName) : base(bundle.pathOrUri, bundle.size, true)
        {
            m_fileName = fileName + ".tmp";
        }

        protected override UnityWebRequest CreateRequest()
        {
            try
            {
                var wr = new UnityWebRequest(m_uri, "GET")
                {
                    downloadHandler = new DownloadHandlerFile(m_fileName)
                };
                return wr;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
        
        public override void End()
        {
            // После скачки переименовываем временный файл. При запуске все временные файлы вычищаются.
            if (m_isBundle && m_request != null)
                System.IO.File.Move(m_fileName, m_fileName.Replace(".tmp", ""));
            
            base.End();
        }
    }

    public class CacheBundleTask : DownloadTaskAbstract
    {
        public Bundleinfo Bundle { get { return m_bundleInfo; } }

        protected Bundleinfo m_bundleInfo;

        public CacheBundleTask(Bundleinfo bundle) : base(bundle.pathOrUri, bundle.size, true)
        {
            m_bundleInfo = bundle;
        }

        protected override UnityWebRequest CreateRequest()
        {
            try
            {
                return UnityWebRequestAssetBundle.GetAssetBundle($"{m_bundleInfo.pathOrUri}?={m_bundleInfo.hash}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
    }
    
#if ENABLE_IOS_ON_DEMAND_RESOURCES
    public class DownloadODRFileTask : DownloadTaskAbstract
    {
        private UnityEngine.iOS.OnDemandResourcesRequest requestODR;
        private readonly string bundleName;

        private static readonly Dictionary<string, UnityEngine.iOS.OnDemandResourcesRequest> requestsDic =
            new Dictionary<string, UnityEngine.iOS.OnDemandResourcesRequest>();

        public override long Downloaded { get { return requestODR == null ? 0L : (long) (requestODR.progress * m_sizeToDownload); } }

        public DownloadODRFileTask(Bundleinfo bundle) : base("odr://" + bundle.bundleName, bundle.size, true)
        {
            bundleName = bundle.bundleName.Replace('/', '_');
        }
        
        public override bool Start()
        {
            End();
            if (!requestsDic.TryGetValue(bundleName, out requestODR))
                requestODR = UnityEngine.iOS.OnDemandResources.PreloadAsync(new[] {bundleName});
            
            return requestODR != null;
        }

        // public override void End()
        // {
        //     // if (requestODR != null)
        //     // {
        //         
        //         // requestODR.Dispose();
        //         requestODR = null;
        //     // }
        // }
        
        public override bool Update()
        {
            if (requestODR.isDone)
            {
                //Сохраняем request, чтобы оставить в живых ссылку res:// 
                if (!requestsDic.ContainsKey(bundleName))
                    requestsDic.Add(bundleName, requestODR);
            
                requestODR = null;
                return false;
            }
            
            return true;
        }

        protected override UnityWebRequest CreateRequest()
        {
            return null;
        }
    }
#endif
    
#if UNITY_ANDROID && !NO_GPGS
    public class DownloadPADFileTask : DownloadTaskAbstract
    {
        private PlayAssetPackRequest requestPAD;
        private readonly string bundleName;
        private bool waitingForWifi;

        public override long Downloaded { get { return requestPAD == null ? 0L : (long)(requestPAD.DownloadProgress * m_sizeToDownload); } }

        public DownloadPADFileTask(Bundleinfo bundle) : base(bundle.bundleName, bundle.size, true)
        {
            bundleName = bundle.bundleName.Replace('/', '_');
        }
        
        public override bool Start()
        {
            End();
            requestPAD = PlayAssetDelivery.RetrieveAssetPackAsync(bundleName);
            return requestPAD != null;
        }

        public override void End()
        {
            if (requestPAD == null)
                return;
            
            AssetDeliveryErrorCode errorCode = requestPAD.Error;
            requestPAD = null;
            
            if (errorCode != AssetDeliveryErrorCode.NoError) 
                Debug.LogError(errorCode.ToString());
        }
        
        public override bool Update()
        {
            if (!waitingForWifi && requestPAD.Status == AssetDeliveryStatus.WaitingForWifi)
            {
                PlayAssetDelivery.ShowCellularDataConfirmation();
                waitingForWifi = true;
                return true;
            }

            bool isDone = requestPAD.IsDone;
            if (isDone)
                waitingForWifi = false;
            
            return !isDone;
        }

        protected override UnityWebRequest CreateRequest()
        {
            return null;
        }
    }
#endif
}

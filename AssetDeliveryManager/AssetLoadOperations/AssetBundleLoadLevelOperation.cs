#if USE_BUNDLES
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace AssetDelivery
{
    public class AssetBundleLoadLevelOperation : AssetLoadOperation
    {
        protected string                m_AssetBundleName;
        protected string                m_LevelName;
        protected bool                  m_IsAdditive;
        protected string                m_DownloadingError;
        protected AsyncOperation        m_Request;

        Action<DiContainer> m_extraBindings;

        protected ZenjectSceneLoader m_sceneLoader;

        public AssetBundleLoadLevelOperation(
            string assetbundleName,
            string levelName,
            bool isAdditive,
            Action<DiContainer> extraBindings,
            [Inject] ZenjectSceneLoader sceneLoader)
        {
            m_AssetBundleName = assetbundleName;
            m_LevelName = levelName;
            m_IsAdditive = isAdditive;
            m_extraBindings = extraBindings;
            m_sceneLoader = sceneLoader;
        }

        public override bool Update()
        {
            if (m_Request != null)
                return false;

            LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle(m_AssetBundleName, out m_DownloadingError);

            if (bundle != null)
            {
                m_Request = m_sceneLoader.LoadSceneAsync(m_LevelName, m_IsAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single, m_extraBindings);

                return false;
            }
            
            return true;
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_Request == null && m_DownloadingError != null)
            {
                Debug.LogError(m_DownloadingError);
                return true;
            }

            return m_Request != null && m_Request.isDone;
        }

        public class Factory : PlaceholderFactory<string, string, bool, Action<DiContainer>, AssetBundleLoadLevelOperation> { }
    }
}
#endif
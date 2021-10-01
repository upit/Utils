using System;
using System.Collections;
using System.Threading.Tasks;
using Zenject;

namespace AssetDelivery
{
    public interface IAssetManager
    {
        IEnumerator Initialize();
        
        IEnumerator LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, Action<DiContainer> extraBindings = null);

        Task<T> LoadAssetAsync<T>(string assetBundleName, string assetName) where T : UnityEngine.Object;

        Task<T[]> LoadAllAssetsAsync<T>(string assetBundleName) where T : UnityEngine.Object;
    }
}
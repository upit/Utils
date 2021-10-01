using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Zenject;

namespace AssetDelivery
{
    public class AddressableAssetsManager : MonoBehaviour, IAssetManager
    {
        private static Dictionary<string, IResourceLocation> resourceLocations;
        
        public IEnumerator Initialize()
        {
            resourceLocations = new Dictionary<string, IResourceLocation>();
            yield break;
        }

        public IEnumerator LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, Action<DiContainer> extraBindings = null)
        {
            AsyncOperationHandle<SceneInstance> opHandle = Addressables.LoadSceneAsync(levelName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single, false);
            yield return opHandle;
            
            // TODO@Upit подумать как сделать красиво.
            SceneContext.ExtraBindingsInstallMethod = extraBindings;
            
            yield return opHandle.Result.ActivateAsync();
        }

        public async Task<T> LoadAssetAsync<T>(string assetBundleName, string assetName) where T : UnityEngine.Object
        {
            IResourceLocation location = await GetAssetLocation<T>(assetBundleName, assetName);
            AsyncOperationHandle<T> loadOpHandle = Addressables.LoadAssetAsync<T>(location);
            await loadOpHandle.Task;
            if (loadOpHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Error while loading asset " + assetName);
                return null;
            }
            
            return loadOpHandle.Result;
        }

        public async Task<T[]> LoadAllAssetsAsync<T>(string assetBundleName) where T : UnityEngine.Object
        {
            AsyncOperationHandle<IList<T>> opHandle = Addressables.LoadAssetsAsync<T>(assetBundleName,null);
            await opHandle.Task;
            if (opHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Error while loading assets " + assetBundleName);
                return null;
            }
            
            return opHandle.Result.ToArray();
            //
            // Addressables.LoadAssetsAsync()
            // Task<T> loadAssetOp= LoadAssetAsync<T>(null, assetBundleName);
            // return null;
            // throw new NotImplementedException();
        }

        private static bool getLocationInProgress;
        private static async Task<IResourceLocation> GetAssetLocation<T>(string bundleName, string assetName)
        {
            if (getLocationInProgress)
                await new WaitUntil(() => getLocationInProgress == false);
            
            // Пробуем получить из закешированных.
            if (resourceLocations.TryGetValue(assetName, out IResourceLocation result))
                return result;

            // Ищем по bundleName.
            AsyncOperationHandle<IList<IResourceLocation>> locationOpHandle = Addressables.LoadResourceLocationsAsync(bundleName, typeof(T));
            getLocationInProgress = true;
            await locationOpHandle.Task;

            IList<IResourceLocation> locations = locationOpHandle.Result;
            if (locations.Count == 0)   // Если не нашли, пробуем по адресу assetName.
            {
                Addressables.Release(locationOpHandle);
                locationOpHandle = Addressables.LoadResourceLocationsAsync(assetName, typeof(T));
                await locationOpHandle.Task;
                locations = locationOpHandle.Result;
            }

            getLocationInProgress = false;
            foreach (IResourceLocation location in locations)
            {
                if (location.InternalId.Contains(assetName))
                {
                    resourceLocations.Add(assetName, location);
                    return location;
                }
            }

            Debug.LogError($"Error: {bundleName}: {assetName} addressable not found!");
            return null;
        }
    }
}
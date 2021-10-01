#if UNITY_EDITOR && USE_BUNDLES
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace AssetDelivery
{
    public class AssetBundleLoadLevelSimulationOperation : AssetLoadOperation
    {
        private readonly AsyncOperation mOperation;

        public AssetBundleLoadLevelSimulationOperation(
            string assetBundleName, 
            string levelName, 
            bool isAdditive, 
            Action<DiContainer> extraBindings)
        {
            string[] levelPaths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, levelName);

            if (levelPaths.Length == 0)
            {
                //TODO: The error needs to differentiate that an asset bundle name doesn't exist from that there right scene does not exist in the asset bundle...
                Debug.LogError("There is no scene with name \"" + levelName + "\" in " + assetBundleName);
                return;
            }

            SceneContext.ExtraBindingsInstallMethod = extraBindings;
            LoadSceneParameters parameters = new LoadSceneParameters
            {
                loadSceneMode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single
            };
            mOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(levelPaths[0],parameters);
        }
        
        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return mOperation == null || mOperation.isDone;
        }

        public class Factory : PlaceholderFactory<string, string, bool, Action<DiContainer>, AssetBundleLoadLevelSimulationOperation> { }
    }
}
#endif
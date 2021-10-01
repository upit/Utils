#if USE_BUNDLES
using System.Collections.Generic;
using System.Linq;

namespace AssetDelivery
{
    public class AssetBundleLoadAssetOperationSimulation : AssetBundleLoadAssetOperation
    {
        UnityEngine.Object m_SimulatedObject;

        public AssetBundleLoadAssetOperationSimulation(UnityEngine.Object simulatedObject)
        {
            m_SimulatedObject = simulatedObject;
        }

        public override T GetAsset<T>()
        {
            return m_SimulatedObject as T;
        }

        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return true;
        }
    }

    public class AssetBundleLoadAllAssetsOperationSimulation : AssetBundleLoadAllAssetsOperation
    {
        List<UnityEngine.Object> m_SimulatedObjects;

        public AssetBundleLoadAllAssetsOperationSimulation(List<UnityEngine.Object> simulatedObjects)
        {
            m_SimulatedObjects = simulatedObjects;
        }

        public override T[] GetAssets<T>()
        {
            return m_SimulatedObjects.Select(x => x as T).ToArray();
        }

        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return true;
        }
    }
}
#endif
#if USE_BUNDLES
using System.Collections;

namespace AssetDelivery
{
    public abstract class AssetLoadOperation : IEnumerator
    {
        public object Current
        {
            get
            {
                return null;
            }
        }

        public bool MoveNext()
        {
            return !IsDone();
        }

        public void Reset()
        {
        }

        public abstract bool Update();

        public abstract bool IsDone();
    }
    
    public abstract class AssetBundleLoadAssetOperation : AssetLoadOperation
    {
        public abstract T GetAsset<T>() where T: UnityEngine.Object;
    }

    public abstract class AssetBundleLoadAllAssetsOperation : AssetLoadOperation
    {
        public abstract T[] GetAssets<T>() where T : UnityEngine.Object;
    }
}
#endif
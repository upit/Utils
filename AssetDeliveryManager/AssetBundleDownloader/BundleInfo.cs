using System.IO;
using UnityEngine;

namespace AssetDelivery
{
    public class Bundleinfo
    {
        public string bundleName;
        public string pathOrUri;
        public Hash128 hash;
        public long size;

#if !(UNITY_WSA && !UNITY_WSA_10_0)
        public static Bundleinfo FromLocalPath(string bundleName, string fileName, Hash128 hash)
        {
            return new Bundleinfo
            {
                bundleName = bundleName,
                pathOrUri = fileName,
                hash = hash,
                size = new FileInfo(fileName).Length
            };
        }
#endif

        public static Bundleinfo FromRemotePath(string bundleName, string uri, Hash128 hash, long size)
        {
            return new Bundleinfo
            {
                bundleName = bundleName,
                pathOrUri = uri,
                hash = hash,
                size = size
            };
        }
    }
}

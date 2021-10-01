using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AssetDelivery
{
    [CreateAssetMenu(fileName = "AssetsDownloadingScenario", menuName = "ScriptableObjects/Assets/AssetsDownloadingScenario")]
    public class AssetsDownloadingScenario : ScriptableObject
    {
        [Header("Ассеты, которые грузятся всегда при первом запуске (локализация, тутор и т.д.)"), SerializeField, AssetBundleName]
        private string[] primary;

        [Header("Ассеты, которые грузятся в фоне, сразу после загрузки первичных"), SerializeField, AssetBundleName]
        private string[] secondary;
        
        [Header("Ассеты, которые грузятся в ангаре (карты и т.д.)"), SerializeField, AssetBundleName]
        private string[] extra;

        [Header("Исключать из загрузки ассеты, которые есть в Streaming Assets"), SerializeField]
        private bool excludeStreamingAssets;

        public string[] PrimaryAssets => excludeStreamingAssets ? ExcludeStreamingAssets(primary) : primary;
        public string[] SecondaryAssets => excludeStreamingAssets ? ExcludeStreamingAssets(secondary) : secondary;

        /// <summary> Сценарий имеет дополнительные ассеты, кроме первичных </summary>
        public bool HasAdditionalAssets { get => secondary?.Length > 0 && extra?.Length > 0; }

        /// <summary> Получение дополнительных ассетов </summary>
        /// <param name="sortByLevel">Сортировать ассеты по уровню доступности (карты)</param>
        /// <returns>Extra assets</returns>
        public string[] GetExtraAssets(bool sortByLevel = false)
        {
            string[] extraAssets = excludeStreamingAssets ? ExcludeStreamingAssets(extra) : extra;
            if (!sortByLevel)
                return extraAssets;
            
            // Сортируем карты по уровню/доступности, смотрим есть они в ассетах или нет и выдаем список в этом порядке.
            Dictionary<SceneLauncher.MapId, MapInfo> allMaps = GameData.allMapsDic;
            List<MapInfo> orderedMapInfos =
                allMaps.Values
                    .OrderByDescending(mapInfo => mapInfo.IsAvailableByLevel) // Последними будут недоступные по уровню карты.
                    .ThenByDescending(mapInfo => mapInfo.isEnabled) // А теперь последними будут выключенные.
                    .ToList();

            var extraList = new List<string>(extraAssets);
            var result = new List<string>();
            foreach (MapInfo mapInfo in orderedMapInfos)
            {
                string sceneName = mapInfo.id.ToString();
                for (int i = 0; i < extraList.Count; i++)
                {
                    if (extraList[i].Contains(sceneName))
                    {
                        result.Add(extraList[i]);
                        extraList.RemoveAt(i);
                        break;
                    }
                }
            }
            result.AddRange(extraList);
            return result.ToArray();
        }

        /// <summary> Исключает из сценария загрузки ассеты, которые уже есть в streaming assets. </summary>
        private static string[] ExcludeStreamingAssets(string[] assets)
        {
            // TODO возможно стоит сделать какую-то проверку по хешу, чтобы перекачивать обновленные ассеты.
            string streamingAssetsPath = $"{Application.streamingAssetsPath}/{Utility.PlatformName}/";
            
            bool changed = false;
            var assetsList = new List<string>(assets);
            
            for (int i = 0; i < assetsList.Count; i++)
            {
                if (File.Exists(streamingAssetsPath + assetsList[i]))
                {
                    assetsList.RemoveAt(i--);
                    changed = true;
                }
            }

            return changed ? assetsList.ToArray() : assets;
        }

        #region Custom Editor
        
#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(AssetsDownloadingScenario))]
        private class AssetsDownloadingScenarioEditor: UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                if (GUILayout.Button("Добавить все"))
                    AddAllBundleNames();
            }

            private void AddAllBundleNames()
            {
                AssetsDownloadingScenario scenario = (AssetsDownloadingScenario) target;
                
                UnityEditor.Undo.RegisterCompleteObjectUndo(scenario, "Add all bundle names");
                scenario.primary = scenario.secondary = scenario.extra = UnityEditor.AssetDatabase.GetAllAssetBundleNames();
            }
        }
#endif
        
        #endregion
    }
}
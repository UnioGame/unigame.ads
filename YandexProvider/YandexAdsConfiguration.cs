using Game.Runtime.Game.Liveplay.Ads.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace VN.Runtime.Ads
{
    [CreateAssetMenu(fileName = "Yandex Ads Config", menuName = "Game/Configuration/Ads/YandexAdsConfig")]
    public class YandexAdsConfiguration : ScriptableObject
    {
        public bool EnableAds;
        public float ReloadAdsInterval = 30f;
        
        [BoxGroup("placements")]
        [InlineEditor]
        [HideLabel]
        public PlacementIdDataAsset placementIds;
        
        public string GetRewardedPlacement()
        {
            foreach(AdsPlacementItem item in placementIds.Types)
            {
                if (item.Type == PlacementType.Rewarded)
                    return item.Name;
            }
            return "demo-rewarded-yandex";
        }
    }
}

using Game.Runtime.Game.Liveplay.Ads.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace VN.Runtime.Ads
{
    using global::Game.Modules.unigame.levelplay.Shared;

    [CreateAssetMenu(fileName = "Yandex Ads Config", menuName = "Ads/Yandex/Yandex Ads Config")]
    public class YandexAdsConfiguration : AdsConfig
    {
        public AdsPlacementItem GetRewardedPlacement()
        {
            foreach(AdsPlacementItem item in placementIds.Placements)
            {
                if (item.Type == PlacementType.Rewarded)
                    return item;
            }
            return default;
        }
    }
}

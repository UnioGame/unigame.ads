namespace VN.Game.Modules.unigame.levelplay.AdsCommonProvider
{
    using System.Collections.Generic;
    using UnityEngine;
    using VN.Runtime.Ads;

    [CreateAssetMenu(fileName = "Composite Ads Configuration", menuName = "Ads/Composite Configuration")]
    public class CompositeAdsConfiguration : ScriptableObject
    {
        [Header("providers")]
        public List<AdsProviderItem> adsProviders = new();
    }
}
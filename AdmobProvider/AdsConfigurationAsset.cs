namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using Sirenix.OdinInspector;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game/Configurations/Admob Ads Configuration", fileName = "AdmobAdsConfiguration")]
    public class AdsConfigurationAsset : ScriptableObject
    {
        public bool useDebugAds = false;

        [InlineProperty]
        [HideLabel]
        public AdmobAdsConfig adsConfiguration = new();
    }
}
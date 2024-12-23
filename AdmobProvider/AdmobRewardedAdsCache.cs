namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using GoogleMobileAds.Api;
    [Serializable]
    public class AdmobRewardedAdsCache
    {
        public RewardedAd RewardedAd;
        public bool Available;
        public bool LoadProcess;
        public string Name;

        public AdmobRewardedAdsCache(string name)
        {
            Name = name;
            Available = false;
            LoadProcess = false;
            RewardedAd = null;
        }
    }
}

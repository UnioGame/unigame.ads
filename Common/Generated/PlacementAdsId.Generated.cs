namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    public partial struct PlacementAdsId
    {
        public static PlacementAdsId demo_rewarded => new PlacementAdsId { value = 0 };
        public static PlacementAdsId demo_interstitial => new PlacementAdsId { value = 1 };
    }
}

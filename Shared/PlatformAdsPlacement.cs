namespace Game.Modules.unigame.ads.Shared
{
    using System;
    using UniGame.Ads.Runtime;

    [Serializable]
    public class PlatformAdsPlacement
    {
        public string id;
        public string platform;
        public string platformPlacement;
        public PlacementType placementType;
    }
}
namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using Sirenix.OdinInspector;
    [Serializable]
    public class AdmobAdsConfig
    {
        public bool enableAds = true;
		
        public float reloadAdsInterval = 30f;

        [BoxGroup("placements")]
        [InlineEditor]
        [HideLabel]
        public PlacementIdDataAsset placementIds;
    }
}
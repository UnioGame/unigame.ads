namespace Game.Modules.unigame.levelplay.Shared
{
    using Runtime.Game.Liveplay.Ads.Runtime;
    using Sirenix.OdinInspector;
    using UnityEngine;

    public class AdsConfig : ScriptableObject
    {
        public bool EnableAds = true;
        public float ReloadAdsInterval = 30f;
        private PlacementPlatfrom placementPlatfrom;
        public PlacementPlatfrom PlacementPlatfrom
        {
            get => placementPlatfrom;
            set => placementPlatfrom = value;
        }
        [BoxGroup("placements")]
        [HideLabel]
        public PlacementIdDataAsset placementIds = new();
    }
}
namespace UniGame.Ads.Runtime
{
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu( menuName = "UniGame/Ads/Ads Platform Config",fileName = "AdsPlatformConfiguration")]
    public class AdsDataConfigurationAsset : ScriptableObject
    {
        public AdsDataConfiguration configuration = new();
    }

    [Serializable]
    public class AdsDataConfiguration
    {
        public float reloadAdsInterval = 30f;

        public List<string> platforms = new();
        
#if ODIN_INSPECTOR
        [BoxGroup("placements")]
        [HideLabel]
#endif
        public PlacementData placementData = new();
    }
}
namespace UniGame.Ads.Runtime
{
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class AdsDataConfiguration
    {
        public float reloadAdsInterval = 30f;

#if ODIN_INSPECTOR
        [BoxGroup("placements")]
        [HideLabel]
        [ListDrawerSettings(ListElementLabelName = "@id")]
#endif
        public List<AdsPlacement> placements = new();

        public AdsPlacement GetPlatformPlacementByName(string name)
        {
            foreach (var item in placements)
            {
                if (item.id == name)
                {
                    return item;
                }
            }

            return default;
        }
    }
}
using System;
using System.Collections.Generic;

namespace UniGame.Ads.Runtime
{
    using Sirenix.OdinInspector;

    [Serializable]
    public class AdsPlacement
    {
        public string id;
        public string description;
        public PlacementType placementType;
        
        [ListDrawerSettings(ListElementLabelName = "@placement")]
        public List<PlatformPlacementData> placements =new();
        
        public string GetPlacementIdByPlatform(string platform)
        {
            foreach (var item in placements)
            {
                if (item.platform == platform)
                    return item.placement;
            }

            return default;
        }
    }

    [Serializable]
    public class PlatformPlacementData
    {
        public string placement;
        public AdsPlatformId platform;
    }
}
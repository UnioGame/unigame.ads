using System;
using System.Collections.Generic;

namespace UniGame.Ads.Runtime
{
    [Serializable]
    public class AdsPlacementItem
    {
        public int id;
        public string name = string.Empty;
        public string description;
        
        public List<AdsPlatformPlacement> placements =new();
        
        public PlacementType type;
        
        public string GetPlacementIdByPlatform(string platform)
        {
            foreach (var item in placements)
            {
                if (item.platform == platform)
                    return item.placementId;
            }

            return default;
        }
    }

    [Serializable]
    public class AdsPlatformPlacement
    {
        public string placementId;
        public AdsPlatformId platform;
    }
}
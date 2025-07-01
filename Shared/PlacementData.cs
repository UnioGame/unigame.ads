namespace UniGame.Ads.Runtime
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class PlacementData
    {
        public List<AdsPlacementItem> placements = new();

        public AdsPlacementItem GetPlatformPlacementByName(string name)
        {
            foreach (var item in placements)
            {
                if (item.name == name)
                {
                    return item;
                }
            }

            return default;
        }
    }
}
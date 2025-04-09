using System;
using System.Collections.Generic;

namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System.Linq;
    using Modules.unigame.levelplay.Shared;

    [Serializable]
    public struct AdsPlacementItem
    {
        public int Id;
        public string Name;
        public List<AdsPlacementPlatformItem> Placements;
        public PlacementType Type;
        public string GetPlacementIdByPlatform(PlacementPlatfrom platform)
        {
            foreach (var item in Placements)
            {
                if (item.Platfrom == platform)
                    return item.PlacementId;
            }

            return default;
        }
    }

    [Serializable]
    public class AdsPlacementPlatformItem
    {
        public PlacementPlatfrom Platfrom;
        public string PlacementId;

    }
}
namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sirenix.OdinInspector;
    using UnityEngine;
    using UnityEngine.Serialization;

    [Serializable]
    public class PlacementIdDataAsset
    {
        public List<AdsPlacementItem> Placements = new List<AdsPlacementItem>();

        public AdsPlacementItem GetPlatformPlacementByName(string name)
        {
            foreach (var item in Placements)
            {
                if (item.Name == name)
                {
                    return item;
                }
            }

            return default;
        }
    }
}
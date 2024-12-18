namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System.Collections.Generic;
    using Sirenix.OdinInspector;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game/AdsPlacementId Data Asset", fileName = "AdsPlacementId  Data Asset")]
    public class PlacementIdDataAsset : ScriptableObject
    {
        [InlineProperty]
        public List<AdsPlacementItem> Types = new List<AdsPlacementItem>();
    }
}
namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using UnityEngine;
    using Sirenix.OdinInspector;
    using System.Collections.Generic;
    [CreateAssetMenu(menuName = "Game/AdmobAdsPlacementId Data Asset", fileName = "AdmobAdsPlacementId  Data Asset")]
    public class AdmobPlacementIdDataAsset : ScriptableObject
    {
        [InlineProperty]
        public List<AdmobPlacementItem> Types = new List<AdmobPlacementItem>();
    }
}


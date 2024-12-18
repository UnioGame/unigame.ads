namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System.Collections.Generic;
    using Sirenix.OdinInspector;
    using UnityEngine;
    
    [CreateAssetMenu(menuName = "Game/AdmobAdsPlacementId Data Asset", fileName = "AdmobAdsPlacementId  Data Asset")]
    public class AdmobPlacementIdDataAsset : ScriptableObject
    {
        [InlineProperty]
        public List<AdmobPlacementItem> Types = new();
    }
}


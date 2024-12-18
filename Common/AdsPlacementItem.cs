using System;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    [Serializable]
    public struct AdsPlacementItem
    {
        public int Id;
        public string Name;
        public PlacementType Type;
    }
}
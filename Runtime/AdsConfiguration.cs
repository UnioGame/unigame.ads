namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using System.Collections.Generic;
    using GameLib.AdsCore.Config;
    using Sirenix.OdinInspector;

    [Serializable]
    public class AdsConfiguration
    {
        [InlineProperty]
        public List<AdsPlacementItem> types = new();

        [FoldoutGroup(nameof(GetProviders))]
        public string adsProvider = string.Empty;
		
        public AdsProvider[] providers = Array.Empty<AdsProvider>();
        
        public IEnumerable<string> GetProviders()
        {
            foreach (var provider in providers)
            {
                yield return provider.providerName;
            }
        }
    }
}
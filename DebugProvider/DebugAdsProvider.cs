namespace UniGame.Ads.Runtime
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using Sirenix.OdinInspector;
    using UniGame.Core.Runtime;

    [Serializable]
    public class DebugAdsProvider : AdsProvider
    {
        [InlineProperty]
        public DebugAdsConfiguration debugConfiguration = new();
        
        public override async UniTask<IAdsService> Create(IContext context,
            AdsConfiguration configuration)
        {
            var service = new DebugAdsService(debugConfiguration);
            return service;
        }
        
    }

    [Serializable]
    public class DebugAdsConfiguration
    {
        public bool rewardedAvailable = true;
        public bool interstitialAvailable = true;

        public List<string> unavailablePlacements = new();
    }
}
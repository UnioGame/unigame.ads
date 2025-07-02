namespace UniGame.Ads.Runtime
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using UniGame.Core.Runtime;

    [Serializable]
    public class LevelPlayAdsProvider : AdsProvider
    {
        public LevelPlayAdsConfig levelPlayAdsConfig = new();

        public override UniTask<IAdsService> Create(IContext context, AdsConfiguration configuration)
        {
            var placements = configuration
                .GetPlatformPlacements(adsPlatformName);
            var adsService = new LevelPlayAdsService(adsPlatformName,
                levelPlayAdsConfig,configuration,placements);
            return UniTask.FromResult<IAdsService>(adsService);
        }
    }
}
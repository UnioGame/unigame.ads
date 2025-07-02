namespace UniGame.Ads.Runtime
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using Sirenix.OdinInspector;
    using UniGame.Core.Runtime;

    [Serializable]
    public class AdMobAdsProvider : AdsProvider
    {
        public override async UniTask<IAdsService> Create(IContext context, AdsConfiguration configuration)
        {
            var platformPlacements = configuration
                .GetPlatformPlacements(adsPlatformName);
            var service = new AdmobAdsService(adsPlatformName,configuration.adsData,platformPlacements);
            await service.InitializeAsync();
            return service;
        }
    }
}
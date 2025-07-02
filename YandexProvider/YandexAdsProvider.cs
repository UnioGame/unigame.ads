namespace UniGame.Ads.Runtime
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using UniGame.Core.Runtime;

    [Serializable]
    public class YandexAdsProvider : AdsProvider
    {
        public override UniTask<IAdsService> Create(IContext context,
            AdsConfiguration configuration)
        {
            var platformPlacements = configuration
                .GetPlatformPlacements(adsPlatformName);
            
            var adsService = new YandexAdsService(adsPlatformName, configuration.adsData,platformPlacements);
            return UniTask.FromResult<IAdsService>(adsService);
        }
        
       
    }
}
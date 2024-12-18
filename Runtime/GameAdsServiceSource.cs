namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using System.Linq;
    using Cysharp.Threading.Tasks;
    using UniGame.Core.Runtime;
    using UniGame.GameFlow.Runtime.Services;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game/Services/Ads/Ads Source", fileName = "Ads Source")]
    public class GameAdsServiceSource   : DataSourceAsset<IAdsService>
    {
        public AdsConfiguration adsConfiguration = new AdsConfiguration();
        
        protected override async UniTask<IAdsService> CreateInternalAsync(IContext context)
        {
            var serviceSource = adsConfiguration.providers
                .FirstOrDefault(x =>
                    adsConfiguration.adsProvider.Equals(x.providerName, StringComparison.OrdinalIgnoreCase));
            
            if(serviceSource == null)
                throw new Exception($"Ads provider not found: {adsConfiguration.adsProvider}");
            
            return await serviceSource.Create(context);
        }
    }
}
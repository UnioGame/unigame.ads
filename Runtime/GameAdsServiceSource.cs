namespace UniGame.Ads.Runtime
{
    using Context.Runtime;
    using Core.Runtime;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using UnityEngine;

    [CreateAssetMenu(menuName = "UniGame/Ads/Ads Service Source", fileName = "Ads Service Source")]
    public class GameAdsServiceSource : DataSourceAsset<IAdsService>
    {
        public AdsConfigurationAsset adsConfiguration;

        protected override async UniTask<IAdsService> CreateInternalAsync(IContext context)
        {
            var configAsset = Instantiate(adsConfiguration);
            var config = configAsset.configuration;
            var awaitForInit = config.waitForInitialization;
            
            context.Publish(config);
            context.Publish(config.adsData);
            
            var providers = config.providers;
            var service = new AdsService(config);
            service.AddTo(context.LifeTime);

            
            if (!awaitForInit)
            {
                foreach (var adsProvider in providers)
                {
                    RegisterProviderAsync(adsProvider,service,config, context).Forget();
                }
            }
            else
            {
                var awaitTasks = providers
                    .Select(x => RegisterProviderAsync(x,service,config,context));

                await UniTask.WhenAll(awaitTasks);
            }
            
            return service;
        }

        private async UniTask RegisterProviderAsync(AdsProvider provider,
            AdsService service,
            AdsConfiguration configuration,
            IContext context)
        {
            if (provider.enabled == false) return;
            var adsProvider = await provider.Create(context,configuration);
            service.RegisterAdsProvider(provider.adsPlatformName,adsProvider);
        }
    }
}
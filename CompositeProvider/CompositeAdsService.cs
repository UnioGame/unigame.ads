namespace VN.Game.Modules.unigame.levelplay.AdsCommonProvider
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cysharp.Threading.Tasks;
    using global::Game.Modules.unigame.levelplay.Shared;
    using global::Game.Runtime.Game.Liveplay.Ads.Runtime;
    using UniGame.Core.Runtime;
    using UniModules.UniCore.Runtime.DataFlow;

    public class CompositeAdsService : IAdsService
    {
        private LifeTimeDefinition _lifeTime;
        public ILifeTime LifeTime => _lifeTime;
        public bool RewardedAvailable { get; }
        public bool InterstitialAvailable { get; }
        public IObservable<AdsActionData> AdsAction { get; }
        private List<IAdsService> adsServices;
        private IAdsService serviceWithAvaiableAds = null;
        public CompositeAdsService(List<IAdsService> services)
        {
            adsServices = services;
            _lifeTime = new LifeTimeDefinition();
        }
        public void Dispose()
        {
            _lifeTime.Terminate();
        }

        public void ValidateIntegration()
        {
            throw new NotImplementedException();
        }

        public bool IsPlacementAvailable(string placementName)
        {
            foreach (var service in adsServices)
            {
                if (service.IsPlacementAvailable(placementName))
                {
                    serviceWithAvaiableAds = service;
                    return true;
                }
            }

            return false;
        }
        public UniTask LoadAdsAsync()
        {
            throw new NotImplementedException();
        }

        public UniTask<AdsShowResult> Show(PlacementAdsId placement)
        {
            throw new NotImplementedException();
        }

        public UniTask<AdsShowResult> Show(string placement, PlacementType type)
        {
            throw new NotImplementedException();
        }

        public UniTask<AdsShowResult> Show(PlacementType type)
        {
            throw new NotImplementedException();
        }

        public async UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId)
        {
            return await serviceWithAvaiableAds.ShowRewardedAdAsync(placeId);
        }

        public UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId)
        {
            throw new NotImplementedException();
        }
    }
}
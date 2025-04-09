namespace VN.Game.Modules.unigame.levelplay.AdsCommonProvider
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cysharp.Threading.Tasks;
    using global::Game.Modules.unigame.levelplay.Shared;
    using global::Game.Runtime.Game.Liveplay.Ads.Runtime;
    using UniGame.Core.Runtime;
    using UniModules.UniCore.Runtime.DataFlow;
    using UniRx;

    public class CompositeAdsService : IAdsService
    {
        private LifeTimeDefinition _lifeTime;
        private List<IAdsService> _adsServices;
        private IAdsService _serviceWithAvailableAds;
        private Subject<AdsActionData> _adsAction;
        
        public CompositeAdsService(List<IAdsService> services)
        {
            _adsServices = services;
            _lifeTime = new LifeTimeDefinition();
            _adsAction = new Subject<AdsActionData>()
                .AddTo(_lifeTime);
            
            foreach (var service in _adsServices)
                service.AdsAction.Subscribe(_adsAction).AddTo(_lifeTime);
        }
        
        public ILifeTime LifeTime => _lifeTime;

        public bool RewardedAvailable => _adsServices.Any(x => x.RewardedAvailable);
        
        public bool InterstitialAvailable  => _adsServices.Any(x => x.InterstitialAvailable);
        public IObservable<AdsActionData> AdsAction => _adsAction;
        
        public void Dispose() => _lifeTime.Terminate();

        public void ValidateIntegration()
        {
            foreach (var adsService in _adsServices)
            {
                adsService.ValidateIntegration();
            }
        }

        public bool IsPlacementAvailable(string placementName)
        {
            foreach (var service in _adsServices)
            {
                if (!service.IsPlacementAvailable(placementName)) continue;
                _serviceWithAvailableAds = service;
                return true;
            }

            return false;
        }
        
        public async UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId)
        {
            return await _serviceWithAvailableAds.ShowRewardedAdAsync(placeId);
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

        public UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId)
        {
            throw new NotImplementedException();
        }
    }
}
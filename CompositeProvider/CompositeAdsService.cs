namespace VN.Game.Modules.unigame.levelplay.AdsCommonProvider
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cysharp.Threading.Tasks;
    using global::Game.Runtime.Game.Liveplay.Ads.Runtime;
    using R3;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;

    public enum AdsStatus { Ready, Failed, Loading }
    public class CompositeAdsService : IAdsService
    {
        private LifeTimeDefinition _lifeTime;
        private List<IAdsService> _adsServices;
        private IAdsService _serviceWithAvailableAds;
        private Subject<AdsActionData> _adsAction;
        private Dictionary<IAdsService, AdsStatus> _adsAvailableStatus = new ();
        private float _timeoutAds = 1.5f;
        
        public CompositeAdsService(List<IAdsService> services)
        {
            _adsServices = services;
            _lifeTime = new LifeTimeDefinition();
            _adsAction = new Subject<AdsActionData>()
                .AddTo(_lifeTime);

            foreach (var service in _adsServices)
            {
                service.AdsAction
                    .Subscribe(_adsAction.OnNext).AddTo(_lifeTime);
            }
        }
        
        public ILifeTime LifeTime => _lifeTime;

        public bool RewardedAvailable => _adsServices.Any(x => x.RewardedAvailable);
        
        public bool InterstitialAvailable  => _adsServices.Any(x => x.InterstitialAvailable);
        public Observable<AdsActionData> AdsAction => _adsAction;
        
        public void Dispose() => _lifeTime.Terminate();

        public void ValidateIntegration()
        {
            foreach (var adsService in _adsServices)
            {
                adsService.ValidateIntegration();
            }
        }

        public async UniTask<bool> IsPlacementAvailable(string placementName)
        {
            foreach (var service in _adsServices)
            {
                _adsAvailableStatus.TryAdd(service, AdsStatus.Loading);
                RequestAds(placementName, service).Forget();
            }

            await UniTask.Delay(TimeSpan.FromSeconds(_timeoutAds));

            foreach (var service in _adsAvailableStatus)
            {
                if (service.Value == AdsStatus.Failed || service.Value == AdsStatus.Loading)
                    continue;
                
                _serviceWithAvailableAds = service.Key;
                return true;
            }

            return false;
        }

        private async UniTask RequestAds(string placementName, IAdsService service)
        {
            _adsAvailableStatus[service] = AdsStatus.Loading;
            
            if (!await service.IsPlacementAvailable(placementName))
            {
                _adsAvailableStatus[service] = AdsStatus.Failed;
                return;
            }

            _adsAvailableStatus[service] = AdsStatus.Ready;
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
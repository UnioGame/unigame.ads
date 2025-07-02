using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniGame.Ads.Runtime
{
    using System;
    using System.Linq;
    using R3;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;

    [Serializable]
    public class DebugAdsService : IAdsService
    {
        private readonly DebugAdsConfiguration _debugAdsConfiguration;
        public LifeTime lifeTime = new();

        public DebugAdsService(DebugAdsConfiguration debugAdsConfiguration)
        {
            _debugAdsConfiguration = debugAdsConfiguration;
        }
        
        public bool RewardedAvailable => _debugAdsConfiguration.rewardedAvailable;

        public bool InterstitialAvailable => _debugAdsConfiguration.interstitialAvailable;
        
        private Subject<AdsActionData> _adsAction = new();
        public Observable<AdsActionData> AdsAction => _adsAction;
        
        public ILifeTime LifeTime => lifeTime;

        public void Dispose()
        {
            lifeTime.Release();
        }

        public UniTask<bool> IsPlacementAvailable(string placementName)
        {
            var unavailablePlacement = _debugAdsConfiguration
                .unavailablePlacements
                .FirstOrDefault(x => x.Equals(placementName, StringComparison.InvariantCultureIgnoreCase));

            if (unavailablePlacement != null) 
                return UniTask.FromResult(false);
            
            return UniTask.FromResult(true);
        }

        public UniTask<bool> IsPlacementAvailable(PlacementType placementName)
        {
            return UniTask.FromResult(true);
        }

        public async UniTask LoadAdsAsync()
        {
            Debug.Log("Loaded debug ad");
            await UniTask.DelayFrame(1);
        }

        public async UniTask<AdsShowResult> Show(AdsPlacementId placement)
        {
            AdsShowResult result = new AdsShowResult()
            {
                Message = "Complete debug!",
                PlacementName = "none",
                PlacementType = PlacementType.Rewarded,
                Rewarded = true
            };

            return result;
        }

        public async UniTask<AdsShowResult> Show(string placement, PlacementType type)
        {
            AdsShowResult result = new AdsShowResult()
            {
                Message = "Complete debug!",
                PlacementName = placement,
                PlacementType = type,
                Rewarded = true
            };

            return result;
        }

        public async UniTask<AdsShowResult> Show(PlacementType type)
        {
            AdsShowResult result = new AdsShowResult()
            {
                Message = "Complete debug!",
                PlacementName = "none",
                PlacementType = type,
                Rewarded = true
            };

            return result;
        }

        public async UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId)
        {
            AdsShowResult result = new AdsShowResult()
            {
                Message = "Complete debug!",
                PlacementName = placeId,
                PlacementType = PlacementType.Rewarded,
                Rewarded = true
            };

            return result;
        }

        public async UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId)
        {
            var result = new AdsShowResult()
            {
                Message = "Complete debug!",
                PlacementName = placeId,
                PlacementType = PlacementType.Rewarded,
                Rewarded = true
            };
            
            if (!RewardedAvailable)
            {
                result = new AdsShowResult()
                {
                    Message = "Debug Ads Failed!",
                    PlacementName = placeId,
                    PlacementType = PlacementType.Rewarded,
                    Rewarded = false,
                    Error = true,
                    RewardAmount = 0,
                };
            }
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placeId,
                Message = "Reward open",
                ActionType = RewardedAvailable ?  PlacementActionType.Opened : PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded
            });
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placeId,
                Message = "Reward granted",
                ActionType = RewardedAvailable ?  PlacementActionType.Rewarded : PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded
            });
            
            return result;
        }

        public void ValidateIntegration()
        {
            Debug.Log("Debug: Validate");
        }
    }
}

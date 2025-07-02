using Cysharp.Threading.Tasks;
using UniGame.Ads.Runtime;
using UnityEngine;

namespace UniGame.Ads.Runtime
{
    using R3;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;

    public class DebugAdsService : IAdsService
    {
        public LifeTime lifeTime = new();
        public bool RewardedAvailable => true;

        public bool InterstitialAvailable => true;
        private Subject<AdsActionData> _adsAction = new();
        public Observable<AdsActionData> AdsAction => _adsAction;
        
        public ILifeTime LifeTime => lifeTime;

        public void Dispose()
        {
            lifeTime.Release();
        }

        public UniTask<bool> IsPlacementAvailable(string placementName)
        {
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
            AdsShowResult result = new AdsShowResult()
            {
                Message = "Complete debug!",
                PlacementName = placeId,
                PlacementType = PlacementType.Rewarded,
                Rewarded = true
            };
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placeId,
                Message = "Reward open",
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Rewarded
            });
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placeId,
                Message = "Reward granted",
                ActionType = PlacementActionType.Rewarded,
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

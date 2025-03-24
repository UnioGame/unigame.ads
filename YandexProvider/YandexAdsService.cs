using Cysharp.Threading.Tasks;
using Game.Runtime.Game.Liveplay.Ads.Runtime;
using System;
using System.Collections.Generic;
using UniModules.UniCore.Runtime.DataFlow;
using UniRx;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace VN.Runtime.Ads
{
    using UniGame.Core.Runtime;

    public class YandexAdsService : IAdsService
    {
        private YandexAdsConfiguration _adsConfig;
        private PlacementIdDataAsset _placementIds;
        private Dictionary<string, AdsPlacementItem> _placements = new();
        private Dictionary<PlacementAdsId, AdsPlacementItem> _idPlacements = new();
        private ReactiveCommand<AdsShowResult> _applyRewardedCommand = new();
        private LifeTimeDefinition _lifeTime;
        private List<AdsShowResult> _rewardedHistory = new();
        private Dictionary<string, AdsShowResult> _awaitedRewards = new();
        private float _reloadAdsInterval;
        private float _lastAdsReloadTime;
        private bool _loadingAds;
        private bool _isInProgress = new();
        private string _activePlacement = string.Empty;

        private Subject<AdsActionData> _adsAction = new();
        private RewardedAdLoader _rewardedAdLoader;
        private RewardedAd _rewardedAd;

        public ILifeTime LifeTime => _lifeTime;
        
        public bool RewardedAvailable => _rewardedAd != null;

        public bool InterstitialAvailable => throw new NotImplementedException();

        public IObservable<AdsActionData> AdsAction => _adsAction;
        public YandexAdsService(YandexAdsConfiguration yandexAdsConfiguration)
        {
            _lifeTime = new LifeTimeDefinition();
            _adsConfig = yandexAdsConfiguration;
            _placementIds = yandexAdsConfiguration.placementIds;
            _reloadAdsInterval = yandexAdsConfiguration.ReloadAdsInterval;
            _lastAdsReloadTime = -_reloadAdsInterval;

            foreach (var adsPlacementId in _placementIds.Types)
            {
                _placements[adsPlacementId.Name] = adsPlacementId;
                _idPlacements[(PlacementAdsId)adsPlacementId.Id] = adsPlacementId;
            }

            if (_adsConfig.EnableAds == false)
                return;

            Initialize();
        }
        private void Initialize()
        {
            SubscribeToEvents();
            _rewardedAdLoader = new RewardedAdLoader();
            _rewardedAdLoader.OnAdFailedToLoad += YandexRewardAdFailedToLoad;
            _rewardedAdLoader.OnAdLoaded += YandexRewardAdLoaded;

            _applyRewardedCommand
                .Subscribe(ApplyRewardedCommand)
                .AddTo(_lifeTime);

            LoadAdsAsync()
                .AttachExternalCancellation(_lifeTime.Token)
                .Forget();
        }
        private void AddPlacementResult(string placeId, PlacementType type, bool rewarded, bool error = false, string message = "")
        {
            var result = new AdsShowResult
            {
                Error = error,
                Message = message,
                PlacementType = type,
                PlacementName = placeId,
                Rewarded = rewarded,
            };

            _awaitedRewards[placeId] = result;
        }
        public bool IsPlacementAvailable(string placementName)
        {
            if (_placements.TryGetValue(placementName, out var adsPlacementId) == false)
            {
                Debug.Log($"Placement haven't {placementName}");
                return false;
            }
            var placementType = adsPlacementId.Type;
            switch (placementType)
            {
                case PlacementType.Rewarded:
                    {
                        Debug.Log($"Reward load: {_rewardedAd != null}");
                        return _rewardedAd != null;
                    }
                case PlacementType.Interstitial:
                    return InterstitialAvailable;
            }

            return false;
        }

        public async UniTask LoadAdsAsync()
        {
            if (_loadingAds || _lifeTime.IsTerminated) return;

            _loadingAds = true;

            var delay = Time.realtimeSinceStartup - _lastAdsReloadTime;
            delay = delay > _reloadAdsInterval ? 0 : _reloadAdsInterval - delay;
            delay = Mathf.Max(0, delay);

            await UniTask.Delay(TimeSpan.FromSeconds(delay))
                .AttachExternalCancellation(_lifeTime.Token);

            _lastAdsReloadTime = Time.realtimeSinceStartup;

            var adRequestConfiguration = new AdRequestConfiguration
                .Builder(_adsConfig.GetRewardedPlacement())
                .Build();
            
            _rewardedAdLoader.LoadAd(adRequestConfiguration);

            _loadingAds = false;
        }

        public async UniTask<AdsShowResult> Show(PlacementAdsId placement)
        {
            if (!_idPlacements.TryGetValue(placement, out var placementItem))
            {
                return new AdsShowResult
                {
                    Error = true,
                    Message = AdsMessages.PlacementNotFound,
                    PlacementType = PlacementType.Rewarded,
                    PlacementName = string.Empty,
                    Rewarded = false,
                };
            }

            var showResult = await Show(placementItem.Name, PlacementType.Rewarded);
            return showResult;
        }

        public async UniTask<AdsShowResult> Show(string placement, PlacementType type)
        {
            _activePlacement = placement;
            
            Debug.Log($"ADS SERVICE: Show {placement} {type}");

            if (_isInProgress)
            {
                return new AdsShowResult()
                {
                    PlacementName = placement,
                    Rewarded = false,
                    Error = true,
                    Message = AdsMessages.AdsAlreadyInProgress,
                    PlacementType = type,
                };
            }

            _isInProgress = true;
            _activePlacement = placement;

            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placement,
                Message = string.Empty,
                ActionType = PlacementActionType.Requested,
                PlacementType = type,
            });

            if (!IsPlacementAvailable(placement))
            {
                AddPlacementResult(placement, type, false, true, AdsMessages.PlacementCapped);
            }
            else
            {
                ShowPlacement(placement, type);
            }

            await UniTask
                .WaitWhile(() => _awaitedRewards.ContainsKey(placement) == false)
                .AttachExternalCancellation(_lifeTime.Token);

            var placementResult = _awaitedRewards[placement];

            _awaitedRewards.Remove(placement);
            _isInProgress = false;

            return placementResult;
        }
        
        public void ShowPlacement(string placeId, PlacementType type)
        {
            Debug.Log($"ADS SERVICE: Show {placeId} : {type}");

            switch (type)
            {
                case PlacementType.Rewarded:
                    ShowRewardedVideo(placeId);
                    break;
                case PlacementType.Interstitial:
                    throw new NotImplementedException();
                case PlacementType.Banner:
                    throw new NotImplementedException();
            }
        }
        private void ShowRewardedVideo(string placeId)
        {
            var isVideoAvailable = _rewardedAd != null;
            if (isVideoAvailable == false)
            {
                var rewardedResult = new AdsShowResult
                {
                    PlacementName = placeId,
                    Rewarded = false,
                    Error = true,
                    Message = AdsMessages.RewardedUnavailable
                };
                _applyRewardedCommand.Execute(rewardedResult);
                return;
            }

            _rewardedAd.Show();
        }

        public UniTask<AdsShowResult> Show(PlacementType type)
        {
            throw new NotImplementedException();
        }

        public UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId)
        {
            throw new NotImplementedException();
        }

        public async UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId)
        {
            var showResult = await Show(placeId, PlacementType.Rewarded);
            return showResult;
        }

        public void ValidateIntegration()
        {
            throw new NotImplementedException();
        }
        private void ApplyRewardedCommand(AdsShowResult result)
        {
            _rewardedHistory.Add(result);

            if (_awaitedRewards.ContainsKey(result.PlacementName))
                return;
            else
                _awaitedRewards.Add(result.PlacementName, result);
        }
        public void Dispose()
        {
            _lifeTime.Terminate();

            if (_rewardedAd == null)
                return;

            _rewardedAd.OnAdClicked -= YandexRewardAdClickEvent;
            _rewardedAd.OnAdDismissed -= YandexRewardAdDismissedEvent;
            _rewardedAd.OnAdFailedToShow -= YandexRewardAdFailedToShowEvent;
            _rewardedAd.OnAdImpression -= YandexRewardAdImpressionEvent;
            _rewardedAd.OnAdShown -= YandexRewardAdShowEvent;
            _rewardedAd.OnRewarded -= YandexRewardAdRewardedEvent;
        }

        private void SubscribeToEvents()
        {
            _adsAction.Subscribe(LoadAdsAction).AddTo(_lifeTime);
        }
        private void SubscribeToAd()
        {
            _rewardedAd.OnAdClicked += YandexRewardAdClickEvent;
            _rewardedAd.OnAdDismissed += YandexRewardAdDismissedEvent;
            _rewardedAd.OnAdFailedToShow += YandexRewardAdFailedToShowEvent;
            _rewardedAd.OnAdImpression += YandexRewardAdImpressionEvent;
            _rewardedAd.OnAdShown += YandexRewardAdShowEvent;
            _rewardedAd.OnRewarded += YandexRewardAdRewardedEvent;
        }
        public void LoadAdsAction(AdsActionData actionData)
        {
            Debug.Log($"[Ads Service] Action: {actionData.PlacementName} {actionData.PlacementType} {actionData.Message} {actionData.ActionType}");
        }
        public void YandexRewardAdLoaded(object sender, RewardedAdLoadedEventArgs args)
        {
            _rewardedAd = args.RewardedAd;
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = "Ad loadeed",
                ActionType = PlacementActionType.Available,
                PlacementType = PlacementType.Rewarded,
            });
            SubscribeToAd();
        }

        public void YandexRewardAdFailedToLoad(object sender, AdFailedToLoadEventArgs args)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = args.Message,
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded,
            });

            LoadAdsAsync().Forget();
        }
        public void YandexRewardAdClickEvent(object sender, EventArgs args)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = "Click to reward ad",
                ActionType = PlacementActionType.Clicked,
                PlacementType = PlacementType.Rewarded,
            });
        }

        public void YandexRewardAdShowEvent(object sender, EventArgs args)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = "Show reward ad",
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Rewarded,
            });
            LoadAdsAsync().Forget();
        }

        public void YandexRewardAdFailedToShowEvent(object sender, AdFailureEventArgs args)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = args.Message,
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded,
            });
            DestroyRewardedAd();
            LoadAdsAsync().Forget();
        }

        public void YandexRewardAdDismissedEvent(object sender, EventArgs args)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = "Ad dissmissed",
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded,
            });
            DestroyRewardedAd();
            LoadAdsAsync().Forget();
        }

        public void YandexRewardAdImpressionEvent(object sender, ImpressionData impressionData)
        {
            var data = impressionData == null ? "null" : impressionData.rawData;
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = $"HandleImpression event received with data: {data}",
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded,
            });
        }

        public void YandexRewardAdRewardedEvent(object sender, Reward args)
        {
            var placementId = _activePlacement;

            var rewardedResult = new AdsShowResult
            {
                PlacementName = placementId,
                Rewarded = true,
                Error = false,
                Message = "Rewarded"
            };

            _applyRewardedCommand.Execute(rewardedResult);

            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placementId,
                Message = $"Recieve reward with id {placementId}",
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Rewarded,
            });
            LoadAdsAsync().Forget();
        }
        public void DestroyRewardedAd()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }
        }
    }
}

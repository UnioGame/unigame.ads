namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using UniModules.UniCore.Runtime.DataFlow;
    using UniModules.UniCore.Runtime.Extension;
    using UniModules.UniGame.Core.Runtime.Rx;
    using UniRx;
    using UnityEngine;

    [Serializable]
    public class LevelPlayAdsService : IAdsService
    {
        private LifeTimeDefinition _lifeTime;
        private LevelPlayAdsConfig _adsConfig;
        private ReactiveValue<bool> _isInitialized = new();
        private float _reloadAdsInterval;
        private float _lastAdsReloadTime;
        private bool _loadingAds;
        private PlacementIdDataAsset _placementIds;
        
        private string _activePlacement = string.Empty;
        private bool _isInProgress = new();
        private List<AdsShowResult> _rewardedHistory = new();
        private Dictionary<string,AdsShowResult> _awaitedRewards = new();
        private ReactiveCommand<AdsShowResult> _applyRewardedCommand = new();
        private Subject<AdsActionData> _adsAction = new();
        private Dictionary<string, AdsPlacementItem> _placements = new();
        private Dictionary<PlacementAdsId, AdsPlacementItem> _idPlacements = new();
        
        public LevelPlayAdsService(LevelPlayAdsConfig config)
        {
            Debug.Log($"ADS SERVICE: Created");
            
            _adsConfig = config;
            _lifeTime = new LifeTimeDefinition();
            _reloadAdsInterval = config.reloadAdsInterval;
            _lastAdsReloadTime = -_reloadAdsInterval;
            _placementIds = config.placementIds;

            foreach (var adsPlacementId in _placementIds.Types)
            {
                _placements[adsPlacementId.Name] = adsPlacementId;
                _idPlacements[(PlacementAdsId)adsPlacementId.Id] = adsPlacementId;
            }
            
            if(_adsConfig.enableAds == false)
                return;
            
            SubscribeToEvents();
            
            InitializeAsync().Forget();
        }

        public void LoadAdsAction(AdsActionData actionData)
        {
            Debug.Log($"[Ads Service] Action: {actionData.PlacementName} {actionData.PlacementType} {actionData.Message} {actionData.ActionType}");
        }
        
        public virtual bool RewardedAvailable => IronSource.Agent.isRewardedVideoAvailable();

        public virtual bool InterstitialAvailable => IronSource.Agent.isInterstitialReady();

        public IObservable<AdsActionData> AdsAction => _adsAction;
        
        public bool IsInProgress => _isInProgress;

        public async UniTask<AdsShowResult> Show(PlacementAdsId placementAdsId)
        {
            if (!_idPlacements.TryGetValue(placementAdsId, out var placementItem))
            {
                return new AdsShowResult
                {
                    Error = true,
                    Message = LevelPlayMessages.PlacementNotFound,
                    PlacementType = PlacementType.Rewarded,
                    PlacementName = string.Empty,
                    Rewarded = false,
                };
            }
            
            var showResult = await Show(placementItem.Name, PlacementType.Rewarded);
            return showResult;
        }

        public async UniTask<AdsShowResult> Show(string placeId, PlacementType type)
        {
            Debug.Log($"ADS SERVICE: Show {placeId} {type}");

            if (_isInProgress)
            {
                return new AdsShowResult()
                {
                    PlacementName = placeId,
                    Rewarded = false,
                    Error = true,
                    Message = LevelPlayMessages.AdsAlreadyInProgress,
                    PlacementType = type,
                };
            }

            _isInProgress = true;
            _activePlacement = placeId;
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placeId,
                Message = string.Empty,
                ActionType = PlacementActionType.Requested,
                PlacementType =type,
            });

            if (!IsPlacementAvailable(placeId))
            {
                AddPlacementResult(placeId,type,false,true,LevelPlayMessages.PlacementCapped);
            }
            else
            {
                ShowPlacement(placeId, type);
            }
            
            await UniTask
                .WaitWhile(() => _awaitedRewards.ContainsKey(placeId) == false)
                .AttachExternalCancellation(_lifeTime.Token);

            var placementResult = _awaitedRewards[placeId];
            
            Debug.Log($"Rewarded video result {placementResult.Rewarded} {placementResult.Message} {placementResult.PlacementName}");
            
            _awaitedRewards.Remove(placeId);
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
                    IronSource.Agent.showInterstitial(placeId);
                    break;
                case PlacementType.Banner:
                    AddPlacementResult(placeId,type,false,true,LevelPlayMessages.PlacementCapped);
                    break;
            }
        }
        
        public void ValidateIntegration()
        {
            Debug.Log($"ADS SERVICE: ValidateIntegration");

            IronSource.Agent.validateIntegration();
        }
        
        public async UniTask<AdsShowResult> Show(PlacementType type)
        {
            AdsPlacementItem adsPlacementId = default;
            foreach (var placement in _placements)
            {
                var placementValue = placement.Value;
                if(placementValue.Type != type)
                    continue;
                if(IsPlacementAvailable(placementValue.Name) == false)
                    continue;
                adsPlacementId = placementValue;
                break;
            }

            if (string.IsNullOrEmpty(adsPlacementId.Name))
            {
                return new AdsShowResult
                {
                    Error = true,
                    Message = LevelPlayMessages.PlacementNotFound,
                    PlacementType = type,
                    PlacementName = string.Empty,
                    Rewarded = false,
                };
            }
            
            var showResult = await Show(adsPlacementId.Name, PlacementType.Rewarded);
            return showResult;
        }
        
        public virtual async UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId)
        {
            var showResult = await Show(placeId, PlacementType.Rewarded);
            return showResult;
        }

        public virtual async UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId)
        {
            var showResult = await Show(placeId, PlacementType.Interstitial);
            return showResult;
        }
        
        public bool IsPlacementAvailable(string placementName)
        {
            if(_placements.TryGetValue(placementName,out var adsPlacementId) == false)
                return false;
            var placementType = adsPlacementId.Type;
            
            switch (placementType)
            {
                case PlacementType.Rewarded:
                    return RewardedAvailable &&
                           IronSource.Agent.isRewardedVideoPlacementCapped(placementName) == false;
                case PlacementType.Interstitial:
                    return InterstitialAvailable;
            }
            
            return false;
        }

        public virtual async UniTask LoadAdsAsync()
        {
            if (_loadingAds || _lifeTime.IsTerminated) return;
            
            _loadingAds = true;
            
            var delay = Time.realtimeSinceStartup - _lastAdsReloadTime;
            delay = delay > _reloadAdsInterval ? 0 : _reloadAdsInterval - delay;
            delay = Mathf.Max(0, delay);
            
            await UniTask.Delay(TimeSpan.FromSeconds(delay))
                .AttachExternalCancellation(_lifeTime.Token);
            
            _lastAdsReloadTime = Time.realtimeSinceStartup;
            
            IronSource.Agent.loadRewardedVideo();
            IronSource.Agent.loadInterstitial();
            
            _loadingAds = false;
        }
        
        public void Dispose()
        {
            _lifeTime.Terminate();
            
            IronSourceRewardedVideoEvents.onAdOpenedEvent -= RewardedVideoOnAdOpenedEvent;
            IronSourceRewardedVideoEvents.onAdClosedEvent -= RewardedVideoOnAdClosedEvent;
            IronSourceRewardedVideoEvents.onAdAvailableEvent -= RewardedVideoOnAdAvailable;
            IronSourceRewardedVideoEvents.onAdUnavailableEvent -= RewardedVideoOnAdUnavailable;
            IronSourceRewardedVideoEvents.onAdShowFailedEvent -= RewardedVideoOnAdShowFailedEvent;
            IronSourceRewardedVideoEvents.onAdRewardedEvent -= RewardedVideoOnAdRewardedEvent;
            IronSourceRewardedVideoEvents.onAdClickedEvent -= RewardedVideoOnAdClickedEvent;
            
            IronSourceInterstitialEvents.onAdReadyEvent -= InterstitialOnAdReadyEvent;
            IronSourceInterstitialEvents.onAdLoadFailedEvent -= InterstitialOnAdLoadFailed;
            IronSourceInterstitialEvents.onAdOpenedEvent -= InterstitialOnAdOpenedEvent;
            IronSourceInterstitialEvents.onAdClickedEvent -= InterstitialOnAdClickedEvent;
            IronSourceInterstitialEvents.onAdShowSucceededEvent -= InterstitialOnAdShowSucceededEvent;
            IronSourceInterstitialEvents.onAdShowFailedEvent -= InterstitialOnAdShowFailedEvent;
            IronSourceInterstitialEvents.onAdClosedEvent -= InterstitialOnAdClosedEvent;
        }
        

        private async UniTask InitializeAsync()
        {
            IronSource.Agent.init (_adsConfig.LivePlayAppKey);
            
            var isInitialized = await _isInitialized
                .Where(x => x)
                .AwaitFirstAsync(_lifeTime);

            IronSource.Agent.shouldTrackNetworkState (_adsConfig.shouldTrackNetworkState);
            
            Debug.Log($"[Ads Service] initialized {isInitialized}");

            if (_adsConfig.validateIntegration)
                ValidateIntegration();

            _applyRewardedCommand
                .Subscribe(ApplyRewardedCommand)
                .AddTo(_lifeTime);
            
            LoadAdsAsync()
                .AttachExternalCancellation(_lifeTime.Token)
                .Forget();
        }

        private void AddPlacementResult(string placeId, PlacementType type,bool rewarded, bool error = false,string message = "")
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
        
        private void SubscribeToEvents()
        {
            Observable.EveryApplicationPause()
                .Subscribe(x => IronSource.Agent.onApplicationPause(x))
                .AddTo(_lifeTime);

            _adsAction.Subscribe(LoadAdsAction).AddTo(_lifeTime);
            
            IronSourceEvents.onSdkInitializationCompletedEvent += SdkInitializationCompletedEvent;
            
            //add AdInfo Rewarded Video Events
            IronSourceRewardedVideoEvents.onAdOpenedEvent += RewardedVideoOnAdOpenedEvent;
            IronSourceRewardedVideoEvents.onAdClosedEvent += RewardedVideoOnAdClosedEvent;
            IronSourceRewardedVideoEvents.onAdAvailableEvent += RewardedVideoOnAdAvailable;
            IronSourceRewardedVideoEvents.onAdUnavailableEvent += RewardedVideoOnAdUnavailable;
            IronSourceRewardedVideoEvents.onAdShowFailedEvent += RewardedVideoOnAdShowFailedEvent;
            IronSourceRewardedVideoEvents.onAdRewardedEvent += RewardedVideoOnAdRewardedEvent;
            IronSourceRewardedVideoEvents.onAdClickedEvent += RewardedVideoOnAdClickedEvent;
            
            //add Interstitial AdInfo Events
            IronSourceInterstitialEvents.onAdReadyEvent += InterstitialOnAdReadyEvent;
            IronSourceInterstitialEvents.onAdLoadFailedEvent += InterstitialOnAdLoadFailed;
            IronSourceInterstitialEvents.onAdOpenedEvent += InterstitialOnAdOpenedEvent;
            IronSourceInterstitialEvents.onAdClickedEvent += InterstitialOnAdClickedEvent;
            IronSourceInterstitialEvents.onAdShowSucceededEvent += InterstitialOnAdShowSucceededEvent;
            IronSourceInterstitialEvents.onAdShowFailedEvent += InterstitialOnAdShowFailedEvent;
            IronSourceInterstitialEvents.onAdClosedEvent += InterstitialOnAdClosedEvent;
        }
        
        private void ShowRewardedVideo(string placeId)
        {
            var isVideoAvailable = IronSource.Agent.isRewardedVideoAvailable();
            if (isVideoAvailable == false)
            {
                var rewardedResult = new AdsShowResult { 
                    PlacementName = placeId, 
                    Rewarded = false, 
                    Error = true,
                    Message = LevelPlayMessages.RewardedUnavailable
                };
                _applyRewardedCommand.Execute(rewardedResult);
                return;
            }
            
            var placementCapped = IronSource.Agent.isRewardedVideoPlacementCapped(placeId);
            if (placementCapped)
            {
                var rewardedResult = new AdsShowResult { 
                    PlacementName = placeId, 
                    Rewarded = false, 
                    Error = true,
                    Message = LevelPlayMessages.RewardedPlacementCapped
                };
                _applyRewardedCommand.Execute(rewardedResult);
                return;
            }
            
            IronSource.Agent.showRewardedVideo(placeId);
        }
        
        private void ApplyRewardedCommand(AdsShowResult result)
        {
            _rewardedHistory.Add(result);
            _awaitedRewards[result.PlacementName] = result;
        }
        
        /************* RewardedVideo AdInfo Delegates *************/
        // Indicates that there’s an available ad.
        // The adInfo object includes information about the ad that was loaded successfully
        // This replaces the RewardedVideoAvailabilityChangedEvent(true) event
        private void RewardedVideoOnAdAvailable(IronSourceAdInfo adInfo)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = String.Empty,
                ActionType = PlacementActionType.Available,
                PlacementType = PlacementType.Rewarded,
            });
        }
        
        // Indicates that no ads are available to be displayed
        // This replaces the RewardedVideoAvailabilityChangedEvent(false) event
        private void RewardedVideoOnAdUnavailable() 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = string.Empty,
                Message = string.Empty,
                ActionType = PlacementActionType.Unavailable,
                PlacementType = PlacementType.Rewarded,
            });
            
            LoadAdsAsync().Forget();
        }
        
        // The Rewarded Video ad view has opened. Your activity will loose focus.
        private void RewardedVideoOnAdOpenedEvent(IronSourceAdInfo adInfo)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Rewarded,
            });
        }
        
        // The Rewarded Video ad view is about to be closed. Your activity will regain its focus.
        private void RewardedVideoOnAdClosedEvent(IronSourceAdInfo adInfo)
        {
            var placement = adInfo.adUnit;
            
            if(!_awaitedRewards.TryGetValue(placement,out var result))
            {
                var rewardedResult = new AdsShowResult { 
                    PlacementName = _activePlacement, 
                    Rewarded = false,
                    Error = false,
                    Message = LevelPlayMessages.RewardedPlacementCapped
                };
                _applyRewardedCommand.Execute(rewardedResult);
            }
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Closed,
                PlacementType = PlacementType.Rewarded,
            });
        }
        
        // The user completed to watch the video, and should be rewarded.
        // The placement parameter will include the reward data.
        // When using server-to-server callbacks, you may ignore this event and wait for the ironSource server callback.
        private void RewardedVideoOnAdRewardedEvent(IronSourcePlacement placement, IronSourceAdInfo adInfo)
        {
            var placementId = placement.getPlacementName();
            
            var rewardedResult = new AdsShowResult { 
                PlacementName = placementId, 
                Rewarded = true,
                Error = false,
                Message = LevelPlayMessages.RewardedPlacementCapped
            };
            
            _applyRewardedCommand.Execute(rewardedResult);
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placementId,
                Message = placement.getRewardName(),
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Rewarded,
            });
        }
        
        // The rewarded video ad was failed to show.
        private void RewardedVideoOnAdShowFailedEvent(IronSourceError error, IronSourceAdInfo adInfo)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                ErrorCode = error.getErrorCode(),
                Message = error.getDescription(),
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded,
            });
            
            LoadAdsAsync().Forget();
        }
        
        // Invoked when the video ad was clicked.
        // This callback is not supported by all networks, and we recommend using it only if
        // it’s supported by all networks you included in your build.
        private void RewardedVideoOnAdClickedEvent(IronSourcePlacement placement, IronSourceAdInfo adInfo)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Clicked,
                PlacementType = PlacementType.Rewarded,
            });
        }
        
        /************* Interstitial AdInfo Delegates *************/
        // Invoked when the interstitial ad was loaded succesfully.
        private void InterstitialOnAdReadyEvent(IronSourceAdInfo adInfo) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = adInfo.adUnit,
                ErrorCode = 0,
                ActionType = PlacementActionType.Available,
                PlacementType = PlacementType.Interstitial,
            });    
        }
        
        // Invoked when the initialization process has failed.
        private void InterstitialOnAdLoadFailed(IronSourceError ironSourceError) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = string.Empty,
                Message = ironSourceError.getDescription(),
                ErrorCode = ironSourceError.getErrorCode(),
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Interstitial,
            });
            
            LoadAdsAsync().Forget();
        }
        
        // Invoked when the Interstitial Ad Unit has opened. This is the impression indication. 
        private void InterstitialOnAdOpenedEvent(IronSourceAdInfo adInfo) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Interstitial,
            });
        }
        
        // Invoked when end user clicked on the interstitial ad
        private void InterstitialOnAdClickedEvent(IronSourceAdInfo adInfo) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Clicked,
                PlacementType = PlacementType.Interstitial,
            });
        }
        // Invoked when the ad failed to show.
        private void InterstitialOnAdShowFailedEvent(IronSourceError ironSourceError, IronSourceAdInfo adInfo) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = ironSourceError.getDescription(),
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Interstitial,
                ErrorCode = ironSourceError.getErrorCode(),
            });
        }
        
        // Invoked when the interstitial ad closed and the user went back to the application screen.
        private void InterstitialOnAdClosedEvent(IronSourceAdInfo adInfo) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Closed,
                PlacementType = PlacementType.Interstitial,
            });
        }
        
        // Invoked before the interstitial ad was opened, and before the InterstitialOnAdOpenedEvent is reported.
        // This callback is not supported by all networks, and we recommend using it only if  
        // it's supported by all networks you included in your build. 
        private void InterstitialOnAdShowSucceededEvent(IronSourceAdInfo adInfo) 
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Interstitial,
            });
        }

        private void SdkInitializationCompletedEvent()
        {
            _isInitialized.Value = true;
        }
    }
}

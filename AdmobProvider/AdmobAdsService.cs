namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using UnityEngine;
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using GoogleMobileAds.Api;
    using UniGame.Core.Runtime;
    using UniModules.UniCore.Runtime.DataFlow;
    using UniModules.UniCore.Runtime.Extension;
    using UniModules.UniGame.Core.Runtime.Rx;
    using UniRx;
    
    [Serializable]
    public class AdmobAdsService : IAdsService
    {
        private LifeTimeDefinition _lifeTime;
        private AdmobAdsConfig _adsConfig;
        private ReactiveValue<bool> _isInitialized = new();
        private AdmobPlacementIdDataAsset _placementIds;
        
        private string _activePlacement = string.Empty;
        private bool _isInProgress;
        private float _reloadAdsInterval;
        private float _lastAdsReloadTime;
        private bool _loadingAds;
        
        private List<AdsShowResult> _rewardedHistory = new();
        private Dictionary<string,AdsShowResult> _awaitedRewards = new();
        private ReactiveCommand<AdsShowResult> _applyRewardedCommand = new();
        private Subject<AdsActionData> _adsAction = new();
        private Dictionary<string, AdmobPlacementItem> _placements = new();
        private Dictionary<PlacementAdsId, AdmobPlacementItem> _idPlacements = new();

        private RewardedAd _rewardedAdCache = null;
        private InterstitialAd _interstitialAdCache = null;
        
        public AdmobAdsService(AdmobAdsConfig config)
        {
            Debug.Log($"ADS SERVICE: admob created");
            
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
            
            if(_adsConfig.enableAds == false) return;
            
            SubscribeToEvents();
            
            InitializeAsync().Forget();
        }
        
        public ILifeTime LifeTime => _lifeTime;
        
        public void LoadAdsAction(AdsActionData actionData)
        {
            Debug.Log($"[Admob] ads action: NAME:{actionData.PlacementName} ERROR:{actionData.ErrorCode} MESSAGE:{actionData.Message}");
            return;
        }
        
        public async UniTask<bool> LoadRewardedAd(string placementId)
        {
            if (_rewardedAdCache != null)
            {
                _rewardedAdCache.Destroy();
                _rewardedAdCache = null;
            }

            Debug.Log($"Loading the rewarded ad {placementId}");

            var adRequest = new AdRequest();
            string cppId = _placements[placementId].AndroidAdmobId;
            bool loadComplete = false;
            bool loaded = false;

            Debug.Log($"Loading cppId: {cppId}");
            RewardedAd.Load(cppId, adRequest,
                (RewardedAd ad, LoadAdError error) =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogError("Rewarded ad failed to load an ad " +
                                       "with error : " + error);
                        loadComplete = true;
                        return;
                    }

                    Debug.Log("Rewarded ad loaded with response : "
                              + ad.GetResponseInfo());

                    _rewardedAdCache = ad;
                    loaded = true;
                    loadComplete = true;
                });

            await UniTask
                .WaitWhile(() => loadComplete == false)
                .AttachExternalCancellation(_lifeTime.Token);
            
            Debug.Log($"Rewarded ad loaded: {loaded}");
            return loaded;
        }
        public async UniTask<bool> LoadInterstitialAd(string placementId)
        {
            if (_interstitialAdCache != null)
            {
                _interstitialAdCache.Destroy();
                _interstitialAdCache = null;
            }

            Debug.Log("Loading the interstitial ad.");

            var adRequest = new AdRequest();
            bool loadComplete = false;
            bool loaded = false;
            
            InterstitialAd.Load(placementId, adRequest,
                (InterstitialAd ad, LoadAdError error) =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogError("interstitial ad failed to load an ad " +
                                       "with error : " + error);
                        loadComplete = true;

                        return;
                    }

                    Debug.Log("Interstitial ad loaded with response : "
                              + ad.GetResponseInfo());

                    _interstitialAdCache = ad;
                    loadComplete = true;
                    loaded = true;
                });

            await UniTask
                .WaitWhile(() => loadComplete == true)
                .AttachExternalCancellation(_lifeTime.Token);

            return loaded;
        }
        public virtual bool RewardedAvailable => true;
        public virtual bool InterstitialAvailable => true;
        public IObservable<AdsActionData> AdsAction => _adsAction;
        public bool IsInProgress => _isInProgress;
        public async UniTask<AdsShowResult> Show(PlacementAdsId placementAdsId)
        {
            if (!_idPlacements.TryGetValue(placementAdsId, out var placementItem))
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
                    Message = AdsMessages.AdsAlreadyInProgress,
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
                AddPlacementResult(placeId,type,false,true,AdsMessages.PlacementCapped);
            }
            else
            {
                ShowPlacement(placeId, type);
            }
            
            await UniTask
                .WaitWhile(() => _awaitedRewards.ContainsKey(placeId) == false)
                .AttachExternalCancellation(_lifeTime.Token);

            var placementResult = _awaitedRewards[placeId];
            
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
                    ShowRewardedVideo(placeId).Forget();
                    break;
                case PlacementType.Interstitial:
                    ShowInterstitialVideo(placeId).Forget();
                    break;
                case PlacementType.Banner:
                    AddPlacementResult(placeId,type,false,true,AdsMessages.PlacementCapped);
                    break;
            }
        }
        public async UniTask<AdsShowResult> Show(PlacementType type)
        {
            AdmobPlacementItem adsPlacementId = default;
            foreach (var placement in _placements)
            {
                var placementValue = placement.Value;
                if(placementValue.PlacementType != type)
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
                    Message = AdsMessages.PlacementNotFound,
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
        public void ValidateIntegration()
        {
            
        }
        public bool IsPlacementAvailable(string placementName)
        {
            if(_placements.TryGetValue(placementName,out var adsPlacementId) == false)
            {
                Debug.Log($"Placement haven't {placementName}");
                return false;
            }
            var placementType = adsPlacementId.PlacementType;
            
            switch (placementType)
            {
                case PlacementType.Rewarded:
                    return true;
                case PlacementType.Interstitial:
                    return true;
            }
            
            return false;
        }
        public virtual async UniTask LoadAdsAsync()
        {
            Debug.Log("Do not implement preload into admob");
        }
        public void Dispose()
        {
            _lifeTime.Terminate();
 
            UnsubscribeToRewardedAdEvents(_rewardedAdCache);
            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
        }
        private async UniTask InitializeAsync()
        {
            Debug.Log($"[Ads Service] admob initialization started");
            
            MobileAds.Initialize(SdkInitializationCompletedEvent);
            
            var isInitialized = await _isInitialized
                .Where(x => x)
                .AwaitFirstAsync(_lifeTime);

            Debug.Log($"[Ads Service] admob initialized {isInitialized}");

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
            _adsAction.Subscribe(LoadAdsAction).AddTo(_lifeTime);
        }
        #region rewarded block
        private async UniTask ShowRewardedVideo(string placeId)
        {
            if (_rewardedAdCache == null)
            {
                Debug.Log("Start load reward video");
                bool loaded = await LoadRewardedAd(placeId);
                Debug.Log("Complete load reward video");

                if (loaded == false)
                {
                    var rewardedResult = new AdsShowResult { 
                        PlacementName = placeId, 
                        Rewarded = false, 
                        Error = true,
                        Message = AdsMessages.RewardedUnavailable
                    };
                    _applyRewardedCommand.Execute(rewardedResult);
                    return;
                }
                
                SubscribeToRewardedAdEvents(_rewardedAdCache);
            }
            
            if (_rewardedAdCache.CanShowAd())
            {
                _rewardedAdCache.Show((Reward reward) =>
                {
                    OnGetInvokeRewardVideo().Forget();
                    
                    Debug.Log(String.Format("Instead msg about reward video complete", reward.Type, reward.Amount));
                });
            }
            else
            {
                Debug.Log("Something went wrong with CanShowAd");
            }
        }

        public async UniTask OnGetInvokeRewardVideo()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(5));
            
            var placementId = _rewardedAdCache.GetAdUnitID();
    
            var rewardedResult = new AdsShowResult { 
                PlacementName = placementId, 
                Rewarded = true,
                Error = false,
                Message = AdsMessages.RewardedPlacementCapped
            };
    
            _applyRewardedCommand.Execute(rewardedResult);
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placementId,
                Message = "Complete",
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Rewarded,
            });
        }
        private void SubscribeToRewardedAdEvents(RewardedAd rewardedAd)
        {
            if(rewardedAd == null)
                return;
            
            rewardedAd.OnAdClicked += RewardedVideoOnAdClickedEvent;
            rewardedAd.OnAdPaid += RewardedVideoOnAdPaidEvent;
            rewardedAd.OnAdImpressionRecorded += RewardedVideoOnAdImpressionRecordedEvent;
            rewardedAd.OnAdFullScreenContentClosed += RewardedVideoOnAdFullScreenContentClosedEvent;
            rewardedAd.OnAdFullScreenContentFailed += RewardedVideoOnAdFullScreenContentFailedEvent;
            rewardedAd.OnAdFullScreenContentOpened += RewardedVideoOnAdFullScreenContentOpenedEvent;
        }
        private void UnsubscribeToRewardedAdEvents(RewardedAd rewardedAd)
        {
            if(rewardedAd == null)
                return;
            
            rewardedAd.OnAdClicked -= RewardedVideoOnAdClickedEvent;
            rewardedAd.OnAdPaid -= RewardedVideoOnAdPaidEvent;
            rewardedAd.OnAdImpressionRecorded -= RewardedVideoOnAdImpressionRecordedEvent;
            rewardedAd.OnAdFullScreenContentClosed -= RewardedVideoOnAdFullScreenContentClosedEvent;
            rewardedAd.OnAdFullScreenContentFailed -= RewardedVideoOnAdFullScreenContentFailedEvent;
            rewardedAd.OnAdFullScreenContentOpened -= RewardedVideoOnAdFullScreenContentOpenedEvent;
        }
        private void RewardedVideoOnAdClickedEvent()
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Clicked,
                PlacementType = PlacementType.Rewarded,
            });
            
            Debug.Log("Rewarded: on ad clicked");
        }
        private void RewardedVideoOnAdPaidEvent(AdValue adValue)
        {
            
            
            Debug.Log("Rewarded: on ad paid");
        }
        private void RewardedVideoOnAdImpressionRecordedEvent()
        {
            Debug.Log("Rewarded: on ad impression");
        }
        private void RewardedVideoOnAdFullScreenContentClosedEvent()
        {
            var placement = _rewardedAdCache.GetAdUnitID();
            
            if(!_awaitedRewards.TryGetValue(placement,out var result))
            {
                var rewardedResult = new AdsShowResult { 
                    PlacementName = _activePlacement, 
                    Rewarded = false,
                    Error = false,
                    Message = AdsMessages.RewardedPlacementCapped
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
            Debug.Log("Rewarded: on ad full screen closed");

            UnsubscribeToRewardedAdEvents(_rewardedAdCache);
            _rewardedAdCache.Destroy();
            _rewardedAdCache = null;
        }
        private void RewardedVideoOnAdFullScreenContentOpenedEvent()
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Rewarded,
            });
            
            Debug.Log("Rewarded: on ad full screen opened");
        }
        private void RewardedVideoOnAdFullScreenContentFailedEvent(AdError error)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Rewarded,
            });
            
            UnsubscribeToRewardedAdEvents(_rewardedAdCache);
            _rewardedAdCache.Destroy();
            Debug.Log("Rewarded: on ad full screen failed");
        }
        private void ApplyRewardedCommand(AdsShowResult result)
        {
            _rewardedHistory.Add(result);

            if (_awaitedRewards.ContainsKey(result.PlacementName))
                return;
            else
                _awaitedRewards.Add(result.PlacementName, result);
        }
        #endregion
        #region interstitial block

        private async UniTask ShowInterstitialVideo(string placeId)
        {
            if (_interstitialAdCache == null)
            {
                bool loaded = await LoadInterstitialAd(placeId);

                if (loaded == false)
                {
                    var rewardedResult = new AdsShowResult { 
                        PlacementName = placeId, 
                        Rewarded = false, 
                        Error = true,
                        Message = AdsMessages.RewardedUnavailable
                    };
                    _applyRewardedCommand.Execute(rewardedResult);
                    return;
                }
                
                SubscribeToInterstitialAdEvents(_interstitialAdCache);
            }
            
            if (_interstitialAdCache.CanShowAd())
            {
                Debug.Log("Showing interstitial ad.");
                _interstitialAdCache.Show();
            }
            else
            {
                Debug.LogError("Interstitial ad is not ready yet.");
            }
        }
        private void SubscribeToInterstitialAdEvents(InterstitialAd interstitialAd)
        {
            if(interstitialAd == null)
                return;
            
            interstitialAd.OnAdClicked += InterstitialVideoOnAdClickedEvent;
            interstitialAd.OnAdPaid += InterstitialVideoOnAdPaidEvent;
            interstitialAd.OnAdImpressionRecorded += InterstitialVideoOnAdImpressionRecordedEvent;
            interstitialAd.OnAdFullScreenContentClosed += InterstitialVideoOnAdFullScreenContentClosedEvent;
            interstitialAd.OnAdFullScreenContentFailed += InterstitialVideoOnAdFullScreenContentFailedEvent;
            interstitialAd.OnAdFullScreenContentOpened += InterstitialVideoOnAdFullScreenContentOpenedEvent;
        }
        private void UnsubscribeToInterstitialAdEvents(InterstitialAd interstitialAd)
        {
            if(interstitialAd == null)
                return;
            
            interstitialAd.OnAdClicked -= InterstitialVideoOnAdClickedEvent;
            interstitialAd.OnAdPaid -= InterstitialVideoOnAdPaidEvent;
            interstitialAd.OnAdImpressionRecorded -= InterstitialVideoOnAdImpressionRecordedEvent;
            interstitialAd.OnAdFullScreenContentClosed -= InterstitialVideoOnAdFullScreenContentClosedEvent;
            interstitialAd.OnAdFullScreenContentFailed -= InterstitialVideoOnAdFullScreenContentFailedEvent;
            interstitialAd.OnAdFullScreenContentOpened -= InterstitialVideoOnAdFullScreenContentOpenedEvent;
        }
        private void InterstitialVideoOnAdClickedEvent()
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Clicked,
                PlacementType = PlacementType.Rewarded,
            });
            
            Debug.Log("Interstitial: on ad clicked");
        }
        private void InterstitialVideoOnAdPaidEvent(AdValue adValue)
        {
            var placementId = _rewardedAdCache.GetAdUnitID();
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placementId,
                Message = "Paid",
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Interstitial,
            });
            
            Debug.Log("Interstitial: on ad paid");
        }
        private void InterstitialVideoOnAdImpressionRecordedEvent()
        {
            Debug.Log("Interstitial: on ad impression");
        }
        private void InterstitialVideoOnAdFullScreenContentClosedEvent()
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Closed,
                PlacementType = PlacementType.Interstitial,
            });
            Debug.Log("Interstitial: on ad full screen closed");

            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
            _interstitialAdCache.Destroy();
        }
        private void InterstitialVideoOnAdFullScreenContentOpenedEvent()
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Interstitial,
            });
            
            Debug.Log("Interstitial: on ad full screen opened");
        }
        private void InterstitialVideoOnAdFullScreenContentFailedEvent(AdError error)
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Interstitial,
            });
            
            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
            _interstitialAdCache.Destroy();
            Debug.Log("Interstitial: on ad full screen failed");
        }
        #endregion
        private void SdkInitializationCompletedEvent(InitializationStatus status)
        {
            _isInitialized.Value = true;
        }
    }
}

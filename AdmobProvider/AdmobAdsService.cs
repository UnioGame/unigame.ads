namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using UnityEngine;
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using GoogleMobileAds.Api;
    using R3;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;
    using UniGame.Runtime.Rx;

    [Serializable]
    public class AdmobAdsService : IAdsService
    {
        public const string AdmobSdk = "admob";
        
        private LifeTime _lifeTime;
        private AdmobAdsConfig _adsConfig;
        private ReactiveValue<bool> _isInitialized = new();
        private PlacementIdDataAsset _placementIds;
        
        private string _activePlacement = string.Empty;
        private bool _isInProgress;
        private float _reloadAdsInterval;
        private float _lastAdsReloadTime;
        private bool _loadingAds;
        
        private List<AdsShowResult> _rewardedHistory = new();
        private Dictionary<string,AdsShowResult> _awaitedRewards = new();
        private ReactiveCommand<AdsShowResult> _applyRewardedCommand = new();
        private Subject<AdsActionData> _adsAction = new();
        private Dictionary<string, AdsPlacementItem> _placements = new();
        private Dictionary<PlacementAdsId, AdsPlacementItem> _idPlacements = new();

        private InterstitialAd _interstitialAdCache = null;
        
        private Dictionary<string, AdmobRewardedAdsCache> _rewardedAdsCache = new();
        public AdmobAdsService(AdmobAdsConfig config)
        {
            Debug.Log($"[ADS SERVICE]: admob created");
            
            _adsConfig = config;
            _lifeTime = new LifeTime();
            _reloadAdsInterval = config.ReloadAdsInterval;
            _lastAdsReloadTime = -_reloadAdsInterval;
            _placementIds = config.placementIds;

            foreach (var adsPlacementId in _placementIds.Placements)
            {
                _placements[adsPlacementId.Name] = adsPlacementId;
                _idPlacements[(PlacementAdsId)adsPlacementId.Id] = adsPlacementId;
            }
            
            if(_adsConfig.EnableAds == false) return;
            
            SubscribeToEvents();
            
            InitializeAsync().Forget();
        }

        public ILifeTime LifeTime => _lifeTime;
                
        public virtual bool RewardedAvailable => true;
        public virtual bool InterstitialAvailable => true;
        public Observable<AdsActionData> AdsAction => _adsAction;
        public bool IsInProgress => _isInProgress;
        
        public void LoadAdsAction(AdsActionData actionData)
        {
            Debug.Log($"[ADS SERVICE]:admob action: NAME:{actionData.PlacementName} ERROR:{actionData.ErrorCode} MESSAGE:{actionData.Message}");
            return;
        }
        
        public async UniTask<bool> LoadRewardedAd(string placementId)
        {
            Debug.Log($"Loading the rewarded ad {placementId}. " +
                      $"Load:{_rewardedAdsCache[placementId].LoadProcess}. " +
                      $"Cache:{_rewardedAdsCache[placementId].RewardedAd}");
            
            if(!_rewardedAdsCache.ContainsKey(placementId))
                throw new Exception($"{placementId} not found into map");
            
            if (_rewardedAdsCache[placementId].LoadProcess == true)
            {
                await UniTask
                    .WaitWhile(() => _rewardedAdsCache[placementId].LoadProcess == true)
                    .AttachExternalCancellation(_lifeTime.Token);
            }
            
            var adsRewardedAd = _rewardedAdsCache[placementId].RewardedAd;
            
            if (adsRewardedAd != null) return true;

            var adRequest = new AdRequest();
            var cppId = _placements[placementId].GetPlacementIdByPlatform(_adsConfig.PlacementPlatfrom);
            var loadComplete = false;
            var loaded = false;
            
            _rewardedAdsCache[placementId].LoadProcess = true;

            Debug.Log($"[ADS SERVICE]:Loading cppId: {cppId}");
            RewardedAd.Load(cppId, adRequest, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad " +
                                   "with error : " + error);
                    loadComplete = true;
                    return;
                }

                Debug.Log("[ADS SERVICE]:Rewarded ad loaded with response : "
                          + ad.GetResponseInfo());

                _rewardedAdsCache[placementId].RewardedAd = ad;
                
// #if GAME_ANALYTICS_SDK
//                 if(ad!=null)
//                     GameAnalyticsSDK.Events.GA_Ads...SubscribeAdMobImpressions(placementId, ad);     
// #endif
                
                SubscribeToRewardedAdEvents(ad);
                loaded = true;
                loadComplete = true;
            });

            await UniTask
                .WaitWhile(() => loadComplete == false)
                .AttachExternalCancellation(_lifeTime.Token);
            
            await UniTask.SwitchToMainThread();
            
            _rewardedAdsCache[placementId].LoadProcess = false;
            _rewardedAdsCache[placementId].Available = loaded;    
            
            Debug.Log($"[ADS SERVICE]:Rewarded ad loaded: {loaded}");
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
            var loadComplete = false;
            var loaded = false;
            
            InterstitialAd.Load(placementId, adRequest,
                (ad, error) =>
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
            Debug.Log($"[ADS SERVICE]: Show {placeId} {type}");

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
                SdkName = AdmobSdk,
            });

            if (!await IsPlacementAvailable(placeId))
            {
                AddPlacementResult(placeId,type,false,true,AdsMessages.PlacementCapped);
                return new AdsShowResult()
                {
                    PlacementName = placeId,
                    Rewarded = false,
                    Error = true,
                    Message = AdsMessages.PlacementCapped,
                    PlacementType = type,
                };
            }
            else
            {
                ShowPlacement(placeId, type);
            }
            
            await UniTask
                .WaitWhile(() => _awaitedRewards.ContainsKey(placeId) == false)
                .AttachExternalCancellation(_lifeTime.Token);

            await UniTask.SwitchToMainThread();
            
            var placementResult = _awaitedRewards[placeId];
            
            _awaitedRewards.Remove(placeId);
            _isInProgress = false;
            
            Debug.Log($"[ADS SERVICE]: Show {placeId} {type} result: {placementResult.Error} {placementResult.Message}");
            
            return placementResult;
        }
        
        public void ShowPlacement(string placeId, PlacementType type)
        {
            ShowPlacementAsync(placeId, type).Forget();
        }
        
        public async UniTask ShowPlacementAsync(string placeId, PlacementType type)
        {
            Debug.Log($"[ADS SERVICE]: Show PlacementAsync {placeId} : {type}");
            
            switch (type)
            {
                case PlacementType.Rewarded:
                    await ShowRewardedVideo(placeId);
                    break;
                case PlacementType.Interstitial:
                    await ShowInterstitialVideo(placeId);
                    break;
                case PlacementType.Banner:
                    AddPlacementResult(placeId,type,false,true,AdsMessages.PlacementCapped);
                    break;
            }
        }
        
        public async UniTask<AdsShowResult> Show(PlacementType type)
        {
            AdsPlacementItem adsPlacementId = default;
            foreach (var placement in _placements)
            {
                var placementValue = placement.Value;
                if(placementValue.Type != type)
                    continue;
                if(await IsPlacementAvailable(placementValue.Name) == false)
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
        
        public async UniTask<bool> IsPlacementAvailable(string placementName)
        {
            if(_placements.TryGetValue(placementName,out var adsPlacementId) == false)
            {
                Debug.Log($"[ADS SERVICE]:Placement haven't {placementName}");
                return false;
            }
            
            var placementType = adsPlacementId.Type;
            
            switch (placementType)
            {
                case PlacementType.Rewarded:
                    return await LoadRewardedAd(placementName);
                case PlacementType.Interstitial:
                    return true;
            }
            
            return false;
        }
        public virtual async UniTask LoadAdsAsync()
        {
            Debug.Log("[ADS SERVICE]:Do not implement preload into admob");
        }
        
        public void Dispose()
        {
            _lifeTime.Terminate();

            foreach (var (key, val) in _rewardedAdsCache)
                UnsubscribeToRewardedAdEvents(_rewardedAdsCache[key].RewardedAd);
            
            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
        }
        
        private async UniTask InitializeAsync()
        {
            Debug.Log($"[ADS SERVICE]: admob initialization started");
            
            MobileAds.Initialize(SdkInitializationCompletedEvent);
            
            var isInitialized = await _isInitialized
                .Where(x => x)
                .FirstAsync(_lifeTime.Token);

            Debug.Log($"[ADS SERVICE]: admob initialized {isInitialized}");

            foreach (var adsPlacementId in _placementIds.Placements)
            {
                if (adsPlacementId.Type == PlacementType.Rewarded)
                {
                    _rewardedAdsCache.Add(adsPlacementId.Name, new AdmobRewardedAdsCache(adsPlacementId.Name));
                    LoadRewardedAd(adsPlacementId.Name).Forget();
                }
            }
            
            _applyRewardedCommand
                .Subscribe(ApplyRewardedCommand)
                .AddTo(_lifeTime);
            
            LoadAdsAsync()
                .AttachExternalCancellation(_lifeTime.Token)
                .Forget();
        }
        
        private AdsShowResult AddPlacementResult(string placeId, 
            PlacementType type,bool rewarded, 
            bool error = false,string message = "")
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

            return result;
        }
        private void SubscribeToEvents()
        {
            _adsAction.Subscribe(LoadAdsAction).AddTo(_lifeTime);
        }
        
        #region rewarded block
        
        private async UniTask ShowRewardedVideo(string placeId)
        {
            Debug.Log($"[ADS SERVICE]: Try to show {placeId}");
            
            var rewardedAd = _rewardedAdsCache[placeId].RewardedAd;
            
            if (rewardedAd == null)
            {
                Debug.Log($"[ADS SERVICE]: Start load reward video {placeId}");
                var loaded = await LoadRewardedAd(placeId);
                Debug.Log($"[ADS SERVICE]: Complete load reward video {placeId}");

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

                rewardedAd = _rewardedAdsCache[placeId].RewardedAd;
            }
            
            if (rewardedAd!=null && rewardedAd.CanShowAd())
            {
                _activePlacement = placeId;
                rewardedAd.Show(reward =>
                {
                    CompleteRewardedVideoAsync(new AdmobRewardedResult
                    {
                        Reward = reward,
                        PlacementId = _activePlacement,
                        Message = string.Empty,
                        Error = null,
                    }).Forget();
                });
            }
            else
            {
                Debug.Log("[ADS SERVICE]:Something went wrong with CanShowAd");
            }
        }
        
        private async UniTask CompleteRewardedVideoAsync(AdmobRewardedResult adResult)
        {
            var placementId = _activePlacement;
            await UniTask.SwitchToMainThread();
            
            var rewarded = adResult.Reward != null;
            var adError = adResult.Error;
            var reward = adResult.Reward;
            
            var error = adError !=null ? adError.GetMessage() : string.Empty;
            var message = !rewarded ? error : adResult.Message;
            var rewardName = reward != null ? reward.Type : placementId;
            var rewardAmount = reward?.Amount ?? 0f;
            var errorCode = adError?.GetCode() ?? 0;
            
            if(!_awaitedRewards.TryGetValue(placementId,out var result))
            {
                var rewardedResult = new AdsShowResult { 
                    PlacementName = placementId, 
                    Rewarded = rewarded,
                    Error = !rewarded,
                    Message = message,
                    PlacementType = PlacementType.Rewarded,
                    RewardName = rewardName,
                    RewardAmount = (float)rewardAmount,
                };
                
                _applyRewardedCommand.Execute(rewardedResult);
            }
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placementId,
                Message = message,
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Rewarded,
                SdkName = AdmobSdk,
                Duration = 30,
                ErrorCode = errorCode,
            });
        }
        
        private void SubscribeToRewardedAdEvents(RewardedAd rewardedAd)
        {
            if(rewardedAd == null) return;
            
            rewardedAd.OnAdClicked += RewardedVideoOnAdClickedEvent;
            rewardedAd.OnAdPaid += RewardedVideoOnAdPaidEvent;
            rewardedAd.OnAdImpressionRecorded += RewardedVideoOnAdImpressionRecordedEvent;
            rewardedAd.OnAdFullScreenContentClosed += RewardedVideoOnAdFullScreenContentClosedEvent;
            rewardedAd.OnAdFullScreenContentFailed += RewardedVideoOnAdFullScreenContentFailedEvent;
            rewardedAd.OnAdFullScreenContentOpened += RewardedVideoOnAdFullScreenContentOpenedEvent;
        }
        
        private void UnsubscribeToRewardedAdEvents(RewardedAd rewardedAd)
        {
            if(rewardedAd == null) return;
            
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
                SdkName = AdmobSdk,
                Duration = 0,
                ErrorCode = 0,
            });
            
            Debug.Log("[ADS SERVICE]: Rewarded: on ad clicked");
        }
        
        private void RewardedVideoOnAdPaidEvent(AdValue adValue)
        {
            Debug.Log("[ADS SERVICE]: Rewarded: on ad paid");
        }
        
        private void RewardedVideoOnAdImpressionRecordedEvent()
        {
            Debug.Log("[ADS SERVICE]: Rewarded: on ad impression");
        }
        
        private void RewardedVideoOnAdFullScreenContentClosedEvent()
        {
            KillRewardedAds(_activePlacement);
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Closed,
                PlacementType = PlacementType.Rewarded,
                SdkName = AdmobSdk,
            });
            
            Debug.Log("[ADS SERVICE]: Rewarded: on ad full screen closed");
            // CompleteRewardedVideo(false,AdsMessages.RewardedClosed);
        }
        
        private void RewardedVideoOnAdFullScreenContentOpenedEvent()
        {
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = string.Empty,
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Rewarded,
                SdkName = AdmobSdk,
            });
            
            Debug.Log("[ADS SERVICE]: Rewarded: on ad full screen opened");
        }
        
        private void RewardedVideoOnAdFullScreenContentFailedEvent(AdError error)
        {
            CompleteRewardedVideoAsync(new AdmobRewardedResult
            {
                Reward = null,
                PlacementId = _activePlacement,
                Message = string.Empty,
                Error = error,
            }).Forget();
        }

        private void KillRewardedAds(string placementId)
        {
            if (!_rewardedAdsCache.TryGetValue(placementId, out var adsRewardedAd))
                return;
            
            var rewardedAd = adsRewardedAd.RewardedAd;
            if(rewardedAd == null) return;
            
            UnsubscribeToRewardedAdEvents(rewardedAd);
            
            rewardedAd.Destroy();
            adsRewardedAd.RewardedAd = null;

            LoadRewardedAd(placementId).Forget();
        }
        
        private void ApplyRewardedCommand(AdsShowResult result)
        {
            _rewardedHistory.Add(result);
            _awaitedRewards.TryAdd(result.PlacementName, result);
        }
        
        #endregion
        
        #region interstitial block

        private async UniTask ShowInterstitialVideo(string placeId)
        {
            if (_interstitialAdCache == null)
            {
                var loaded = await LoadInterstitialAd(placeId);

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
                SdkName = AdmobSdk,
            });
            
            Debug.Log("Interstitial: on ad clicked");
        }
        
        private void InterstitialVideoOnAdPaidEvent(AdValue adValue)
        {
            var placementId = _interstitialAdCache.GetAdUnitID();
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = placementId,
                Message = "Paid",
                ActionType = PlacementActionType.Rewarded,
                PlacementType = PlacementType.Interstitial,
                SdkName = AdmobSdk,
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
                SdkName = AdmobSdk,
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
                SdkName = AdmobSdk,
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
                SdkName = AdmobSdk,
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

    public struct AdmobRewardedResult
    {
        public Reward Reward;
        public AdError Error;
        public string PlacementId;
        public string Message;
    }
}

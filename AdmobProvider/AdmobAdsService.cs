namespace UniGame.Ads.Runtime
{
    using UnityEngine;
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using Game.Modules.unigame.ads.Shared;
    using GoogleMobileAds.Api;
    using R3;
    using UniCore.Runtime.ProfilerTools;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;
    using UniGame.Runtime.Rx;

    [Serializable]
    public class AdmobAdsService : IAdsService
    {
#if UNITY_EDITOR || GAME_DEBUG
#if UNITY_ANDROID
        private const string TestInterstitialPlacementId = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewardedPlacementId =     "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
        private const string TestInterstitialPlacementId =  "ca-app-pub-3940256099942544/4411468910";
        private const string TestRewardedPlacementId =      "ca-app-pub-3940256099942544/5224354917";
#endif
#endif
        public const string AdmobSdk = "admob";

        private LifeTime _lifeTime;
        private string _platformName;
        private ReactiveValue<bool> _isInitialized = new();

        private string _activePlacement = string.Empty;
        private bool _isInProgress;
        private float _reloadAdsInterval;
        private float _lastAdsReloadTime;
        private bool _loadingAds;

        private List<AdsShowResult> _rewardedHistory = new();
        private Dictionary<string,AdsShowResult> _awaitedRewards = new();
        private ReactiveCommand<AdsShowResult> _applyRewardedCommand = new();
        private Subject<AdsActionData> _adsAction = new();
        private Dictionary<string,PlatformAdsPlacement> _placements = new();

        private InterstitialAd _interstitialAdCache = null;

        private Dictionary<string, AdmobRewardedAdsCache> _rewardedAdsCache = new();

        public AdmobAdsService(
            string platformName,
            AdsDataConfiguration config,
            Dictionary<string,PlatformAdsPlacement> placements)
        {
            GameLog.Log($"[ADS SERVICE]: admob created");

            _platformName = platformName;
            _lifeTime = new LifeTime();
            _reloadAdsInterval = config.reloadAdsInterval;
            _lastAdsReloadTime = -_reloadAdsInterval;
            _placements = placements;
 
            SubscribeToEvents();
        }

        public ILifeTime LifeTime => _lifeTime;

        public virtual bool RewardedAvailable => true;
        public virtual bool InterstitialAvailable => true;
        public Observable<AdsActionData> AdsAction => _adsAction;
        public bool IsInProgress => _isInProgress;

        public async UniTask InitializeAsync()
        {
            Debug.Log($"[ADS SERVICE]: admob initialization started");
            
            MobileAds.Initialize(SdkInitializationCompletedEvent);
            
            var isInitialized = await _isInitialized
                .Where(x => x)
                .FirstAsync(_lifeTime.Token);

            Debug.Log($"[ADS SERVICE]: admob initialized {isInitialized}");

            foreach (PlatformAdsPlacement placementData in _placements.Values)
            {
                switch (placementData.placementType)
                {
                    case PlacementType.Interstitial:
                        LoadInterstitialAd(placementData.platformPlacement).Forget();
                        break;
                    case PlacementType.Rewarded:
                        _rewardedAdsCache.Add(placementData.id, new AdmobRewardedAdsCache(placementData.platformPlacement));
                        LoadRewardedAd(placementData.id).Forget();
                        break;
                }
            }

            _applyRewardedCommand
                .Subscribe(ApplyRewardedCommand)
                .AddTo(_lifeTime);
            
            LoadAdsAsync()
                .AttachExternalCancellation(_lifeTime.Token)
                .Forget();
        }

        public void LoadAdsAction(AdsActionData actionData)
        {
            if (actionData.PlacementType == PlacementType.Interstitial && actionData.ActionType == PlacementActionType.Closed)
                LoadInterstitialAd(actionData.PlacementName).Forget();
            
            Debug.Log($"[ADS SERVICE]:admob action: NAME:{actionData.PlacementName} ERROR:{actionData.ErrorCode} MESSAGE:{actionData.Message}");
        }

        public async UniTask<bool> LoadRewardedAd(string placementId)
        {
            if (!_placements.TryGetValue(placementId, out var placementData))
                return false;
            
            GameLog.Log($"Loading the rewarded ad {placementId}. " +
                      $"Load:{_rewardedAdsCache[placementId].LoadProcess}. " +
                      $"Cache:{_rewardedAdsCache[placementId].RewardedAd}");
            
            if(!_rewardedAdsCache.ContainsKey(placementId))
                throw new Exception($"{placementId} not found into map");
            
            if (_rewardedAdsCache[placementId].LoadProcess == true)
            {
                await UniTask
                    .WaitWhile(_rewardedAdsCache, x => x[placementId].LoadProcess == true,cancellationToken:_lifeTime);
            }
            
            var adsRewardedAd = _rewardedAdsCache[placementId].RewardedAd;
            
            if (adsRewardedAd != null) return true;

            var adRequest = new AdRequest();
            var loadComplete = false;
            var loaded = false;
            var cppId = GetPlatformPlacementID(placementId); 
            _rewardedAdsCache[placementId].LoadProcess = true;

            Debug.Log($"[ADS SERVICE]:Loading cppId: {cppId}");
            RewardedAd.Load(cppId, adRequest, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad " + "with error : " + error);
                    loadComplete = true;
                    return;
                }

                Debug.Log("[ADS SERVICE]:Rewarded ad loaded with response : " + ad.GetResponseInfo());

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

        private string GetPlatformPlacementID(string placementId)
        {
#if UNITY_EDITOR || GAME_DEBUG
            return TestRewardedPlacementId;
#else
            return _placements[placementId].platformPlacement;
#endif
        }

        public async UniTask<bool> LoadInterstitialAd(string placementId)
        {
#if UNITY_EDITOR || GAME_DEBUG
            placementId = TestInterstitialPlacementId;
#endif
            Debug.Log($"Interstitial placement loading: {placementId}");
            
            if (_interstitialAdCache != null)
            {
                _interstitialAdCache.Destroy();
                _interstitialAdCache = null;
            }

            var adRequest = new AdRequest();
            var loadComplete = false;
            var loaded = false;
            
            InterstitialAd.Load(placementId, adRequest,
                (ad, error) =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogError("interstitial ad failed to load an ad " + "with error : " + error);
                        loadComplete = true;

                        return;
                    }

                    Debug.Log("Interstitial ad loaded with response : " + ad.GetResponseInfo());

                    _interstitialAdCache = ad;
                    loadComplete = true;
                    loaded = true;
                });

            await UniTask
                .WaitWhile(() => loadComplete == false)
                .AttachExternalCancellation(_lifeTime.Token);

            return loaded;
        }

        public async UniTask<AdsShowResult> Show(string placementId)
        {
            if (!_placements.TryGetValue(placementId, out var placementItem))
            {
                GameLog.LogError($"[ADS SERVICE]: Placement not found: {placementId}");
                
                return new AdsShowResult
                {
                    Error = true,
                    Message = AdsMessages.PlacementNotFound,
                    PlacementType = PlacementType.Rewarded,
                    PlacementName = string.Empty,
                    Rewarded = false,
                };
            }
            
            var showResult = await Show(placementItem.platformPlacement, PlacementType.Rewarded);
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

            ShowPlacementAsync(placeId, type).Forget();

            await UniTask
                .WaitWhile(() => !_awaitedRewards.ContainsKey(placeId))
                .AttachExternalCancellation(_lifeTime.Token);
            
            await UniTask.SwitchToMainThread();
            
            AdsShowResult placementResult = _awaitedRewards[placeId];
            
            _awaitedRewards.Remove(placeId);
            _isInProgress = false;
            
            Debug.Log($"[ADS SERVICE]: Show {placeId} {type} result: {placementResult.Error} {placementResult.Message}");
            
            return placementResult;
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
            PlatformAdsPlacement adsPlacement = null;
            foreach (var placement in _placements)
            {
                var placementValue = placement.Value;
                if(placementValue.placementType != type)
                    continue;
                var isAvailable = await IsPlacementAvailable(placementValue.platformPlacement);
                if(!isAvailable) continue;
                adsPlacement = placementValue;
                break;
            }

            if (adsPlacement == null)
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
            
            var showResult = await Show(adsPlacement.platformPlacement, PlacementType.Rewarded);
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
            if(_placements.TryGetValue(placementName,out PlatformAdsPlacement adsPlacement) == false)
            {
                Debug.Log($"[ADS SERVICE]:Placement haven't {placementName}");
                return false;
            }
            
            var placementType = adsPlacement.placementType;
            
            switch (placementType)
            {
                case PlacementType.Rewarded:
                    return await LoadRewardedAd(placementName);
                case PlacementType.Interstitial:
                    return await LoadInterstitialAd(adsPlacement.platformPlacement);
            }
            
            return false;
        }

        public async UniTask<bool> IsPlacementAvailable(PlacementType placementName)
        {
            foreach (var platformAdsPlacement in _placements)
            {
                var value = platformAdsPlacement.Value;
                if(value.placementType != placementName)
                    continue;
                var isAvailable = await IsPlacementAvailable(value.platformPlacement);
                if(isAvailable) return true;
            }

            return false;
        }

        public virtual async UniTask LoadAdsAsync()
        {
            Debug.Log("[ADS SERVICE]:Do not implement preload into admob");
        }
        
        public void Dispose()
        {
            Debug.Log("[ADS SERVICE]:Dispose");
            _lifeTime.Terminate();

            foreach (var (key, val) in _rewardedAdsCache)
                UnsubscribeToRewardedAdEvents(_rewardedAdsCache[key].RewardedAd);
            
            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
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
            }
            
            if (_interstitialAdCache.CanShowAd())
            {
                Debug.Log("Showing interstitial ad.");
                SubscribeToInterstitialAdEvents(_interstitialAdCache);
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
            var message = "Interstitial: on ad clicked";
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = message,
                ActionType = PlacementActionType.Clicked,
                PlacementType = PlacementType.Rewarded,
                SdkName = AdmobSdk,
            });

            Debug.Log(message);
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
            var message = "Interstitial: on ad full screen closed";
            
            _adsAction.OnNext(new AdsActionData
            {
                PlacementName = _activePlacement,
                Message = message,
                ActionType = PlacementActionType.Closed,
                PlacementType = PlacementType.Interstitial,
                SdkName = AdmobSdk,
            });

            Debug.Log(message);
            AddPlacementResult(_activePlacement, PlacementType.Interstitial, true, false, message);
            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
            _interstitialAdCache.Destroy();
        }
        private void InterstitialVideoOnAdFullScreenContentOpenedEvent()
        {
            var message = "Interstitial: on ad full screen opened";
            
            _adsAction.OnNext(new AdsActionData
            {
                PlacementName = _activePlacement,
                Message = message,
                ActionType = PlacementActionType.Opened,
                PlacementType = PlacementType.Interstitial,
                SdkName = AdmobSdk,
            });

            Debug.Log(message);
        }
        private void InterstitialVideoOnAdFullScreenContentFailedEvent(AdError error)
        {
            var message = "Interstitial: on ad full screen failed";
            
            _adsAction.OnNext(new AdsActionData()
            {
                PlacementName = _activePlacement,
                Message = message,
                ActionType = PlacementActionType.Failed,
                PlacementType = PlacementType.Interstitial,
                SdkName = AdmobSdk,
            });
            
            AddPlacementResult(_activePlacement, PlacementType.Interstitial, false, true, message);
            UnsubscribeToInterstitialAdEvents(_interstitialAdCache);
            _interstitialAdCache.Destroy();
            Debug.Log(message);
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

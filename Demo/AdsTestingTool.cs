using UnityEngine;

namespace Game.Sandbox.AdsCommands
{
    using Cysharp.Threading.Tasks;
    using Runtime.Game.Liveplay.Ads.Runtime;
    using UniGame.Core.Runtime;
    using UniModules.UniGame.Core.Runtime.DataFlow.Extensions;
    using UniRx;
    using UnityEngine.UI;
    using Zenject;

    public class AdsTestingTool : MonoBehaviour
    {

        public Button showRewarded;
        public Button showInterstitial;
        public Button validateIntegration;
        
        public PlacementAdsId rewardedPlacement;
        public PlacementAdsId interstitialPlacement;
        
        private bool _isInitialized;
        private ILifeTime _lifeTime;
        private IAdsService _adsController;

        [Inject]
        public void Initialize(IAdsService adsController)
        {
            _adsController = adsController;
            _lifeTime = this.GetAssetLifeTime();
            
            showRewarded.onClick
                .AsObservable()
                .Subscribe(_ => ShowRewarded())
                .AddTo(_lifeTime);
                
            showInterstitial.onClick
                .AsObservable()
                .Subscribe(_ => ShowInterstitial())
                .AddTo(_lifeTime);
            
            validateIntegration.onClick
                .AsObservable()
                .Subscribe(_ => adsController.ValidateIntegration())
                .AddTo(_lifeTime);
            
            _isInitialized = true;
        }
        
        public void ShowRewarded()
        {
            _adsController.Show(rewardedPlacement).Forget();
        }
        
        public void ShowInterstitial()
        {
            _adsController.Show(interstitialPlacement).Forget();
        }

    }
}

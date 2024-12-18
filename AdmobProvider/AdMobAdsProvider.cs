namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using Sirenix.OdinInspector;
    using UniGame.Core.Runtime;

    [Serializable]
    public class AdMobAdsProvider : AdsProvider
    {
        [InlineProperty]
        public AdmobAdsConfig adsMobConfig = new();
        
        public override UniTask<IAdsService> Create(IContext context)
        {
            var service = new AdmobAdsService(adsMobConfig);
            return UniTask.FromResult<IAdsService>(service);
        }
    }
}
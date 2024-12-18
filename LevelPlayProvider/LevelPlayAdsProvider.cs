namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameLib.AdsCore.Config;
    using UniGame.Core.Runtime;

    [Serializable]
    public class LevelPlayAdsProvider : AdsProvider
    {
        public LevelPlayAdsConfig levelPlayAdsConfig = new();
        
        public override UniTask<IAdsService> Create(IContext context)
        {
            var adsService = new LevelPlayAdsService(levelPlayAdsConfig);
            return UniTask.FromResult<IAdsService>(adsService);
        }
    }
}
namespace GameLib.AdsCore.Config
{
    using System;
    using Cysharp.Threading.Tasks;
    using UniGame.Ads.Runtime;
    using UniGame.Core.Runtime;

    [Serializable]
    public abstract class AdsProvider
    {
        public string providerName;
        public abstract UniTask<IAdsService> Create(IContext context);
    }
}
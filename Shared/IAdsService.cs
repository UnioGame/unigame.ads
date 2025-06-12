namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
	using System;
	using Cysharp.Threading.Tasks;
	using R3;
	using UniGame.GameFlow.Runtime;

	public interface IAdsService : IGameService, IDisposable
	{
		bool RewardedAvailable { get; }
		bool InterstitialAvailable { get; }
		
		Observable<AdsActionData> AdsAction { get; }

		void ValidateIntegration();

		UniTask<bool> IsPlacementAvailable(string placementName);
		
		UniTask LoadAdsAsync();

		UniTask<AdsShowResult> Show(string placement, PlacementType type);

		UniTask<AdsShowResult> Show(PlacementType type);
		
		UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId);
		
		UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId);
		
		
	}
}
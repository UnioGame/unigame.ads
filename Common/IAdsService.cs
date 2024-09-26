namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
	using System;
	using Cysharp.Threading.Tasks;

	public interface IAdsService : IDisposable
	{
		bool RewardedAvailable { get; }
		bool InterstitialAvailable { get; }
		IObservable<AdsActionData> AdsAction { get; }

		void ValidateIntegration();

		bool IsPlacementAvailable(string placementName);
		
		UniTask LoadAdsAsync();

		UniTask<AdsShowResult> Show(PlacementAdsId placement);
		
		UniTask<AdsShowResult> Show(string placement, PlacementType type);

		UniTask<AdsShowResult> Show(PlacementType type);
		
		UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId);
		UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId);
		
		
	}
}
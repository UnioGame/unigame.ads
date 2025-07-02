namespace UniGame.Ads.Runtime
{
	using Cysharp.Threading.Tasks;
	using R3;
	using UniGame.GameFlow.Runtime;

	public interface IAdsService : IGameService
	{
		bool RewardedAvailable { get; }
		
		bool InterstitialAvailable { get; }
		
		Observable<AdsActionData> AdsAction { get; }

		void ValidateIntegration();

		/// <summary>
		/// placementName is a string that represents the name of the
		/// ad placement as an ID of placements ot for the target platform.
		/// </summary>
		/// <param name="placementName"></param>
		/// <returns></returns>
		UniTask<bool> IsPlacementAvailable(string placementName);
		
		UniTask<bool> IsPlacementAvailable(PlacementType placementName);
		
		UniTask LoadAdsAsync();

		/// <summary>
		/// placement is a string that represents the name of the
		/// ad_placement as an global ID of placements ot for the target platform.
		/// </summary>
		UniTask<AdsShowResult> Show(string placement, PlacementType type);

		UniTask<AdsShowResult> Show(PlacementType type);
		
		UniTask<AdsShowResult> ShowRewardedAdAsync(string placeId);
		
		UniTask<AdsShowResult> ShowInterstitialAdAsync(string placeId);
		
		
	}
}
namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
	using System;

	[Serializable]
	public class AdsShowResult
	{
		public string PlacementName = string.Empty;
		public string RewardName = string.Empty;
		public float RewardAmount = 0.0f;
		public PlacementType PlacementType = PlacementType.Rewarded;
		public bool Rewarded = false;
		public bool Error = false;
		public string Message = string.Empty;
	}
}
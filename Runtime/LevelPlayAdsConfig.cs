namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
	using System;
	using System.Collections.Generic;
	using Sirenix.OdinInspector;

	[Serializable]
	public class LevelPlayAdsConfig
	{
		public bool enableAds = true;
		
		public string LivePlayAppKey = "1ece5c335";
	
		public bool validateIntegration = true;
		public bool shouldTrackNetworkState = false;
		public float reloadAdsInterval = 30f;

		[BoxGroup("placements")]
		[InlineEditor]
		[HideLabel]
		public PlacementIdDataAsset placementIds;
	}
}
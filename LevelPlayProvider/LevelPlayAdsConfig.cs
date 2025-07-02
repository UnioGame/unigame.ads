namespace UniGame.Ads.Runtime
{
	using System;
	using System.Collections.Generic;
	using Sirenix.OdinInspector;

	[Serializable]
	public class LevelPlayAdsConfig
	{
		public string livePlayAppKey = "put_your_levelplay_app_key_here";
		public bool validateIntegration = true;
		public bool shouldTrackNetworkState = false;
		public float reloadAdsInterval = 30f;
	}
}
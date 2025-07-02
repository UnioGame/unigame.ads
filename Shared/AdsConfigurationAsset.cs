namespace UniGame.Ads.Runtime
{
    using Sirenix.OdinInspector;
    using UnityEngine;

    [CreateAssetMenu(menuName = "UniGame/Ads/Configuration", fileName = "AdsConfiguration")]
    public class AdsConfigurationAsset : ScriptableObject
    {
        [InlineProperty]
        [HideLabel]
        public AdsConfiguration configuration = new ();
    }
}
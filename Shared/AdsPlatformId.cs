namespace UniGame.Ads.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sirenix.OdinInspector;
    using UnityEngine;

#if UNITY_EDITOR
    using UniModules.Editor;
#endif

    [Serializable]
    [ValueDropdown("@UniGame.Ads.Runtime.AdsPlatformId.GetPlacementIds()", IsUniqueList = true, 
        DropdownTitle = "PlacementId")]
    public struct AdsPlatformId : IEquatable<string>
    {
        [SerializeField]
        public string value;

        #region static editor data

        private static AdsConfigurationAsset _dataAsset;

        public static IEnumerable<ValueDropdownItem<AdsPlatformId>> GetPlacementIds()
        {
#if UNITY_EDITOR
             _dataAsset ??= AssetEditorTools.GetAsset<AdsConfigurationAsset>();
            var config = _dataAsset.configuration.providers;
            if (config == null)
            {
                yield return new ValueDropdownItem<AdsPlatformId>()
                {
                    Text = "EMPTY",
                    Value = (AdsPlatformId)string.Empty,
                };
                yield break;
            }

            foreach (var provider in config)
            {
                yield return new ValueDropdownItem<AdsPlatformId>()
                {
                    Text = provider.adsPlatformName,
                    Value = (AdsPlatformId)provider.adsPlatformName,
                };
            }
#endif
            yield break;
        }

        public static string GetPlacementIdName(AdsPlatformId slotId)
        {
#if UNITY_EDITOR
            var types = GetPlacementIds();
            var filteredTypes = types
                .FirstOrDefault(x => x.Value == slotId);
            var slotName = filteredTypes.Text;
            return string.IsNullOrEmpty(slotName) ? string.Empty : slotName;
#endif
            return string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            _dataAsset = null;
        }

        #endregion

        public static implicit operator string(AdsPlatformId v)
        {
            return v.value;
        }

        public static explicit operator AdsPlatformId(string v)
        {
            return new AdsPlatformId { value = v };
        }

        public override string ToString() => value;

        public override int GetHashCode() => value == null ? 0 : value.GetHashCode();

        public AdsPlatformId FromString(string data)
        {
            value = data;

            return this;
        }

        public bool Equals(string other)
        {
            if (string.IsNullOrEmpty(value)) return value == other;
            return value.Equals(other, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (obj is AdsPlatformId mask)
                return mask.value == value;

            return false;
        }
    }
}
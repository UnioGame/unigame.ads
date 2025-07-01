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
    [ValueDropdown("@UniGame.Ads.Runtime.AdsPlacementId.GetPlacementIds()", IsUniqueList = true, DropdownTitle = "PlacementId")]
    public struct AdsPlacementId
    {
        [SerializeField]
        public int value;

        #region static editor data

        private static AdsDataConfigurationAsset _dataAsset;

        public static IEnumerable<ValueDropdownItem<AdsPlacementId>> GetPlacementIds()
        {
#if UNITY_EDITOR
             _dataAsset ??= AssetEditorTools.GetAsset<AdsDataConfigurationAsset>();
            var config = _dataAsset.configuration;
            if (config == null)
            {
                yield return new ValueDropdownItem<AdsPlacementId>()
                {
                    Text = "EMPTY",
                    Value = (AdsPlacementId)0,
                };
                yield break;
            }

            foreach (var type in config.placementData.placements)
            {
                yield return new ValueDropdownItem<AdsPlacementId>()
                {
                    Text = type.name,
                    Value = (AdsPlacementId)type.id,
                };
            }
#endif
            yield break;
        }

        public static string GetPlacementIdName(AdsPlacementId slotId)
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

        public static implicit operator int(AdsPlacementId v)
        {
            return v.value;
        }

        public static explicit operator AdsPlacementId(int v)
        {
            return new AdsPlacementId { value = v };
        }

        public override string ToString() => value.ToString();

        public override int GetHashCode() => value;

        public AdsPlacementId FromInt(int data)
        {
            value = data;

            return this;
        }

        public override bool Equals(object obj)
        {
            if (obj is AdsPlacementId mask)
                return mask.value == value;

            return false;
        }
    }
}
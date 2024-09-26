namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sirenix.OdinInspector;
    using UniModules.Editor;
    using UnityEngine;

    [Serializable]
    [ValueDropdown("@Game.Runtime.Game.Liveplay.Ads.Runtime.PlacementAdsId.GetPlacementIds()", IsUniqueList = true, DropdownTitle = "PlacementId")]
    public partial struct PlacementAdsId
    {
        [SerializeField]
        public int value;

        #region static editor data

        private static PlacementIdDataAsset _dataAsset;

        public static IEnumerable<ValueDropdownItem<PlacementAdsId>> GetPlacementIds()
        {
#if UNITY_EDITOR
            _dataAsset ??= AssetEditorTools.GetAsset<PlacementIdDataAsset>();
            var types = _dataAsset;
            if (types == null)
            {
                yield return new ValueDropdownItem<PlacementAdsId>()
                {
                    Text = "EMPTY",
                    Value = (PlacementAdsId)0,
                };
                yield break;
            }

            foreach (var type in types.Types)
            {
                yield return new ValueDropdownItem<PlacementAdsId>()
                {
                    Text = type.Name,
                    Value = (PlacementAdsId)type.Id,
                };
            }
#endif
            yield break;
        }

        public static string GetPlacementIdName(PlacementAdsId slotId)
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

        public static implicit operator int(PlacementAdsId v)
        {
            return v.value;
        }

        public static explicit operator PlacementAdsId(int v)
        {
            return new PlacementAdsId { value = v };
        }

        public override string ToString() => value.ToString();

        public override int GetHashCode() => value;

        public PlacementAdsId FromInt(int data)
        {
            value = data;

            return this;
        }

        public override bool Equals(object obj)
        {
            if (obj is PlacementAdsId mask)
                return mask.value == value;

            return false;
        }
    }
}
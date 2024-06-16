namespace Game.Runtime.Game.Liveplay.Ads.Runtime
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Sirenix.OdinInspector;
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [CreateAssetMenu(menuName = "Game/AdsPlacementId Data Asset", fileName = "AdsPlacementId  Data Asset")]
    public class PlacementIdDataAsset : ScriptableObject
    {
        [InlineProperty]
        public List<AdsPlacementId> Types = new List<AdsPlacementId>();

        #region IdGenerator

#if UNITY_EDITOR
        [Button("Generate Static Properties")]
        public void GenerateProperties()
        {
            GenerateStaticProperties(this);
        }

        public static void GenerateStaticProperties(PlacementIdDataAsset dataAsset)
        {
            var idType = typeof(PlacementAdsId);
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(dataAsset));
            var directoryPath = Path.GetDirectoryName(scriptPath);
            var outputPath = Path.Combine(directoryPath, "Generated");
            var outputFileName = "PlacementAdsId.Generated.cs";

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var namespaceName = idType.Namespace;

            var filePath = Path.Combine(outputPath, outputFileName);
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine($"namespace {namespaceName}");
                writer.WriteLine("{");
                writer.WriteLine("    public partial struct PlacementAdsId");
                writer.WriteLine("    {");

                var typesField = typeof(PlacementIdDataAsset).GetField("Types",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (typesField != null)
                {
                    var types = (List<AdsPlacementId>)typesField.GetValue(dataAsset);
                    foreach (var type in types)
                    {
                        var propertyName = type.Name.Replace(" ", "");
                        writer.WriteLine(
                            $"        public static PlacementAdsId {propertyName} => new PlacementAdsId {{ value = {type.Id} }};");
                    }
                }

                writer.WriteLine("    }");
                writer.WriteLine("}");
            }

            AssetDatabase.Refresh();
            Debug.Log("Partial class with static properties generated successfully.");
        }
#endif

        #endregion
    }
}
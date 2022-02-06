// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter
{
    /// <summary>
    /// Scriptable Object holding Bridge settings,
    /// autocreated when needed.
    /// </summary>
    public class FigmaSettings : ScriptableObject
    {
        public const string FigmaFolder = "FigmaToolkit";
        public static readonly string FigmaFolderPath = Path.Combine(AssetsFolder, FigmaFolder);
        public const string GeneratedFolder = "Resources";
        public const string GeneratedAssetsFolder = "Assets";
        public const string AssetsFolder = "Assets";
        public static readonly string FigmaBasePath = Path.Combine(AssetsFolder, FigmaFolder, GeneratedFolder);
        public const string PackageName = "com.risd.figmabridge";
        public static readonly string PackagePath = Path.Combine("Packages", PackageName);
        public static readonly string FigmaStylesheetPath = Path.Combine(PackagePath, Path.Combine("Editor", "FigmaSettingsInspector.uss"));

        private const string AssetName = "FigmaToolkitSettings";
        private static readonly string FigmaSettingsPath = Path.Combine(FigmaBasePath, $"{AssetName}.asset");

        public const float PositionScale = 0.00033333f;
        public const string FigmaBaseURL = "https://api.figma.com/v1";

        public string FigmaToken;
        public FigmaToolkitCustomMap DefaultCustomMap;

        private static void OpenSettings()
        {
            Selection.activeObject = EditorGetOrCreateSettings();
        }
        public static bool TryLoadProjectSettings(out FigmaSettings settings)
        {
            settings = Resources.Load<FigmaSettings>(AssetName);
            return settings != null;
        }
        public static FigmaSettings EditorGetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FigmaSettings>(FigmaSettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(Path.Combine(AssetsFolder,FigmaFolder)))
                {
                    AssetDatabase.CreateFolder(AssetsFolder, FigmaFolder);
                    AssetDatabase.Refresh();
                }

                if (!AssetDatabase.IsValidFolder(Path.Combine(AssetsFolder, FigmaFolder,GeneratedFolder)))
                {
                    AssetDatabase.CreateFolder(Path.Combine(AssetsFolder, FigmaFolder), GeneratedFolder);
                    AssetDatabase.Refresh();
                }

                FigmaSettings newSettings = ScriptableObject.CreateInstance<FigmaSettings>();
                newSettings.name = AssetName;
                AssetDatabase.CreateAsset(newSettings, FigmaSettingsPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                settings = AssetDatabase.LoadAssetAtPath<FigmaSettings>(FigmaSettingsPath);
            }

            return settings;
        }
    }
}
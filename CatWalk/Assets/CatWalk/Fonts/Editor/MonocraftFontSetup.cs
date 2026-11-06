using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CatWalk.UI.Editor
{
    public static class MonocraftFontSetup
    {
        public const string FontPath = "Assets/CatWalk/Fonts/Monocraft.ttf";
        public const string AssetPath = "Assets/CatWalk/Fonts/Resources/Fonts & Materials/Monocraft.asset";

        // Printable ASCII, all 33 Russian letters in both cases, and common punctuation.
        static string Characters => new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray())
            + "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя"
            + "\u00A0«»–—‘’“”„•…₽";

        [MenuItem("Tools/CatWalk/Fonts/Configure Monocraft")]
        public static void Configure()
        {
            var settings = TMP_Settings.instance;
            if (!settings) throw new InvalidOperationException("Import TMP Essential Resources first.");

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (!asset)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                if (!source) throw new InvalidOperationException("Monocraft.ttf is missing.");

                // Monocraft's em is nine pixels; 36 preserves its grid at a 4x scale.
                FontEngine.InitializeFontEngine();
                asset = TMP_FontAsset.CreateFontAsset(source, 36, 2, GlyphRenderMode.RASTER,
                    512, 512, AtlasPopulationMode.Dynamic, false);
                if (!asset) throw new InvalidOperationException("Could not create the Monocraft font asset.");
                if (!asset.TryAddCharacters(Characters, out string missing))
                    throw new InvalidOperationException("Monocraft atlas is missing characters: " + missing);

                asset.name = "Monocraft";
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                asset.atlasTextures[0].name = "Monocraft Atlas";
                asset.atlasTextures[0].filterMode = FilterMode.Point;
                asset.atlasTextures[0].wrapMode = TextureWrapMode.Clamp;
                asset.material.name = "Monocraft Material";
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                EditorUtility.SetDirty(asset.atlasTextures[0]);
                EditorUtility.SetDirty(asset.material);
                EditorUtility.SetDirty(asset);
            }

            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = asset;
            serialized.FindProperty("m_defaultFontSize").floatValue = 36;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Validate();
            Selection.activeObject = asset;
        }

        [MenuItem("Tools/CatWalk/Fonts/Validate Monocraft")]
        public static void Validate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (!asset || TMP_Settings.defaultFontAsset != asset)
                throw new InvalidOperationException("Monocraft must be the default TMP font.");
            if (!asset.HasCharacters(Characters, out uint[] missing, false, false))
                throw new InvalidOperationException("Missing baked glyphs: " + string.Join(", ", missing));
            if (asset.atlasPopulationMode != AtlasPopulationMode.Static
                || asset.atlasRenderMode != GlyphRenderMode.RASTER
                || asset.atlasTextures[0].filterMode != FilterMode.Point
                || asset.atlasTextures[0].mipmapCount != 1
                || asset.material.shader.name != "TextMeshPro/Mobile/Bitmap")
                throw new InvalidOperationException("Monocraft requires a static, point-filtered bitmap atlas.");

            Debug.Log("Monocraft validated: default TMP font, 66 Russian letters, 52 English letters, digits and punctuation; crisp bitmap atlas.", asset);
        }
    }
}

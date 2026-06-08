using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Cabo.Client.Editor
{
    public static class CaboTextResourceBuilder
    {
        const string FontPath = "Assets/Resources/Fonts/CaboChinese.ttf";
        const string FontAssetPath = "Assets/Resources/Fonts/CaboChineseFont.asset";
        const string TextSettingsPath = "Assets/Resources/CaboPanelTextSettings.asset";

        [MenuItem("Cabo/Rebuild UI Text Resources")]
        public static void Rebuild()
        {
            if (!File.Exists(FontPath))
            {
                Debug.LogError($"[CaboTextResourceBuilder] Missing font file: {FontPath}");
                return;
            }

            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(FontPath) is TrueTypeFontImporter importer)
            {
                importer.includeFontData = true;
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.SaveAndReimport();
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[CaboTextResourceBuilder] Failed to load font: {FontPath}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath) != null)
                AssetDatabase.DeleteAsset(FontAssetPath);

            var fontAsset = FontAsset.CreateFontAsset(
                sourceFont,
                40,
                6,
                GlyphRenderMode.SDFAA,
                4096,
                4096,
                AtlasPopulationMode.Dynamic,
                true);
            fontAsset.name = "CaboChineseFont";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            var characters = CollectProjectCharacters();
            if (!fontAsset.TryAddCharacters(characters, out var missingCharacters, false))
                Debug.LogWarning($"[CaboTextResourceBuilder] Missing {missingCharacters.Length} prewarmed UI character(s).");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            PersistGeneratedSubAssets(fontAsset);
            fontAsset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(fontAsset);

            var textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (textSettings == null)
            {
                textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
                textSettings.name = "CaboPanelTextSettings";
                AssetDatabase.CreateAsset(textSettings, TextSettingsPath);
            }

            textSettings.defaultFontAsset = fontAsset;
            textSettings.fallbackFontAssets = new List<FontAsset>();
            textSettings.clearDynamicDataOnBuild = false;
            textSettings.missingCharacterUnicode = 0x25A1;
            textSettings.displayWarnings = true;
            EditorUtility.SetDirty(textSettings);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var atlasCount = fontAsset.atlasTextures == null ? 0 : fontAsset.atlasTextures.Length;
            var hasAtlas = fontAsset.atlasTexture != null;
            var characterCount = fontAsset.characterTable == null ? 0 : fontAsset.characterTable.Count;
            Debug.Log($"[CaboTextResourceBuilder] Rebuilt UI text resources with {characterCount}/{characters.Length} prewarmed character(s), atlasCount={atlasCount}, hasAtlas={hasAtlas}.");
        }

        static string CollectProjectCharacters()
        {
            var chars = new SortedSet<char>();
            for (var c = 32; c <= 126; c++)
                chars.Add((char)c);

            AddRange(chars, 0x3000, 0x303F);
            AddRange(chars, 0xFF00, 0xFFEF);

            var extensions = new HashSet<string> { ".cs", ".uxml", ".uss", ".json", ".proto", ".txt", ".md" };
            foreach (var path in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                    continue;

                var text = File.ReadAllText(path);
                foreach (var ch in text)
                {
                    if (char.IsControl(ch))
                        continue;
                    if (char.IsSurrogate(ch))
                        continue;
                    chars.Add(ch);
                }
            }

            return new string(new List<char>(chars).ToArray());
        }

        static void AddRange(ISet<char> chars, int start, int end)
        {
            for (var code = start; code <= end; code++)
                chars.Add((char)code);
        }

        static void PersistGeneratedSubAssets(FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            if (fontAsset.material == null)
            {
                var shader = Shader.Find("TextCore/Distance Field") ?? Shader.Find("TextMeshPro/Distance Field");
                if (shader != null)
                {
                    fontAsset.material = new Material(shader)
                    {
                        name = $"{fontAsset.name} Material"
                    };
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{fontAsset.name} Material";
                if (fontAsset.atlasTexture != null)
                    fontAsset.material.mainTexture = fontAsset.atlasTexture;
                if (!AssetDatabase.Contains(fontAsset.material))
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                EditorUtility.SetDirty(fontAsset.material);
            }

            if (fontAsset.atlasTextures == null)
                return;

            for (var i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                var texture = fontAsset.atlasTextures[i];
                if (texture == null)
                    continue;
                texture.name = $"{fontAsset.name} Atlas {i}";
                if (!AssetDatabase.Contains(texture))
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                EditorUtility.SetDirty(texture);
            }
        }
    }
}

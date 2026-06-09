using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    public readonly struct ArtAsset
    {
        public readonly string DisplayName;
        public readonly string AssetPath;
        public readonly string PackName;

        public ArtAsset(string displayName, string assetPath, string packName = "")
        {
            DisplayName = displayName;
            AssetPath = assetPath;
            PackName = packName;
        }
    }

    public static class PlayerProfileStore
    {
        public const string AvatarPrefsKey = "Cabo.SelectedAvatarPath";
        const string AvatarAssetFolder = "Assets/Art/Avatars";
        const string StickerAssetFolder = "Assets/Art/Stickers";
        const string ResourcePrefix = "res://";
        const string ResourceManifestPath = "Art/manifest";

        static readonly List<ArtAsset> AvatarAssets = new();
        static readonly List<ArtAsset> StickerAssets = new();
        static readonly Dictionary<string, Texture2D> TextureCache = new();
        static bool _avatarsScanned;
        static bool _stickersScanned;

        public static string SelectedAvatarPath
        {
            get => PlayerPrefs.GetString(AvatarPrefsKey, "");
            set
            {
                PlayerPrefs.SetString(AvatarPrefsKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public static IReadOnlyList<ArtAsset> GetAvatarAssets()
        {
            if (!_avatarsScanned)
            {
                AvatarAssets.Clear();
                ScanResourceManifest(AvatarAssets, null);
                ScanFlatPngFolder(AvatarAssetFolder, AvatarAssets);
                _avatarsScanned = true;
            }
            return AvatarAssets;
        }

        public static IReadOnlyList<ArtAsset> GetStickerAssets()
        {
            if (!_stickersScanned)
            {
                StickerAssets.Clear();
                ScanResourceManifest(null, StickerAssets);
                ScanStickerFolders(StickerAssetFolder, StickerAssets);
                _stickersScanned = true;
            }
            return StickerAssets;
        }

        public static void RefreshAssetLists()
        {
            _avatarsScanned = false;
            _stickersScanned = false;
        }

        public static string GetAvatarPathForPlayer(long playerId, bool isSelf)
        {
            var avatars = GetAvatarAssets();
            var selected = SelectedAvatarPath;
            if (isSelf && !string.IsNullOrEmpty(selected))
            {
                if (LoadTexture(selected) != null)
                    return selected;

                var selectedName = Path.GetFileNameWithoutExtension(selected);
                foreach (var avatar in avatars)
                    if (string.Equals(avatar.DisplayName, selectedName, StringComparison.OrdinalIgnoreCase))
                        return avatar.AssetPath;
            }
            if (avatars.Count == 0)
                return "";

            var index = Mathf.Abs((int)(playerId % avatars.Count));
            return avatars[index].AssetPath;
        }

        public static string GetStickerAssetPath(string packName, string stickerName)
        {
            if (string.IsNullOrWhiteSpace(packName) || string.IsNullOrWhiteSpace(stickerName))
                return "";

            var stickers = GetStickerAssets();
            foreach (var sticker in stickers)
            {
                if (string.Equals(sticker.PackName, packName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(sticker.DisplayName, stickerName, StringComparison.OrdinalIgnoreCase))
                    return sticker.AssetPath;
            }
            return "";
        }

        public static Texture2D LoadTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            if (TextureCache.TryGetValue(assetPath, out var cached))
                return cached;

            if (assetPath.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var resourcePath = assetPath.Substring(ResourcePrefix.Length);
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                    TextureCache[assetPath] = texture;
                return texture;
            }

            var fullPath = ToFullPath(assetPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return null;

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                texture.name = Path.GetFileNameWithoutExtension(assetPath);
                TextureCache[assetPath] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Profile] Failed to load art asset {assetPath}: {ex.Message}");
                return null;
            }
        }

        public static VisualElement CreateAvatarVisual(string playerName, string avatarPath, int size)
        {
            var avatar = new VisualElement();
            avatar.style.width = size;
            avatar.style.height = size;
            avatar.style.flexShrink = 0;
            avatar.style.borderTopLeftRadius = size / 2;
            avatar.style.borderTopRightRadius = size / 2;
            avatar.style.borderBottomLeftRadius = size / 2;
            avatar.style.borderBottomRightRadius = size / 2;
            avatar.style.borderTopWidth = 1;
            avatar.style.borderRightWidth = 1;
            avatar.style.borderBottomWidth = 1;
            avatar.style.borderLeftWidth = 1;
            avatar.style.borderTopColor = new Color(0.58f, 0.78f, 0.70f);
            avatar.style.borderRightColor = new Color(0.58f, 0.78f, 0.70f);
            avatar.style.borderBottomColor = new Color(0.58f, 0.78f, 0.70f);
            avatar.style.borderLeftColor = new Color(0.58f, 0.78f, 0.70f);
            var fallbackColor = GetFallbackColor(playerName);
            avatar.style.backgroundColor = fallbackColor;
            avatar.style.alignItems = Align.Center;
            avatar.style.justifyContent = Justify.Center;

            var texture = LoadTexture(avatarPath);
            if (texture != null)
            {
                avatar.style.backgroundImage = new StyleBackground(texture);
#pragma warning disable CS0618
                avatar.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
#pragma warning restore CS0618
                return avatar;
            }

            var initial = new Label(GetInitial(playerName));
            initial.style.fontSize = Mathf.Max(12, size / 3);
            initial.style.unityFontStyleAndWeight = FontStyle.Bold;
            initial.style.unityTextAlign = TextAnchor.MiddleCenter;
            initial.style.color = GetReadableTextColor(fallbackColor);
            avatar.Add(initial);
            return avatar;
        }

        public static VisualElement CreateStickerVisual(string stickerPath, int size)
        {
            var sticker = new VisualElement();
            sticker.style.width = size;
            sticker.style.height = size;
            sticker.style.flexShrink = 0;
            sticker.style.backgroundColor = Color.clear;
            var texture = LoadTexture(stickerPath);
            if (texture != null)
            {
                sticker.style.backgroundImage = new StyleBackground(texture);
#pragma warning disable CS0618
                sticker.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore CS0618
            }
            return sticker;
        }

        static void ScanFlatPngFolder(string assetFolder, List<ArtAsset> results)
        {
            var fullFolder = ToFullPath(assetFolder);
            if (string.IsNullOrEmpty(fullFolder))
                return;
            Directory.CreateDirectory(fullFolder);
            foreach (var file in Directory.GetFiles(fullFolder, "*.png", SearchOption.TopDirectoryOnly))
            {
                var assetPath = ToAssetPath(file);
                AddUnique(results, new ArtAsset(Path.GetFileNameWithoutExtension(file), assetPath));
            }
        }

        static void ScanStickerFolders(string assetFolder, List<ArtAsset> results)
        {
            var fullFolder = ToFullPath(assetFolder);
            if (string.IsNullOrEmpty(fullFolder))
                return;
            Directory.CreateDirectory(fullFolder);
            foreach (var file in Directory.GetFiles(fullFolder, "*.png", SearchOption.AllDirectories))
            {
                var pack = Path.GetFileName(Path.GetDirectoryName(file));
                var display = Path.GetFileNameWithoutExtension(file);
                AddUnique(results, new ArtAsset(display, ToAssetPath(file), pack));
            }
        }

        static void ScanResourceManifest(List<ArtAsset> avatars, List<ArtAsset> stickers)
        {
            var manifestAsset = Resources.Load<TextAsset>(ResourceManifestPath);
            if (manifestAsset == null)
                return;

            try
            {
                var manifest = JsonUtility.FromJson<ArtResourceManifest>(manifestAsset.text);
                if (avatars != null && manifest?.avatars != null)
                {
                    foreach (var entry in manifest.avatars)
                        if (!string.IsNullOrWhiteSpace(entry.name) && !string.IsNullOrWhiteSpace(entry.path))
                            AddUnique(avatars, new ArtAsset(entry.name, ResourcePrefix + entry.path));
                }

                if (stickers != null && manifest?.stickers != null)
                {
                    foreach (var entry in manifest.stickers)
                        if (!string.IsNullOrWhiteSpace(entry.pack) && !string.IsNullOrWhiteSpace(entry.name) && !string.IsNullOrWhiteSpace(entry.path))
                            AddUnique(stickers, new ArtAsset(entry.name, ResourcePrefix + entry.path, entry.pack));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Profile] Failed to parse Resources/{ResourceManifestPath}.json: {ex.Message}");
            }
        }

        static void AddUnique(List<ArtAsset> results, ArtAsset asset)
        {
            foreach (var existing in results)
            {
                if (string.Equals(existing.PackName, asset.PackName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.DisplayName, asset.DisplayName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            results.Add(asset);
        }

        static string ToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "";
            var normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return "";
            var relative = normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relative);
        }

        static string ToAssetPath(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');
            var assetsRoot = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                return normalized;
            return "Assets" + normalized.Substring(assetsRoot.Length);
        }

        static string GetInitial(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return "?";
            var trimmed = playerName.Trim();
            return trimmed.Substring(0, 1).ToUpperInvariant();
        }

        static Color GetFallbackColor(string playerName)
        {
            var hash = string.IsNullOrEmpty(playerName) ? 0 : playerName.GetHashCode();
            var palette = new[]
            {
                new Color(0.18f, 0.42f, 0.66f),
                new Color(0.49f, 0.31f, 0.64f),
                new Color(0.27f, 0.56f, 0.41f),
                new Color(0.68f, 0.34f, 0.28f),
                new Color(0.56f, 0.47f, 0.24f),
                new Color(0.28f, 0.50f, 0.56f)
            };
            return palette[Mathf.Abs(hash % palette.Length)];
        }

        static Color GetReadableTextColor(Color background)
        {
            var luminance = 0.2126f * ToLinear(background.r)
                + 0.7152f * ToLinear(background.g)
                + 0.0722f * ToLinear(background.b);
            return luminance > 0.22f ? new Color(0.08f, 0.10f, 0.08f) : Color.white;
        }

        static float ToLinear(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        [Serializable]
        class ArtResourceManifest
        {
            public ArtResourceEntry[] avatars;
            public ArtResourceEntry[] stickers;
        }

        [Serializable]
        class ArtResourceEntry
        {
            public string name;
            public string pack;
            public string path;
        }
    }
}

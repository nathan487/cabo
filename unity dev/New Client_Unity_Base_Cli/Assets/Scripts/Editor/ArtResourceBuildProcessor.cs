using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cabo.Client.Editor
{
    public sealed class ArtResourceBuildProcessor : IPreprocessBuildWithReport
    {
        const string SourceRoot = "Assets/Art";
        const string ResourceRoot = "Assets/Resources/Art";
        const string AvatarSource = SourceRoot + "/Avatars";
        const string StickerSource = SourceRoot + "/Stickers";
        const string AvatarResource = ResourceRoot + "/Avatars";
        const string StickerResource = ResourceRoot + "/Stickers";
        const string ManifestPath = ResourceRoot + "/manifest.json";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            SyncArtResources();
            ValidateSyncedResourcesOrThrow();
            CaboArtCatalogBuilder.RebuildCatalog();
            CaboArtCatalogBuilder.ValidateCatalogOrThrow();
        }

        [MenuItem("Cabo/Sync Art Resources")]
        public static void SyncArtResources()
        {
            var manifest = new ArtManifest();
            Directory.CreateDirectory(ToFullPath(ResourceRoot));

            ResetFolder(AvatarResource);
            ResetFolder(StickerResource);

            CopyAvatars(manifest);
            CopyStickers(manifest);

            ValidateManifestOrThrow(manifest);
            File.WriteAllText(ToFullPath(ManifestPath), JsonUtility.ToJson(manifest, true));
            AssetDatabase.Refresh();
            ConfigureTextureFolder(AvatarResource, 512);
            ConfigureTextureFolder(StickerResource, 512);
            AssetDatabase.Refresh();
            ValidateSyncedResourcesOrThrow();
            Debug.Log($"[Cabo] Synced {manifest.avatars.Count} avatars and {manifest.stickers.Count} stickers into Resources.");
        }

        [MenuItem("Cabo/Validate Synced Art Resources")]
        public static void ValidateSyncedResources()
        {
            ValidateSyncedResourcesOrThrow();
            Debug.Log("[Cabo] Avatar/sticker manifest validation passed.");
        }

        public static void ValidateSyncedResourcesOrThrow()
        {
            string manifestFullPath = ToFullPath(ManifestPath);
            if (!File.Exists(manifestFullPath))
                throw new InvalidOperationException($"Missing art manifest: {ManifestPath}");

            var manifest = JsonUtility.FromJson<ArtManifest>(File.ReadAllText(manifestFullPath));
            ValidateManifestOrThrow(manifest);
            ValidateEntriesExist(manifest.avatars);
            ValidateEntriesExist(manifest.stickers);
        }

        static void CopyAvatars(ArtManifest manifest)
        {
            var source = ToFullPath(AvatarSource);
            if (!Directory.Exists(source))
                return;

            Directory.CreateDirectory(ToFullPath(AvatarResource));
            var files = Directory.GetFiles(source, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var targetAssetPath = $"{AvatarResource}/{Path.GetFileName(file)}";
                File.Copy(file, ToFullPath(targetAssetPath), true);
                manifest.avatars.Add(new ArtEntry
                {
                    name = name,
                    path = $"Art/Avatars/{name}"
                });
            }
        }

        static void CopyStickers(ArtManifest manifest)
        {
            var source = ToFullPath(StickerSource);
            if (!Directory.Exists(source))
                return;

            Directory.CreateDirectory(ToFullPath(StickerResource));
            var files = Directory.GetFiles(source, "*.png", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var pack = Path.GetFileName(Path.GetDirectoryName(file));
                var name = Path.GetFileNameWithoutExtension(file);
                var targetFolder = $"{StickerResource}/{pack}";
                Directory.CreateDirectory(ToFullPath(targetFolder));
                File.Copy(file, ToFullPath($"{targetFolder}/{Path.GetFileName(file)}"), true);
                manifest.stickers.Add(new ArtEntry
                {
                    pack = pack,
                    name = name,
                    path = $"Art/Stickers/{pack}/{name}"
                });
            }
        }

        static void ResetFolder(string assetPath)
        {
            var fullPath = ToFullPath(assetPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            Directory.CreateDirectory(fullPath);
        }

        static void ConfigureTextureFolder(string folder, int maxTextureSize)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = maxTextureSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static void ValidateManifestOrThrow(ArtManifest manifest)
        {
            if (manifest == null)
                throw new InvalidOperationException("Art manifest is not readable JSON.");

            manifest.avatars = manifest.avatars ?? new List<ArtEntry>();
            manifest.stickers = manifest.stickers ?? new List<ArtEntry>();
            var avatarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stickerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < manifest.avatars.Count; i++)
            {
                var entry = manifest.avatars[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.name) || string.IsNullOrWhiteSpace(entry.path))
                    throw new InvalidOperationException($"Avatar manifest entry {i} is incomplete.");
                if (!avatarIds.Add(entry.name))
                    throw new InvalidOperationException($"Duplicate avatar id: {entry.name}");
            }

            for (int i = 0; i < manifest.stickers.Count; i++)
            {
                var entry = manifest.stickers[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.pack)
                    || string.IsNullOrWhiteSpace(entry.name) || string.IsNullOrWhiteSpace(entry.path))
                    throw new InvalidOperationException($"Sticker manifest entry {i} is incomplete.");
                string stableId = entry.pack + "/" + entry.name;
                if (!stickerIds.Add(stableId))
                    throw new InvalidOperationException($"Duplicate sticker id: {stableId}");
            }
        }

        static void ValidateEntriesExist(List<ArtEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                string assetPath = "Assets/Resources/" + entries[i].path + ".png";
                if (!File.Exists(ToFullPath(assetPath)))
                    throw new InvalidOperationException($"Manifest asset is missing: {assetPath}");

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite
                    || importer.mipmapEnabled || importer.maxTextureSize > 512)
                    throw new InvalidOperationException($"Manifest asset has invalid sprite import settings: {assetPath}");
            }
        }

        static string ToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        [Serializable]
        class ArtManifest
        {
            public List<ArtEntry> avatars = new();
            public List<ArtEntry> stickers = new();
        }

        [Serializable]
        class ArtEntry
        {
            public string name;
            public string pack;
            public string path;
        }
    }
}

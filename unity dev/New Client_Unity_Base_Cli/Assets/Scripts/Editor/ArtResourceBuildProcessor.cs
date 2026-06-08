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

            File.WriteAllText(ToFullPath(ManifestPath), JsonUtility.ToJson(manifest, true));
            AssetDatabase.Refresh();
            Debug.Log($"[Cabo] Synced {manifest.avatars.Count} avatars and {manifest.stickers.Count} stickers into Resources.");
        }

        static void CopyAvatars(ArtManifest manifest)
        {
            var source = ToFullPath(AvatarSource);
            if (!Directory.Exists(source))
                return;

            Directory.CreateDirectory(ToFullPath(AvatarResource));
            foreach (var file in Directory.GetFiles(source, "*.png", SearchOption.TopDirectoryOnly))
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
            foreach (var file in Directory.GetFiles(source, "*.png", SearchOption.AllDirectories))
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

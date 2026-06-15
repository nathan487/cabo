using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cabo.Client.Editor
{
    public static class CaboDirectionalTableArtBuilder
    {
        const string BackgroundFolder = "Assets/Art/UI/Backgrounds";
        const string StationFolder = "Assets/Art/UI/TableStations";

        static readonly Color[] ThemeBase =
        {
            new(0.96f, 0.72f, 0.30f),
            new(0.38f, 0.76f, 0.63f),
            new(0.91f, 0.47f, 0.58f),
            new(0.43f, 0.57f, 0.86f)
        };

        [MenuItem("Cabo/Art/Generate Directional Table Art")]
        public static void Generate()
        {
            EnsureAssets(false);
            Debug.Log("[Cabo] Directional table art checked. Existing authored backgrounds and tablecloths were preserved.");
        }

        public static void EnsureAssets(bool overwrite = false)
        {
            Directory.CreateDirectory(ToFullPath(BackgroundFolder));
            Directory.CreateDirectory(ToFullPath(StationFolder));

            WriteBackground("background_cool_night.png", new Color(0.13f, 0.16f, 0.34f), new Color(0.42f, 0.31f, 0.62f), overwrite, 0);
            WriteBackground("background_warm_sunset.png", new Color(0.98f, 0.58f, 0.36f), new Color(1f, 0.86f, 0.55f), overwrite, 1);
            WriteBackground("background_neutral_cream.png", new Color(0.83f, 0.82f, 0.78f), new Color(1f, 0.96f, 0.86f), overwrite, 2);

            string[] views = { "self", "left", "opposite", "right" };
            for (int seat = 0; seat < 4; seat++)
                for (int view = 0; view < views.Length; view++)
                    WriteStation(seat, views[view], view, overwrite);

            AssetDatabase.Refresh();
        }

        static void WriteBackground(string fileName, Color bottom, Color top, bool overwrite, int motif)
        {
            string path = $"{BackgroundFolder}/{fileName}";
            if (!overwrite && File.Exists(ToFullPath(path)))
                return;

            const int width = 1280;
            const int height = 720;
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                Color row = Color.Lerp(bottom, top, Smooth(v));
                for (int x = 0; x < width; x++)
                {
                    float glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x / (float)width, v), new Vector2(0.72f, 0.66f)) * 1.4f);
                    pixels[y * width + x] = Color.Lerp(row, Color.white, glow * 0.13f);
                }
            }

            texture.SetPixels(pixels);
            DrawBackgroundMotifs(texture, motif);
            Save(texture, path);
        }

        static void DrawBackgroundMotifs(Texture2D texture, int motif)
        {
            int w = texture.width;
            int h = texture.height;
            Color light = new(1f, 1f, 1f, motif == 0 ? 0.18f : 0.22f);
            for (int i = 0; i < 24; i++)
            {
                int x = (i * 173 + 91) % w;
                int y = (i * 97 + 53) % h;
                int radius = 5 + i % 9;
                DrawCircle(texture, x, y, radius, light);
            }

            Color band = motif == 0
                ? new Color(0.76f, 0.67f, 1f, 0.16f)
                : motif == 1
                    ? new Color(0.78f, 0.25f, 0.18f, 0.12f)
                    : new Color(0.45f, 0.52f, 0.48f, 0.10f);
            for (int i = 0; i < 7; i++)
                DrawRing(texture, w / 2, h / 2, 170 + i * 42, 3, band);
        }

        static void WriteStation(int seat, string viewName, int view, bool overwrite)
        {
            string path = $"{StationFolder}/station_{seat + 1}_{viewName}.png";
            if (!overwrite && File.Exists(ToFullPath(path)))
                return;

            const int width = 640;
            const int height = 320;
            var texture = NewTexture(width, height);
            Color theme = ThemeBase[seat];
            Color pale = Color.Lerp(theme, Color.white, 0.78f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f);
                    float weave = (Mathf.Sin(u * 44f) + Mathf.Sin(v * 36f)) * 0.018f;
                    pixels[y * width + x] = Color.Lerp(pale, theme, 0.12f + weave);
                }
            }
            texture.SetPixels(pixels);

            DrawFrame(texture, theme);
            DrawCheckPattern(texture, Color.Lerp(theme, Color.white, 0.38f));
            DrawDirectionalOrnament(texture, view, theme);
            Save(texture, path);
        }

        static void DrawFrame(Texture2D texture, Color theme)
        {
            int w = texture.width;
            int h = texture.height;
            Color outer = Color.Lerp(theme, Color.black, 0.35f);
            Color inner = Color.Lerp(theme, Color.white, 0.52f);
            DrawRectBorder(texture, 8, 8, w - 16, h - 16, 7, outer);
            DrawRectBorder(texture, 22, 22, w - 44, h - 44, 3, inner);
        }

        static void DrawCheckPattern(Texture2D texture, Color color)
        {
            for (int x = 44; x < texture.width - 44; x += 52)
                FillRect(texture, x, 28, 2, texture.height - 56, new Color(color.r, color.g, color.b, 0.14f));
            for (int y = 44; y < texture.height - 44; y += 44)
                FillRect(texture, 28, y, texture.width - 56, 2, new Color(color.r, color.g, color.b, 0.14f));
        }

        static void DrawDirectionalOrnament(Texture2D texture, int view, Color theme)
        {
            int cx = texture.width / 2;
            int cy = texture.height / 2;
            int offsetX = 0;
            int offsetY = 0;
            if (view == 0) offsetY = -92;
            else if (view == 1) offsetX = -245;
            else if (view == 2) offsetY = 92;
            else offsetX = 245;

            cx += offsetX;
            cy += offsetY;
            Color dark = Color.Lerp(theme, Color.black, 0.28f);
            DrawCircle(texture, cx, cy, 34, new Color(1f, 1f, 1f, 0.82f));
            DrawRing(texture, cx, cy, 34, 4, dark);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                int px = cx + Mathf.RoundToInt(Mathf.Cos(angle) * 52f);
                int py = cy + Mathf.RoundToInt(Mathf.Sin(angle) * 38f);
                DrawCircle(texture, px, py, 15, new Color(theme.r, theme.g, theme.b, 0.78f));
            }
            DrawCircle(texture, cx, cy, 10, new Color(theme.r, theme.g, theme.b, 0.92f));
        }

        static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "CaboGeneratedArt"
            };
        }

        static void DrawRectBorder(Texture2D texture, int x, int y, int width, int height, int thickness, Color color)
        {
            FillRect(texture, x, y, width, thickness, color);
            FillRect(texture, x, y + height - thickness, width, thickness, color);
            FillRect(texture, x, y, thickness, height, color);
            FillRect(texture, x + width - thickness, y, thickness, height, color);
        }

        static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            int minX = Mathf.Clamp(x, 0, texture.width);
            int maxX = Mathf.Clamp(x + width, 0, texture.width);
            int minY = Mathf.Clamp(y, 0, texture.height);
            int maxY = Mathf.Clamp(y + height, 0, texture.height);
            for (int py = minY; py < maxY; py++)
                for (int px = minX; px < maxX; px++)
                    BlendPixel(texture, px, py, color);
        }

        static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= r2)
                        BlendPixel(texture, cx + x, cy + y, color);
        }

        static void DrawRing(Texture2D texture, int cx, int cy, int radius, int thickness, Color color)
        {
            int outer = radius * radius;
            int innerRadius = Mathf.Max(0, radius - thickness);
            int inner = innerRadius * innerRadius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    int d = x * x + y * y;
                    if (d <= outer && d >= inner)
                        BlendPixel(texture, cx + x, cy + y, color);
                }
        }

        static void BlendPixel(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;
            Color current = texture.GetPixel(x, y);
            texture.SetPixel(x, y, Color.Lerp(current, color, color.a));
        }

        static float Smooth(float value) => value * value * (3f - 2f * value);

        static void Save(Texture2D texture, string assetPath)
        {
            texture.Apply(false, false);
            File.WriteAllBytes(ToFullPath(assetPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static string ToFullPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}

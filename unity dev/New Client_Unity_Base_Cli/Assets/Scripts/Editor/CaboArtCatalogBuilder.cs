using System;
using System.Collections.Generic;
using Cabo.Client.Art;
using UnityEditor;
using UnityEngine;

namespace Cabo.Client.Editor
{
    public static class CaboArtCatalogBuilder
    {
        const string CatalogFolder = "Assets/Resources/Art";
        const string CatalogPath = CatalogFolder + "/CaboArtCatalog.asset";
        const string FoodFolder = "Assets/Art/Cards/Foods";
        const string CardBackPath = "Assets/Art/Cards/Backs/card_back_default.png";
        const string BackgroundFolder = "Assets/Art/UI/Backgrounds";
        const string TableStationFolder = "Assets/Art/UI/TableStations";
        const string SettlementPropsFolder = "Assets/Art/SettlementProps/Pilot";
        const string PomeloPrefabPath = "Assets/Art/Characters/pomelo/PomeloSettlement.prefab";
        const string PomeloPortraitPath = "Assets/Art/Characters/pomelo/Parts/head.png";

        static readonly string[] FileNames =
        {
            "food_00_water.png", "food_01_cucumber.png", "food_02_boiled_egg.png", "food_03_plain_yogurt.png",
            "food_04_corn_cup.png", "food_05_chicken_salad.png", "food_06_wholegrain_sandwich.png",
            "food_07_blueberry_oatmeal.png", "food_08_vegetable_soup.png", "food_09_sushi_bento.png",
            "food_10_banana_milk.png", "food_11_cheese_pizza.png", "food_12_chocolate_cake.png", "food_13_bubble_tea.png"
        };

        static readonly string[] DisplayNames =
        {
            "\u6E05\u6C34\u676F", "\u9EC4\u74DC\u7247", "\u6C34\u716E\u86CB", "\u65E0\u7CD6\u9178\u5976", "\u7389\u7C73\u676F", "\u9E21\u80F8\u8089\u6C99\u62C9", "\u5168\u9EA6\u4E09\u660E\u6CBB",
            "\u84DD\u8393\u71D5\u9EA6\u676F", "\u852C\u83DC\u80FD\u91CF\u6C64", "\u5BFF\u53F8\u4FBF\u5F53", "\u9999\u8549\u725B\u5976", "\u829D\u58EB\u62AB\u8428", "\u5DE7\u514B\u529B\u86CB\u7CD5", "\u8D85\u5927\u676F\u73CD\u73E0\u5976\u8336"
        };

        static readonly string[] Categories =
        {
            "\u96F6\u8D1F\u62C5", "\u6E05\u723D\u852C\u83DC", "\u8F7B\u86CB\u767D", "\u8F7B\u4E73\u5236\u54C1", "\u4E3B\u98DF\u5C11\u91CF", "\u5747\u8861\u8F7B\u98DF", "\u9971\u8179\u4E3B\u98DF",
            "\u8F7B\u751C\u70B9", "\u6696\u5FC3\u8F7B\u98DF", "\u7EFC\u5408\u9910", "\u751C\u996E", "\u9AD8\u70ED\u91CF\u4E3B\u98DF", "\u9AD8\u7CD6\u751C\u70B9", "\u9AD8\u7CD6\u996E\u54C1"
        };

        [MenuItem("Cabo/Rebuild Art Catalog")]
        public static void RebuildCatalog()
        {
            EnsureFolder("Assets/Resources", "Art");
            ConfigureTextureImporters();

            var catalog = AssetDatabase.LoadAssetAtPath<CaboArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CaboArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var foods = new FoodCardDefinition[14];
            for (int value = 0; value < foods.Length; value++)
            {
                foods[value] = new FoodCardDefinition
                {
                    value = value,
                    displayName = DisplayNames[value],
                    category = Categories[value],
                    skillLabel = SkillLabel(value),
                    foodSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FoodFolder}/{FileNames[value]}"),
                    accentColor = Accent(value)
                };
            }

            ConfigurePilotFood(foods[0], "water_full.png", "water_half.png", ConsumePose.Drink);
            ConfigurePilotFood(foods[1], "cucumber_full.png", "cucumber_bitten.png", ConsumePose.Handheld);
            ConfigurePilotFood(foods[2], "boiled_egg_full.png", "boiled_egg_bitten.png", ConsumePose.Handheld);
            ConfigurePilotFood(foods[3], "yogurt_full.png", "yogurt_half.png", ConsumePose.Bowl);
            ConfigurePilotFood(foods[4], "corn_cup_full.png", "corn_cup_half.png", ConsumePose.Bowl);
            ConfigurePilotFood(foods[5], "chicken_salad_full.png", "chicken_salad_half.png", ConsumePose.Bowl);
            ConfigurePilotFood(foods[6], "sandwich_full.png", "sandwich_bitten.png", ConsumePose.Handheld);
            ConfigurePilotFood(foods[7], "blueberry_oatmeal_full.png", "blueberry_oatmeal_half.png", ConsumePose.Bowl);
            ConfigurePilotFood(foods[8], "soup_full.png", "soup_half.png", ConsumePose.Bowl);
            ConfigurePilotFood(foods[9], "sushi_bento_full.png", "sushi_bento_half.png", ConsumePose.Bowl);
            ConfigurePilotFood(foods[10], "banana_milk_full.png", "banana_milk_half.png", ConsumePose.Drink);
            ConfigurePilotFood(foods[11], "pizza_full.png", "pizza_bitten.png", ConsumePose.Handheld);
            ConfigurePilotFood(foods[12], "chocolate_cake_full.png", "chocolate_cake_bitten.png", ConsumePose.Handheld);
            ConfigurePilotFood(foods[13], "bubble_tea_full.png", "bubble_tea_half.png", ConsumePose.Drink);

            catalog.foods = foods;
            catalog.characters = new[]
            {
                new CharacterDefinition
                {
                    characterId = "pomelo",
                    displayName = "\u67da\u67da",
                    portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PomeloPortraitPath),
                    settlementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PomeloPrefabPath)
                }
            };
            catalog.cardBack = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackPath);
            catalog.homeBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{BackgroundFolder}/home_kitchen.png");
            catalog.tableBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{BackgroundFolder}/table_layout_subtle_v2.png");
            catalog.settlementBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{BackgroundFolder}/settlement_alcove.png");
            catalog.seatTopBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{TableStationFolder}/seat_top_honey.png");
            catalog.seatSelfBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{TableStationFolder}/seat_self_seaside.png");
            catalog.seatLeftBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{TableStationFolder}/seat_left_garden.png");
            catalog.seatRightBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{TableStationFolder}/seat_right_strawberry.png");
            catalog.tableCenterBackground = AssetDatabase.LoadAssetAtPath<Sprite>($"{TableStationFolder}/table_center_island.png");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CaboArt.ResetCache();

            var errors = CollectValidationErrors(catalog);
            if (errors.Count > 0)
                throw new InvalidOperationException("Cabo art catalog is incomplete:\n- " + string.Join("\n- ", errors));

            Debug.Log("[Cabo] Art catalog rebuilt: 14 food cards, card back, 3 screen backgrounds, 4 player stations, and center table.");
        }

        [MenuItem("Cabo/Validate Art Catalog")]
        public static void ValidateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CaboArtCatalog>(CatalogPath);
            var errors = CollectValidationErrors(catalog);
            if (errors.Count > 0)
                throw new InvalidOperationException("Cabo art catalog validation failed:\n- " + string.Join("\n- ", errors));
            Debug.Log("[Cabo] Art catalog validation passed.");
        }

        public static void ValidateCatalogOrThrow()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CaboArtCatalog>(CatalogPath);
            var errors = CollectValidationErrors(catalog);
            if (errors.Count > 0)
                throw new InvalidOperationException("Cabo art catalog validation failed:\n- " + string.Join("\n- ", errors));
        }

        static void ConfigureTextureImporters()
        {
            ConfigureFolder(FoodFolder, 512);
            ConfigureAsset(CardBackPath, 1024);
            ConfigureFolder(BackgroundFolder, 2048);
            ConfigureFolder(TableStationFolder, 2048);
            ConfigureFolder(SettlementPropsFolder, 1024, 256f);
            ConfigureFolder("Assets/Art/Characters/pomelo/Parts", 1024, 256f);
            AssetDatabase.Refresh();
        }

        static void ConfigurePilotFood(FoodCardDefinition food, string fullName, string consumedName, ConsumePose pose)
        {
            food.consumeSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SettlementPropsFolder}/{fullName}");
            food.consumedSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SettlementPropsFolder}/{consumedName}");
            food.consumePose = pose;
        }

        static void ConfigureFolder(string folder, int maxSize, float pixelsPerUnit = 100f)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
                ConfigureAsset(AssetDatabase.GUIDToAssetPath(guids[i]), maxSize, pixelsPerUnit);
        }

        static void ConfigureAsset(string path, int maxSize, float pixelsPerUnit = 100f)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.maxTextureSize != maxSize
                || !importer.alphaIsTransparency
                || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = maxSize;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (changed)
                importer.SaveAndReimport();
        }

        static List<string> CollectValidationErrors(CaboArtCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add($"Missing {CatalogPath}");
                return errors;
            }

            if (catalog.foods == null || catalog.foods.Length != 14)
                errors.Add("Food definitions must contain exactly values 0-13");
            else
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < catalog.foods.Length; i++)
                {
                    var food = catalog.foods[i];
                    if (food == null)
                    {
                        errors.Add($"Food definition slot {i} is null");
                        continue;
                    }

                    if (!seen.Add(food.value)) errors.Add($"Duplicate food value {food.value}");
                    if (food.value < 0 || food.value > 13) errors.Add($"Food value {food.value} is outside 0-13");
                    if (food.foodSprite == null) errors.Add($"Food value {food.value} has no sprite");
                    if (food.consumeSprite == null) errors.Add($"Food value {food.value} has no consume sprite");
                    if (food.consumedSprite == null) errors.Add($"Food value {food.value} has no consumed sprite");
                    if (food.consumePose == ConsumePose.None) errors.Add($"Food value {food.value} has no consume pose");
                    if (string.IsNullOrWhiteSpace(food.displayName)) errors.Add($"Food value {food.value} has no display name");
                }
            }

            if (catalog.cardBack == null) errors.Add("Card back sprite is missing");
            if (catalog.homeBackground == null) errors.Add("Home background is missing");
            if (catalog.tableBackground == null) errors.Add("Table background is missing");
            if (catalog.settlementBackground == null) errors.Add("Settlement background is missing");
            if (catalog.seatTopBackground == null) errors.Add("Top player station is missing");
            if (catalog.seatSelfBackground == null) errors.Add("Self player station is missing");
            if (catalog.seatLeftBackground == null) errors.Add("Left player station is missing");
            if (catalog.seatRightBackground == null) errors.Add("Right player station is missing");
            if (catalog.tableCenterBackground == null) errors.Add("Center table illustration is missing");
            if (catalog.characters == null || catalog.characters.Length == 0)
                errors.Add("At least one character definition is required");
            else if (catalog.characters[0].settlementPrefab == null)
                errors.Add("Default character settlement prefab is missing");
            return errors;
        }

        static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        static string SkillLabel(int value)
        {
            if (value == 7 || value == 8) return "\u81EA\u68C0";
            if (value == 9 || value == 10) return "\u4FA6\u67E5";
            if (value == 11 || value == 12) return "\u4EA4\u6362";
            return "";
        }

        static Color Accent(int value)
        {
            if (value <= 3) return new Color(0.49f, 0.70f, 0.39f, 1f);
            if (value <= 6) return new Color(0.88f, 0.68f, 0.28f, 1f);
            if (value <= 10) return new Color(0.94f, 0.52f, 0.24f, 1f);
            return new Color(0.88f, 0.35f, 0.31f, 1f);
        }
    }
}

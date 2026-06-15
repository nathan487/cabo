#if UNITY_EDITOR
using System;
using Cabo.Client.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cabo.Client.Editor
{
    public static class PomeloSettlementPilotBuilder
    {
        const string CharacterRoot = "Assets/Art/Characters";
        const string PropsFolder = "Assets/Art/SettlementProps/Pilot";
        const string PreviewScenePath = "Assets/Scenes/PomeloSettlementPilot.unity";
        const string ReferencePath = "Assets/Art/Characters/pomelo/Source/pomelo_front_reference.png";

        sealed class CharacterBuild
        {
            public readonly string Id;
            public readonly string PrefabName;
            public readonly CustomRig Rig;

            public CharacterBuild(string id, string prefabName, CustomRig rig = null)
            {
                Id = id;
                PrefabName = prefabName;
                Rig = rig;
            }

            public string PartsFolder => $"{CharacterRoot}/{Id}/Parts";
            public string PrefabPath => $"{CharacterRoot}/{Id}/{PrefabName}.prefab";
        }

        sealed class CustomRig
        {
            public bool BodyIncludesLegs;
            public Vector3 BodyTop;
            public Vector2 BodyScale;
            public Vector3 HeadCenter;
            public Vector2 HeadScale;
            public Vector3 LeftShoulder;
            public Vector3 RightShoulder;
            public Vector2 LeftRest;
            public Vector2 RightRest;
            public float UpperArmLength;
            public float ForearmLength;
            public float UpperArmScale;
            public float ForearmScale;
            public float HandScale;
            public Vector3 LeftLegTop;
            public Vector3 RightLegTop;
            public float LegScale;
            public Vector3 LeftEye;
            public Vector3 RightEye;
            public float EyeScale;
            public Vector3 LeftBrow;
            public Vector3 RightBrow;
            public float BrowScale;
            public Vector3 Mouth;
            public float MouthScale;
            public Vector3 IdlePropPosition;
            public float IdlePropScale;
            public Vector2 NameAnchor;
            public Vector2 BowlLeft;
            public Vector2 BowlRight;
            public Vector2 BowlProp;
            public Vector2 DrinkRight;
            public Vector2 DrinkProp;
            public Vector2 HandheldLeft;
            public Vector2 HandheldRight;
            public Vector2 HandheldProp;
            public float DefeatScale;
            public float PortraitCameraY;
            public float PortraitSize;
        }

        static readonly CharacterBuild[] Characters =
        {
            new CharacterBuild("pomelo", "PomeloSettlement"),
            new CharacterBuild("strawberry", "StrawberrySettlement"),
            new CharacterBuild("oat", "OatSettlement"),
            new CharacterBuild("bean", "BeanSettlement"),
            new CharacterBuild("trainee", "TraineeSettlement", TraineeRig()),
            new CharacterBuild("milkdragon", "MilkDragonSettlement", MilkDragonRig())
        };

        static CustomRig TraineeRig()
        {
            return new CustomRig
            {
                BodyIncludesLegs = true,
                BodyTop = new Vector3(0f, 0.58f, 0f),
                BodyScale = Vector2.one * 1.15f,
                HeadCenter = new Vector3(0f, 1.23f, 0f),
                HeadScale = Vector2.one * 1.45f,
                LeftShoulder = new Vector3(-0.48f, 0.37f, 0f),
                RightShoulder = new Vector3(0.48f, 0.37f, 0f),
                LeftRest = new Vector2(-0.70f, -0.12f),
                RightRest = new Vector2(0.70f, -0.12f),
                UpperArmLength = 0.48f,
                ForearmLength = 0.50f,
                UpperArmScale = 0.80f,
                ForearmScale = 0.78f,
                HandScale = 0.72f,
                LeftEye = new Vector3(-0.19f, -0.03f, 0f),
                RightEye = new Vector3(0.19f, -0.03f, 0f),
                EyeScale = 0.72f,
                LeftBrow = new Vector3(-0.19f, 0.18f, 0f),
                RightBrow = new Vector3(0.19f, 0.18f, 0f),
                BrowScale = 0.55f,
                Mouth = new Vector3(0f, -0.35f, 0f),
                MouthScale = 0.65f,
                IdlePropPosition = new Vector3(-0.82f, -0.12f, 0f),
                IdlePropScale = 0.56f,
                NameAnchor = new Vector2(0f, 0.72f),
                BowlLeft = new Vector2(-0.28f, 0.58f),
                BowlRight = new Vector2(0.28f, 0.58f),
                BowlProp = new Vector2(0f, 0.84f),
                DrinkRight = new Vector2(0.20f, 0.86f),
                DrinkProp = new Vector2(0.20f, 0.91f),
                HandheldLeft = new Vector2(-0.18f, 0.73f),
                HandheldRight = new Vector2(0.18f, 0.80f),
                HandheldProp = new Vector2(0f, 0.89f),
                DefeatScale = 1.55f,
                PortraitCameraY = 1.24f,
                PortraitSize = 0.72f
            };
        }

        static CustomRig MilkDragonRig()
        {
            return new CustomRig
            {
                BodyIncludesLegs = false,
                BodyTop = new Vector3(0f, 0.48f, 0f),
                BodyScale = Vector2.one,
                HeadCenter = new Vector3(0f, 1.05f, 0f),
                HeadScale = Vector2.one * 1.05f,
                LeftShoulder = new Vector3(-0.46f, 0.27f, 0f),
                RightShoulder = new Vector3(0.46f, 0.27f, 0f),
                LeftRest = new Vector2(-0.58f, -0.35f),
                RightRest = new Vector2(0.58f, -0.35f),
                UpperArmLength = 0.38f,
                ForearmLength = 0.38f,
                UpperArmScale = 0.78f,
                ForearmScale = 0.76f,
                HandScale = 0.68f,
                LeftLegTop = new Vector3(-0.27f, -0.55f, 0f),
                RightLegTop = new Vector3(0.27f, -0.55f, 0f),
                LegScale = 0.68f,
                LeftEye = new Vector3(-0.25f, -0.02f, 0f),
                RightEye = new Vector3(0.25f, -0.02f, 0f),
                EyeScale = 0.72f,
                LeftBrow = new Vector3(-0.22f, 0.16f, 0f),
                RightBrow = new Vector3(0.22f, 0.16f, 0f),
                BrowScale = 0.55f,
                Mouth = new Vector3(0f, -0.31f, 0f),
                MouthScale = 0.70f,
                NameAnchor = new Vector2(0f, 0.67f),
                BowlLeft = new Vector2(-0.25f, 0.48f),
                BowlRight = new Vector2(0.25f, 0.48f),
                BowlProp = new Vector2(0f, 0.70f),
                DrinkRight = new Vector2(0.18f, 0.72f),
                DrinkProp = new Vector2(0.18f, 0.78f),
                HandheldLeft = new Vector2(-0.16f, 0.62f),
                HandheldRight = new Vector2(0.16f, 0.69f),
                HandheldProp = new Vector2(0f, 0.76f),
                DefeatScale = 1.0f,
                PortraitCameraY = 1.05f,
                PortraitSize = 0.72f
            };
        }

        [MenuItem("Cabo/Art/Build Pomelo Settlement Pilot")]
        public static void Build()
        {
            ConfigureImports();
            AssetDatabase.Refresh();
            for (int i = 0; i < Characters.Length; i++)
                BuildPrefab(Characters[i]);
            BuildPreviewScene();
            CaboArtCatalogBuilder.RebuildCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CaboArt] Built six settlement character rigs, including two independently measured custom rigs.");
        }

        static void BuildPreviewScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var loadedPreviewScene = SceneManager.GetSceneByPath(PreviewScenePath);
            if (loadedPreviewScene.IsValid() && loadedPreviewScene.isLoaded)
            {
                SettlementPilotPreview loadedPreview = null;
                foreach (var root in loadedPreviewScene.GetRootGameObjects())
                {
                    loadedPreview = root.GetComponentInChildren<SettlementPilotPreview>(true);
                    if (loadedPreview != null)
                        break;
                }

                if (loadedPreview == null)
                {
                    var loadedDriver = new GameObject("PomeloSettlementPilotPreview");
                    SceneManager.MoveGameObjectToScene(loadedDriver, loadedPreviewScene);
                    loadedPreview = loadedDriver.AddComponent<SettlementPilotPreview>();
                }

                loadedPreview.referenceImage = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
                EditorUtility.SetDirty(loadedPreview);
                EditorSceneManager.MarkSceneDirty(loadedPreviewScene);
                EditorSceneManager.SaveScene(loadedPreviewScene);
                return;
            }

            var previousActiveScene = SceneManager.GetActiveScene();
            var previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var driver = new GameObject("PomeloSettlementPilotPreview");
            var preview = driver.AddComponent<SettlementPilotPreview>();
            preview.referenceImage = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
            SceneManager.MoveGameObjectToScene(driver, previewScene);
            EditorSceneManager.SaveScene(previewScene, PreviewScenePath);
            EditorSceneManager.CloseScene(previewScene, true);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }

        static void ConfigureImports()
        {
            for (int i = 0; i < Characters.Length; i++)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Characters[i].PartsFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    Vector2 pivot = GetSpritePivot(name, Characters[i].Rig != null);
                    ConfigureSprite(path, 256f, pivot, 1024);
                }
            }

            for (int i = 0; i < Characters.Length; i++)
                ConfigureSprite($"{CharacterRoot}/{Characters[i].Id}/gameover_defeat_v1.png", 256f, new Vector2(0.5f, 0.5f), 2048);

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PropsFolder }))
                ConfigureSprite(AssetDatabase.GUIDToAssetPath(guid), 256f, new Vector2(0.5f, 0.5f), 1024);
        }

        static Vector2 GetSpritePivot(string name, bool customRig)
        {
            if (customRig)
            {
                if (name == "body") return new Vector2(0.5f, 0.96f);
                if (name == "left_upper_arm" || name == "right_upper_arm") return new Vector2(0.5f, 0.94f);
                if (name == "left_forearm" || name == "right_forearm") return new Vector2(0.5f, 0.92f);
                if (name.StartsWith("left_hand", StringComparison.Ordinal) || name.StartsWith("right_hand", StringComparison.Ordinal))
                    return new Vector2(0.5f, 0.82f);
                if (name == "left_leg" || name == "right_leg") return new Vector2(0.5f, 0.92f);
                return new Vector2(0.5f, 0.5f);
            }

            switch (name)
            {
                case "body": return new Vector2(178f / 356f, 1f - 14f / 449f);
                case "left_forearm": return new Vector2(76.6f / 108f, 1f - 19f / 183f);
                case "right_forearm": return new Vector2(29.7f / 107f, 1f - 19.6f / 190f);
                case "left_hand_relaxed": return new Vector2(64.2f / 98f, 1f - 15.8f / 109f);
                case "right_hand_relaxed": return new Vector2(33.3f / 98f, 1f - 15.7f / 110f);
                case "left_hand_grip": return new Vector2(44.5f / 80f, 1f - 15.6f / 103f);
                case "right_hand_grip": return new Vector2(35.1f / 80f, 1f - 15.7f / 102f);
                case "left_hand_raised": return new Vector2(16.6f / 103f, 1f - 56.2f / 81f);
                case "right_hand_raised": return new Vector2(85.4f / 103f, 1f - 56.1f / 81f);
                case "left_leg": return new Vector2(56.2f / 115f, 1f - 28.2f / 397f);
                case "right_leg": return new Vector2(58.9f / 117f, 1f - 28.4f / 398f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        static void ConfigureSprite(string path, float pixelsPerUnit, Vector2 pivot, int maxSize)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void BuildPrefab(CharacterBuild character)
        {
            if (character.Rig != null)
            {
                BuildCustomPrefab(character);
                return;
            }

            var root = new GameObject(character.PrefabName);
            var actor = root.AddComponent<SettlementCharacterActor>();
            var visualRoot = NewTransform("VisualRoot", root.transform, Vector3.zero);
            var bodyBone = NewTransform("BodyBone", visualRoot, Vector3.zero);

            AddSprite(character, "LeftLeg", bodyBone, "left_leg", PomeloRigLayout.ToLocal(PomeloRigLayout.LeftLegTopPx), 0,
                PomeloRigLayout.LegScale, 0f);
            AddSprite(character, "RightLeg", bodyBone, "right_leg", PomeloRigLayout.ToLocal(PomeloRigLayout.RightLegTopPx), 0,
                PomeloRigLayout.LegScale, 0f);

            var leftShoulder = NewTransform("LeftCuff", bodyBone, PomeloRigLayout.ToLocal(PomeloRigLayout.LeftCuffPx));
            var leftElbow = NewTransform("LeftArmAxis", leftShoulder, Vector3.zero);
            AddSprite(character, "LeftForearm", leftElbow, "left_forearm", Vector3.zero, 7,
                Vector2.one * PomeloRigLayout.ForearmScale, PomeloRigLayout.LeftForearmCorrectionDegrees);
            var leftWrist = NewTransform("LeftWrist", leftShoulder,
                Vector3.down * PomeloRigLayout.ArmLength(PomeloRigLayout.LeftCuffPx, PomeloRigLayout.LeftWristPx));
            var leftHand = AddSprite(character, "LeftHand", leftWrist, "left_hand_relaxed", Vector3.zero, 8,
                Vector2.one * PomeloRigLayout.HandScale, PomeloRigLayout.LeftHandCorrectionDegrees);

            var rightShoulder = NewTransform("RightCuff", bodyBone, PomeloRigLayout.ToLocal(PomeloRigLayout.RightCuffPx));
            var rightElbow = NewTransform("RightArmAxis", rightShoulder, Vector3.zero);
            AddSprite(character, "RightForearm", rightElbow, "right_forearm", Vector3.zero, 7,
                Vector2.one * PomeloRigLayout.ForearmScale, PomeloRigLayout.RightForearmCorrectionDegrees);
            var rightWrist = NewTransform("RightWrist", rightShoulder,
                Vector3.down * PomeloRigLayout.ArmLength(PomeloRigLayout.RightCuffPx, PomeloRigLayout.RightWristPx));
            var rightHand = AddSprite(character, "RightHand", rightWrist, "right_hand_relaxed", Vector3.zero, 8,
                Vector2.one * PomeloRigLayout.HandScale, PomeloRigLayout.RightHandCorrectionDegrees);

            AddSprite(character, "Body", bodyBone, "body", PomeloRigLayout.ToLocal(PomeloRigLayout.BodyNeckPx), 2,
                PomeloRigLayout.BodyScale, 0f);

            var headBone = NewTransform("HeadBone", bodyBone, PomeloRigLayout.ToLocal(PomeloRigLayout.HeadCenterPx));
            AddSprite(character, "Head", headBone, "head", Vector3.zero, 4, PomeloRigLayout.HeadScale, 0f);
            var leftEye = AddSprite(character, "LeftEye", headBone, "eye_open_left",
                PomeloRigLayout.ToLocal(PomeloRigLayout.LeftEyeCenterPx) - PomeloRigLayout.ToLocal(PomeloRigLayout.HeadCenterPx),
                5, PomeloRigLayout.EyeScale, 0f);
            var rightEye = AddSprite(character, "RightEye", headBone, "eye_open_right",
                PomeloRigLayout.ToLocal(PomeloRigLayout.RightEyeCenterPx) - PomeloRigLayout.ToLocal(PomeloRigLayout.HeadCenterPx),
                5, PomeloRigLayout.EyeScale, 0f);
            AddSprite(character, "LeftBrow", headBone, "brow_left",
                PomeloRigLayout.ToLocal(PomeloRigLayout.LeftBrowCenterPx) - PomeloRigLayout.ToLocal(PomeloRigLayout.HeadCenterPx),
                6, PomeloRigLayout.BrowScale, 0f);
            AddSprite(character, "RightBrow", headBone, "brow_right",
                PomeloRigLayout.ToLocal(PomeloRigLayout.RightBrowCenterPx) - PomeloRigLayout.ToLocal(PomeloRigLayout.HeadCenterPx),
                6, PomeloRigLayout.BrowScale, 0f);
            var mouth = AddSprite(character, "Mouth", headBone, "mouth_eat",
                PomeloRigLayout.ToLocal(PomeloRigLayout.MouthCenterPx) - PomeloRigLayout.ToLocal(PomeloRigLayout.HeadCenterPx),
                6, PomeloRigLayout.MouthScale, 0f);

            var prop = AddSprite(character, "FoodProp", visualRoot, null, Vector3.zero, 7);
            prop.enabled = true;

            actor.visualRoot = visualRoot;
            actor.bodyBone = bodyBone;
            actor.headBone = headBone;
            actor.leftShoulder = leftShoulder;
            actor.leftElbow = leftElbow;
            actor.leftWrist = leftWrist;
            actor.rightShoulder = rightShoulder;
            actor.rightElbow = rightElbow;
            actor.rightWrist = rightWrist;
            actor.singleSegmentArms = true;
            actor.leftRestTarget = PomeloRigLayout.ToLocal2(PomeloRigLayout.LeftWristPx);
            actor.rightRestTarget = PomeloRigLayout.ToLocal2(PomeloRigLayout.RightWristPx);
            actor.forearmLength = PomeloRigLayout.ArmLength(PomeloRigLayout.LeftCuffPx, PomeloRigLayout.LeftWristPx);
            actor.leftEye = leftEye;
            actor.rightEye = rightEye;
            actor.mouth = mouth;
            actor.leftEyeOpen = Sprite(character, "eye_open_left");
            actor.rightEyeOpen = Sprite(character, "eye_open_right");
            actor.leftEyeClosed = Sprite(character, "eye_closed_left");
            actor.rightEyeClosed = Sprite(character, "eye_closed_right");
            actor.mouthNeutral = Sprite(character, "mouth_eat");
            actor.mouthEat = Sprite(character, "mouth_eat");
            actor.mouthChew = Sprite(character, "mouth_chew");
            actor.mouthHappy = Sprite(character, "mouth_happy");
            actor.mouthFail = Sprite(character, "mouth_fail");
            actor.leftHand = leftHand;
            actor.rightHand = rightHand;
            actor.leftHandRelaxed = Sprite(character, "left_hand_relaxed");
            actor.rightHandRelaxed = Sprite(character, "right_hand_relaxed");
            actor.leftHandGrip = Sprite(character, "left_hand_grip");
            actor.rightHandGrip = Sprite(character, "right_hand_grip");
            actor.leftHandRaised = Sprite(character, "left_hand_raised");
            actor.rightHandRaised = Sprite(character, "right_hand_raised");
            actor.propRenderer = prop;

            EditorUtility.SetDirty(actor);
            PrefabUtility.SaveAsPrefabAsset(root, character.PrefabPath);
            BuildPortrait(root, character);
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void BuildCustomPrefab(CharacterBuild character)
        {
            var rig = character.Rig;
            var root = new GameObject(character.PrefabName);
            var actor = root.AddComponent<SettlementCharacterActor>();
            var visualRoot = NewTransform("VisualRoot", root.transform, Vector3.zero);
            var bodyBone = NewTransform("BodyBone", visualRoot, Vector3.zero);

            if (!rig.BodyIncludesLegs)
            {
                AddSprite(character, "LeftLeg", bodyBone, "left_leg", rig.LeftLegTop, 0,
                    Vector2.one * rig.LegScale, 0f);
                AddSprite(character, "RightLeg", bodyBone, "right_leg", rig.RightLegTop, 0,
                    Vector2.one * rig.LegScale, 0f);
            }

            var leftShoulder = NewTransform("LeftShoulder", bodyBone, rig.LeftShoulder);
            AddSprite(character, "LeftUpperArm", leftShoulder, "left_upper_arm", Vector3.zero, 1,
                Vector2.one * rig.UpperArmScale, 0f);
            var leftElbow = NewTransform("LeftElbow", leftShoulder, Vector3.down * rig.UpperArmLength);
            AddSprite(character, "LeftForearm", leftElbow, "left_forearm", Vector3.zero, 7,
                Vector2.one * rig.ForearmScale, 0f);
            var leftWrist = NewTransform("LeftWrist", leftElbow, Vector3.down * rig.ForearmLength);
            var leftHand = AddSprite(character, "LeftHand", leftWrist, "left_hand_relaxed", Vector3.zero, 8,
                Vector2.one * rig.HandScale, 0f);

            var rightShoulder = NewTransform("RightShoulder", bodyBone, rig.RightShoulder);
            AddSprite(character, "RightUpperArm", rightShoulder, "right_upper_arm", Vector3.zero, 1,
                Vector2.one * rig.UpperArmScale, 0f);
            var rightElbow = NewTransform("RightElbow", rightShoulder, Vector3.down * rig.UpperArmLength);
            AddSprite(character, "RightForearm", rightElbow, "right_forearm", Vector3.zero, 7,
                Vector2.one * rig.ForearmScale, 0f);
            var rightWrist = NewTransform("RightWrist", rightElbow, Vector3.down * rig.ForearmLength);
            var rightHand = AddSprite(character, "RightHand", rightWrist, "right_hand_relaxed", Vector3.zero, 8,
                Vector2.one * rig.HandScale, 0f);

            AddSprite(character, "Body", bodyBone, "body", rig.BodyTop, 2, rig.BodyScale, 0f);
            var headBone = NewTransform("HeadBone", bodyBone, rig.HeadCenter);
            AddSprite(character, "Head", headBone, "head", Vector3.zero, 4, rig.HeadScale, 0f);
            var leftEye = AddSprite(character, "LeftEye", headBone, "eye_open_left", rig.LeftEye, 5,
                Vector2.one * rig.EyeScale, 0f);
            var rightEye = AddSprite(character, "RightEye", headBone, "eye_open_right", rig.RightEye, 5,
                Vector2.one * rig.EyeScale, 0f);
            AddSprite(character, "LeftBrow", headBone, "brow_left", rig.LeftBrow, 6,
                Vector2.one * rig.BrowScale, 0f);
            AddSprite(character, "RightBrow", headBone, "brow_right", rig.RightBrow, 6,
                Vector2.one * rig.BrowScale, 0f);
            var mouth = AddSprite(character, "Mouth", headBone, "mouth_neutral", rig.Mouth, 6,
                Vector2.one * rig.MouthScale, 0f);

            var prop = AddSprite(character, "FoodProp", visualRoot, null, Vector3.zero, 9);
            prop.enabled = true;
            SpriteRenderer idleProp = null;
            if (Sprite(character, "idle_prop") != null)
            {
                idleProp = AddSprite(character, "IdleProp", visualRoot, "idle_prop", rig.IdlePropPosition, 1,
                    Vector2.one * rig.IdlePropScale, 0f);
            }

            actor.visualRoot = visualRoot;
            actor.bodyBone = bodyBone;
            actor.headBone = headBone;
            actor.leftShoulder = leftShoulder;
            actor.leftElbow = leftElbow;
            actor.leftWrist = leftWrist;
            actor.rightShoulder = rightShoulder;
            actor.rightElbow = rightElbow;
            actor.rightWrist = rightWrist;
            actor.singleSegmentArms = false;
            actor.leftRestTarget = rig.LeftRest;
            actor.rightRestTarget = rig.RightRest;
            actor.nameAnchorOffset = rig.NameAnchor;
            actor.upperArmLength = rig.UpperArmLength;
            actor.forearmLength = rig.ForearmLength;
            actor.leftEye = leftEye;
            actor.rightEye = rightEye;
            actor.mouth = mouth;
            actor.leftEyeOpen = Sprite(character, "eye_open_left");
            actor.rightEyeOpen = Sprite(character, "eye_open_right");
            actor.leftEyeClosed = Sprite(character, "eye_closed_left");
            actor.rightEyeClosed = Sprite(character, "eye_closed_right");
            actor.mouthNeutral = Sprite(character, "mouth_neutral");
            actor.mouthEat = Sprite(character, "mouth_eat");
            actor.mouthChew = Sprite(character, "mouth_chew");
            actor.mouthHappy = Sprite(character, "mouth_happy");
            actor.mouthFail = Sprite(character, "mouth_fail");
            actor.leftHand = leftHand;
            actor.rightHand = rightHand;
            actor.leftHandRelaxed = Sprite(character, "left_hand_relaxed");
            actor.rightHandRelaxed = Sprite(character, "right_hand_relaxed");
            actor.leftHandGrip = Sprite(character, "left_hand_raised");
            actor.rightHandGrip = Sprite(character, "right_hand_raised");
            actor.leftHandRaised = Sprite(character, "left_hand_raised");
            actor.rightHandRaised = Sprite(character, "right_hand_raised");
            actor.propRenderer = prop;
            actor.idlePropRenderer = idleProp;
            actor.bowlLeftTarget = rig.BowlLeft;
            actor.bowlRightTarget = rig.BowlRight;
            actor.bowlPropPosition = rig.BowlProp;
            actor.bowlPropScale = character.Id == "trainee" ? 0.28f : 0.26f;
            actor.drinkRightTarget = rig.DrinkRight;
            actor.drinkPropPosition = rig.DrinkProp;
            actor.drinkPropScale = character.Id == "trainee" ? 0.27f : 0.25f;
            actor.handheldLeftTarget = rig.HandheldLeft;
            actor.handheldRightTarget = rig.HandheldRight;
            actor.handheldPropPosition = rig.HandheldProp;
            actor.handheldPropScale = character.Id == "trainee" ? 0.27f : 0.25f;
            actor.propScaleMultiplier = 1.4f;
            actor.animationDurationMultiplier = 1.6f;
            actor.gameOverDefeatScale = rig.DefeatScale;

            EditorUtility.SetDirty(actor);
            PrefabUtility.SaveAsPrefabAsset(root, character.PrefabPath);
            BuildPortrait(root, character);
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void BuildPortrait(GameObject characterRoot, CharacterBuild character)
        {
            SetLayerRecursively(characterRoot, 31);
            var cameraObject = new GameObject($"{character.PrefabName}PortraitCamera");
            var camera = cameraObject.AddComponent<Camera>();
            float cameraY = character.Rig != null ? character.Rig.PortraitCameraY : 1.95f;
            camera.transform.position = new Vector3(0f, cameraY, -10f);
            camera.orthographic = true;
            camera.orthographicSize = character.Rig != null ? character.Rig.PortraitSize : 1.35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.cullingMask = 1 << 31;

            var renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var portrait = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            portrait.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
            portrait.Apply();

            string assetPath = $"{CharacterRoot}/{character.Id}/portrait.png";
            string fullPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "..", assetPath));
            System.IO.File.WriteAllBytes(fullPath, portrait.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(portrait);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(cameraObject);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(assetPath, 100f, new Vector2(0.5f, 0.5f), 512);
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        static Transform NewTransform(string name, Transform parent, Vector3 localPosition)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.localPosition = localPosition;
            return result;
        }

        static SpriteRenderer AddSprite(CharacterBuild character, string name, Transform parent, string spriteName, Vector3 localPosition,
            int sortingOrder, Vector2 localScale, float localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, localRotation);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = string.IsNullOrEmpty(spriteName) ? null : Sprite(character, spriteName);
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static SpriteRenderer AddSprite(CharacterBuild character, string name, Transform parent, string spriteName, Vector3 localPosition, int sortingOrder)
        {
            return AddSprite(character, name, parent, spriteName, localPosition, sortingOrder, Vector2.one, 0f);
        }

        static Sprite Sprite(CharacterBuild character, string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{character.PartsFolder}/{name}.png");
        }
    }
}
#endif

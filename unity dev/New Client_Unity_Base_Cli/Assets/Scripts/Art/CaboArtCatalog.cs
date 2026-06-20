using System;
using UnityEngine;

namespace Cabo.Client.Art
{
    public enum CaboSfx
    {
        Draw,
        Flip,
        Discard,
        Swap,
        Skill,
        Cabo,
        Eat,
        Penalty,
        Victory
    }

    public enum CaboSpecialEffect
    {
        None,
        PeekSelf,
        Spy,
        Swap,
        Cabo
    }

    public enum ConsumePose
    {
        None,
        Handheld,
        Bowl,
        Drink
    }

    [Serializable]
    public sealed class FoodCardDefinition
    {
        public int value;
        public string displayName;
        public string category;
        public string skillLabel;
        public Sprite foodSprite;
        public Sprite consumeSprite;
        public Sprite consumedSprite;
        public ConsumePose consumePose;
        public Color accentColor = Color.white;
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public string characterId;
        public string displayName;
        public Sprite portraitSprite;
        public GameObject settlementPrefab;
        public Sprite gameOverDefeatSprite;
    }

    [Serializable]
    public sealed class TableStationConfig
    {
        public int playerRoomIndex;
        public Sprite selfView;
        public Sprite oppositeView;
        public Sprite leftView;
        public Sprite rightView;

        public Sprite GetView(int relativeSeat)
        {
            switch (relativeSeat)
            {
                case 0: return selfView;
                case 1: return leftView;
                case 2: return oppositeView;
                case 3: return rightView;
                default: return selfView;
            }
        }
    }

    [CreateAssetMenu(fileName = "CaboArtCatalog", menuName = "Cabo/Art Catalog")]
    public sealed class CaboArtCatalog : ScriptableObject
    {
        public FoodCardDefinition[] foods = Array.Empty<FoodCardDefinition>();
        public CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();
        public Sprite cardBack;
        public Sprite homeBackground;
        public Sprite tableBackground;
        public Sprite settlementBackground;
        public TableStationConfig[] tableStations = Array.Empty<TableStationConfig>();
        public Sprite tableCenterBackground;
        public Sprite skillPeekSelfEffect;
        public Sprite skillSpyEffect;
        public Sprite skillSwapEffect;
        public Sprite caboCallEffect;
        public AudioClip bgmClip;
        public AudioClip drawSfx;
        public AudioClip flipSfx;
        public AudioClip discardSfx;
        public AudioClip swapSfx;
        public AudioClip skillSfx;
        public AudioClip caboSfx;
        public AudioClip eatSfx;
        public AudioClip penaltySfx;
        public AudioClip victorySfx;

        public FoodCardDefinition GetFood(int value)
        {
            if (foods == null)
                return null;

            for (int i = 0; i < foods.Length; i++)
            {
                var food = foods[i];
                if (food != null && food.value == value)
                    return food;
            }

            return null;
        }

        public CharacterDefinition GetCharacter(string characterId)
        {
            if (characters == null)
                return null;

            for (int i = 0; i < characters.Length; i++)
            {
                var character = characters[i];
                if (character != null && string.Equals(character.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return character;
            }

            return null;
        }

        public Sprite GetSpecialEffect(CaboSpecialEffect cue)
        {
            switch (cue)
            {
                case CaboSpecialEffect.PeekSelf: return skillPeekSelfEffect;
                case CaboSpecialEffect.Spy: return skillSpyEffect;
                case CaboSpecialEffect.Swap: return skillSwapEffect;
                case CaboSpecialEffect.Cabo: return caboCallEffect;
                default: return null;
            }
        }

        public AudioClip GetSfx(CaboSfx cue)
        {
            switch (cue)
            {
                case CaboSfx.Draw: return drawSfx;
                case CaboSfx.Flip: return flipSfx;
                case CaboSfx.Discard: return discardSfx;
                case CaboSfx.Swap: return swapSfx;
                case CaboSfx.Skill: return skillSfx;
                case CaboSfx.Cabo: return caboSfx;
                case CaboSfx.Eat: return eatSfx;
                case CaboSfx.Penalty: return penaltySfx;
                case CaboSfx.Victory: return victorySfx;
                default: return null;
            }
        }
    }
}

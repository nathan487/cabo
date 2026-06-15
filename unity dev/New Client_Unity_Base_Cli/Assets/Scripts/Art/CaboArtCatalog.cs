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

    [CreateAssetMenu(fileName = "CaboArtCatalog", menuName = "Cabo/Art Catalog")]
    public sealed class CaboArtCatalog : ScriptableObject
    {
        public FoodCardDefinition[] foods = Array.Empty<FoodCardDefinition>();
        public CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();
        public Sprite cardBack;
        public Sprite homeBackground;
        public Sprite tableBackground;
        public Sprite settlementBackground;
        public Sprite seatTopBackground;
        public Sprite seatSelfBackground;
        public Sprite seatLeftBackground;
        public Sprite seatRightBackground;
        public Sprite tableCenterBackground;
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

using System;
using UnityEngine;

namespace Cabo.Client.Art
{
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
    }
}

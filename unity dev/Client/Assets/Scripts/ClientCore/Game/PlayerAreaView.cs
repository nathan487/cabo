using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    /// <summary>One player's area: name, score, 4 card slots, turn highlight.</summary>
    public class PlayerAreaView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Image highlightBorder;
        [SerializeField] private Transform cardsContainer;

        [Header("Config")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField] private Color normalColor = new Color(0.25f, 0.25f, 0.35f);

        public Transform CardsContainer => cardsContainer;

        public long PlayerId { get; private set; }
        public CardView[] CardViews { get; private set; } = new CardView[4];

        public void Initialize(long playerId, string playerName, int initialScore)
        {
            PlayerId = playerId;
            if (nameText) nameText.text = playerName;
            if (scoreText) scoreText.text = $"{initialScore} pts";
            if (highlightBorder) highlightBorder.color = normalColor;
        }

        public void SetTurnHighlight(bool isCurrentTurn)
        {
            if (highlightBorder)
                highlightBorder.color = isCurrentTurn ? highlightColor : normalColor;
        }

        public void SetScore(int score)
        {
            if (scoreText) scoreText.text = $"{score} pts";
        }

        public void SetCardKnown(int slotIndex, int value)
        {
            if (slotIndex >= 0 && slotIndex < CardViews.Length && CardViews[slotIndex] != null)
                CardViews[slotIndex].SetKnown(value);
        }

        public void SetCardUnknown(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < CardViews.Length && CardViews[slotIndex] != null)
                CardViews[slotIndex].SetUnknown();
        }

        public void SetCardCount(int count)
        {
            for (int i = 0; i < CardViews.Length; i++)
                if (CardViews[i] != null) CardViews[i].gameObject.SetActive(i < count);
        }

        public void FlipCardToKnown(int slotIndex, int value)
        {
            if (slotIndex >= 0 && slotIndex < CardViews.Length && CardViews[slotIndex] != null)
                StartCoroutine(CardViews[slotIndex].FlipToKnown(value));
        }
    }
}

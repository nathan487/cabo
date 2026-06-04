using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    /// <summary>Draw pile + discard pile display.</summary>
    public class PileView : MonoBehaviour
    {
        [SerializeField] private TMP_Text drawPileText;
        [SerializeField] private TMP_Text discardPileText;
        [SerializeField] private Image drawPileImage;
        [SerializeField] private Image discardPileImage;

        public void SetDrawPileCount(int count)
        {
            if (drawPileText) drawPileText.text = count > 0 ? $"{count}" : "0";
            if (drawPileImage) drawPileImage.gameObject.SetActive(count > 0);
        }

        public void SetDiscardPile(int count, int? topValue)
        {
            if (discardPileText)
                discardPileText.text = topValue.HasValue ? $"{topValue.Value}" : "-";
            if (discardPileImage) discardPileImage.gameObject.SetActive(count > 0);
        }
    }
}

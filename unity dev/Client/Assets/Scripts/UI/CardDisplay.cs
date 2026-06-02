using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a single card with its value and state (face-up/face-down).
/// </summary>
public class CardDisplay : MonoBehaviour
{
    public Image background;
    public Text valueText;
    public Image cardBackImage;
    public Button clickButton;

    private Card card;
    private bool isKnown;
    private int slotIndex;

    public void SetCard(Card card, bool known, int slot)
    {
        this.card = card;
        this.isKnown = known;
        this.slotIndex = slot;

        if (known)
        {
            valueText.text = card.Value.ToString();
            valueText.gameObject.SetActive(true);
            if (cardBackImage != null) cardBackImage.gameObject.SetActive(false);
            if (background != null)
                background.color = ValueToColor(card.Value);
        }
        else
        {
            valueText.text = "?";
            valueText.gameObject.SetActive(false);
            if (cardBackImage != null) cardBackImage.gameObject.SetActive(true);
            if (background != null)
                background.color = new Color(0.12f, 0.18f, 0.28f);
        }
    }

    private Color ValueToColor(int value)
    {
        float t = value / 13f;
        if (t < 0.33f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f), new Color(0.8f, 0.8f, 0.1f), t / 0.33f);
        else if (t < 0.66f) return Color.Lerp(new Color(0.8f, 0.8f, 0.1f), new Color(1f, 0.4f, 0.1f), (t - 0.33f) / 0.33f);
        else return Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.1f, 0.1f), (t - 0.66f) / 0.34f);
    }

    public void OnClick()
    {
        // Try the local GameUI, fall back to old ReplaceCardExternal if it exists
        var gui = GameUI.Instance;
        if (gui != null)
        {
            gui.ReplaceCardExternal(slotIndex);
        }
    }
}

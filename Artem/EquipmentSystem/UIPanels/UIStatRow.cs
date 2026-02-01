using TMPro;
using UnityEngine;

public class UIStatRow : MonoBehaviour
{
    [Header("Labels")]
    [SerializeField] private TMP_Text statName;
    [SerializeField] private TMP_Text currentVal;
    [SerializeField] private TMP_Text nextVal;
    [SerializeField] private TMP_Text deltaVal;

    [Header("Delta Colors")]
    [SerializeField] private Color positiveColor = new Color32(27, 94, 32, 255);   // #1B5E20 dark green
    [SerializeField] private Color negativeColor = new Color32(183, 28, 28, 255);  // #B71C1C dark red
    [SerializeField] private Color neutralColor = new Color32(200, 200, 200, 255);// light gray (used if you want to show 0)


    public void Set(string name, int current, int next, int delta)
    {
        if (statName) statName.text = name;
        if (currentVal) currentVal.text = current.ToString();
        if (nextVal) nextVal.text = next.ToString();

        if (!deltaVal) return;

        if (delta == 0)
        {
            deltaVal.text  = "0";
            deltaVal.color = neutralColor;

            deltaVal.text = "";
            // Optionally reset to default color so previous state doesn't "bleed" through:
            deltaVal.color = neutralColor;
        }
        else if (delta > 0)
        {
            deltaVal.text = $"+{delta}";
            deltaVal.color = positiveColor;
        }
        else
        {
            deltaVal.text = delta.ToString(); // already has '-'
            deltaVal.color = negativeColor;
        }
    }
}

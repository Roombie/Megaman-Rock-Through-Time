using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A player's HUD health bar. Each player's Mega Man finds theirs with For(playerIndex).</summary>
public class UIHealthBar : MonoBehaviour
{
    public Image mask;
    [Tooltip("Whose health this shows: 0 = player 1.")]
    public int playerIndex;
    float originalSize;

    private static readonly List<UIHealthBar> huds = new();

    /// <summary>That player's HUD health bar, or null.</summary>
    public static UIHealthBar For(int playerIndex)
    {
        foreach (UIHealthBar hud in huds)
            if (hud.playerIndex == playerIndex) return hud;
        return null;
    }

    private void Awake()
    {
        // get the initial height of the mask before anyone calls SetValue
        originalSize = mask.rectTransform.rect.height;

        // Only HUD bars: the bars inside the old weapons menu also use this component
        if (GetComponentInParent<WeaponsMenu>(true) == null) huds.Add(this);
    }

    private void OnDestroy() => huds.Remove(this);

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => huds.Clear();

    public void SetValue(float value)
    {
        Debug.Log("Setting health bar value to: " + value);
        // adjust the height of the mask to "hide" lost health bars
        mask.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalSize * value);
    }
}

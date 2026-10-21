using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A player's HUD weapon energy bar. Each player's Mega Man finds theirs with For(playerIndex).</summary>
public class UIEnergyBar : MonoBehaviour
{
    public Image mask;  // The image mask for energy levels
    public Image weaponImage;
    [Tooltip("Whose weapon energy this shows: 0 = player 1.")]
    public int playerIndex;
    float originalSize;

    private static readonly List<UIEnergyBar> huds = new();

    /// <summary>That player's HUD energy bar, or null.</summary>
    public static UIEnergyBar For(int playerIndex)
    {
        foreach (UIEnergyBar hud in huds)
            if (hud.playerIndex == playerIndex) return hud;
        return null;
    }

    private void Awake()
    {
        // Read the full height before anyone calls SetValue (Megaman sets the bar in its own Start)
        originalSize = mask.rectTransform.rect.height;

        // Only HUD bars are the players' weapon bars. The bars inside the old weapons menu also use this
        // component; counting them (they wake up when the menu first opens) made the HUD bar stop updating
        // on weapon switches.
        if (GetComponentInParent<WeaponsMenu>(true) == null) huds.Add(this);
    }

    private void OnDestroy() => huds.Remove(this);

    // With Enter Play Mode Options (no domain reload) statics survive between play sessions
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => huds.Clear();

    public void SetValue(float value)
    {
        // Debug.Log("Setting energy bar value to: " + value);
        mask.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalSize * value);
    }

    public void SetEnergyBar(Sprite weaponBarImage)
    {
        if (weaponImage != null)
        {
            // Set the weaponImage's sprite to the passed weapon sprite
            weaponImage.sprite = weaponBarImage;
            // Debug.Log("Weapon image updated successfully!");
        }
        else
        {
            Debug.LogError("Weapon image is not assigned or missing!");
        }
    }

    public void SetVisibility(bool isVisible)
    {
        gameObject.GetComponent<CanvasGroup>().alpha = isVisible ? 1f : 0f;     
        // Debug.Log($"Energy bar visibility set to {isVisible}");
    }
}

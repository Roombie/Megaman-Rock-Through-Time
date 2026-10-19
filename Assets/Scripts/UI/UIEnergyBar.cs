using UnityEngine;
using UnityEngine.UI;

public class UIEnergyBar : MonoBehaviour
{
    public Image mask;  // The image mask for energy levels
    public Image weaponImage;
    float originalSize;

    public static UIEnergyBar Instance { get; private set; }

    private void Awake()
    {
        // Read the full height before anyone calls SetValue (Megaman sets the bar in its own Start)
        originalSize = mask.rectTransform.rect.height;

        // Only the HUD bar is the player's weapon bar. The bars inside the weapons menu also use this
        // component; letting them take over Instance (they wake up when the menu first opens) made the
        // HUD bar stop updating on weapon switches.
        if (Instance == null && GetComponentInParent<WeaponsMenu>(true) == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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

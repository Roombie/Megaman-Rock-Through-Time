using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIHealthBar : MonoBehaviour
{
    public Image mask;
    float originalSize;

    public static UIHealthBar Instance { get; private set; }

    private void Awake()
    {
        // get the initial height of the mask before anyone calls SetValue
        originalSize = mask.rectTransform.rect.height;

        if (Instance == null && GetComponentInParent<WeaponsMenu>(true) == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetValue(float value)
    {
        Debug.Log("Setting health bar value to: " + value);
        // adjust the height of the mask to "hide" lost health bars
        mask.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalSize * value);
    }
}

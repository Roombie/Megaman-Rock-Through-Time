using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One weapon line of the weapons menu (icon, name, energy bar). The menu clones an inactive template row
/// once per weapon, so editing the template in the scene changes every row.
/// </summary>
public class WeaponRowUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text label;
    public SegmentBar bar;
}

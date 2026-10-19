using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows an options row only for one filter (e.g. GB PALETTE only with the Game Boy filter).
/// While hidden, the rows below move up to close the gap and navigation skips the row.
/// Put it on the page object (it must stay active while the row is hidden).
/// </summary>
public class FilterDependentRow : MonoBehaviour
{
    [SerializeField] private FilterMode visibleFor = FilterMode.GameBoy;

    [Header("Row")]
    [SerializeField] private RectTransform row;
    [SerializeField] private Selectable rowSelectable;

    [Header("Neighbours (for navigation)")]
    [SerializeField] private Selectable above;
    [SerializeField] private Selectable below;

    private RectTransform[] rowsBelow;
    private float[] rowsBelowY;
    private float rowSpacing;
    private bool initialized;

    private void Initialize()
    {
        if (initialized || row == null) return;
        initialized = true;

        // Everything under the row on this page moves up by one row while it is hidden
        var parent = row.parent;
        var list = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform child in parent)
        {
            if (child is RectTransform rect && rect != row && rect.anchoredPosition.y < row.anchoredPosition.y - 0.5f)
                list.Add(rect);
        }
        rowsBelow = list.ToArray();
        rowsBelowY = new float[rowsBelow.Length];
        for (int i = 0; i < rowsBelow.Length; i++)
            rowsBelowY[i] = rowsBelow[i].anchoredPosition.y;

        var aboveRect = above != null ? above.transform as RectTransform : null;
        rowSpacing = aboveRect != null && aboveRect.parent == parent
            ? aboveRect.anchoredPosition.y - row.anchoredPosition.y
            : row.rect.height;
    }

    private void OnEnable()
    {
        Initialize();
        FilterManager.FilterChanged += Refresh;
        Refresh(FilterManager.Instance != null ? FilterManager.Instance.CurrentFilter : (FilterMode)PlayerPrefs.GetInt(SettingsKeys.FilterKey, 0));
    }

    private void OnDisable()
    {
        FilterManager.FilterChanged -= Refresh;
    }

    private void Refresh(FilterMode mode)
    {
        if (row == null) return;
        bool show = mode == visibleFor;

        row.gameObject.SetActive(show);

        for (int i = 0; i < rowsBelow.Length; i++)
        {
            var pos = rowsBelow[i].anchoredPosition;
            pos.y = rowsBelowY[i] + (show ? 0f : rowSpacing);
            rowsBelow[i].anchoredPosition = pos;
        }

        if (above != null)
        {
            var nav = above.navigation;
            nav.selectOnDown = show ? rowSelectable : below;
            above.navigation = nav;
        }
        if (below != null)
        {
            var nav = below.navigation;
            nav.selectOnUp = show ? rowSelectable : above;
            below.navigation = nav;
        }
    }
}

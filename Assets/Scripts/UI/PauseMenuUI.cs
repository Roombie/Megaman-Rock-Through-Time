using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game menus as scene objects on their own canvas, laid out on the 256x240 game-pixel grid.
/// Pages side by side (Pause, Weapons...) slide left/right; a page indicator stays on top.
/// Weapon rows and item slots are cloned from templates (TemplateGrid), sections stack by their content
/// (PixelStack) and panels fit their background around it (PixelPanelFit), so the layout adapts to how many
/// weapons/items there are. PauseManager drives it. Create one with Tools > Mega Man > Create Pause Menu Canvas.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public enum ItemEffect { None, Health, Weapons, HealthAndWeapons }

    [System.Serializable]
    public class ItemEntry
    {
        public Item.ItemType item = Item.ItemType.ETank;
        public Sprite icon;
        public ItemEffect effect = ItemEffect.Health;
    }

    [Header("Pages (index 0 = Pause, 1 = Weapons)")]
    public RectTransform pageStrip;
    public RectTransform[] pages;
    [Tooltip("Distance between pages. 0 = the canvas width (pages slide fully off screen).")]
    public int pageSpacing = 0;
    public float slideDuration = 0.25f;

    [Header("Page indicator")]
    public TMP_Text[] pageLabels;
    public RectTransform arrowLeft;
    public RectTransform arrowRight;

    [Header("Pause page")]
    public PixelPanelFit pausePanel;
    public TMP_Text[] pauseOptions;
    public GameObject confirmPanel;
    public TMP_Text confirmNo;
    public TMP_Text confirmYes;

    [Header("Weapons page")]
    public PixelPanelFit weaponsPanel;
    public PixelStack weaponsStack;
    public TemplateGrid weaponGrid;
    public TemplateGrid itemGrid;
    [Tooltip("Items shown in the item grid, in order.")]
    public List<ItemEntry> items = new();
    [Tooltip("Only show items the player has at least one of.")]
    public bool hideEmptyItems;
    public TMP_Text livesText;
    public TMP_Text screwsText;
    public SegmentBar healthBar;

    [Header("Cursor")]
    public RectTransform cursor;
    [Tooltip("Pixels between the cursor and the selected item.")]
    public int cursorGap = 4;

    [Header("Colours")]
    public Color selectedColor = new Color32(0xFC, 0xFC, 0xFC, 0xFF);
    public Color unselectedColor = new Color32(0x7C, 0x7C, 0x7C, 0xFF);
    public Color activePageColor = new Color32(0x3C, 0xBC, 0xFC, 0xFF);

    public int CurrentPage { get; private set; }
    public bool IsSliding { get; private set; }

    public readonly List<WeaponRowUI> weaponRows = new();
    public readonly List<ItemSlotUI> itemSlots = new();
    public readonly List<ItemEntry> shownItems = new();

    private Canvas canvas;
    public Canvas Canvas => canvas != null ? canvas : canvas = GetComponent<Canvas>();

    private void Awake()
    {
        // The canvas is saved visible so it can be edited; in game it starts hidden
        gameObject.SetActive(false);
    }

    #region Visibility & pages
    public void Open(int page)
    {
        gameObject.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        SetPage(page);
    }

    public void Close()
    {
        StopAllCoroutines();
        IsSliding = false;
        gameObject.SetActive(false);
    }

    public void ShowConfirm(bool show)
    {
        if (confirmPanel == null) return;
        confirmPanel.SetActive(show);
        if (show && confirmPanel.TryGetComponent(out PixelPanelFit fit)) fit.Fit();
    }

    private float PageWidth()
    {
        if (pageSpacing > 0) return pageSpacing;
        var root = (RectTransform)transform;
        return Mathf.Round(root.rect.width) + 16;
    }

    /// <summary>Jumps to a page without animation (also lays the pages out side by side).</summary>
    public void SetPage(int page)
    {
        CurrentPage = Mathf.Clamp(page, 0, pages.Length - 1);
        float width = PageWidth();
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].anchoredPosition = new Vector2(i * width, 0);
        if (pageStrip != null) pageStrip.anchoredPosition = new Vector2(-CurrentPage * width, 0);
        UpdateIndicator();
    }

    /// <summary>Slides to a neighbouring page, moving in whole pixels every frame.</summary>
    public bool SlideTo(int page)
    {
        if (IsSliding || page < 0 || page >= pages.Length || page == CurrentPage) return false;
        StartCoroutine(Slide(page));
        return true;
    }

    private IEnumerator Slide(int page)
    {
        IsSliding = true;
        if (cursor != null) cursor.gameObject.SetActive(false);

        float width = PageWidth();
        float from = -CurrentPage * width, to = -page * width;
        CurrentPage = page;
        UpdateIndicator();

        for (float t = 0f; t < slideDuration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / slideDuration, 3f);    // ease out
            pageStrip.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(from, to, k)), 0);
            yield return null;
        }
        pageStrip.anchoredPosition = new Vector2(to, 0);
        IsSliding = false;
    }

    private void UpdateIndicator()
    {
        if (pageLabels != null)
            for (int i = 0; i < pageLabels.Length; i++)
                if (pageLabels[i] != null) pageLabels[i].color = i == CurrentPage ? activePageColor : unselectedColor;
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(CurrentPage > 0);
        if (arrowRight != null) arrowRight.gameObject.SetActive(CurrentPage < pages.Length - 1);
    }
    #endregion

    #region Weapons page
    public void BuildWeapons(IList<Megaman.WeaponsStruct> weapons, WeaponTypes equipped)
    {
        weaponRows.Clear();
        if (weaponGrid == null) return;
        weaponGrid.Fill(weapons.Count);
        for (int i = 0; i < weaponGrid.cells.Count; i++)
        {
            var row = weaponGrid.cells[i].GetComponent<WeaponRowUI>();
            var w = weapons[i];
            if (row.icon != null)
            {
                bool isEquipped = w.weaponType == equipped || w.weaponData.weaponIconNotSelected == null;
                row.icon.sprite = isEquipped ? w.weaponData.weaponIcon : w.weaponData.weaponIconNotSelected;
                row.icon.color = row.icon.sprite != null ? Color.white : Color.clear;
            }
            if (row.label != null)
                row.label.text = (string.IsNullOrEmpty(w.weaponData.weaponName) ? w.weaponType.ToString() : w.weaponData.weaponName).ToUpperInvariant();
            weaponRows.Add(row);
        }
    }

    public void BuildItems(System.Func<Item.ItemType, int> countOf)
    {
        itemSlots.Clear();
        shownItems.Clear();
        if (itemGrid == null) return;

        foreach (var entry in items)
            if (!hideEmptyItems || countOf(entry.item) > 0) shownItems.Add(entry);

        itemGrid.gameObject.SetActive(shownItems.Count > 0);
        itemGrid.Fill(shownItems.Count);
        for (int i = 0; i < itemGrid.cells.Count; i++)
        {
            var slot = itemGrid.cells[i].GetComponent<ItemSlotUI>();
            if (slot.icon != null)
            {
                slot.icon.sprite = shownItems[i].icon;
                slot.icon.color = shownItems[i].icon != null ? Color.white : Color.clear;
                // keep sprites 1:1 with game pixels
                if (shownItems[i].icon != null) slot.icon.rectTransform.sizeDelta = shownItems[i].icon.rect.size;
            }
            itemSlots.Add(slot);
        }
    }

    /// <summary>Re-stacks the sections and refits the panels around what they now show.</summary>
    public void Relayout()
    {
        if (weaponsStack != null) weaponsStack.Rebuild();
        if (weaponsPanel != null) weaponsPanel.Fit();
        if (pausePanel != null) pausePanel.Fit();
    }
    #endregion

    #region Selection
    public void SetLabelSelected(TMP_Text label, bool selected)
    {
        if (label != null) label.color = selected ? selectedColor : unselectedColor;
    }

    /// <summary>
    /// Puts the cursor `cursorGap` px left of the target, vertically centred on it, on whole game pixels.
    /// For text, keep the label's rect 9 px tall with its top on the glyph ascent (as the builder makes them).
    /// </summary>
    public void PlaceCursor(RectTransform target)
    {
        if (cursor == null) return;
        cursor.gameObject.SetActive(target != null && !IsSliding);
        if (target == null) return;

        var space = (RectTransform)cursor.parent;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 bottomLeft = space.InverseTransformPoint(corners[0]);
        Vector2 topRight = space.InverseTransformPoint(corners[2]);

        Vector2 size = cursor.rect.size;
        float x = Mathf.Round(bottomLeft.x - cursorGap - size.x / 2f) + (size.x % 2) * 0.5f;
        float centreY = (bottomLeft.y + topRight.y) / 2f;
        float y = Mathf.Floor(centreY) + (size.y % 2) * 0.5f;
        cursor.anchoredPosition = new Vector2(x, y);
        cursor.SetAsLastSibling();
    }
    #endregion
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Look of the generated pause menus (font, colours, sprites).</summary>
[Serializable]
public class PauseMenuStyle
{
    public TMP_FontAsset font;
    [Tooltip("Font size where 1 font pixel = 1 game pixel (Mega Man fonts: 9 = 8 px glyph + 1 px spacing).")]
    public float fontSize = 9f;
    public Color panelColor = new Color32(0x00, 0x00, 0x00, 0xFF);
    public Color borderColor = new Color32(0x3C, 0xBC, 0xFC, 0xFF);
    public Color textColor = new Color32(0xFC, 0xFC, 0xFC, 0xFF);
    public Color dimTextColor = new Color32(0x7C, 0x7C, 0x7C, 0xFF);
    public Color titleColor = new Color32(0x3C, 0xBC, 0xFC, 0xFF);
    public Color cursorColor = new Color32(0xF8, 0x38, 0x00, 0xFF);
    public Color emptyBarColor = new Color32(0x3C, 0x3C, 0x3C, 0xFF);
    public Sprite cursorSprite;
    public Sprite eTankSprite;
    public Sprite wTankSprite;
    public Sprite mTankSprite;
    public Sprite sTankSprite;
    public Sprite lTankSprite;
    public Sprite livesSprite;
    public Sprite screwSprite;
}

/// <summary>
/// Builds the "Pause Menu Canvas": a page indicator on top and a strip of pages that slide left/right
/// (0 = Pause menu, 1 = Weapons menu), on the 256x240 game-pixel grid.
/// Used by the editor tool (Tools > Mega Man > Create Pause Menu Canvas) and as a runtime fallback.
/// Coordinates are game pixels from the screen centre, y up.
/// </summary>
public static class PauseMenuFactory
{
    private const int UILayer = 5;

    public static PauseMenuUI Build(PauseMenuStyle style, Camera gameCamera)
    {
        var go = new GameObject("Pause Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(PauseMenuUI));
        go.layer = UILayer;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = gameCamera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 100;

        // 240 canvas units = the 240 game pixel rows at any screen size / Screen mode
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(256, 240);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16f;

        var ui = go.GetComponent<PauseMenuUI>();
        var root = (RectTransform)go.transform;
        ui.selectedColor = style.textColor;
        ui.unselectedColor = style.dimTextColor;
        ui.activePageColor = style.titleColor;

        ui.pageStrip = NewRect(root, "Pages", 0, 0, 0, 0);
        var pausePage = NewRect(ui.pageStrip, "Page 0 - Pause", 0, 0, 0, 0);
        var weaponsPage = NewRect(ui.pageStrip, "Page 1 - Weapons", 256, 0, 0, 0);
        ui.pages = new[] { pausePage, weaponsPage };

        BuildPausePage(ui, pausePage, style);
        BuildWeaponsPage(ui, weaponsPage, style);
        BuildIndicator(ui, root, style);

        var cursor = NewImage(root, "Cursor", style.cursorSprite, style.cursorColor, 0, 0, 4, 7);
        ui.cursor = cursor.rectTransform;
        cursor.gameObject.SetActive(false);

        ui.SetPage(0);
        return ui;
    }

    #region Pages
    private static void BuildIndicator(PauseMenuUI ui, RectTransform root, PauseMenuStyle style)
    {
        var indicator = NewRect(root, "Page Indicator", 0, 0, 0, 0);
        const int top = 112;
        ui.pageLabels = new[]
        {
            Label(indicator, "Pause", style, "PAUSE", -40, top, style.dimTextColor, "Pause_Title", centred: true),
            Label(indicator, "Weapons", style, "WEAPONS", 40, top, style.dimTextColor, "Weapons_Title", centred: true),
        };

        // Arrows show which way you can slide; the left one is the cursor sprite mirrored
        var left = NewImage(indicator, "Arrow Left", style.cursorSprite, style.textColor, -88, top - 5, 4, 7);
        left.rectTransform.localScale = new Vector3(-1, 1, 1);
        ui.arrowLeft = left.rectTransform;
        ui.arrowRight = NewImage(indicator, "Arrow Right", style.cursorSprite, style.textColor, 88, top - 5, 4, 7).rectTransform;
    }

    private static void BuildPausePage(PauseMenuUI ui, RectTransform page, PauseMenuStyle style)
    {
        var panel = Panel(page, "Pause Panel", style, out var content);
        ui.pausePanel = panel;
        panel.paddingLeft = 16;
        panel.paddingRight = 12;
        panel.paddingTop = 10;
        panel.paddingBottom = 10;
        ui.pauseOptions = new[]
        {
            Label(content, "Resume", style, "RESUME", -36, 16, style.textColor, "Pause_Resume"),
            Label(content, "Options", style, "OPTIONS", -36, 0, style.dimTextColor, "Pause_Options"),
            Label(content, "Exit", style, "EXIT", -36, -16, style.dimTextColor, "Pause_Exit"),
        };

        var confirm = Panel(page, "Exit Confirmation", style, out var confirmContent);
        confirm.paddingLeft = confirm.paddingRight = 12;
        confirm.paddingTop = confirm.paddingBottom = 10;
        Label(confirmContent, "Question", style, "RETURN TO TITLE?", 0, 22, style.textColor, "Pause_ConfirmTitle", centred: true);
        Label(confirmContent, "Warning", style, "PROGRESS WILL BE LOST", 0, 6, style.dimTextColor, "Pause_ConfirmWarning", centred: true);
        ui.confirmYes = Label(confirmContent, "Yes", style, "YES", -56, -18, style.dimTextColor, "Pause_Yes");
        ui.confirmNo = Label(confirmContent, "No", style, "NO", 16, -18, style.textColor, "Pause_No");
        ui.confirmPanel = confirm.gameObject;
        confirm.gameObject.SetActive(false);
    }

    private static void BuildWeaponsPage(PauseMenuUI ui, RectTransform page, PauseMenuStyle style)
    {
        var panel = Panel(page, "Weapons Panel", style, out var content);
        ui.weaponsPanel = panel;
        panel.paddingLeft = 14;
        panel.paddingRight = 8;
        panel.paddingTop = panel.paddingBottom = 8;

        var stack = content.gameObject.AddComponent<PixelStack>();
        stack.spacing = 6;
        ui.weaponsStack = stack;

        // Weapons: one row per unlocked weapon (icon, name, energy bar)
        var weaponsSection = NewRect(content, "Weapons", 0, 40, 0, 0);
        ui.weaponGrid = weaponsSection.gameObject.AddComponent<TemplateGrid>();
        var row = NewRect(weaponsSection, "Weapon Row Template", 0, 0, 0, 0);
        var rowUI = row.gameObject.AddComponent<WeaponRowUI>();
        rowUI.icon = NewImage(row, "Icon", null, Color.white, -92, 0, 16, 16);
        rowUI.label = Label(row, "Name", style, "WEAPON NAME", -74, 4, style.dimTextColor);
        rowUI.bar = Bar(row, "Energy Bar", style, 40, 0);
        ui.weaponGrid.template = row;
        ui.weaponGrid.cellSize = new Vector2Int(200, 18);
        ui.weaponGrid.columns = 1;
        row.gameObject.SetActive(false);

        Box(content, "Divider", style.borderColor, 0, 0, 196, 1);

        // Items: heading + grid of slots (icon with its count under it)
        var itemsSection = NewRect(content, "Items", 0, -20, 0, 0);
        Label(itemsSection, "Heading", style, "ITEMS", -92, 0, style.titleColor, "Weapons_Items");
        var gridRect = NewRect(itemsSection, "Item Grid", 0, 0, 0, 0);
        ui.itemGrid = gridRect.gameObject.AddComponent<TemplateGrid>();
        var slot = NewRect(gridRect, "Item Slot Template", -84, -22, 0, 0);
        var slotUI = slot.gameObject.AddComponent<ItemSlotUI>();
        slotUI.icon = NewImage(slot, "Icon", null, Color.white, 0, 0, 16, 16);
        slotUI.count = Label(slot, "Count", style, "00", -9, -10, style.textColor);
        ui.itemGrid.template = slot;
        ui.itemGrid.cellSize = new Vector2Int(26, 30);
        ui.itemGrid.columns = 6;
        slot.gameObject.SetActive(false);

        ui.items = new List<PauseMenuUI.ItemEntry>
        {
            new() { item = Item.ItemType.ETank, icon = style.eTankSprite, effect = PauseMenuUI.ItemEffect.Health },
            new() { item = Item.ItemType.WTank, icon = style.wTankSprite, effect = PauseMenuUI.ItemEffect.Weapons },
            new() { item = Item.ItemType.MTank, icon = style.mTankSprite, effect = PauseMenuUI.ItemEffect.HealthAndWeapons },
            new() { item = Item.ItemType.STank, icon = style.sTankSprite, effect = PauseMenuUI.ItemEffect.HealthAndWeapons },
            new() { item = Item.ItemType.LTank, icon = style.lTankSprite, effect = PauseMenuUI.ItemEffect.Health },
        };

        // Status: lives, life bar, screws
        var status = NewRect(content, "Status", 0, -80, 0, 0);
        Icon(status, "Lives Icon", style.livesSprite, -84, 0);
        ui.livesText = Label(status, "Lives", style, "00", -72, 4, style.textColor);
        ui.healthBar = Bar(status, "Life Bar", style, -44, 0);
        ui.healthBar.fillColor = style.textColor;
        Icon(status, "Screws Icon", style.screwSprite, 40, 0);
        ui.screwsText = Label(status, "Screws", style, "000", 52, 4, style.textColor);
    }
    #endregion

    #region Elements (all on whole game pixels)
    private static RectTransform NewRect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);
        return rect;
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite, Color color, float x, float y, int w, int h)
    {
        // Odd sizes centre on a half pixel; nudge them so every edge lands on a whole game pixel
        var rect = NewRect(parent, name, x + (w % 2) * 0.5f, y + (h % 2) * 0.5f, w, h);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image Box(Transform parent, string name, Color color, int x, int y, int w, int h)
        => NewImage(parent, name, null, color, x, y, w, h);

    private static Image Icon(Transform parent, string name, Sprite sprite, int x, int y)
    {
        Vector2Int size = sprite != null ? Vector2Int.RoundToInt(sprite.rect.size) : new Vector2Int(16, 16);
        return NewImage(parent, name, sprite, sprite != null ? Color.white : Color.clear, x, y, size.x, size.y);
    }

    // Panel > Border, Background, Content. PixelPanelFit sizes the border/background around the content.
    private static PixelPanelFit Panel(Transform parent, string name, PauseMenuStyle style, out RectTransform content)
    {
        var panel = NewRect(parent, name, 0, 0, 0, 0);
        var border = Box(panel, "Border", style.borderColor, 0, 0, 64, 64);
        var background = Box(panel, "Background", style.panelColor, 0, 0, 60, 60);
        content = NewRect(panel, "Content", 0, 0, 0, 0);

        var fit = panel.gameObject.AddComponent<PixelPanelFit>();
        fit.content = content;
        fit.border = border.rectTransform;
        fit.background = background.rectTransform;
        fit.borderThickness = 2;
        return fit;
    }

    // Text with its top-left corner at (x, top): rect 9 px tall (the glyph ascent) so the cursor can centre on it.
    // Alignment stays Top Left; centred texts are centred by LocalizedText on a whole pixel.
    private static TMP_Text Label(Transform parent, string name, PauseMenuStyle style, string value, int x, int top, Color color,
                                  string key = null, bool centred = false)
    {
        var rect = NewRect(parent, name, x, top, 200, 9);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, top);

        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (style.font != null) text.font = style.font;
        text.fontSize = style.fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = color;
        text.raycastTarget = false;
        text.text = value;

        if (!string.IsNullOrEmpty(key) || centred)
        {
            var localized = rect.gameObject.AddComponent<LocalizedText>();
            localized.key = key;
            localized.fallback = value;
            localized.pixelCentre = centred;
            localized.centreX = x;
        }
        if (centred)
            rect.anchoredPosition = new Vector2(Mathf.Round(x - text.GetPreferredValues(value).x / 2f), top);
        return text;
    }

    // 28 one-pixel segments with one-pixel gaps on a 58 x 8 px dark background; centred vertically on centreY
    private static SegmentBar Bar(Transform parent, string name, PauseMenuStyle style, int left, int centreY)
    {
        var background = Box(parent, name, style.panelColor, left + 29, centreY, 58, 8);
        var bar = background.gameObject.AddComponent<SegmentBar>();
        bar.segments = 28;
        bar.segmentWidth = 1;
        bar.gap = 1;
        bar.segmentHeight = 6;
        bar.padding = 1;
        bar.emptyColor = style.emptyBarColor;
        return bar;
    }
    #endregion
}

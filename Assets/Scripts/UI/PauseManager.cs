using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Logic of the in-game menus. The menus are scene objects (PauseMenuUI on its own canvas, created with
/// Tools > Mega Man > Create Pause Menu Canvas): pages that slide left/right with a page indicator.
///   Page 0 - Pause:   Resume / Options / Exit (with confirmation). Opened with the OptionsMenu action (Start / Space).
///   Page 1 - Weapons: weapons, items (tanks), lives, screws. Opened with the WeaponsMenu action (Y / P).
/// Left/right at the edge of a page, or the weapon-switch buttons (LB/RB, Q/E), slide to the next page.
/// Options loads the Options screen additively over the paused game and comes back to the pause menu.
/// The player who pauses owns the menus (MenuInput): only their devices drive them until they close.
/// Lives on the GameManager object, so it persists between scenes.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    private const int PausePage = 0;
    private const int WeaponsPage = 1;

    private enum State { Closed, Open, ConfirmExit, Options }

    [Tooltip("Used by the editor tool that creates the menu canvas, and if a scene has no PauseMenuUI.")]
    [SerializeField] private PauseMenuStyle style = new();

    [Header("Sounds")]
    [SerializeField] private AudioEvent openSound;
    [SerializeField] private AudioEvent closeSound;
    [SerializeField] private AudioEvent moveSound;
    [SerializeField] private AudioEvent selectSound;
    [SerializeField] private AudioEvent refillSound;
    [Tooltip("Played when sliding between pages (move sound if empty).")]
    [SerializeField] private AudioEvent pageSound;

    [Header("Scenes")]
    [SerializeField] private string optionsSceneName = "OptionsScreen";
    [SerializeField] private string titleSceneName = "TitleScreen";

    [Header("Options transition")]
    [Tooltip("NES-style fade to black between the game and the Options screen: how dark each step is (1 = black).")]
    [SerializeField] private float[] fadeSteps = { 0.25f, 0.5f, 0.75f, 1f };
    [Tooltip("Seconds per fade step (0.05 = 3 frames at 60 fps).")]
    [SerializeField] private float fadeStepTime = 0.05f;

    [Header("Navigation")]
    [SerializeField] private float repeatDelay = 0.35f;
    [SerializeField] private float repeatRate = 0.1f;

    public bool IsOpen => state != State.Closed;
    public PauseMenuStyle Style => style;

    private State state = State.Closed;
    private LocalPlayerInput owner;     // the player who paused: only they drive the menus (MenuInput)
    private Megaman player;             // their Mega Man
    private PauseMenuUI ui;

    // Weapons page selection: weapon rows first, then the item grid
    private class Entry
    {
        public RectTransform target;
        public TMP_Text label;
        public WeaponData weapon;
        public WeaponRowUI row;
        public PauseMenuUI.ItemEntry item;
        public int rowIndex, column;
        public bool IsWeapon => row != null;
    }
    private readonly List<Entry> entries = new();
    private readonly List<WeaponData> weapons = new();
    private int selected;
    private int pauseSelected;
    private int confirmSelected;          // 0 = NO, 1 = YES
    private bool selectionDirty;

    private readonly List<GameObject> hiddenForOptions = new();
    private Image fadeImage;
    private bool optionsBusy;      // opening / closing the Options overlay (fades, scene load / unload)

    /// <summary>A menu fade / Options swap is running: menus should ignore input until it ends.</summary>
    public bool IsTransitioning => optionsBusy;

    private Vector2Int heldDirection;
    private float nextRepeatTime;

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) return;   // the GameManager duplicate destroys itself
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        MenuInput.SubmitPerformed += OnSubmit;
        MenuInput.CancelPerformed += OnCancel;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance != this) return;
        MenuInput.SubmitPerformed -= OnSubmit;
        MenuInput.CancelPerformed -= OnCancel;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            PauseTime(false);   // never leave the game stuck at timeScale 0
            ReleaseMenus();
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The Options screen over the paused game: both scenes share world space, so its cameras would also
        // draw the level. Limit them to the UI layers before their first frame.
        if (mode == LoadSceneMode.Additive && state == State.Options && scene.name == optionsSceneName)
        {
            int uiLayers = LayerMask.GetMask("UI", "PixelPerfectOutput");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    cam.cullingMask &= uiLayers;
            return;
        }

        // A normal scene change (respawn, title...) always leaves the menus closed
        if (mode == LoadSceneMode.Single && state != State.Options)
        {
            PauseTime(false);
            state = State.Closed;
            ui = null;
            ReleaseMenus();
        }
    }
    #endregion

    #region Opening / closing
    // The pause / weapons buttons, from each local player's own devices: whoever presses one pauses and then owns
    // the menus. While open, only the owner's buttons close them or slide the pages.
    private void PollMenuButtons()
    {
        if (state == State.Closed)
        {
            foreach (var p in LocalPlayerInput.All)
            {
                if (p.PausePressed) { OnPageButton(PausePage, p); return; }
                if (p.WeaponsMenuPressed) { OnPageButton(WeaponsPage, p); return; }
            }
            return;
        }

        if (owner == null) return;
        if (owner.PausePressed) OnPageButton(PausePage, owner);
        else if (owner.WeaponsMenuPressed) OnPageButton(WeaponsPage, owner);
        else if (state == State.Open && owner.NextWeaponPressed) GoToPage(ui.CurrentPage + 1);
        else if (state == State.Open && owner.PreviousWeaponPressed) GoToPage(ui.CurrentPage - 1);
    }

    private bool CanOpen(LocalPlayerInput by)
    {
        if (ScreenFader.Instance != null && ScreenFader.Instance.IsTransitioning) return false;
        if (RoomTransition.AnyRunning) return false;   // the screen is scrolling to the next room
        player = by != null ? by.GetComponent<Megaman>() : null;
        return player != null && player.CurrentHealth > 0 && FindMenu() != null;
    }

    // The menus go back to any device (Title screen style) and to no one in particular
    private void ReleaseMenus()
    {
        owner = null;
        MenuInput.SetOwner(null);
    }

    // The scene's PauseMenuUI; scenes without one get a generated copy so the game always has menus
    private PauseMenuUI FindMenu()
    {
        if (ui != null) return ui;
        ui = FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            ui = PauseMenuFactory.Build(style, GameCamera());
            Debug.Log("PauseManager: no PauseMenuUI in the scene, generated one (Tools > Mega Man > Create Pause Menu Canvas adds an editable one).");
        }
        return ui;
    }

    private static Camera GameCamera()
    {
        var ppc = FindFirstObjectByType<PixelPerfectCamera>();
        return ppc != null ? ppc.GetComponent<Camera>() : Camera.main;
    }

    // Opens on that page; if open on another page slides to it; if already on it, closes
    private void OnPageButton(int page, LocalPlayerInput by)
    {
        if (optionsBusy) return;
        if (state == State.Closed)
        {
            if (CanOpen(by)) Open(page, by);
        }
        else if (state == State.Open)
        {
            if (ui.CurrentPage == page) Close();
            else GoToPage(page);
        }
        else if (state == State.ConfirmExit && page == PausePage)
        {
            Close();
        }
    }

    private void Open(int page, LocalPlayerInput by)
    {
        owner = by;
        MenuInput.SetOwner(by.PlayerInput);

        if (ui.Canvas.renderMode == RenderMode.ScreenSpaceCamera && ui.Canvas.worldCamera == null)
            ui.Canvas.worldCamera = GameCamera();

        GameManager.Instance?.FreezeEverything(true);
        PauseTime(true);
        Play(openSound);

        state = State.Open;
        ui.Open(page);
        BuildContent();
        pauseSelected = 0;
        RefreshPauseLabels();   // now, not after a slide: the off-screen page must not show the last selection
        selectionDirty = true;
        heldDirection = Vector2Int.zero;
    }

    private void Close()
    {
        state = State.Closed;
        ReleaseMenus();
        if (ui != null) ui.Close();
        Play(closeSound);
        StartCoroutine(UnfreezeNextFrame());
    }

    // The button that closed the menu (e.g. Jump = Submit) must not also reach Mega Man this frame
    private IEnumerator UnfreezeNextFrame()
    {
        yield return null;
        if (state != State.Closed) yield break;
        PauseTime(false);
        GameManager.Instance?.FreezeEverything(false);
    }

    // Real pause: anything not covered by the freeze (bullets, beams, items, particles, Animators, physics,
    // scaled timers) stops too. Menus, fades and menu sounds run on unscaled time.
    private float timeScaleBeforePause = 1f;
    private bool timePaused;

    private void PauseTime(bool pause)
    {
        if (pause == timePaused) return;
        timePaused = pause;
        if (pause)
        {
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else Time.timeScale = timeScaleBeforePause;
    }

    private void GoToPage(int page)
    {
        if (optionsBusy) return;
        if (ui.SlideTo(page))
        {
            Play(pageSound != null ? pageSound : moveSound);
            selectionDirty = true;
        }
    }
    #endregion

    #region Input
    private void Update()
    {
        PollMenuButtons();
        if (state == State.Closed || state == State.Options || ui == null || optionsBusy) return;

        if (state == State.Open && ui.CurrentPage == WeaponsPage) RefreshWeaponValues();   // e.g. life refilling
        if (selectionDirty && !ui.IsSliding) UpdateSelection();

        Vector2 raw = MenuInput.ReadNavigate();
        Vector2Int dir = new Vector2Int(Mathf.Abs(raw.x) > 0.5f ? (int)Mathf.Sign(raw.x) : 0,
                                        Mathf.Abs(raw.y) > 0.5f ? (int)Mathf.Sign(raw.y) : 0);
        if (dir.x != 0 && dir.y != 0) dir.x = 0;   // one axis at a time

        if (dir == Vector2Int.zero)
        {
            heldDirection = Vector2Int.zero;
            return;
        }

        if (dir != heldDirection)
        {
            heldDirection = dir;
            nextRepeatTime = Time.unscaledTime + repeatDelay;
            Navigate(dir);
        }
        else if (Time.unscaledTime >= nextRepeatTime)
        {
            nextRepeatTime = Time.unscaledTime + repeatRate;
            Navigate(dir);
        }
    }

    private void Navigate(Vector2Int dir)
    {
        if (ui.IsSliding) return;

        if (state == State.ConfirmExit)
        {
            confirmSelected = 1 - confirmSelected;
            Play(moveSound);
            UpdateSelection();
            return;
        }

        if (ui.CurrentPage == PausePage)
        {
            if (dir.x != 0) { GoToPage(ui.CurrentPage + dir.x); return; }
            if (ui.pauseOptions == null || ui.pauseOptions.Length == 0) return;
            pauseSelected = (pauseSelected - dir.y + ui.pauseOptions.Length) % ui.pauseOptions.Length;
            Play(moveSound);
            UpdateSelection();
            return;
        }

        // Weapons page: move inside the grid; pushing past its left/right edge slides to the next page
        int next = FindNeighbour(dir);
        if (next == selected)
        {
            if (dir.x != 0) GoToPage(ui.CurrentPage + dir.x);
            return;
        }
        selected = next;
        Play(moveSound);
        UpdateSelection();
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (ui == null || ui.IsSliding || optionsBusy) return;
        if (state == State.ConfirmExit) { SubmitConfirm(); return; }
        if (state != State.Open) return;

        if (ui.CurrentPage == PausePage) SubmitPause();
        else SubmitWeaponsEntry();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (optionsBusy || (ui != null && ui.IsSliding)) return;
        if (state == State.ConfirmExit)
        {
            Play(closeSound);
            ShowConfirm(false);
        }
        else if (state == State.Open)
        {
            Close();
        }
    }
    #endregion

    #region Content
    private void BuildContent()
    {
        weapons.Clear();
        foreach (var w in player.GetWeapons())
            if (w != null && w.isEnabled) weapons.Add(w);

        var equipped = player.GetCurrentWeapon();
        ui.BuildWeapons(weapons, equipped);
        ui.BuildItems(ItemCount);

        entries.Clear();
        int weaponRowCount = 0;
        for (int i = 0; i < ui.weaponRows.Count; i++)
        {
            var row = ui.weaponRows[i];
            Vector2Int cell = ui.weaponGrid.CellPosition(i);
            weaponRowCount = Mathf.Max(weaponRowCount, cell.y + 1);
            entries.Add(new Entry
            {
                target = row.icon != null ? row.icon.rectTransform : (RectTransform)row.transform,
                label = row.label, weapon = weapons[i], row = row, rowIndex = cell.y, column = cell.x
            });
        }
        for (int i = 0; i < ui.itemSlots.Count; i++)
        {
            var slot = ui.itemSlots[i];
            Vector2Int cell = ui.itemGrid.CellPosition(i);
            entries.Add(new Entry
            {
                target = slot.icon != null ? slot.icon.rectTransform : (RectTransform)slot.transform,
                label = slot.count, item = ui.shownItems[i], rowIndex = weaponRowCount + cell.y, column = cell.x
            });
        }

        selected = Mathf.Max(0, entries.FindIndex(e => e.IsWeapon && e.weapon == equipped));
        RefreshWeaponValues();     // creates the bar segments, so the layout below measures the real size
        ui.Relayout();
    }

    private static int ItemCount(Item.ItemType item) =>
        InventoryManager.Instance != null ? InventoryManager.Instance.GetItemCount(item) : 0;

    private void RefreshWeaponValues()
    {
        if (player == null) return;
        float health = player.CurrentHealth / (float)player.MaxHealth;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.IsWeapon)
            {
                var data = e.weapon;
                float energy = data.usesHealthAsEnergy ? health : data.EnergyFraction;
                if (e.row.bar != null) e.row.bar.SetValue(energy, data.primaryColor);
                ui.SetLabelSelected(e.label, i == selected);
            }
            else if (e.label != null)
            {
                int n = ItemCount(e.item.item);
                e.label.text = n.ToString("00");
                ui.SetLabelSelected(e.label, n > 0);
            }
        }

        if (ui.healthBar != null) ui.healthBar.SetValue(health);
        if (GameManager.Instance != null)
        {
            if (ui.livesText != null) ui.livesText.text = Mathf.Max(0, GameManager.Instance.playerLives).ToString("00");
            if (ui.screwsText != null) ui.screwsText.text = GameManager.Instance.screwsCollected.ToString("000");
        }
    }

    private int FindNeighbour(Vector2Int dir)
    {
        if (entries.Count == 0) return 0;
        var cur = entries[selected];
        if (dir.x != 0)
        {
            int idx = entries.FindIndex(e => e.rowIndex == cur.rowIndex && e.column == cur.column + dir.x);
            return idx >= 0 ? idx : selected;
        }

        int maxRow = 0;
        foreach (var e in entries) maxRow = Mathf.Max(maxRow, e.rowIndex);
        int targetRow = cur.rowIndex - dir.y;   // up = previous row, wrapping around
        if (targetRow < 0) targetRow = maxRow;
        if (targetRow > maxRow) targetRow = 0;

        int best = selected, bestDist = int.MaxValue;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].rowIndex != targetRow) continue;
            int d = Mathf.Abs(entries[i].column - cur.column);
            if (d < bestDist) { best = i; bestDist = d; }
        }
        return best;
    }

    private void SubmitWeaponsEntry()
    {
        if (entries.Count == 0) return;
        var e = entries[selected];
        if (e.IsWeapon)
        {
            Play(selectSound);
            player.SwitchWeapon(e.weapon);
            Close();
            return;
        }

        var effect = e.item.effect;
        if (effect == PauseMenuUI.ItemEffect.None || InventoryManager.Instance == null || !InventoryManager.Instance.UseItem(e.item.item))
        {
            Play(moveSound);
            return;
        }

        bool health = effect == PauseMenuUI.ItemEffect.Health || effect == PauseMenuUI.ItemEffect.HealthAndWeapons;
        bool allWeapons = effect == PauseMenuUI.ItemEffect.Weapons || effect == PauseMenuUI.ItemEffect.HealthAndWeapons;
        if (health) player.RestoreHealth(player.MaxHealth, refillSound, false);
        if (allWeapons) player.Weapons.RestoreAllWeapons(refillSound);   // point by point, with the fill sound
        RefreshWeaponValues();
    }
    #endregion

    #region Pause page
    private void SubmitPause()
    {
        Play(selectSound);
        switch (pauseSelected)
        {
            case 0: Close(); break;
            case 1: StartCoroutine(OpenOptions()); break;
            default: ShowConfirm(true); break;
        }
    }

    private void ShowConfirm(bool show)
    {
        state = show ? State.ConfirmExit : State.Open;
        confirmSelected = 0;    // "NO" by default
        ui.ShowConfirm(show);
        UpdateSelection();
    }

    private void SubmitConfirm()
    {
        if (confirmSelected == 0)
        {
            Play(closeSound);
            ShowConfirm(false);
            return;
        }

        Play(selectSound);
        state = State.Closed;
        ReleaseMenus();
        ui.Close();
        PauseTime(false);
        GameManager.Instance?.FreezeEverything(false);
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeToScene(titleSceneName);
        else SceneManager.LoadScene(titleSceneName);
    }
    #endregion

    #region Options overlay
    private IEnumerator OpenOptions()
    {
        if (optionsBusy) yield break;
        optionsBusy = true;
        state = State.Options;

        // NES-style: the whole screen steps down to black, the Options screen is swapped in, then it steps back up
        yield return Fade(true);
        ui.Close();

        // Hide the game's cameras and event system while the Options screen is on top (it brings its own),
        // so BorderManager and the UI pick up the Options scene.
        hiddenForOptions.Clear();
        Scene game = player.gameObject.scene;
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.gameObject.scene == game) Hide(cam.transform.root.gameObject);
        foreach (var es in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            if (es.gameObject.scene == game) Hide(es.gameObject);

        yield return SceneManager.LoadSceneAsync(optionsSceneName, LoadSceneMode.Additive);
        yield return null;    // let the Options screen lay itself out before it shows
        yield return Fade(false);
        optionsBusy = false;
    }

    // Steps a black screen over everything (darker each step), or back to clear. No smooth blending.
    private IEnumerator Fade(bool toBlack)
    {
        EnsureFadeImage();
        fadeImage.gameObject.SetActive(true);
        int n = fadeSteps.Length;
        for (int i = 0; i < n; i++)
        {
            // to black: steps in order (…, 1). back: the same steps in reverse, ending at 0 (clear)
            float darkness = toBlack ? fadeSteps[i] : (i < n - 1 ? fadeSteps[n - 2 - i] : 0f);
            fadeImage.color = new Color(0f, 0f, 0f, darkness);
            yield return new WaitForSecondsRealtime(fadeStepTime);
        }
        if (!toBlack) fadeImage.gameObject.SetActive(false);
    }

    private void EnsureFadeImage()
    {
        if (fadeImage != null) return;
        var go = new GameObject("Menu Fade", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;     // over the game, the border and the screen filters

        var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)imageGo.transform;
        rect.SetParent(go.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fadeImage = imageGo.GetComponent<Image>();
        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(false);
    }

    private void Hide(GameObject go)
    {
        if (!go.activeSelf || hiddenForOptions.Contains(go)) return;
        go.SetActive(false);
        hiddenForOptions.Add(go);
    }

    /// <summary>
    /// Called by the Options screen's Exit (ChangeScene) while it is open over the game.
    /// Returns true when it handled the exit (so the Options screen doesn't load the Title screen).
    /// </summary>
    public bool TryCloseOptionsOverlay()
    {
        if (state != State.Options) return false;
        if (!optionsBusy) StartCoroutine(CloseOptions());   // spammed Back: already on its way out
        return true;
    }

    private IEnumerator CloseOptions()
    {
        optionsBusy = true;
        yield return Fade(true);

        Scene options = SceneManager.GetSceneByName(optionsSceneName);
        if (options.isLoaded) yield return SceneManager.UnloadSceneAsync(options);

        foreach (var go in hiddenForOptions)
            if (go != null) go.SetActive(true);
        hiddenForOptions.Clear();

        BorderManager.Instance?.RefreshCamera();

        // Controls may have been rebound in Options: pick them up, and make sure Mega Man's input is on
        MenuInput.ReloadBindings();
        if (player != null && player.TryGetComponent(out PlayerInput playerInput) && playerInput.actions != null)
        {
            InputBindingStore.Load(playerInput.actions, player.PlayerIndex);
            playerInput.ActivateInput();
            playerInput.actions.Enable();
        }

        state = State.Open;
        ui.Open(PausePage);
        BuildContent();            // texts and layout again: the language may have changed
        pauseSelected = 1;
        selectionDirty = true;

        yield return null;
        yield return Fade(false);
        optionsBusy = false;
    }
    #endregion

    #region Selection / cursor
    private void UpdateSelection()
    {
        selectionDirty = false;
        RectTransform target = null;

        if (state == State.ConfirmExit)
        {
            ui.SetLabelSelected(ui.confirmNo, confirmSelected == 0);
            ui.SetLabelSelected(ui.confirmYes, confirmSelected == 1);
            var chosen = confirmSelected == 0 ? ui.confirmNo : ui.confirmYes;
            if (chosen != null) target = chosen.rectTransform;
        }
        else if (ui.CurrentPage == PausePage)
        {
            RefreshPauseLabels();
            if (ui.pauseOptions != null && ui.pauseOptions.Length > 0) target = ui.pauseOptions[pauseSelected].rectTransform;
        }
        else
        {
            RefreshWeaponValues();
            if (entries.Count > 0) target = entries[selected].target;
        }
        ui.PlaceCursor(target);
    }

    private void RefreshPauseLabels()
    {
        if (ui.pauseOptions == null) return;
        for (int i = 0; i < ui.pauseOptions.Length; i++)
            ui.SetLabelSelected(ui.pauseOptions[i], i == pauseSelected);
    }
    #endregion

    private void Play(AudioEvent clip)
    {
        if (clip != null && Audio.IsReady) Audio.Play(clip);
    }
}

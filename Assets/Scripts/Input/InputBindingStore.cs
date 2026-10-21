using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Saved control rebinds, one set per player (PlayerPrefs "rebinds_p1", "rebinds_p2"...), each loaded into that
/// player's own copy of the actions. Player 1 also picks up the old single "rebinds" key the first time.
/// </summary>
public static class InputBindingStore
{
    private const string LegacyKey = "rebinds";

    /// <summary>0 = player 1.</summary>
    public static string Key(int playerIndex) => $"rebinds_p{playerIndex + 1}";

    public static bool Has(int playerIndex)
    {
        MigrateLegacy(playerIndex);
        return PlayerPrefs.HasKey(Key(playerIndex));
    }

    /// <summary>Applies that player's saved rebinds to `actions` (nothing saved: leaves it as it is).</summary>
    public static void Load(IInputActionCollection2 actions, int playerIndex)
    {
        if (actions == null || !Has(playerIndex)) return;
        actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(Key(playerIndex)));
    }

    public static void Save(IInputActionCollection2 actions, int playerIndex)
    {
        if (actions == null) return;
        PlayerPrefs.SetString(Key(playerIndex), actions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    // Before per-player rebinds there was one "rebinds" key: it becomes player 1's
    private static void MigrateLegacy(int playerIndex)
    {
        if (playerIndex != 0 || PlayerPrefs.HasKey(Key(0)) || !PlayerPrefs.HasKey(LegacyKey)) return;
        PlayerPrefs.SetString(Key(0), PlayerPrefs.GetString(LegacyKey));
        PlayerPrefs.DeleteKey(LegacyKey);
        PlayerPrefs.Save();
    }
}

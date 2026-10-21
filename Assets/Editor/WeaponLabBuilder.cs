using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Mega Man > Create Weapon Lab Scene: copies Test_00 (so the camera, HUD, managers and pause menu are the
/// same as the game), removes its level, enemies and items, and builds a test room: floor, walls, a low ceiling,
/// a step, a 45° ramp and training dummies. Open the Scene view's Weapon Preview to see the predicted paths.
/// </summary>
public static class WeaponLabBuilder
{
    private const string SourceScene = "Assets/Scenes/Test_00.unity";
    private const string LabScene = "Assets/Scenes/WeaponLab.unity";
    private const float Floor = -2.8f;                 // top of the floor = where Mega Man stands in Test_00
    private const float Left = -10f, Right = 30f;      // room walls
    private const float Bottom = -5.65f, Top = 9.35f;  // the camera's vertical range in Test_00

    [MenuItem("Tools/Mega Man/Create Weapon Lab Scene")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScene) != null &&
            !EditorUtility.DisplayDialog("Weapon Lab", "WeaponLab.unity already exists. Rebuild it from Test_00?", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        AssetDatabase.DeleteAsset(LabScene);
        if (!AssetDatabase.CopyAsset(SourceScene, LabScene))
        {
            EditorUtility.DisplayDialog("Weapon Lab", $"Couldn't copy {SourceScene}.", "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(LabScene, OpenSceneMode.Single);

        // The block sprite from Test_00's ground
        var square = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Square");
        var blockSprite = square != null && square.TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null;
        var blockMaterial = square != null && square.TryGetComponent(out SpriteRenderer sr2) ? sr2.sharedMaterial : null;

        StripLevel(scene);
        BuildRoom(blockSprite, blockMaterial);
        PlaceMegaman(scene);
        SetCameraBounds(scene);

        var lab = new GameObject("Weapon Lab");
        lab.AddComponent<WeaponLab>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        WeaponPreview.Settings.enabled = true;
        WeaponPreview.Settings.Save();
        EditorUtility.DisplayDialog("Weapon Lab",
            "Weapon Lab created (Assets/Scenes/WeaponLab.unity).\n\nScene view: Overlays menu > Weapon Preview to see the predicted paths.\nPlay: R resets the dummies, F1 hides the info panel.", "OK");
    }

    private static void StripLevel(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            bool remove =
                root.name == "Level test" || root.name == "Square" ||
                root.GetComponentInChildren<EnemyController>(true) != null ||
                root.GetComponentInChildren<Item>(true) != null ||
                root.GetComponentInChildren<LadderHandlers>(true) != null ||
                root.GetComponentInChildren<RushCoil>(true) != null ||
                root.GetComponentInChildren<NewRushCoil>(true) != null ||
                root.GetComponentInChildren<RushJet>(true) != null;
            if (remove) Object.DestroyImmediate(root);
        }
    }

    private static void BuildRoom(Sprite sprite, Material material)
    {
        var room = new GameObject("Room").transform;
        int ground = LayerMask.NameToLayer("Ground");
        var wallColor = new Color(0.35f, 0.4f, 0.55f);

        GameObject Block(string name, float xMin, float yMin, float xMax, float yMax, float angle = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(room, false);
            go.layer = ground;
            go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (material != null) r.sharedMaterial = material;
            r.color = wallColor;
            go.AddComponent<BoxCollider2D>();
            return go;
        }

        Block("Floor", Left, Bottom, Right, Floor);
        Block("Wall Left", Left, Floor, Left + 1, Top);
        Block("Wall Right", Right - 1, Floor, Right, Top);
        Block("Low Ceiling", 4f, Floor + 3f, 10f, Floor + 3.5f);
        Block("Step", 12f, Floor, 15f, Floor + 1.5f);

        // Slopes (ordinary ground): 45° up, a platform, a gentle 2:1 down
        void Slope(string name, float x, float y, int widthPx, int heightPx, SlopeBlock.Rise rise)
        {
            var go = new GameObject(name, typeof(SlopeBlock));
            go.transform.SetParent(room, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.layer = ground;
            var slope = go.GetComponent<SlopeBlock>();
            slope.rise = rise;
            slope.widthPixels = widthPx;
            slope.heightPixels = heightPx;
            slope.color = wallColor;
            if (material != null) slope.material = material;
            slope.Rebuild();
        }
        Slope("Slope 45 Up", 16f, Floor, 32, 32, SlopeBlock.Rise.UpToTheRight);
        Block("Slope Platform", 18f, Floor, 20f, Floor + 2f);
        Slope("Slope 2-1 Down", 20f, Floor, 64, 32, SlopeBlock.Rise.UpToTheLeft);

        // Training dummies
        var dummies = new GameObject("Dummies").transform;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        void Dummy(string name, float x, float y, int health = 28)
        {
            var go = new GameObject(name);
            go.transform.SetParent(dummies, false);
            go.transform.position = new Vector3(x, y + 1f, 0f);
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            if (enemyLayer >= 0) go.layer = enemyLayer;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (material != null) r.sharedMaterial = material;
            r.color = new Color(0.95f, 0.55f, 0.2f);
            r.sortingOrder = 5;
            var c = go.AddComponent<BoxCollider2D>();
            c.isTrigger = true;
            var d = go.AddComponent<TrainingDummy>();
            d.maxHealth = health;
            d.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("f47decd5411deaf46a0af54ba445a67e"));   // megaman_2
        }
        Dummy("Dummy Near", 3f, Floor);
        Dummy("Dummy On Step", 13.5f, Floor + 1.5f);
        Dummy("Dummy Far", 26f, Floor);
        Dummy("Dummy High", 7f, Floor + 5f);
    }

    private static void PlaceMegaman(Scene scene)
    {
        var megaman = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Megaman>(true)).FirstOrDefault(m => m != null);
        if (megaman != null) megaman.transform.position = new Vector3(-6f, Floor, 0f);
    }

    private static void SetCameraBounds(Scene scene)
    {
        foreach (var follow in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CameraFollow>(true)))
        {
            follow.levelBoundsMin = new Vector2(Left, Bottom);
            follow.levelBoundsMax = new Vector2(Right, Top);
            follow.transform.position = new Vector3(Left + 8f, follow.transform.position.y, follow.transform.position.z);
            EditorUtility.SetDirty(follow);
        }
    }
}

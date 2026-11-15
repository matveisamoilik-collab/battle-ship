using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Lets a terminal tool (e.g. Claude Code) drive the open Unity Editor without clicks.
// Write commands (one per line) to <project>/Temp/claude_request.txt:
//   buildall          — run ShipButtlr > Build All
//   capture <level>   — render GameScene for that level from several cameras into Screenshots/
// The bridge runs them on the next editor tick, writes every console message produced meanwhile
// to Logs/claude_bridge.log, and finally creates Temp/claude_done.txt.
// Also available from the menu: ShipButtlr > Capture Level 6 Screenshots. Works in batch mode too:
//   -executeMethod ClaudeBridge.CaptureLevel6
[InitializeOnLoad]
public static class ClaudeBridge
{
    static readonly string ProjectRoot = Path.GetDirectoryName(Application.dataPath);
    static string RequestPath => Path.Combine(ProjectRoot, "Temp", "claude_request.txt");
    static string DonePath    => Path.Combine(ProjectRoot, "Temp", "claude_done.txt");
    static string LogPath     => Path.Combine(ProjectRoot, "Logs", "claude_bridge.log");
    static string ShotDir     => Path.Combine(ProjectRoot, "Screenshots");

    static double nextPoll;
    static bool   running;

    static ClaudeBridge()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (running || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(RequestPath)) return;

        string[] lines;
        try { lines = File.ReadAllLines(RequestPath); File.Delete(RequestPath); }
        catch (IOException) { return; }

        running = true;
        var log = new StringBuilder();
        Application.LogCallback cb = (msg, stack, type) =>
        {
            log.Append('[').Append(type).Append("] ").AppendLine(msg);
            if (type == LogType.Exception || type == LogType.Error) log.AppendLine(stack);
        };
        Application.logMessageReceived += cb;
        try
        {
            foreach (var raw in lines)
            {
                var parts = raw.Trim().Split(' ');
                if (parts.Length == 0 || parts[0].Length == 0) continue;
                switch (parts[0].ToLowerInvariant())
                {
                    case "buildall":
                        GameSetup.BuildAll();
                        break;
                    case "capture":
                        int lvl = parts.Length > 1 && int.TryParse(parts[1], out var l) ? l : 6;
                        Capture(lvl);
                        break;
                    default:
                        Debug.LogWarning("[ClaudeBridge] Unknown command: " + raw);
                        break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            Application.logMessageReceived -= cb;
            running = false;
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, log.ToString());
            File.WriteAllText(DonePath, System.DateTime.Now.ToString("o"));
        }
    }

    [MenuItem("ShipButtlr/Capture Level 6 Screenshots")]
    public static void CaptureLevel6() => Capture(6);

    [MenuItem("ShipButtlr/Capture Level 7 Screenshots")]
    public static void CaptureLevel7() => Capture(7);

    // Opens GameScene, shows only the given level's island set (as GameManager does at runtime),
    // and renders the game camera views to PNG. The scene is reloaded afterwards, so nothing is saved.
    public static void Capture(int level)
    {
        string previous = EditorSceneManager.GetActiveScene().path;
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty && active.path != "Assets/Scenes/GameScene.unity")
        {
            Debug.LogError("[ClaudeBridge] Current scene has unsaved changes — save it first. Capture skipped.");
            return;
        }
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);

        string[] roots = { "Level2_Islands", "Level3_SkullShoals", "Level4_Volcano", "Level5_AnchorBay", "Level6_Dreadhold", "Level4_Clouds", "Level7_KrakensThrone" };
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.parent != null) continue;
            string n = go.name;
            if (System.Array.IndexOf(roots, n) < 0) continue;
            bool on = (n == "Level2_Islands" && level == 2) || (n == "Level3_SkullShoals" && level == 3)
                   || ((n == "Level4_Volcano" || n == "Level4_Clouds") && level == 4)
                   || (n == "Level5_AnchorBay" && level == 5) || (n == "Level6_Dreadhold" && level == 6)
                   || (n == "Level7_KrakensThrone" && level == 7);
            go.gameObject.SetActive(on);
        }

        var cam    = Camera.main;
        var player = GameObject.FindWithTag("Player");
        var bot    = GameObject.Find("BotShip");
        if (cam == null || player == null) { Debug.LogError("[ClaudeBridge] Main Camera or PlayerShip missing."); return; }

        Directory.CreateDirectory(ShotDir);
        string prefix = Path.Combine(ShotDir, "level" + level + "_");

        GameObject boat = null;
        if (level == 6)
        {
            if (bot != null) bot.SetActive(false); // L6 has no regular bot ship
            // Show one gunboat the way a destroyed pillar spawns it (12 units outward of pillar 1)
            var boatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Dreadhold/DreadholdBoat.fbx");
            if (boatPrefab != null)
            {
                boat = (GameObject)Object.Instantiate(boatPrefab);
                boat.transform.position = new Vector3(-30f, 0f, -38f);
                boat.transform.rotation = Quaternion.Euler(0f, 120f, 0f) * boatPrefab.transform.localRotation;
            }
        }

        if (level == 7)
        {
            if (bot != null) bot.SetActive(false);
            player.transform.position = GameManager.Level7PlayerStart;
            PoseLevel7();
        }

        Vector3 p = player.transform.position;
        // 1. Exactly what CameraFollow shows at game start
        Shot(cam, p - player.transform.forward * 21.6f + Vector3.up * 8.8f, p + Vector3.up * 5f, prefix + "1_gameplay.png");
        // 2. Wide overview of the arena
        Shot(cam, new Vector3(-62f, 38f, -72f), new Vector3(0f, 6f, 0f), prefix + "2_overview.png");
        if (level == 6)
        {
            // 3. Close-up of the pillar facing the player spawn + the gunboat
            Shot(cam, new Vector3(14f, 6f, -50f), new Vector3(-4f, 4f, -27f), prefix + "3_pillar.png");
            // 4. Castle walls and cannons
            Shot(cam, new Vector3(22f, 26f, -40f), new Vector3(0f, 18f, 0f), prefix + "4_castle.png");
        }

        if (level == 7)
        {
            Shot(cam, new Vector3(0f, 95f, -95f), new Vector3(0f, 0f, 5f), prefix + "3_topdown.png");
            Shot(cam, new Vector3(-20f, 14f, -38f), new Vector3(0f, 14f, 0f), prefix + "4_throne.png");
            Shot(cam, new Vector3(0f, 16f, 20f), new Vector3(0f, 10f, 57f), prefix + "5_kraken.png");
            Shot(cam, new Vector3(30f, 6f, -26f), new Vector3(18f, 3f, -12f), prefix + "6_sunfish.png");
            Shot(cam, new Vector3(-14f, 4f, -44f), new Vector3(-14f, 4f, -30f), prefix + "7_dolphin.png");
            Shot(cam, new Vector3(-30f, 30f, -5f), new Vector3(-55f, 0f, 25f), prefix + "8_whirlpool.png");
            Shot(cam, new Vector3(40f, 20f, -70f), new Vector3(25f, 10f, -32f), prefix + "9_tentacles.png");
        }

        if (boat != null) Object.DestroyImmediate(boat);
        // Discard every temporary change
        EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? "Assets/Scenes/GameScene.unity" : previous, OpenSceneMode.Single);
    }

    // Level 7 preview poses: sample some tentacle clips, apply the dolphin clap, raise the whirlpool
    static void PoseLevel7()
    {
        string tentPath = "Assets/Models/Kraken/KrakenTentacle.fbx";
        AnimationClip Clip(string fbx, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (o is AnimationClip c && c.name == name) return c;
            return null;
        }
        string[] poses = { "Idle", "Windup", "Slam", "Throw", "Idle", "Windup", "Rise", "Idle" };
        for (int i = 0; i < 8; i++)
        {
            var t = GameObject.Find("Level7_KrakensThrone/Tentacle_" + i);
            if (t == null) continue;
            var model = t.transform.Find("Pivot/Model");
            var clip = Clip(tentPath, poses[i]);
            if (model != null && clip != null)
            {
                // aim the slam / windup ones at the arena centre
                clip.SampleAnimation(model.gameObject, poses[i] == "Slam" ? clip.length : clip.length * 0.5f);
            }
        }
        foreach (var name in new[] { "ThrowerL", "ThrowerR" })
        {
            var t = GameObject.Find("Level7_KrakensThrone/KrakenThrone/" + name + "/Pivot/Model");
            var clip = Clip(tentPath, name == "ThrowerL" ? "Throw" : "Idle");
            if (t != null && clip != null) clip.SampleAnimation(t.gameObject, clip.length * 0.4f);
        }
        var dol = GameObject.Find("Level7_KrakensThrone/Dolphin/Pivot/Model");
        var clap = Clip("Assets/Models/Kraken/Dolphin.fbx", "Applaud");
        if (dol != null && clap != null) clap.SampleAnimation(dol.gameObject, 0.25f);
        var wp = GameObject.Find("Level7_KrakensThrone/Whirlpool/Visual");
        if (wp != null) wp.transform.localScale = new Vector3(1f, 0.025f, 1f);
        var wpRoot = GameObject.Find("Level7_KrakensThrone/Whirlpool");
        if (wpRoot != null) wpRoot.transform.position = new Vector3(wpRoot.transform.position.x, 0.3f, wpRoot.transform.position.z);
        var shield = GameObject.Find("Level7_KrakensThrone/KrakenThrone")?.transform.Find("Shield");
        if (shield != null) shield.gameObject.SetActive(true);
    }

    static void Shot(Camera cam, Vector3 pos, Vector3 lookAt, string path, int w = 1600, int h = 900)
    {
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Debug.Log("[ClaudeBridge] Saved " + path);
    }
}

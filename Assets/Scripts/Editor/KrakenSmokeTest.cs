using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless play-through of the Level 7 kraken encounter (no input needed):
//   Unity.exe -batchmode -projectPath <proj> -executeMethod KrakenSmokeTest.Run -logFile Logs/batch_smoke.log
// (no -quit: the test exits Unity itself; no -nographics: it renders a few frames.)
// 1. Plays the natural flow for ~40 s game time (tentacles -> whirlpool -> surfaced -> dive ...)
//    while steering the player in a circle; the player is healed every frame and the damage it
//    took is tallied per state.
// 2. Drives the rest with the debug API: kraken dies, sunfish, throne rise, jellyfish, rocks,
//    victory + dolphin -> GameManager.LevelCleared.
// Writes Logs/kraken_smoke.log and Screenshots/level7_play_*.png; exit code 0 = pass.
// CurrentLevel is set to 7 for the run and restored afterwards.
[InitializeOnLoad]
public static class KrakenSmokeTest
{
    const string ActiveKey = "KrakenSmoke_Active";
    const string PrevKey   = "KrakenSmoke_PrevLevel";
    const string DoneKey   = "KrakenSmoke_Done";
    const string ErrKey    = "KrakenSmoke_Errors";

    static readonly string Root       = Path.GetDirectoryName(Application.dataPath);
    static readonly string ReportPath = Path.Combine(Root, "Logs", "kraken_smoke.log");
    static readonly string ShotDir    = Path.Combine(Root, "Screenshots");

    static BossEncounterController ctrl;
    static PlayerShip player;
    static BossEncounterController.State lastState = BossEncounterController.State.None;
    static float playStart = -1f;
    static int   step;
    static float stepTime;
    static float damageInState;
    static int   errors;
    static bool  inkChecked;
    static float totalDamage, markDamage;
    static readonly StringBuilder report = new StringBuilder();

    static KrakenSmokeTest()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(DoneKey, false);
        SessionState.SetInt(ErrKey, 0);
        SessionState.SetInt(PrevKey, PlayerPrefs.GetInt("CurrentLevel", 1));
        PlayerPrefs.SetInt("CurrentLevel", 7);
        PlayerPrefs.Save();
        File.WriteAllText(ReportPath, "[KrakenSmoke] start " + System.DateTime.Now.ToString("o") + "\n");
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        if (msg.Contains("[KrakenSmoke]")) return;
        errors++;
        SessionState.SetInt(ErrKey, SessionState.GetInt(ErrKey, 0) + 1);
        Write("ERROR: " + msg + "\n" + stack);
    }

    static void Write(string line)
    {
        report.AppendLine(line);
        File.AppendAllText(ReportPath, line + "\n");
        Debug.Log("[KrakenSmoke] " + line);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(DoneKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                Finish();
            return;
        }
        if (playStart < 0f)
        {
            ctrl = Object.FindFirstObjectByType<BossEncounterController>();
            var pgo = GameObject.FindWithTag("Player");
            player = pgo != null ? pgo.GetComponent<PlayerShip>() : null;
            if (ctrl == null || player == null)
            {
                if (Time.realtimeSinceStartup > 30f) Fail("BossEncounterController or PlayerShip not found — is Level 7 active?");
                return;
            }
            playStart = Time.time;
            stepTime  = Time.time;
            Time.timeScale = 2f;
            Write($"play mode up: level {GameManager.Instance.PlayingLevel}, player at {player.transform.position}");
        }

        var gm = GameManager.Instance;
        float t = Time.time - playStart;

        // heal + tally damage
        if (player.CurrentHP < player.MaxHP)
        {
            damageInState += player.MaxHP - player.CurrentHP;
            totalDamage   += player.MaxHP - player.CurrentHP;
            player.TakeDamage(-(player.MaxHP - player.CurrentHP));
        }

        if (ctrl.CurrentState != lastState)
        {
            Write($"t={t,6:F1}s  state {lastState} -> {ctrl.CurrentState}   (damage taken in {lastState}: {damageInState})");
            lastState = ctrl.CurrentState;
            damageInState = 0f;
            OnEnterState(ctrl.CurrentState);
        }

        if (step == 0) SteerPlayer(t);

        switch (step)
        {
            case 0:   // natural flow
                if (!inkChecked && t >= 4f && ctrl.inkSplat != null)
                {
                    inkChecked = true;
                    ctrl.inkSplat.ShowCached();
                    var img = ctrl.inkSplat.GetComponent<UnityEngine.UI.RawImage>();
                    var tex = img != null ? img.texture as Texture2D : null;
                    float cov = ctrl.inkSplat.CoveredFraction;
                    Write($"ink overlay: coverage of the last baked splat = {cov:P1} (target {ctrl.config.inkScreenCoverage:P0}), hold {ctrl.inkSplat.holdDuration}s, fade {ctrl.inkSplat.fadeDuration}s");
                    if (Mathf.Abs(cov - ctrl.config.inkScreenCoverage) > 0.05f) Fail("ink coverage off target");
                    if (tex != null) File.WriteAllBytes(Path.Combine(ShotDir, "level7_ink_overlay.png"), tex.EncodeToPNG());
                }
                if (t >= 42f) Next("natural flow done — driving the rest with the debug API");
                break;
            case 1: Jump(BossEncounterController.State.P1_Surfaced, 1.2f); break;
            case 2: Damage(6, 0.15f, "kraken"); break;
            case 3: WaitFor(BossEncounterController.State.P2_Sunfish, 12f); break;
            case 4: Wait(2.5f); break;    // rock wall rises
            case 5:
            {
                // Phase 2 arena: doubled, rock wall on the edge, contact costs HP
                if (counter == 0)
                {
                    Write($"phase 2 arena radius {ctrl.ArenaRadius} (phase 1: {ctrl.config.ArenaRadius})");
                    if (Mathf.Abs(ctrl.ArenaRadius - ctrl.config.ArenaRadius * 2f) > 0.01f) { Fail("phase 2 arena is not twice as wide"); break; }
                    markDamage = totalDamage;
                    player.transform.position = new Vector3(0f, 0f, ctrl.ArenaRadius - 9f);   // into the rock wall
                    counter = 1;
                }
                if (totalDamage > markDamage)
                {
                    Write($"edge rock contact dealt {totalDamage - markDamage} damage; ship pushed to r={KrakenUtil.Flat(player.transform.position).magnitude:F1}");
                    player.transform.position = new Vector3(0f, 0f, -30f);
                    Next();
                }
                else if (Time.time - stepTime > 1.5f) Fail("no damage from the edge rocks");
                break;
            }
            case 6: Damage(2, 0.2f, "sunfish"); break;
            case 7: WaitFor(BossEncounterController.State.P2_Jellyfish, 12f); break;
            case 8: Wait(4f); break;     // jellyfish chase + surface
            case 9:
            {
                float before = ctrl.throne.HP;
                ctrl.throne.DebugDamage(1f);   // respects the shield
                if (!ctrl.throne.Shielded || ctrl.throne.HP < before) Fail("throne took damage while shielded");
                else Next($"shield blocked the hit (throne HP {ctrl.throne.HP}), jellyfish: {ctrl.Jellies.Count}");
                break;
            }
            case 10:
                if (ctrl.CurrentState != BossEncounterController.State.P2_Jellyfish) { Next("all jellyfish dead"); break; }
                Damage(8, 0.3f, "jellyfish"); break;   // DebugDamage kills one live jellyfish per call
            case 11: WaitFor(BossEncounterController.State.P2_Rocks, 5f); break;
            case 12: SteerPlayer(t); Wait(7f); break;   // rocks thrown
            case 13: Damage(4, 0.3f, "throne (-> second wave)"); break;
            case 14:
                if (ctrl.CurrentState != BossEncounterController.State.P2_Jellyfish) { Fail("second jellyfish wave did not start after 4 damage"); break; }
                if (!ctrl.throne.Shielded) { Fail("throne not shielded in the second wave"); break; }
                if (Mathf.Abs(ctrl.throne.HP - 5f) > 0.01f) { Fail($"throne HP {ctrl.throne.HP}, expected 5 (8 - 4 + 1)"); break; }
                if (ctrl.Jellies.Count != 5) { Fail($"second wave has {ctrl.Jellies.Count} jellyfish, expected 5"); break; }
                Next($"second wave: throne HP {ctrl.throne.HP}, shielded, {ctrl.Jellies.Count} jellyfish");
                break;
            case 15: Wait(1.5f); break;
            case 16:
                if (ctrl.CurrentState != BossEncounterController.State.P2_Jellyfish) { Next("second wave cleared"); break; }
                Damage(8, 0.3f, "jellyfish (wave 2)"); break;
            case 17: WaitFor(BossEncounterController.State.P2_Rocks, 5f); break;
            case 18: Wait(1f); break;
            case 19: Damage(5, 0.3f, "throne"); break;
            case 20: WaitFor(BossEncounterController.State.Victory, 3f); break;
            case 21:
                if (gm.IsGameOver) Next("game over panel shown: " + (gm.resultText != null ? gm.resultText.text : "?"));
                else if (Time.time - stepTime > 15f) Fail("victory never reached LevelCleared");
                break;
            case 22:
                Write("CurrentLevel after victory = " + PlayerPrefs.GetInt("CurrentLevel", 1) + " (expected 7 = MaxLevel)");
                SessionState.SetBool(DoneKey, true);
                Time.timeScale = 1f;
                EditorApplication.ExitPlaymode();
                step++;
                break;
        }
    }

    static void OnEnterState(BossEncounterController.State s)
    {
        switch (s)
        {
            case BossEncounterController.State.P1_Tentacles:  Shot("tentacles", 3.0f); break;
            case BossEncounterController.State.P1_Whirlpool:  Shot("whirlpool", 2.3f); break;
            case BossEncounterController.State.P1_Surfaced:   Shot("surfaced", 1.5f); break;
            case BossEncounterController.State.P2_Sunfish:    Shot("sunfish", 2.5f); break;
            case BossEncounterController.State.P2_Jellyfish:  Shot("jellyfish", 2.0f); break;
            case BossEncounterController.State.P2_Rocks:      Shot("rocks", 3.0f); break;
            case BossEncounterController.State.Victory:       Shot("dolphin", 5.2f); break;
        }
    }

    // ---- steps ----------------------------------------------------------------------------

    static void Next(string msg = null)
    {
        if (msg != null) Write(msg);
        step++;
        stepTime = Time.time;
        counter = 0;
    }

    static void Wait(float seconds) { if (Time.time - stepTime >= seconds) Next(); }

    static void WaitFor(BossEncounterController.State s, float timeout)
    {
        if (ctrl.CurrentState == s) { Next(); return; }
        if (Time.time - stepTime > timeout) Fail($"timed out waiting for {s} (still {ctrl.CurrentState})");
    }

    static void Jump(BossEncounterController.State s, float settle)
    {
        if (counter == 0) { ctrl.JumpTo(s); counter = 1; }
        if (Time.time - stepTime >= settle) Next();
    }

    static int   counter;
    static float lastHit;

    static void Damage(int hits, float interval, string what)
    {
        if (counter < hits && Time.time - lastHit >= interval)
        {
            ctrl.DebugDamage();
            counter++;
            lastHit = Time.time;
        }
        if (counter >= hits && Time.time - lastHit >= interval) Next($"dealt {hits} debug hit(s) to {what}");
    }

    // circle inside the arena so slams / ink / whirlpool have a moving target
    static void SteerPlayer(float t)
    {
        if (ctrl.CurrentState == BossEncounterController.State.P1_Whirlpool) return;   // let the pull act
        float a = t * 0.5f;
        var p = new Vector3(Mathf.Sin(a) * 24f, 0f, Mathf.Cos(a) * 24f);
        player.transform.SetPositionAndRotation(p, Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a))));
    }

    static void Shot(string name, float delay)
    {
        float at = Time.time + delay;
        void Cap()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Cap; return; }
            if (Time.time < at) return;
            EditorApplication.update -= Cap;
            var cam = Camera.main;
            if (cam == null) return;
            Directory.CreateDirectory(ShotDir);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();   // warm-up: the first render after a new shader variant shows the async-compile placeholder
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, "level7_play_" + name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
        EditorApplication.update += Cap;
    }

    static void Fail(string msg)
    {
        errors++;
        SessionState.SetInt(ErrKey, SessionState.GetInt(ErrKey, 0) + 1);
        Write("FAIL: " + msg);
        SessionState.SetBool(DoneKey, true);
        step = 99;
        Time.timeScale = 1f;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        PlayerPrefs.SetInt("CurrentLevel", SessionState.GetInt(PrevKey, 1));
        PlayerPrefs.Save();
        int errs = SessionState.GetInt(ErrKey, 0);
        File.AppendAllText(ReportPath, $"[KrakenSmoke] finished: {errs} error(s). CurrentLevel restored to {SessionState.GetInt(PrevKey, 1)}\n");
        SessionState.EraseBool(ActiveKey);
        if (Application.isBatchMode) EditorApplication.Exit(errs > 0 ? 1 : 0);
    }
}

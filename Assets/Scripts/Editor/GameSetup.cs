using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Builds the entire ShipButtlr game from scratch.
/// Run via menu: ShipButtlr > Build All
/// </summary>
public static class GameSetup
{
    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    [MenuItem("ShipButtlr/Build All")]
    static void BuildAll()
    {
        EnsureTags();
        CreateFolders();

        var mats = CreateMaterials();
        var explosionPrefab = CreateExplosionPrefab();
        var torpedoPrefab   = CreateTorpedoPrefab(explosionPrefab, mats["Torpedo"]);

        BuildMainMenuScene();
        BuildGameScene(mats, torpedoPrefab);
        ConfigureBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShipButtlr] Build All complete. Open Assets/Scenes/MainMenu.unity and press Play.");
    }

    // -------------------------------------------------------------------------
    // 1. Register custom tags
    // -------------------------------------------------------------------------

    static void EnsureTags()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        foreach (string tag in new[] { "Player", "Enemy", "Wall", "Island" })
        {
            bool found = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) { found = true; break; }

            if (!found)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }
        }
        tagManager.ApplyModifiedProperties();
    }

    // -------------------------------------------------------------------------
    // 2. Create asset folders
    // -------------------------------------------------------------------------

    static void CreateFolders()
    {
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets/Scripts", "Editor");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    // -------------------------------------------------------------------------
    // 3. URP materials
    // -------------------------------------------------------------------------

    static System.Collections.Generic.Dictionary<string, Material> CreateMaterials()
    {
        var dict = new System.Collections.Generic.Dictionary<string, Material>();
        dict["Player"]     = MakeMat("PlayerMaterial",     new Color(0.30f, 0.40f, 0.70f));
        dict["Bot"]        = MakeMat("BotMaterial",        new Color(0.80f, 0.10f, 0.10f));
        dict["Water"]      = MakeMat("WaterMaterial",      new Color(0.05f, 0.30f, 0.60f), smoothness: 0.8f, metallic: 0.1f);
        dict["Torpedo"]    = MakeMat("TorpedoMaterial",    new Color(0.85f, 0.85f, 0.90f)); // silver-white, clearly visible
        dict["Sand"]       = MakeMat("SandMaterial",       new Color(0.85f, 0.75f, 0.45f));
        dict["Grass"]      = MakeMat("GrassMaterial",      new Color(0.25f, 0.55f, 0.20f));
        dict["TreeTrunk"]  = MakeMat("TreeTrunkMaterial",  new Color(0.45f, 0.28f, 0.10f));
        dict["TreeLeaves"] = MakeMat("TreeLeavesMaterial", new Color(0.15f, 0.45f, 0.15f));
        return dict;
    }

    static Material MakeMat(string assetName, Color color,
                             float smoothness = 0f, float metallic = 0f)
    {
        string path = "Assets/Materials/" + assetName + ".mat";

        // Overwrite if it already exists from a previous run
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[ShipButtlr] URP Lit shader not found. Make sure URP is active.");
            shader = Shader.Find("Standard");
        }
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);  // URP uses _BaseColor, not _Color
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic",   metallic);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // -------------------------------------------------------------------------
    // 4. Explosion particle prefab
    // -------------------------------------------------------------------------

    static GameObject CreateExplosionPrefab()
    {
        string path = "Assets/Prefabs/ExplosionEffect.prefab";
        var go = new GameObject("ExplosionEffect");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration      = 0.5f;
        main.loop          = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed    = new ParticleSystem.MinMaxCurve(5f, 12f);
        main.startSize     = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.stopAction    = ParticleSystemStopAction.Destroy;

        // Orange/yellow gradient
        var grad = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.9f, 0f),       // yellow
            new Color(1f, 0.4f, 0f));       // orange
        main.startColor = grad;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        // Ensure renderer uses URP-compatible material
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
            renderer.material = new Material(particleShader);

        var prefab = SavePrefab(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // -------------------------------------------------------------------------
    // 5. Torpedo prefab
    // -------------------------------------------------------------------------

    static GameObject CreateTorpedoPrefab(GameObject explosionPrefab, Material torpedoMat)
    {
        string path = "Assets/Prefabs/Torpedo.prefab";

        // Root empty GO holds physics and logic
        var root = new GameObject("Torpedo");

        // Visual: cylinder child rotated so its long axis aligns with root's Z (forward)
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.transform.SetParent(root.transform);
        cylinder.transform.localPosition = Vector3.zero;
        cylinder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cylinder.transform.localScale    = new Vector3(0.3f, 1.0f, 0.3f); // 2u long, 0.3u wide (spec: ~2 units)
        cylinder.GetComponent<MeshRenderer>().sharedMaterial = torpedoMat;
        Object.DestroyImmediate(cylinder.GetComponent<CapsuleCollider>()); // collider on root instead

        // Physics on root
        var rb = root.AddComponent<Rigidbody>();
        rb.useGravity          = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Capsule collider aligned to Z (direction=2)
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.height    = 2.0f;  // matches the 2-unit visual length
        col.radius    = 0.15f;

        // Trail renderer — thin white wake
        var trail = root.AddComponent<TrailRenderer>();
        trail.time         = 0.3f;
        trail.startWidth   = 0.25f;
        trail.endWidth     = 0f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var trailShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (trailShader == null) trailShader = Shader.Find("Unlit/Color");
        if (trailShader != null)
        {
            var trailMat = new Material(trailShader);
            trailMat.SetColor("_BaseColor", Color.white);
            trail.material = trailMat;
        }

        // Logic
        var torpedoScript = root.AddComponent<Torpedo>();
        torpedoScript.explosionPrefab = explosionPrefab;

        var prefab = SavePrefab(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // -------------------------------------------------------------------------
    // 6. Main Menu scene
    // -------------------------------------------------------------------------

    static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Directional light
        var lightGO  = new GameObject("Directional Light");
        var light    = lightGO.AddComponent<Light>();
        light.type       = LightType.Directional;
        light.intensity  = 2f;
        light.shadows    = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(30f, -30f, 0f);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.Skybox;
        cam.fieldOfView      = 60f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 5f, -15f);
        camGO.transform.LookAt(Vector3.zero);

        // Decorative water plane
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        water.transform.position   = new Vector3(0f, -0.1f, 0f);
        water.transform.localScale = new Vector3(5f, 1f, 5f);
        var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WaterMaterial.mat");
        if (waterMat != null) water.GetComponent<MeshRenderer>().sharedMaterial = waterMat;

        // CoinManager (persists across scenes via DontDestroyOnLoad)
        new GameObject("CoinManager").AddComponent<CoinManager>();

        // UI Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem with new Input System module
        CreateEventSystem();

        // Title text
        var titleGO  = MakeText("Title", canvasGO.transform, "SEA BATTLE", 72, Color.black);
        var titleRT  = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 0.7f);
        titleRT.anchorMax        = new Vector2(0.5f, 0.9f);
        titleRT.offsetMin        = new Vector2(-400f, 0f);
        titleRT.offsetMax        = new Vector2(400f, 0f);
        titleRT.anchoredPosition = Vector2.zero;
        titleGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        titleGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Manager GO with MainMenu script
        var managerGO     = new GameObject("MainMenuManager");
        var mainMenuScript = managerGO.AddComponent<MainMenu>();

        // Play button
        var playBtnGO = MakeButton("PlayButton", canvasGO.transform, "PLAY");
        var playRT    = playBtnGO.GetComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0.5f, 0.48f);
        playRT.anchorMax        = new Vector2(0.5f, 0.58f);
        playRT.sizeDelta        = new Vector2(300f, 70f);
        playRT.anchoredPosition = Vector2.zero;

        // Shop button
        var shopBtnGO = MakeButton("ShopButton", canvasGO.transform, "SHOP");
        var shopBtnRT = shopBtnGO.GetComponent<RectTransform>();
        shopBtnRT.anchorMin        = new Vector2(0.5f, 0.34f);
        shopBtnRT.anchorMax        = new Vector2(0.5f, 0.44f);
        shopBtnRT.sizeDelta        = new Vector2(300f, 70f);
        shopBtnRT.anchoredPosition = Vector2.zero;

        // Quit button
        var quitBtnGO = MakeButton("QuitButton", canvasGO.transform, "QUIT");
        var quitRT    = quitBtnGO.GetComponent<RectTransform>();
        quitRT.anchorMin        = new Vector2(0.5f, 0.20f);
        quitRT.anchorMax        = new Vector2(0.5f, 0.30f);
        quitRT.sizeDelta        = new Vector2(300f, 70f);
        quitRT.anchoredPosition = Vector2.zero;

        // Wire button callbacks
        UnityEventTools.AddPersistentListener(
            playBtnGO.GetComponent<Button>().onClick,
            mainMenuScript.OnPlayClicked);
        UnityEventTools.AddPersistentListener(
            shopBtnGO.GetComponent<Button>().onClick,
            mainMenuScript.OnShopClicked);
        UnityEventTools.AddPersistentListener(
            quitBtnGO.GetComponent<Button>().onClick,
            mainMenuScript.OnQuitClicked);

        // Coin display — top-left
        var mmCoinGO = MakeText("CoinText", canvasGO.transform, "COINS: 0", 30, Color.yellow);
        var mmCoinRT = mmCoinGO.GetComponent<RectTransform>();
        mmCoinRT.anchorMin        = new Vector2(0f, 1f);
        mmCoinRT.anchorMax        = new Vector2(0f, 1f);
        mmCoinRT.pivot            = new Vector2(0f, 1f);
        mmCoinRT.anchoredPosition = new Vector2(20f, -20f);
        mmCoinRT.sizeDelta        = new Vector2(300f, 50f);
        mmCoinGO.GetComponent<Text>().alignment = TextAnchor.UpperLeft;
        mainMenuScript.coinsText = mmCoinGO.GetComponent<Text>();

        // Shop panel — built last so it renders on top; hidden by default
        var shopPanelGO = BuildShopPanel(canvasGO.transform, mainMenuScript);
        shopPanelGO.SetActive(false);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        Debug.Log("[ShipButtlr] MainMenu scene saved.");
    }

    // -------------------------------------------------------------------------
    // 7. Game scene
    // -------------------------------------------------------------------------

    static void BuildGameScene(
        System.Collections.Generic.Dictionary<string, Material> mats,
        GameObject torpedoPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Directional light
        var lightGO = new GameObject("Directional Light");
        var light   = lightGO.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 2f;
        light.shadows   = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

        // Procedural skybox with sun disk — wire directional light as the sun source
        string skyboxPath = "Assets/Materials/SkyboxMaterial.mat";
        var existingSky = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
        if (existingSky != null) AssetDatabase.DeleteAsset(skyboxPath);
        var skyboxShader = Shader.Find("Skybox/Procedural");
        if (skyboxShader != null)
        {
            var skyboxMat = new Material(skyboxShader);
            skyboxMat.SetInt(  "_SunDisk",             2);                                  // high-quality sun disk
            skyboxMat.SetFloat("_SunSize",             0.04f);
            skyboxMat.SetFloat("_SunSizeConvergence",  5f);
            skyboxMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyboxMat.SetColor("_SkyTint",             new Color(0.5f,  0.5f,  0.5f));
            skyboxMat.SetColor("_GroundColor",         new Color(0.05f, 0.30f, 0.60f));     // ocean blue below horizon
            skyboxMat.SetFloat("_Exposure",            1.3f);
            AssetDatabase.CreateAsset(skyboxMat, skyboxPath);
            RenderSettings.skybox      = skyboxMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.sun         = light;
            DynamicGI.UpdateEnvironment();
        }

        // Sea plane (Plane primitive = 10×10 units; scale 200 = 2000×2000, covers the full horizon)
        var arena = GameObject.CreatePrimitive(PrimitiveType.Plane);
        arena.name = "Arena";
        arena.transform.localScale = new Vector3(200f, 1f, 200f);
        arena.GetComponent<MeshRenderer>().sharedMaterial = mats["Water"];

        // Decorative islands with trees (visual only — no colliders)
        CreateIsland(new Vector3( 72f, 0f,  68f), 12f, 3, mats);
        CreateIsland(new Vector3(-65f, 0f,  75f),  9f, 2, mats);
        CreateIsland(new Vector3( 58f, 0f, -72f), 10f, 3, mats);
        CreateIsland(new Vector3(-80f, 0f, -55f),  8f, 2, mats);
        CreateIsland(new Vector3( 42f, 0f,  82f),  7f, 2, mats);
        CreateIsland(new Vector3(-45f, 0f, -80f), 11f, 3, mats);

        // Invisible boundary walls (BoxCollider only, tagged "Wall")
        CreateWall("Wall_PosX", new Vector3(105f,  5f, 0f),   new Vector3(10f, 10f, 210f));
        CreateWall("Wall_NegX", new Vector3(-105f, 5f, 0f),   new Vector3(10f, 10f, 210f));
        CreateWall("Wall_PosZ", new Vector3(0f,    5f, 105f), new Vector3(210f, 10f, 10f));
        CreateWall("Wall_NegZ", new Vector3(0f,    5f, -105f),new Vector3(210f, 10f, 10f));

        // ----- Ships -----
        // Player at -Z, bot at +Z — both face each other along the Z axis
        var playerShipGO = BuildShipGO("PlayerShip", new Vector3(0f, 0f, -60f),
                                       Quaternion.identity, mats["Player"]);
        playerShipGO.tag = "Player";
        var playerShip   = playerShipGO.AddComponent<PlayerShip>();

        var botShipGO = BuildShipGO("BotShip", new Vector3(0f, 0f, 60f),
                                    Quaternion.Euler(0f, 180f, 0f), mats["Bot"]);
        botShipGO.tag = "Enemy";
        var botShip   = botShipGO.AddComponent<BotShip>();

        // Assign torpedo prefab and spawn point to both ships
        Transform playerSpawn = playerShipGO.transform.Find("TorpedoSpawn");
        playerShip.torpedoPrefab    = torpedoPrefab;
        playerShip.torpedoSpawnPoint = playerSpawn;

        Transform botSpawn = botShipGO.transform.Find("TorpedoSpawn");
        botShip.torpedoPrefab    = torpedoPrefab;
        botShip.torpedoSpawnPoint = botSpawn;

        // ----- Camera -----
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 8f, -75f);  // behind player on Z axis
        var cameraFollow = camGO.AddComponent<CameraFollow>();
        cameraFollow.target = playerShipGO.transform;

        // ----- HUD Canvas -----
        var hudGO    = new GameObject("HUDCanvas");
        var hudCanvas = hudGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 0;
        var hudScaler = hudGO.AddComponent<CanvasScaler>();
        hudScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        hudScaler.referenceResolution = new Vector2(1920f, 1080f);
        hudGO.AddComponent<GraphicRaycaster>();

        // EventSystem
        CreateEventSystem();

        // Player HP bar — bottom-left
        HealthBar playerHealthBar = CreateHPBar(
            "PlayerHP", hudGO.transform,
            "YOUR SHIP", Color.green,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, 20f), new Vector2(220f, 50f));

        // Bot HP bar — top-right
        HealthBar botHealthBar = CreateHPBar(
            "BotHP", hudGO.transform,
            "ENEMY SHIP", new Color(1f, 0.3f, 0.3f),
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-240f, -70f), new Vector2(220f, 50f));

        // Wire health bars to ships
        playerShip.healthBar = playerHealthBar;
        botShip.healthBar    = botHealthBar;

        // ----- End Panel -----
        var endPanelGO = BuildEndPanel(hudGO.transform);

        // ----- GameManager -----
        var gmGO = new GameObject("GameManager");
        var gm   = gmGO.AddComponent<GameManager>();
        gm.endPanel      = endPanelGO;
        gm.cameraFollow  = cameraFollow;

        // Find result text and buttons inside end panel
        gm.resultText      = endPanelGO.transform.Find("ResultText")?.GetComponent<Text>();
        var playAgainBtnGO = endPanelGO.transform.Find("PlayAgainButton");
        var mainMenuBtnGO  = endPanelGO.transform.Find("MainMenuButton");

        if (playAgainBtnGO != null) gm.playAgainButton = playAgainBtnGO.GetComponent<Button>();
        if (mainMenuBtnGO  != null) gm.mainMenuButton  = mainMenuBtnGO.GetComponent<Button>();

        // Wire end panel buttons
        if (gm.playAgainButton != null)
            UnityEventTools.AddPersistentListener(gm.playAgainButton.onClick, gm.PlayAgain);
        if (gm.mainMenuButton != null)
            UnityEventTools.AddPersistentListener(gm.mainMenuButton.onClick, gm.LoadMainMenu);

        endPanelGO.SetActive(false);

        // CoinManager (duplicate destroyed by singleton if MainMenu already created one)
        new GameObject("CoinManager").AddComponent<CoinManager>();

        // Coin display — top-left HUD
        var coinGO = MakeText("CoinText", hudGO.transform, "COINS: 0", 30, Color.yellow);
        var coinRT = coinGO.GetComponent<RectTransform>();
        coinRT.anchorMin        = new Vector2(0f, 1f);
        coinRT.anchorMax        = new Vector2(0f, 1f);
        coinRT.pivot            = new Vector2(0f, 1f);
        coinRT.anchoredPosition = new Vector2(20f, -20f);
        coinRT.sizeDelta        = new Vector2(300f, 50f);
        coinGO.GetComponent<Text>().alignment = TextAnchor.UpperLeft;
        gm.coinsText = coinGO.GetComponent<Text>();

        // ----- Mobile Controls -----
        var circleSprite = MakeCircleSprite();

        // Joystick background — bottom-left
        var joyBgGO = new GameObject("JoystickBackground");
        joyBgGO.transform.SetParent(hudGO.transform, false);
        var joyBgImg = joyBgGO.AddComponent<Image>();
        joyBgImg.sprite = circleSprite;
        joyBgImg.color  = new Color(1f, 1f, 1f, 0.25f);
        var joyBgRT = joyBgGO.GetComponent<RectTransform>();
        joyBgRT.anchorMin        = new Vector2(0f, 0f);
        joyBgRT.anchorMax        = new Vector2(0f, 0f);
        joyBgRT.pivot            = new Vector2(0.5f, 0.5f);
        joyBgRT.anchoredPosition = new Vector2(150f, 150f);
        joyBgRT.sizeDelta        = new Vector2(260f, 260f);

        // Joystick stick — centered inside background
        var joyStickGO = new GameObject("JoystickStick");
        joyStickGO.transform.SetParent(joyBgGO.transform, false);
        var joyStickImg = joyStickGO.AddComponent<Image>();
        joyStickImg.sprite = circleSprite;
        joyStickImg.color  = new Color(1f, 1f, 1f, 0.75f);
        var joyStickRT = joyStickGO.GetComponent<RectTransform>();
        joyStickRT.anchorMin        = new Vector2(0.5f, 0.5f);
        joyStickRT.anchorMax        = new Vector2(0.5f, 0.5f);
        joyStickRT.pivot            = new Vector2(0.5f, 0.5f);
        joyStickRT.anchoredPosition = Vector2.zero;
        joyStickRT.sizeDelta        = new Vector2(110f, 110f);

        // VirtualJoystick script on background
        var joystick = joyBgGO.AddComponent<VirtualJoystick>();
        joystick.background = joyBgRT;
        joystick.stick      = joyStickRT;

        // Fire zone — right 55% of screen, transparent
        var fireZoneGO = new GameObject("FireZone");
        fireZoneGO.transform.SetParent(hudGO.transform, false);
        var fireZoneImg = fireZoneGO.AddComponent<Image>();
        fireZoneImg.color         = new Color(0f, 0f, 0f, 0f);
        fireZoneImg.raycastTarget = false;
        var fireZoneRT = fireZoneGO.GetComponent<RectTransform>();
        fireZoneRT.anchorMin  = new Vector2(0.45f, 0f);
        fireZoneRT.anchorMax  = new Vector2(1f, 1f);
        fireZoneRT.offsetMin  = Vector2.zero;
        fireZoneRT.offsetMax  = Vector2.zero;
        var fireZone = fireZoneGO.AddComponent<FireZone>();

        // Wire mobile controls to player ship
        playerShip.virtualJoystick = joystick;
        playerShip.fireZone        = fireZone;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameScene.unity");
        Debug.Log("[ShipButtlr] GameScene saved.");
    }

    // -------------------------------------------------------------------------
    // 8. Build settings
    // -------------------------------------------------------------------------

    static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true),
        };
        Debug.Log("[ShipButtlr] Build Settings updated.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    static GameObject SavePrefab(GameObject go, string path)
    {
        bool success;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out success);
        if (!success) Debug.LogError("[ShipButtlr] Failed to save prefab at " + path);
        return prefab;
    }

    static void CreateWall(string name, Vector3 pos, Vector3 size)
    {
        var go  = new GameObject(name);
        go.tag  = "Wall";
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider>();
        col.size = size;
    }

    static void CreateIsland(Vector3 pos, float radius, int treeCount,
        System.Collections.Generic.Dictionary<string, Material> mats)
    {
        var island = new GameObject("Island");
        island.transform.position = pos;
        island.tag = "Island";
        island.AddComponent<IslandData>().radius = radius;

        // Sandy base: flat cylinder. Unity Cylinder default: radius=0.5, height=2 total.
        // scaleX/Z = radius*2 → desired radius; scaleY=0.15 → 0.3u total height.
        // Keep the CapsuleCollider so torpedoes physically explode on contact.
        var baseGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseGO.name = "IslandBase";
        baseGO.transform.SetParent(island.transform);
        baseGO.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        baseGO.transform.localScale    = new Vector3(radius * 2f, 0.15f, radius * 2f);
        baseGO.GetComponent<MeshRenderer>().sharedMaterial = mats["Sand"];

        // Grassy top: slightly smaller cylinder sitting on top of base
        var topGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topGO.name = "IslandTop";
        topGO.transform.SetParent(island.transform);
        topGO.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        topGO.transform.localScale    = new Vector3(radius * 1.6f, 0.08f, radius * 1.6f);
        topGO.GetComponent<MeshRenderer>().sharedMaterial = mats["Grass"];
        Object.DestroyImmediate(topGO.GetComponent<Collider>());

        // Trees with deterministic seed so layout is stable across repeated BuildAll runs
        var rng = new System.Random((int)(pos.x * 100 + pos.z));
        for (int i = 0; i < treeCount; i++)
        {
            float angle = (float)(rng.NextDouble() * 360.0);
            float dist  = (float)(rng.NextDouble() * radius * 0.55f);
            float tx    = Mathf.Cos(angle * Mathf.Deg2Rad) * dist;
            float tz    = Mathf.Sin(angle * Mathf.Deg2Rad) * dist;
            CreateTree(new Vector3(pos.x + tx, 0.43f, pos.z + tz), island, mats);
        }
    }

    static void CreateTree(Vector3 pos, GameObject parent,
        System.Collections.Generic.Dictionary<string, Material> mats)
    {
        var tree = new GameObject("Tree");
        tree.transform.SetParent(parent.transform);
        tree.transform.position = pos;

        // Trunk: Cylinder. scaleY=0.75 → 1.5u total height; scaleX/Z=0.3 → 0.3u radius.
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        trunk.transform.localScale    = new Vector3(0.3f, 0.75f, 0.3f);
        trunk.GetComponent<MeshRenderer>().sharedMaterial = mats["TreeTrunk"];
        Object.DestroyImmediate(trunk.GetComponent<Collider>());

        // Canopy: Sphere. scale=2.5 → 2.5u diameter; sits just above trunk top.
        var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "Leaves";
        leaves.transform.SetParent(tree.transform);
        leaves.transform.localPosition = new Vector3(0f, 2.75f, 0f);
        leaves.transform.localScale    = new Vector3(2.5f, 2.5f, 2.5f);
        leaves.GetComponent<MeshRenderer>().sharedMaterial = mats["TreeLeaves"];
        Object.DestroyImmediate(leaves.GetComponent<Collider>());
    }

    // Builds a ship root GO with hull, cabin, and torpedo spawn point
    static GameObject BuildShipGO(string name, Vector3 pos, Quaternion rot, Material mat)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.rotation = rot;

        // Hull — elongated cube, has the BoxCollider torpedoes will hit
        var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hull.name = "Hull";
        hull.transform.SetParent(root.transform);
        hull.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        hull.transform.localScale    = new Vector3(5f, 1f, 12f);
        hull.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Cabin — elevated rear structure
        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        cabin.transform.SetParent(root.transform);
        cabin.transform.localPosition = new Vector3(0f, 1.5f, 1f);
        cabin.transform.localScale    = new Vector3(2f, 1.5f, 3f);
        cabin.GetComponent<MeshRenderer>().sharedMaterial = mat;
        // Remove cabin collider — only hull needs to detect torpedo hits
        Object.DestroyImmediate(cabin.GetComponent<BoxCollider>());

        // Torpedo spawn: 1.5 units clear of hull tip (hull half-length = 6), elevated to Y=0.8
        var spawn = new GameObject("TorpedoSpawn");
        spawn.transform.SetParent(root.transform);
        spawn.transform.localPosition = new Vector3(0f, 0.8f, 7.5f);

        return root;
    }

    // Creates a labelled HP bar and returns its HealthBar component
    static HealthBar CreateHPBar(
        string groupName, Transform parent,
        string label, Color fillColor,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size)
    {
        // Group container
        var groupGO = new GameObject(groupName);
        groupGO.transform.SetParent(parent, false);
        var groupRT        = groupGO.AddComponent<RectTransform>();
        groupRT.anchorMin  = anchorMin;
        groupRT.anchorMax  = anchorMax;
        groupRT.pivot      = anchorMin;         // pivot matches anchor corner
        groupRT.anchoredPosition = anchoredPos;
        groupRT.sizeDelta  = size;

        // Label
        var labelGO = MakeText("Label", groupGO.transform, label, 18, Color.white);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin        = new Vector2(0f, 1f);
        labelRT.anchorMax        = new Vector2(1f, 1f);
        labelRT.pivot            = new Vector2(0f, 0f);
        labelRT.anchoredPosition = new Vector2(0f, 2f);
        labelRT.sizeDelta        = new Vector2(0f, 22f);

        // Background (dark bar)
        var bgGO  = new GameObject("Background");
        bgGO.transform.SetParent(groupGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        var bgRT   = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin        = new Vector2(0f, 0f);
        bgRT.anchorMax        = new Vector2(1f, 0f);
        bgRT.pivot            = new Vector2(0f, 0f);
        bgRT.anchoredPosition = Vector2.zero;
        bgRT.sizeDelta        = new Vector2(0f, 24f);

        // Fill (coloured progress)
        var fillGO  = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color      = fillColor;
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin        = Vector2.zero;
        fillRT.anchorMax        = Vector2.one;
        fillRT.offsetMin        = Vector2.zero;
        fillRT.offsetMax        = Vector2.zero;
        fillRT.pivot            = new Vector2(0f, 0.5f);

        // HealthBar script lives on the background so it knows which fill to update
        var hb = bgGO.AddComponent<HealthBar>();
        hb.fillImage = fillImg;

        return hb;
    }

    // Builds the full-screen end panel (hidden by default via SetActive(false) after wiring)
    static GameObject BuildEndPanel(Transform parent)
    {
        var panelGO = new GameObject("EndPanel");
        panelGO.transform.SetParent(parent, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.75f);
        var panelRT   = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin  = Vector2.zero;
        panelRT.anchorMax  = Vector2.one;
        panelRT.offsetMin  = Vector2.zero;
        panelRT.offsetMax  = Vector2.zero;

        // Result text
        var resultGO = MakeText("ResultText", panelGO.transform, "VICTORY!", 72, Color.white);
        var resultRT = resultGO.GetComponent<RectTransform>();
        resultRT.anchorMin        = new Vector2(0.5f, 0.6f);
        resultRT.anchorMax        = new Vector2(0.5f, 0.75f);
        resultRT.offsetMin        = new Vector2(-400f, 0f);
        resultRT.offsetMax        = new Vector2(400f, 0f);
        resultRT.anchoredPosition = Vector2.zero;
        var resultText = resultGO.GetComponent<Text>();
        resultText.alignment = TextAnchor.MiddleCenter;
        resultText.fontStyle = FontStyle.Bold;

        // Play Again button
        var playGO = MakeButton("PlayAgainButton", panelGO.transform, "PLAY AGAIN");
        var playRT = playGO.GetComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0.5f, 0.42f);
        playRT.anchorMax        = new Vector2(0.5f, 0.52f);
        playRT.sizeDelta        = new Vector2(320f, 70f);
        playRT.anchoredPosition = Vector2.zero;

        // Main Menu button
        var menuGO = MakeButton("MainMenuButton", panelGO.transform, "MAIN MENU");
        var menuRT = menuGO.GetComponent<RectTransform>();
        menuRT.anchorMin        = new Vector2(0.5f, 0.30f);
        menuRT.anchorMax        = new Vector2(0.5f, 0.40f);
        menuRT.sizeDelta        = new Vector2(320f, 70f);
        menuRT.anchoredPosition = Vector2.zero;

        return panelGO;
    }

    // Creates an EventSystem with InputSystemUIInputModule (required for New Input System)
    static void CreateEventSystem()
    {
        // Avoid duplicate EventSystems when called for both Canvas objects in GameScene
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
    }

    // Creates a legacy UI.Text GameObject
    static Sprite MakeCircleSprite(int size = 128)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r, dy = y - r;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    static GameObject MakeText(string name, Transform parent, string content,
                                int fontSize, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var txt  = go.AddComponent<Text>();
        txt.text      = content;
        txt.fontSize  = fontSize;
        txt.color     = color;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = false;
        return go;
    }

    // Builds the full shop overlay panel and wires all callbacks
    static GameObject BuildShopPanel(Transform canvasTransform, MainMenu script)
    {
        // Root panel — modal overlay
        var shopPanelGO = new GameObject("ShopPanel");
        shopPanelGO.transform.SetParent(canvasTransform, false);
        var panelImg = shopPanelGO.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.15f, 0.95f);
        var panelRT = shopPanelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.1f, 0.1f);
        panelRT.anchorMax = new Vector2(0.9f, 0.9f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Close button — top-right corner
        var closeBtnGO = MakeButton("CloseButton", shopPanelGO.transform, "X");
        var closeRT = closeBtnGO.GetComponent<RectTransform>();
        closeRT.anchorMin        = new Vector2(0.88f, 0.92f);
        closeRT.anchorMax        = new Vector2(0.98f, 0.99f);
        closeRT.sizeDelta        = Vector2.zero;
        closeRT.anchoredPosition = Vector2.zero;

        // Tab buttons — side by side across the top strip
        var toBuyTabGO = MakeButton("ToBuyTab", shopPanelGO.transform, "TO BUY");
        var toBuyTabRT = toBuyTabGO.GetComponent<RectTransform>();
        toBuyTabRT.anchorMin        = new Vector2(0.02f, 0.82f);
        toBuyTabRT.anchorMax        = new Vector2(0.48f, 0.92f);
        toBuyTabRT.sizeDelta        = Vector2.zero;
        toBuyTabRT.anchoredPosition = Vector2.zero;

        var boughtTabGO = MakeButton("BoughtTab", shopPanelGO.transform, "BOUGHT");
        var boughtTabRT = boughtTabGO.GetComponent<RectTransform>();
        boughtTabRT.anchorMin        = new Vector2(0.52f, 0.82f);
        boughtTabRT.anchorMax        = new Vector2(0.98f, 0.92f);
        boughtTabRT.sizeDelta        = Vector2.zero;
        boughtTabRT.anchoredPosition = Vector2.zero;

        // ToBuy content panel
        var toBuyContentGO = new GameObject("ToBuyContent");
        toBuyContentGO.transform.SetParent(shopPanelGO.transform, false);
        var toBuyRT = toBuyContentGO.AddComponent<RectTransform>();
        toBuyRT.anchorMin = new Vector2(0.02f, 0.05f);
        toBuyRT.anchorMax = new Vector2(0.98f, 0.80f);
        toBuyRT.offsetMin = Vector2.zero;
        toBuyRT.offsetMax = Vector2.zero;

        // Yellow ship card (To Buy)
        var yellowToBuyGO = new GameObject("YellowShipCard");
        yellowToBuyGO.transform.SetParent(toBuyContentGO.transform, false);
        var yellowToBuyImg = yellowToBuyGO.AddComponent<Image>();
        yellowToBuyImg.color = new Color(0.25f, 0.22f, 0.05f, 1f);
        var yellowToBuyRT = yellowToBuyGO.GetComponent<RectTransform>();
        yellowToBuyRT.anchorMin = new Vector2(0.02f, 0.55f);
        yellowToBuyRT.anchorMax = new Vector2(0.38f, 0.98f);
        yellowToBuyRT.offsetMin = Vector2.zero;
        yellowToBuyRT.offsetMax = Vector2.zero;

        var ySwatchGO = new GameObject("ColorSwatch");
        ySwatchGO.transform.SetParent(yellowToBuyGO.transform, false);
        var ySwatchImg = ySwatchGO.AddComponent<Image>();
        ySwatchImg.color = new Color(1.0f, 0.85f, 0.0f);
        var ySwatchRT = ySwatchGO.GetComponent<RectTransform>();
        ySwatchRT.anchorMin = new Vector2(0.05f, 0.55f);
        ySwatchRT.anchorMax = new Vector2(0.95f, 0.95f);
        ySwatchRT.offsetMin = Vector2.zero;
        ySwatchRT.offsetMax = Vector2.zero;

        var yNameGO = MakeText("ShipNameText", yellowToBuyGO.transform, "YELLOW SHIP", 22, Color.white);
        var yNameRT = yNameGO.GetComponent<RectTransform>();
        yNameRT.anchorMin = new Vector2(0f, 0.40f);
        yNameRT.anchorMax = new Vector2(1f, 0.53f);
        yNameRT.offsetMin = Vector2.zero;
        yNameRT.offsetMax = Vector2.zero;
        yNameGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var yPriceGO = MakeText("PriceText", yellowToBuyGO.transform, "150 COINS", 20, Color.yellow);
        var yPriceRT = yPriceGO.GetComponent<RectTransform>();
        yPriceRT.anchorMin = new Vector2(0f, 0.27f);
        yPriceRT.anchorMax = new Vector2(1f, 0.40f);
        yPriceRT.offsetMin = Vector2.zero;
        yPriceRT.offsetMax = Vector2.zero;

        var buyBtnGO = MakeButton("BuyButton", yellowToBuyGO.transform, "BUY");
        var buyBtnRT = buyBtnGO.GetComponent<RectTransform>();
        buyBtnRT.anchorMin        = new Vector2(0.10f, 0.03f);
        buyBtnRT.anchorMax        = new Vector2(0.90f, 0.24f);
        buyBtnRT.sizeDelta        = Vector2.zero;
        buyBtnRT.anchoredPosition = Vector2.zero;

        // Promo code section — bottom strip of ToBuyContent
        var promoSectionGO = new GameObject("PromoSection");
        promoSectionGO.transform.SetParent(toBuyContentGO.transform, false);
        var promoSectionImg = promoSectionGO.AddComponent<Image>();
        promoSectionImg.color = new Color(0.08f, 0.08f, 0.18f, 0.85f);
        var promoSectionRT = promoSectionGO.GetComponent<RectTransform>();
        promoSectionRT.anchorMin = new Vector2(0.02f, 0.02f);
        promoSectionRT.anchorMax = new Vector2(0.98f, 0.50f);
        promoSectionRT.offsetMin = Vector2.zero;
        promoSectionRT.offsetMax = Vector2.zero;

        var promoLabelGO = MakeText("PromoLabel", promoSectionGO.transform, "PROMO CODE", 22, Color.white);
        var promoLabelRT = promoLabelGO.GetComponent<RectTransform>();
        promoLabelRT.anchorMin = new Vector2(0.02f, 0.78f);
        promoLabelRT.anchorMax = new Vector2(0.98f, 1.00f);
        promoLabelRT.offsetMin = Vector2.zero;
        promoLabelRT.offsetMax = Vector2.zero;
        promoLabelGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // InputField (legacy UI)
        var promoInputGO = new GameObject("PromoInputField");
        promoInputGO.transform.SetParent(promoSectionGO.transform, false);
        var promoInputImg = promoInputGO.AddComponent<Image>();
        promoInputImg.color = new Color(0.04f, 0.04f, 0.12f, 1f);
        var promoInputRT = promoInputGO.GetComponent<RectTransform>();
        promoInputRT.anchorMin        = new Vector2(0.02f, 0.46f);
        promoInputRT.anchorMax        = new Vector2(0.72f, 0.74f);
        promoInputRT.offsetMin        = Vector2.zero;
        promoInputRT.offsetMax        = Vector2.zero;
        var promoInputField = promoInputGO.AddComponent<InputField>();

        var promoPlaceholderGO = MakeText("Placeholder", promoInputGO.transform, "Enter code...", 20,
                                          new Color(0.5f, 0.5f, 0.5f, 0.7f));
        promoPlaceholderGO.GetComponent<Text>().fontStyle = FontStyle.Italic;
        var promoPlaceholderRT = promoPlaceholderGO.GetComponent<RectTransform>();
        promoPlaceholderRT.anchorMin = Vector2.zero;
        promoPlaceholderRT.anchorMax = Vector2.one;
        promoPlaceholderRT.offsetMin = new Vector2(5f, 0f);
        promoPlaceholderRT.offsetMax = new Vector2(-5f, 0f);

        var promoInputTextGO = MakeText("Text", promoInputGO.transform, "", 20, Color.white);
        var promoInputTextRT = promoInputTextGO.GetComponent<RectTransform>();
        promoInputTextRT.anchorMin = Vector2.zero;
        promoInputTextRT.anchorMax = Vector2.one;
        promoInputTextRT.offsetMin = new Vector2(5f, 0f);
        promoInputTextRT.offsetMax = new Vector2(-5f, 0f);

        promoInputField.textComponent = promoInputTextGO.GetComponent<Text>();
        promoInputField.placeholder   = promoPlaceholderGO.GetComponent<Graphic>();

        var redeemBtnGO = MakeButton("RedeemButton", promoSectionGO.transform, "REDEEM");
        var redeemBtnRT = redeemBtnGO.GetComponent<RectTransform>();
        redeemBtnRT.anchorMin        = new Vector2(0.74f, 0.46f);
        redeemBtnRT.anchorMax        = new Vector2(0.98f, 0.74f);
        redeemBtnRT.sizeDelta        = Vector2.zero;
        redeemBtnRT.anchoredPosition = Vector2.zero;

        var promoFeedbackGO = MakeText("PromoFeedback", promoSectionGO.transform, "", 20, Color.white);
        var promoFeedbackRT = promoFeedbackGO.GetComponent<RectTransform>();
        promoFeedbackRT.anchorMin = new Vector2(0.02f, 0.08f);
        promoFeedbackRT.anchorMax = new Vector2(0.98f, 0.43f);
        promoFeedbackRT.offsetMin = Vector2.zero;
        promoFeedbackRT.offsetMax = Vector2.zero;

        // Bought content panel — contains the default blue ship card
        var boughtContentGO = new GameObject("BoughtContent");
        boughtContentGO.transform.SetParent(shopPanelGO.transform, false);
        var boughtRT = boughtContentGO.AddComponent<RectTransform>();
        boughtRT.anchorMin = new Vector2(0.02f, 0.05f);
        boughtRT.anchorMax = new Vector2(0.98f, 0.80f);
        boughtRT.offsetMin = Vector2.zero;
        boughtRT.offsetMax = Vector2.zero;

        // Blue ship card
        var cardGO = new GameObject("BlueShipCard");
        cardGO.transform.SetParent(boughtContentGO.transform, false);
        var cardImg = cardGO.AddComponent<Image>();
        cardImg.color = new Color(0.15f, 0.20f, 0.35f, 1f);
        var cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.02f, 0.70f);
        cardRT.anchorMax = new Vector2(0.38f, 0.98f);
        cardRT.offsetMin = Vector2.zero;
        cardRT.offsetMax = Vector2.zero;

        var swatchGO = new GameObject("ColorSwatch");
        swatchGO.transform.SetParent(cardGO.transform, false);
        var swatchImg = swatchGO.AddComponent<Image>();
        swatchImg.color = new Color(0.30f, 0.40f, 0.70f);
        var swatchRT = swatchGO.GetComponent<RectTransform>();
        swatchRT.anchorMin = new Vector2(0.05f, 0.45f);
        swatchRT.anchorMax = new Vector2(0.95f, 0.92f);
        swatchRT.offsetMin = Vector2.zero;
        swatchRT.offsetMax = Vector2.zero;

        var nameGO = MakeText("ShipNameText", cardGO.transform, "BLUE SHIP", 22, Color.white);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.30f);
        nameRT.anchorMax = new Vector2(1f, 0.43f);
        nameRT.offsetMin = Vector2.zero;
        nameRT.offsetMax = Vector2.zero;
        nameGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var blueSelectBtnGO = MakeButton("SelectButton", cardGO.transform, "SELECT");
        var blueSelectBtnRT = blueSelectBtnGO.GetComponent<RectTransform>();
        blueSelectBtnRT.anchorMin        = new Vector2(0.05f, 0.04f);
        blueSelectBtnRT.anchorMax        = new Vector2(0.95f, 0.26f);
        blueSelectBtnRT.sizeDelta        = Vector2.zero;
        blueSelectBtnRT.anchoredPosition = Vector2.zero;

        var blueSelectedLabelGO = MakeText("SelectedLabel", cardGO.transform, "✓ SELECTED", 20, Color.green);
        var blueSelectedLabelRT = blueSelectedLabelGO.GetComponent<RectTransform>();
        blueSelectedLabelRT.anchorMin = new Vector2(0.05f, 0.04f);
        blueSelectedLabelRT.anchorMax = new Vector2(0.95f, 0.26f);
        blueSelectedLabelRT.offsetMin = Vector2.zero;
        blueSelectedLabelRT.offsetMax = Vector2.zero;
        blueSelectedLabelGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Yellow ship card (Bought) — initially inactive; RefreshShopUI activates on purchase
        var yellowBoughtGO = new GameObject("YellowShipBoughtCard");
        yellowBoughtGO.transform.SetParent(boughtContentGO.transform, false);
        var yellowBoughtImg = yellowBoughtGO.AddComponent<Image>();
        yellowBoughtImg.color = new Color(0.25f, 0.22f, 0.05f, 1f);
        var yellowBoughtRT = yellowBoughtGO.GetComponent<RectTransform>();
        yellowBoughtRT.anchorMin = new Vector2(0.42f, 0.70f);
        yellowBoughtRT.anchorMax = new Vector2(0.78f, 0.98f);
        yellowBoughtRT.offsetMin = Vector2.zero;
        yellowBoughtRT.offsetMax = Vector2.zero;

        var ybSwatchGO = new GameObject("ColorSwatch");
        ybSwatchGO.transform.SetParent(yellowBoughtGO.transform, false);
        var ybSwatchImg = ybSwatchGO.AddComponent<Image>();
        ybSwatchImg.color = new Color(1.0f, 0.85f, 0.0f);
        var ybSwatchRT = ybSwatchGO.GetComponent<RectTransform>();
        ybSwatchRT.anchorMin = new Vector2(0.05f, 0.45f);
        ybSwatchRT.anchorMax = new Vector2(0.95f, 0.92f);
        ybSwatchRT.offsetMin = Vector2.zero;
        ybSwatchRT.offsetMax = Vector2.zero;

        var ybNameGO = MakeText("ShipNameText", yellowBoughtGO.transform, "YELLOW SHIP", 22, Color.white);
        var ybNameRT = ybNameGO.GetComponent<RectTransform>();
        ybNameRT.anchorMin = new Vector2(0f, 0.30f);
        ybNameRT.anchorMax = new Vector2(1f, 0.43f);
        ybNameRT.offsetMin = Vector2.zero;
        ybNameRT.offsetMax = Vector2.zero;
        ybNameGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var yellowSelectBtnGO = MakeButton("SelectButton", yellowBoughtGO.transform, "SELECT");
        var yellowSelectBtnRT = yellowSelectBtnGO.GetComponent<RectTransform>();
        yellowSelectBtnRT.anchorMin        = new Vector2(0.05f, 0.04f);
        yellowSelectBtnRT.anchorMax        = new Vector2(0.52f, 0.26f);
        yellowSelectBtnRT.sizeDelta        = Vector2.zero;
        yellowSelectBtnRT.anchoredPosition = Vector2.zero;

        var sellBtnGO = MakeButton("SellButton", yellowBoughtGO.transform, "SELL");
        var sellBtnRT = sellBtnGO.GetComponent<RectTransform>();
        sellBtnRT.anchorMin        = new Vector2(0.55f, 0.04f);
        sellBtnRT.anchorMax        = new Vector2(0.95f, 0.26f);
        sellBtnRT.sizeDelta        = Vector2.zero;
        sellBtnRT.anchoredPosition = Vector2.zero;
        sellBtnGO.GetComponent<Image>().color = new Color(0.65f, 0.12f, 0.12f);

        var yellowSelectedLabelGO = MakeText("SelectedLabel", yellowBoughtGO.transform, "✓ SELECTED", 20, Color.green);
        var yellowSelectedLabelRT = yellowSelectedLabelGO.GetComponent<RectTransform>();
        yellowSelectedLabelRT.anchorMin = new Vector2(0.05f, 0.04f);
        yellowSelectedLabelRT.anchorMax = new Vector2(0.95f, 0.26f);
        yellowSelectedLabelRT.offsetMin = Vector2.zero;
        yellowSelectedLabelRT.offsetMax = Vector2.zero;
        yellowSelectedLabelGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        yellowBoughtGO.SetActive(false);

        // Yellow-Red ship card (Bought tab only — unlocked via pizza1 promo)
        var yrBoughtGO = new GameObject("YellowRedShipBoughtCard");
        yrBoughtGO.transform.SetParent(boughtContentGO.transform, false);
        var yrBoughtImg = yrBoughtGO.AddComponent<Image>();
        yrBoughtImg.color = new Color(0.20f, 0.05f, 0.05f, 1f);
        var yrBoughtRT = yrBoughtGO.GetComponent<RectTransform>();
        yrBoughtRT.anchorMin = new Vector2(0.02f, 0.38f);
        yrBoughtRT.anchorMax = new Vector2(0.38f, 0.66f);
        yrBoughtRT.offsetMin = yrBoughtRT.offsetMax = Vector2.zero;

        var yrSwatchGO = new GameObject("ColorSwatch");
        yrSwatchGO.transform.SetParent(yrBoughtGO.transform, false);
        yrSwatchGO.AddComponent<Image>().color = Color.clear;
        var yrSwatchRT = yrSwatchGO.GetComponent<RectTransform>();
        yrSwatchRT.anchorMin = new Vector2(0.05f, 0.45f);
        yrSwatchRT.anchorMax = new Vector2(0.95f, 0.92f);
        yrSwatchRT.offsetMin = yrSwatchRT.offsetMax = Vector2.zero;

        var yrBottomGO = new GameObject("SwatchBottom");
        yrBottomGO.transform.SetParent(yrSwatchGO.transform, false);
        yrBottomGO.AddComponent<Image>().color = new Color(1.0f, 0.85f, 0.0f);
        var yrBottomRT = yrBottomGO.GetComponent<RectTransform>();
        yrBottomRT.anchorMin = Vector2.zero;
        yrBottomRT.anchorMax = new Vector2(1f, 0.5f);
        yrBottomRT.offsetMin = yrBottomRT.offsetMax = Vector2.zero;

        var yrTopGO = new GameObject("SwatchTop");
        yrTopGO.transform.SetParent(yrSwatchGO.transform, false);
        yrTopGO.AddComponent<Image>().color = new Color(0.85f, 0.10f, 0.10f);
        var yrTopRT = yrTopGO.GetComponent<RectTransform>();
        yrTopRT.anchorMin = new Vector2(0f, 0.5f);
        yrTopRT.anchorMax = Vector2.one;
        yrTopRT.offsetMin = yrTopRT.offsetMax = Vector2.zero;

        var yrNameGO = MakeText("ShipNameText", yrBoughtGO.transform, "YELLOW-RED SHIP", 20, Color.white);
        var yrNameRT = yrNameGO.GetComponent<RectTransform>();
        yrNameRT.anchorMin = new Vector2(0f, 0.30f);
        yrNameRT.anchorMax = new Vector2(1f, 0.43f);
        yrNameRT.offsetMin = yrNameRT.offsetMax = Vector2.zero;
        yrNameGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var yrSelectBtnGO = MakeButton("SelectButton", yrBoughtGO.transform, "SELECT");
        var yrSelectBtnRT = yrSelectBtnGO.GetComponent<RectTransform>();
        yrSelectBtnRT.anchorMin        = new Vector2(0.05f, 0.04f);
        yrSelectBtnRT.anchorMax        = new Vector2(0.95f, 0.26f);
        yrSelectBtnRT.sizeDelta        = Vector2.zero;
        yrSelectBtnRT.anchoredPosition = Vector2.zero;

        var yrSelectedLabelGO = MakeText("SelectedLabel", yrBoughtGO.transform, "✓ SELECTED", 20, Color.green);
        var yrSelectedLabelRT = yrSelectedLabelGO.GetComponent<RectTransform>();
        yrSelectedLabelRT.anchorMin = new Vector2(0.05f, 0.04f);
        yrSelectedLabelRT.anchorMax = new Vector2(0.95f, 0.26f);
        yrSelectedLabelRT.offsetMin = yrSelectedLabelRT.offsetMax = Vector2.zero;
        yrSelectedLabelGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        yrBoughtGO.SetActive(false);

        // Assign MainMenu fields
        script.shopPanel              = shopPanelGO;
        script.toBuyContent           = toBuyContentGO;
        script.boughtContent          = boughtContentGO;
        script.yellowShipToBuyCard    = yellowToBuyGO;
        script.yellowShipBoughtCard   = yellowBoughtGO;
        script.buyYellowShipButton    = buyBtnGO.GetComponent<Button>();
        script.blueShipSelectButton   = blueSelectBtnGO.GetComponent<Button>();
        script.blueShipSelectedLabel  = blueSelectedLabelGO.GetComponent<Text>();
        script.yellowShipSelectButton  = yellowSelectBtnGO.GetComponent<Button>();
        script.yellowShipSelectedLabel = yellowSelectedLabelGO.GetComponent<Text>();
        script.yellowShipSellButton       = sellBtnGO.GetComponent<Button>();
        script.yellowRedShipBoughtCard    = yrBoughtGO;
        script.yellowRedShipSelectButton  = yrSelectBtnGO.GetComponent<Button>();
        script.yellowRedShipSelectedLabel = yrSelectedLabelGO.GetComponent<Text>();
        script.promoCodeInput             = promoInputField;
        script.promoFeedbackText          = promoFeedbackGO.GetComponent<Text>();

        // Wire callbacks
        UnityEventTools.AddPersistentListener(
            closeBtnGO.GetComponent<Button>().onClick,
            script.OnCloseShopClicked);
        UnityEventTools.AddPersistentListener(
            toBuyTabGO.GetComponent<Button>().onClick,
            script.ShowToBuyTab);
        UnityEventTools.AddPersistentListener(
            boughtTabGO.GetComponent<Button>().onClick,
            script.ShowBoughtTab);
        UnityEventTools.AddPersistentListener(
            buyBtnGO.GetComponent<Button>().onClick,
            script.OnBuyYellowShipClicked);
        UnityEventTools.AddPersistentListener(
            blueSelectBtnGO.GetComponent<Button>().onClick,
            script.OnSelectBlueShip);
        UnityEventTools.AddPersistentListener(
            yellowSelectBtnGO.GetComponent<Button>().onClick,
            script.OnSelectYellowShip);
        UnityEventTools.AddPersistentListener(
            sellBtnGO.GetComponent<Button>().onClick,
            script.OnSellYellowShipClicked);
        UnityEventTools.AddPersistentListener(
            yrSelectBtnGO.GetComponent<Button>().onClick,
            script.OnSelectYellowRedShip);
        UnityEventTools.AddPersistentListener(
            redeemBtnGO.GetComponent<Button>().onClick,
            script.OnRedeemPromoCode);

        // Bought tab visible by default
        toBuyContentGO.SetActive(false);
        boughtContentGO.SetActive(true);

        return shopPanelGO;
    }

    // Creates a Button with a centered text label
    static GameObject MakeButton(string name, Transform parent, string label)
    {
        var go    = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img   = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.4f, 0.8f, 1f);
        var btn   = go.AddComponent<Button>();

        // Hover/press colour tint
        var colors        = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.55f, 1f);
        colors.pressedColor     = new Color(0.1f, 0.25f, 0.6f);
        btn.colors = colors;

        var txtGO = MakeText("Text", go.transform, label, 28, Color.white);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin  = Vector2.zero;
        txtRT.anchorMax  = Vector2.one;
        txtRT.offsetMin  = Vector2.zero;
        txtRT.offsetMax  = Vector2.zero;

        return go;
    }
}

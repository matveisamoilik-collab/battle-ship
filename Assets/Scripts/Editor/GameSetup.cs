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
        CopyMapImages();
        CopyModels();

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
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets", "Models");
        EnsureFolder("Assets/Models", "IslandWithSkull");
        EnsureFolder("Assets/Models", "PiratShip");
        EnsureFolder("Assets/Models", "Vulcano");
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
        dict["Volcanic"]   = MakeMat("VolcanicMaterial",   new Color(0.18f, 0.12f, 0.10f));
        dict["Stone"]      = MakeMat("StoneMaterial",      new Color(0.38f, 0.38f, 0.40f));
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
    // 6. Copy map images into Assets/Resources and import as sprites
    // -------------------------------------------------------------------------

    static void CopyMapImages()
    {
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        string srcDir = System.IO.Path.Combine(projectRoot, "images");
        string dstDir = System.IO.Path.Combine(Application.dataPath, "Resources");

        foreach (string lvl in new[] { "Level_1", "Level_2", "Level_3", "Level_4" })
        {
            string src = System.IO.Path.Combine(srcDir, lvl + ".png");
            string dst = System.IO.Path.Combine(dstDir, lvl + ".png");
            if (System.IO.File.Exists(src))
                System.IO.File.Copy(src, dst, overwrite: true);
            else
                Debug.LogWarning("[ShipButtlr] Map image not found: " + src);
        }

        AssetDatabase.Refresh();

        foreach (string lvl in new[] { "Level_1", "Level_2", "Level_3", "Level_4" })
        {
            string assetPath = "Assets/Resources/" + lvl + ".png";
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;
            importer.textureType      = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled    = false;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }

    // -------------------------------------------------------------------------
    // 6b. Copy 3D models into Assets/Models
    // -------------------------------------------------------------------------

    static void CopyModels()
    {
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        string src        = System.IO.Path.Combine(projectRoot, "3d", "island", "islandWithSkull.fbx");
        string assetPath  = "Assets/Models/IslandWithSkull/islandWithSkull.fbx";
        string dst        = System.IO.Path.Combine(Application.dataPath, "Models", "IslandWithSkull", "islandWithSkull.fbx");

        if (!System.IO.File.Exists(src))
        {
            Debug.LogWarning("[ShipButtlr] islandWithSkull.fbx not found at: " + src);
            return;
        }

        // Pirate ship model
        string piratSrc      = System.IO.Path.Combine(projectRoot, "3d", "ship", "pirat_ship.fbx");
        string piratAsset    = "Assets/Models/PiratShip/pirat_ship.fbx";
        string piratDst      = System.IO.Path.Combine(Application.dataPath, "Models", "PiratShip", "pirat_ship.fbx");
        if (System.IO.File.Exists(piratSrc))
        {
            System.IO.File.Copy(piratSrc, piratDst, overwrite: true);
            AssetDatabase.Refresh();
            var piratImporter = AssetImporter.GetAtPath(piratAsset) as ModelImporter;
            if (piratImporter != null)
            {
                piratImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                piratImporter.materialLocation   = ModelImporterMaterialLocation.External;
                AssetDatabase.ImportAsset(piratAsset, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[ShipButtlr] pirat_ship.fbx imported.");
        }
        else
            Debug.LogWarning("[ShipButtlr] pirat_ship.fbx not found at: " + piratSrc);

        System.IO.File.Copy(src, dst, overwrite: true);
        AssetDatabase.Refresh();

        // Configure importer: extract embedded textures + create external URP-compatible materials
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer != null)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation   = ModelImporterMaterialLocation.External;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[ShipButtlr] islandWithSkull.fbx imported with external materials.");
        }

        // Vulcano model (Level 4 island)
        string vulcanoSrc   = System.IO.Path.Combine(projectRoot, "3d", "island", "Vulcano.fbx");
        string vulcanoAsset = "Assets/Models/Vulcano/Vulcano.fbx";
        string vulcanoDst   = System.IO.Path.Combine(Application.dataPath, "Models", "Vulcano", "Vulcano.fbx");
        if (System.IO.File.Exists(vulcanoSrc))
        {
            System.IO.File.Copy(vulcanoSrc, vulcanoDst, overwrite: true);
            AssetDatabase.Refresh();
            var vulcanoImporter = AssetImporter.GetAtPath(vulcanoAsset) as ModelImporter;
            if (vulcanoImporter != null)
            {
                vulcanoImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                vulcanoImporter.materialLocation   = ModelImporterMaterialLocation.External;
                AssetDatabase.ImportAsset(vulcanoAsset, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[ShipButtlr] Vulcano.fbx imported.");
        }
        else
            Debug.LogWarning("[ShipButtlr] Vulcano.fbx not found at: " + vulcanoSrc);
    }

    // -------------------------------------------------------------------------
    // 7. Main Menu scene
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
        playRT.anchorMin        = new Vector2(0.5f, 0.56f);
        playRT.anchorMax        = new Vector2(0.5f, 0.65f);
        playRT.sizeDelta        = new Vector2(300f, 70f);
        playRT.anchoredPosition = Vector2.zero;

        // Map button
        var mapBtnGO = MakeButton("MapButton", canvasGO.transform, "MAP");
        var mapBtnRT = mapBtnGO.GetComponent<RectTransform>();
        mapBtnRT.anchorMin        = new Vector2(0.5f, 0.44f);
        mapBtnRT.anchorMax        = new Vector2(0.5f, 0.53f);
        mapBtnRT.sizeDelta        = new Vector2(300f, 70f);
        mapBtnRT.anchoredPosition = Vector2.zero;

        // Shop button
        var shopBtnGO = MakeButton("ShopButton", canvasGO.transform, "SHOP");
        var shopBtnRT = shopBtnGO.GetComponent<RectTransform>();
        shopBtnRT.anchorMin        = new Vector2(0.5f, 0.32f);
        shopBtnRT.anchorMax        = new Vector2(0.5f, 0.41f);
        shopBtnRT.sizeDelta        = new Vector2(300f, 70f);
        shopBtnRT.anchoredPosition = Vector2.zero;

        // Quit button
        var quitBtnGO = MakeButton("QuitButton", canvasGO.transform, "QUIT");
        var quitRT    = quitBtnGO.GetComponent<RectTransform>();
        quitRT.anchorMin        = new Vector2(0.5f, 0.20f);
        quitRT.anchorMax        = new Vector2(0.5f, 0.29f);
        quitRT.sizeDelta        = new Vector2(300f, 70f);
        quitRT.anchoredPosition = Vector2.zero;

        // Wire button callbacks
        UnityEventTools.AddPersistentListener(
            playBtnGO.GetComponent<Button>().onClick,
            mainMenuScript.OnPlayClicked);
        UnityEventTools.AddPersistentListener(
            mapBtnGO.GetComponent<Button>().onClick,
            mainMenuScript.OnMapClicked);
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

        // Level display — below coins, top-left
        var mmLevelGO = MakeText("LevelText", canvasGO.transform, "LEVEL: 1", 30, Color.black);
        var mmLevelRT = mmLevelGO.GetComponent<RectTransform>();
        mmLevelRT.anchorMin        = new Vector2(0f, 1f);
        mmLevelRT.anchorMax        = new Vector2(0f, 1f);
        mmLevelRT.pivot            = new Vector2(0f, 1f);
        mmLevelRT.anchoredPosition = new Vector2(20f, -75f);
        mmLevelRT.sizeDelta        = new Vector2(300f, 50f);
        mmLevelGO.GetComponent<Text>().alignment = TextAnchor.UpperLeft;
        mainMenuScript.levelText = mmLevelGO.GetComponent<Text>();

        // Shop panel — hidden by default
        var shopPanelGO = BuildShopPanel(canvasGO.transform, mainMenuScript);
        shopPanelGO.SetActive(false);

        // Map panel — built after shop so it renders on top; hidden by default
        var mapPanelGO = BuildMapPanel(canvasGO.transform, mainMenuScript);
        mapPanelGO.SetActive(false);

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

        // Decorative islands grouped under IslandsRoot so level 1 can disable them all
        var islandsRootGO = new GameObject("Level2_Islands");
        CreateIsland(new Vector3( 72f, 0f,  68f), 12f, 3, mats, islandsRootGO.transform);
        CreateIsland(new Vector3(-65f, 0f,  75f),  9f, 2, mats, islandsRootGO.transform);
        CreateIsland(new Vector3( 58f, 0f, -72f), 10f, 3, mats, islandsRootGO.transform);
        CreateIsland(new Vector3(-80f, 0f, -55f),  8f, 2, mats, islandsRootGO.transform);
        CreateIsland(new Vector3( 42f, 0f,  82f),  7f, 2, mats, islandsRootGO.transform);
        CreateIsland(new Vector3(-45f, 0f, -80f), 11f, 3, mats, islandsRootGO.transform);

        // Level 3: Skull Shoals — one massive central island with skull model + 6 rock sentinels
        var islands3RootGO = new GameObject("Level3_SkullShoals");
        CreateSkullIsland(Vector3.zero, 33.6f, mats, islands3RootGO.transform);

        // Level 4: Volcano — central volcano island matching Level 3 scale
        var islands4RootGO = new GameObject("Level4_Volcano");
        CreateVulcanoIsland(Vector3.zero, 33.6f, mats, islands4RootGO.transform);
        float[] rockAngles = { 30f, 90f, 150f, 210f, 270f, 330f };
        foreach (float angle in rockAngles)
        {
            float rad    = angle * Mathf.Deg2Rad;
            var rockPos  = new Vector3(Mathf.Sin(rad) * 71f, 0f, Mathf.Cos(rad) * 71f);
            var rockRng  = new System.Random((int)(angle * 137 + 42));
            CreateRock(rockPos, rockRng, mats, islands3RootGO.transform);
        }

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
        botShip.torpedoPrefab     = torpedoPrefab;
        botShip.torpedoSpawnPoint = botSpawn;

        var piratPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PiratShip/pirat_ship.fbx");
        if (piratPrefab != null)
        {
            botShip.piratShipModel    = piratPrefab;
            playerShip.piratShipModel = piratPrefab;
        }
        else
            Debug.LogWarning("[ShipButtlr] pirat_ship.fbx not found — run Build All again after Unity imports it.");

        // ----- Camera -----
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 8.8f, -78f); // matches CameraFollow desired at start: player(0,0,-60) - forward*18 + up*8.8
        var cameraFollow = camGO.AddComponent<CameraFollow>();
        cameraFollow.target   = playerShipGO.transform;
        cameraFollow.distance = 21.6f;  // 20% farther than previous 18
        cameraFollow.height   = 8.8f;

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

        // Bot HP bar — top-right (hidden at runtime for Level 4)
        HealthBar botHealthBar = CreateHPBar(
            "BotHP", hudGO.transform,
            "ENEMY SHIP", new Color(1f, 0.3f, 0.3f),
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-240f, -70f), new Vector2(220f, 50f));

        // Wire health bars to ships
        playerShip.healthBar = playerHealthBar;
        botShip.healthBar    = botHealthBar;

        // BotShip prefab — used by GameManager to spawn extra ships in Level 4
        var botShipPrefabGO = CreateBotShipPrefab(mats["Bot"], torpedoPrefab,
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PiratShip/pirat_ship.fbx"));

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
        gm.coinsText    = coinGO.GetComponent<Text>();
        gm.islandsRoot  = islandsRootGO;
        gm.islands3Root = islands3RootGO;
        gm.islands4Root  = islands4RootGO;
        gm.botHPGroup    = botHealthBar.transform.parent.gameObject; // "BotHP" group
        gm.botShipPrefab = botShipPrefabGO;

        // Level display — below coins, top-left HUD
        var levelGO = MakeText("LevelText", hudGO.transform, "LEVEL: 1", 30, Color.black);
        var levelRT = levelGO.GetComponent<RectTransform>();
        levelRT.anchorMin        = new Vector2(0f, 1f);
        levelRT.anchorMax        = new Vector2(0f, 1f);
        levelRT.pivot            = new Vector2(0f, 1f);
        levelRT.anchoredPosition = new Vector2(20f, -75f);
        levelRT.sizeDelta        = new Vector2(300f, 50f);
        levelGO.GetComponent<Text>().alignment = TextAnchor.UpperLeft;
        gm.levelText = levelGO.GetComponent<Text>();

        // Survival timer — top-center, hidden for non-Level-4 levels
        var timerGO = MakeText("TimerText", hudGO.transform, "2:00", 48, Color.white);
        var timerRT = timerGO.GetComponent<RectTransform>();
        timerRT.anchorMin        = new Vector2(0.5f, 1f);
        timerRT.anchorMax        = new Vector2(0.5f, 1f);
        timerRT.pivot            = new Vector2(0.5f, 1f);
        timerRT.anchoredPosition = new Vector2(0f, -15f);
        timerRT.sizeDelta        = new Vector2(200f, 70f);
        timerGO.GetComponent<Text>().fontStyle = FontStyle.Bold;
        gm.timerText  = timerGO.GetComponent<Text>();

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

    // Creates a BotShip prefab for Level 4 dynamic spawning (no health bar)
    static GameObject CreateBotShipPrefab(Material botMat, GameObject torpedoPrefab, GameObject piratModel)
    {
        string path = "Assets/Prefabs/BotShip.prefab";
        var go = BuildShipGO("BotShip", Vector3.zero, Quaternion.identity, botMat);
        go.tag = "Enemy";
        var bot = go.AddComponent<BotShip>();
        bot.torpedoPrefab     = torpedoPrefab;
        bot.torpedoSpawnPoint = go.transform.Find("TorpedoSpawn");
        if (piratModel != null) bot.piratShipModel = piratModel;
        var prefab = SavePrefab(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Creates the Level 4 volcano island using Vulcano.fbx scaled to match Level 3
    static void CreateVulcanoIsland(Vector3 pos, float targetRadius,
        System.Collections.Generic.Dictionary<string, Material> mats,
        Transform parent = null)
    {
        var island = new GameObject("Island");
        if (parent != null) island.transform.SetParent(parent);
        island.transform.position = pos;
        island.tag = "Island";
        island.AddComponent<IslandData>().radius = targetRadius * 0.8f;

        // Invisible capsule collider so torpedoes register hits
        var cap = island.AddComponent<CapsuleCollider>();
        cap.radius = targetRadius * 0.8f;
        cap.height = 30f;
        cap.center = new Vector3(0f, 15f, 0f);

        var vulcanoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Vulcano/Vulcano.fbx");
        if (vulcanoPrefab == null)
        {
            Debug.LogWarning("[ShipButtlr] Vulcano.fbx not found — run Build All again after Unity imports it.");
            return;
        }

        var modelGO = (GameObject)PrefabUtility.InstantiatePrefab(vulcanoPrefab);
        modelGO.name = "VulcanoModel";
        modelGO.transform.localEulerAngles = new Vector3(-90f, 0f, 0f); // Blender Z-up → Unity Y-up
        modelGO.transform.localScale       = Vector3.one;

        // Measure world-space AABB
        Bounds wb = new Bounds();
        bool hasMesh = false;
        foreach (var mf in modelGO.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var wc = mf.transform.TransformPoint(
                    b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz)));
                if (!hasMesh) { wb = new Bounds(wc, Vector3.zero); hasMesh = true; }
                else wb.Encapsulate(wc);
            }
        }

        float xzExtent = Mathf.Max(wb.size.x, wb.size.z);
        float target   = targetRadius * 1.6f;
        float s        = (hasMesh && xzExtent > 0.0001f) ? target / xzExtent : 1f;
        float localY   = -wb.min.y * s;

        Debug.Log($"[ShipButtlr] Vulcano: bounds={wb.size} xzExtent={xzExtent:F3} scale={s:F3} localY={localY:F3}");

        modelGO.transform.SetParent(island.transform, false);
        modelGO.transform.localPosition    = new Vector3(0f, localY, 0f);
        modelGO.transform.localScale       = new Vector3(s, s, s);
        modelGO.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);

        foreach (var col in modelGO.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);
    }

    static void CreateIsland(Vector3 pos, float radius, int treeCount,
        System.Collections.Generic.Dictionary<string, Material> mats,
        Transform parent = null)
    {
        var island = new GameObject("Island");
        if (parent != null) island.transform.SetParent(parent);
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

    static void CreateSkullIsland(Vector3 pos, float islandRadius,
        System.Collections.Generic.Dictionary<string, Material> mats,
        Transform parent = null)
    {
        var island = new GameObject("Island");
        if (parent != null) island.transform.SetParent(parent);
        island.transform.position = pos;
        island.tag = "Island";
        island.AddComponent<IslandData>().radius = islandRadius * 0.8f;

        // Invisible capsule collider on the root so torpedoes still register hits
        var cap = island.AddComponent<CapsuleCollider>();
        cap.radius = islandRadius * 0.8f;
        cap.height = 30f;
        cap.center = new Vector3(0f, 15f, 0f);

        // Full island FBX (base + skull built in)
        var islandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/IslandWithSkull/islandWithSkull.fbx");
        if (islandPrefab == null)
        {
            Debug.LogWarning("[ShipButtlr] islandWithSkull.fbx not found — run Build All again after Unity imports it.");
            return;
        }

        var islandGO = (GameObject)PrefabUtility.InstantiatePrefab(islandPrefab);
        islandGO.name = "IslandModel";

        // Measure with final rotation applied so bounds are accurate
        islandGO.transform.position         = Vector3.zero;
        islandGO.transform.localEulerAngles = new Vector3(-90f, 180f, 0f); // Blender Z-up → Unity Y-up, face toward player (-Z)
        islandGO.transform.localScale       = Vector3.one;

        // Measure full world-space AABB by transforming every mesh corner
        Bounds wb = new Bounds();
        bool hasMesh = false;
        foreach (var mf in islandGO.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var wc = mf.transform.TransformPoint(
                    b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz)));
                if (!hasMesh) { wb = new Bounds(wc, Vector3.zero); hasMesh = true; }
                else wb.Encapsulate(wc);
            }
        }

        // Scale XZ footprint to match the visual base diameter (islandRadius * 1.6)
        float xzExtent = Mathf.Max(wb.size.x, wb.size.z);
        float target   = islandRadius * 1.6f;
        float s        = (hasMesh && xzExtent > 0.0001f) ? target / xzExtent : 1f;

        // Sit the model bottom exactly at water level (y = 0 in island-local space)
        float localY = -wb.min.y * s;

        Debug.Log($"[ShipButtlr] IslandWithSkull: bounds={wb.size} xzExtent={xzExtent:F3} scale={s:F3} localY={localY:F3}");

        islandGO.transform.SetParent(island.transform, false);
        islandGO.transform.localPosition    = new Vector3(0f, localY, 0f);
        islandGO.transform.localScale       = new Vector3(s, s, s);
        islandGO.transform.localEulerAngles = new Vector3(-90f, 180f, 0f);

        // Remove any colliders on the model — the CapsuleCollider on the root handles hits
        foreach (var col in islandGO.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);
    }

    static void CreateRock(Vector3 pos, System.Random rng,
        System.Collections.Generic.Dictionary<string, Material> mats,
        Transform parent = null)
    {
        float radius = 2.5f + (float)rng.NextDouble() * 2.0f;
        float widthX = radius * 2f * (0.85f + (float)rng.NextDouble() * 0.30f);
        float widthZ = radius * 2f * (0.85f + (float)rng.NextDouble() * 0.30f);
        float height = radius * (1.0f  + (float)rng.NextDouble() * 0.80f);
        float yRot   = (float)rng.NextDouble() * 360f;

        var rock = new GameObject("Rock");
        if (parent != null) rock.transform.SetParent(parent);
        rock.transform.position = new Vector3(pos.x, -height * 0.25f, pos.z);
        rock.tag = "Island";
        rock.AddComponent<IslandData>().radius = (widthX + widthZ) * 0.25f;

        // Main boulder — SphereCollider kept for torpedo hits
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "RockBody";
        body.transform.SetParent(rock.transform);
        body.transform.localPosition    = new Vector3(0f, height * 0.5f, 0f);
        body.transform.localScale       = new Vector3(widthX, height, widthZ);
        body.transform.localEulerAngles = new Vector3(0f, yRot, 0f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mats["Stone"];

        // 50% chance: smaller accent chunk on top for a craggy look
        if (rng.NextDouble() > 0.5)
        {
            float cw = widthX * (0.4f + (float)rng.NextDouble() * 0.25f);
            float ch = height  * (0.45f + (float)rng.NextDouble() * 0.25f);
            float ox = (float)(rng.NextDouble() - 0.5) * radius * 0.6f;
            float oz = (float)(rng.NextDouble() - 0.5) * radius * 0.6f;

            var chunk = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chunk.name = "RockChunk";
            chunk.transform.SetParent(rock.transform);
            chunk.transform.localPosition = new Vector3(ox, height * 0.85f + ch * 0.4f, oz);
            chunk.transform.localScale    = new Vector3(cw, ch, cw * (0.8f + (float)rng.NextDouble() * 0.4f));
            chunk.GetComponent<MeshRenderer>().sharedMaterial = mats["Stone"];
            Object.DestroyImmediate(chunk.GetComponent<Collider>());
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

    // Builds the map overlay panel; sprites are loaded at runtime by MainMenu.RefreshMapImage()
    static GameObject BuildMapPanel(Transform canvasTransform, MainMenu script)
    {
        // Full-screen dark overlay
        var panel = new GameObject("MapPanel");
        panel.transform.SetParent(canvasTransform, false);
        var bgImg = panel.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Map image — fills the entire panel; sprite swapped at runtime
        var mapImgGO = new GameObject("MapImage");
        mapImgGO.transform.SetParent(panel.transform, false);
        var mapImg = mapImgGO.AddComponent<Image>();
        mapImg.color = Color.white;
        var mapRT = mapImgGO.GetComponent<RectTransform>();
        mapRT.anchorMin = Vector2.zero;
        mapRT.anchorMax = Vector2.one;
        mapRT.offsetMin = Vector2.zero;
        mapRT.offsetMax = Vector2.zero;

        // Island 1 — Coral Cove (always unlocked)
        // Normalized pos on Level_1.png: center ~(13% left, 76% from top) → Unity y = 24% from bottom
        var isle1GO = new GameObject("Island1Button");
        isle1GO.transform.SetParent(panel.transform, false);
        isle1GO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f); // transparent hit area
        var isle1Btn = isle1GO.AddComponent<Button>();
        var isle1RT  = isle1GO.GetComponent<RectTransform>();
        isle1RT.anchorMin = new Vector2(0.07f, 0.17f);
        isle1RT.anchorMax = new Vector2(0.19f, 0.31f);
        isle1RT.offsetMin = Vector2.zero;
        isle1RT.offsetMax = Vector2.zero;

        // Island 2 — Pirate's Rest (locked until CurrentLevel >= 2)
        // Normalized pos on Level_2.png: center ~(26% left, 46% from top) → Unity y = 54% from bottom
        var isle2GO = new GameObject("Island2Button");
        isle2GO.transform.SetParent(panel.transform, false);
        isle2GO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f); // transparent hit area
        var isle2Btn = isle2GO.AddComponent<Button>();
        var isle2RT  = isle2GO.GetComponent<RectTransform>();
        isle2RT.anchorMin = new Vector2(0.20f, 0.47f);
        isle2RT.anchorMax = new Vector2(0.32f, 0.61f);
        isle2RT.offsetMin = Vector2.zero;
        isle2RT.offsetMax = Vector2.zero;

        // Island 3 — Skull Shoals (locked until CurrentLevel >= 3)
        var isle3GO = new GameObject("Island3Button");
        isle3GO.transform.SetParent(panel.transform, false);
        isle3GO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        var isle3Btn = isle3GO.AddComponent<Button>();
        var isle3RT  = isle3GO.GetComponent<RectTransform>();
        isle3RT.anchorMin = new Vector2(0.23f, 0.15f);
        isle3RT.anchorMax = new Vector2(0.39f, 0.31f);
        isle3RT.offsetMin = Vector2.zero;
        isle3RT.offsetMax = Vector2.zero;

        // Island 4 — Storm Isle / Volcano (locked until CurrentLevel >= 4)
        var isle4GO = new GameObject("Island4Button");
        isle4GO.transform.SetParent(panel.transform, false);
        isle4GO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        var isle4Btn = isle4GO.AddComponent<Button>();
        var isle4RT  = isle4GO.GetComponent<RectTransform>();
        isle4RT.anchorMin = new Vector2(0.37f, 0.38f);
        isle4RT.anchorMax = new Vector2(0.60f, 0.72f);
        isle4RT.offsetMin = Vector2.zero;
        isle4RT.offsetMax = Vector2.zero;

        // Close button — top-right corner
        var closeBtnGO = MakeButton("CloseButton", panel.transform, "X");
        var closeRT    = closeBtnGO.GetComponent<RectTransform>();
        closeRT.anchorMin        = new Vector2(0.93f, 0.94f);
        closeRT.anchorMax        = new Vector2(0.99f, 0.99f);
        closeRT.sizeDelta        = Vector2.zero;
        closeRT.anchoredPosition = Vector2.zero;

        // Assign references to MainMenu
        script.mapPanel      = panel;
        script.mapImage      = mapImg;
        script.island2Button = isle2Btn;
        script.island3Button = isle3Btn;
        script.island4Button = isle4Btn;

        // Wire callbacks
        UnityEventTools.AddPersistentListener(isle1Btn.onClick, script.OnIsland1Clicked);
        UnityEventTools.AddPersistentListener(isle2Btn.onClick, script.OnIsland2Clicked);
        UnityEventTools.AddPersistentListener(isle3Btn.onClick, script.OnIsland3Clicked);
        UnityEventTools.AddPersistentListener(isle4Btn.onClick, script.OnIsland4Clicked);
        UnityEventTools.AddPersistentListener(
            closeBtnGO.GetComponent<Button>().onClick,
            script.OnCloseMapClicked);

        return panel;
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

        // Tab buttons — three tabs across the top strip
        var toBuyTabGO = MakeButton("ToBuyTab", shopPanelGO.transform, "TO BUY");
        var toBuyTabRT = toBuyTabGO.GetComponent<RectTransform>();
        toBuyTabRT.anchorMin        = new Vector2(0.02f, 0.82f);
        toBuyTabRT.anchorMax        = new Vector2(0.31f, 0.92f);
        toBuyTabRT.sizeDelta        = Vector2.zero;
        toBuyTabRT.anchoredPosition = Vector2.zero;

        var boughtTabGO = MakeButton("BoughtTab", shopPanelGO.transform, "BOUGHT");
        var boughtTabRT = boughtTabGO.GetComponent<RectTransform>();
        boughtTabRT.anchorMin        = new Vector2(0.35f, 0.82f);
        boughtTabRT.anchorMax        = new Vector2(0.64f, 0.92f);
        boughtTabRT.sizeDelta        = Vector2.zero;
        boughtTabRT.anchoredPosition = Vector2.zero;

        var statsTabGO = MakeButton("StatsTab", shopPanelGO.transform, "STATS");
        var statsTabRT = statsTabGO.GetComponent<RectTransform>();
        statsTabRT.anchorMin        = new Vector2(0.68f, 0.82f);
        statsTabRT.anchorMax        = new Vector2(0.98f, 0.92f);
        statsTabRT.sizeDelta        = Vector2.zero;
        statsTabRT.anchoredPosition = Vector2.zero;

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

        // Pirate ship card (To Buy)
        var piratToBuyGO = new GameObject("PiratShipCard");
        piratToBuyGO.transform.SetParent(toBuyContentGO.transform, false);
        var piratToBuyImg = piratToBuyGO.AddComponent<Image>();
        piratToBuyImg.color = new Color(0.15f, 0.10f, 0.05f, 1f);
        var piratToBuyRT = piratToBuyGO.GetComponent<RectTransform>();
        piratToBuyRT.anchorMin = new Vector2(0.42f, 0.55f);
        piratToBuyRT.anchorMax = new Vector2(0.78f, 0.98f);
        piratToBuyRT.offsetMin = Vector2.zero;
        piratToBuyRT.offsetMax = Vector2.zero;

        var ptSwatchGO = new GameObject("ColorSwatch");
        ptSwatchGO.transform.SetParent(piratToBuyGO.transform, false);
        var ptSwatchImg = ptSwatchGO.AddComponent<Image>();
        ptSwatchImg.color = new Color(0.45f, 0.30f, 0.15f);
        var ptSwatchRT = ptSwatchGO.GetComponent<RectTransform>();
        ptSwatchRT.anchorMin = new Vector2(0.05f, 0.55f);
        ptSwatchRT.anchorMax = new Vector2(0.95f, 0.95f);
        ptSwatchRT.offsetMin = Vector2.zero;
        ptSwatchRT.offsetMax = Vector2.zero;

        var ptNameGO = MakeText("ShipNameText", piratToBuyGO.transform, "PIRAT SHIP", 22, Color.white);
        var ptNameRT = ptNameGO.GetComponent<RectTransform>();
        ptNameRT.anchorMin = new Vector2(0f, 0.40f);
        ptNameRT.anchorMax = new Vector2(1f, 0.53f);
        ptNameRT.offsetMin = Vector2.zero;
        ptNameRT.offsetMax = Vector2.zero;
        ptNameGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var ptPriceGO = MakeText("PriceText", piratToBuyGO.transform, "200 COINS", 20, Color.yellow);
        var ptPriceRT = ptPriceGO.GetComponent<RectTransform>();
        ptPriceRT.anchorMin = new Vector2(0f, 0.27f);
        ptPriceRT.anchorMax = new Vector2(1f, 0.40f);
        ptPriceRT.offsetMin = Vector2.zero;
        ptPriceRT.offsetMax = Vector2.zero;

        var piratBuyBtnGO = MakeButton("BuyButton", piratToBuyGO.transform, "BUY");
        var piratBuyBtnRT = piratBuyBtnGO.GetComponent<RectTransform>();
        piratBuyBtnRT.anchorMin        = new Vector2(0.10f, 0.03f);
        piratBuyBtnRT.anchorMax        = new Vector2(0.90f, 0.24f);
        piratBuyBtnRT.sizeDelta        = Vector2.zero;
        piratBuyBtnRT.anchoredPosition = Vector2.zero;

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

        // Pirate ship card (Bought tab — initially inactive; RefreshShopUI activates on purchase)
        var piratBoughtGO = new GameObject("PiratShipBoughtCard");
        piratBoughtGO.transform.SetParent(boughtContentGO.transform, false);
        var piratBoughtImg = piratBoughtGO.AddComponent<Image>();
        piratBoughtImg.color = new Color(0.15f, 0.10f, 0.05f, 1f);
        var piratBoughtRT = piratBoughtGO.GetComponent<RectTransform>();
        piratBoughtRT.anchorMin = new Vector2(0.42f, 0.38f);
        piratBoughtRT.anchorMax = new Vector2(0.78f, 0.66f);
        piratBoughtRT.offsetMin = Vector2.zero;
        piratBoughtRT.offsetMax = Vector2.zero;

        var pbSwatchGO = new GameObject("ColorSwatch");
        pbSwatchGO.transform.SetParent(piratBoughtGO.transform, false);
        var pbSwatchImg = pbSwatchGO.AddComponent<Image>();
        pbSwatchImg.color = new Color(0.45f, 0.30f, 0.15f);
        var pbSwatchRT = pbSwatchGO.GetComponent<RectTransform>();
        pbSwatchRT.anchorMin = new Vector2(0.05f, 0.45f);
        pbSwatchRT.anchorMax = new Vector2(0.95f, 0.92f);
        pbSwatchRT.offsetMin = Vector2.zero;
        pbSwatchRT.offsetMax = Vector2.zero;

        var pbNameGO = MakeText("ShipNameText", piratBoughtGO.transform, "PIRAT SHIP", 22, Color.white);
        var pbNameRT = pbNameGO.GetComponent<RectTransform>();
        pbNameRT.anchorMin = new Vector2(0f, 0.30f);
        pbNameRT.anchorMax = new Vector2(1f, 0.43f);
        pbNameRT.offsetMin = Vector2.zero;
        pbNameRT.offsetMax = Vector2.zero;
        pbNameGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var piratSelectBtnGO = MakeButton("SelectButton", piratBoughtGO.transform, "SELECT");
        var piratSelectBtnRT = piratSelectBtnGO.GetComponent<RectTransform>();
        piratSelectBtnRT.anchorMin        = new Vector2(0.05f, 0.04f);
        piratSelectBtnRT.anchorMax        = new Vector2(0.52f, 0.26f);
        piratSelectBtnRT.sizeDelta        = Vector2.zero;
        piratSelectBtnRT.anchoredPosition = Vector2.zero;

        var piratSellBtnGO = MakeButton("SellButton", piratBoughtGO.transform, "SELL");
        var piratSellBtnRT = piratSellBtnGO.GetComponent<RectTransform>();
        piratSellBtnRT.anchorMin        = new Vector2(0.55f, 0.04f);
        piratSellBtnRT.anchorMax        = new Vector2(0.95f, 0.26f);
        piratSellBtnRT.sizeDelta        = Vector2.zero;
        piratSellBtnRT.anchoredPosition = Vector2.zero;
        piratSellBtnGO.GetComponent<Image>().color = new Color(0.65f, 0.12f, 0.12f);

        var piratSelectedLabelGO = MakeText("SelectedLabel", piratBoughtGO.transform, "✓ SELECTED", 20, Color.green);
        var piratSelectedLabelRT = piratSelectedLabelGO.GetComponent<RectTransform>();
        piratSelectedLabelRT.anchorMin = new Vector2(0.05f, 0.04f);
        piratSelectedLabelRT.anchorMax = new Vector2(0.95f, 0.26f);
        piratSelectedLabelRT.offsetMin = Vector2.zero;
        piratSelectedLabelRT.offsetMax = Vector2.zero;
        piratSelectedLabelGO.GetComponent<Text>().fontStyle = FontStyle.Bold;

        piratBoughtGO.SetActive(false);

        // Stats content panel — ship comparison table
        var statsContentGO = new GameObject("StatsContent");
        statsContentGO.transform.SetParent(shopPanelGO.transform, false);
        var statsContentRT = statsContentGO.AddComponent<RectTransform>();
        statsContentRT.anchorMin = new Vector2(0.02f, 0.05f);
        statsContentRT.anchorMax = new Vector2(0.98f, 0.80f);
        statsContentRT.offsetMin = Vector2.zero;
        statsContentRT.offsetMax = Vector2.zero;

        // Table helper: each row occupies 1/6 of the height (header + 5 data rows)
        // Row bands from top: header=0.84–1.0, then rows spaced 0.155 apart downward
        string[] colHeaders = { "SHIP",       "SIZE",  "HP", "SPD", "DMG",  "DELAY" };
        float[]  colMins    = {  0.00f,         0.30f,  0.45f, 0.57f, 0.70f, 0.83f  };
        float[]  colMaxs    = {  0.29f,         0.44f,  0.56f, 0.69f, 0.82f, 1.00f  };

        // Header row background
        var hdrBgGO = new GameObject("HeaderBg");
        hdrBgGO.transform.SetParent(statsContentGO.transform, false);
        var hdrBgImg = hdrBgGO.AddComponent<Image>();
        hdrBgImg.color = new Color(0.12f, 0.12f, 0.30f, 1f);
        var hdrBgRT = hdrBgGO.GetComponent<RectTransform>();
        hdrBgRT.anchorMin = new Vector2(0f, 0.84f);
        hdrBgRT.anchorMax = new Vector2(1f, 1.00f);
        hdrBgRT.offsetMin = hdrBgRT.offsetMax = Vector2.zero;

        for (int c = 0; c < colHeaders.Length; c++)
        {
            var cellGO = MakeText("H_" + colHeaders[c], hdrBgGO.transform, colHeaders[c], 18, Color.white);
            var cellRT = cellGO.GetComponent<RectTransform>();
            cellRT.anchorMin = new Vector2(colMins[c], 0f);
            cellRT.anchorMax = new Vector2(colMaxs[c], 1f);
            cellRT.offsetMin = cellRT.offsetMax = Vector2.zero;
            cellGO.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        // Data rows: Blue, Yellow, YellowRed, Pirate, Black(bot)
        string[][] rows = {
            new[] { "BLUE",        "1.00", "7", "20", "1.0", "2.0s" },
            new[] { "YELLOW",      "1.00", "7", "40", "1.0", "2.0s" },
            new[] { "YELLOW-RED",  "1.00", "8", "24", "1.0", "2.0s" },
            new[] { "PIRATE",      "1.00", "7", "20", "1.2", "1.5s" },
            new[] { "BLACK (BOT)", "0.66", "2", "15", "1.0", "2.5s" },
        };
        Color[] rowBgColors = {
            new Color(0.15f, 0.20f, 0.35f, 0.85f),
            new Color(0.20f, 0.18f, 0.04f, 0.85f),
            new Color(0.18f, 0.04f, 0.04f, 0.85f),
            new Color(0.12f, 0.08f, 0.04f, 0.85f),
            new Color(0.08f, 0.08f, 0.08f, 0.85f),
        };
        float rowHeight = 0.155f;
        float rowTop    = 0.84f;

        for (int r = 0; r < rows.Length; r++)
        {
            float yMax = rowTop - r * rowHeight;
            float yMin = yMax - rowHeight + 0.005f; // 0.5% gap between rows

            var rowBgGO  = new GameObject("Row_" + rows[r][0]);
            rowBgGO.transform.SetParent(statsContentGO.transform, false);
            var rowBgImg = rowBgGO.AddComponent<Image>();
            rowBgImg.color = rowBgColors[r];
            var rowBgRT  = rowBgGO.GetComponent<RectTransform>();
            rowBgRT.anchorMin = new Vector2(0f, yMin);
            rowBgRT.anchorMax = new Vector2(1f, yMax);
            rowBgRT.offsetMin = rowBgRT.offsetMax = Vector2.zero;

            // Ship name column: left-aligned, slightly larger
            var rowNameGO = MakeText("Col0", rowBgGO.transform, rows[r][0], 17, Color.white);
            var rowNameRT = rowNameGO.GetComponent<RectTransform>();
            rowNameRT.anchorMin = new Vector2(colMins[0] + 0.01f, 0f);
            rowNameRT.anchorMax = new Vector2(colMaxs[0], 1f);
            rowNameRT.offsetMin = rowNameRT.offsetMax = Vector2.zero;
            var rowNameTxt = rowNameGO.GetComponent<Text>();
            rowNameTxt.alignment = TextAnchor.MiddleLeft;
            rowNameTxt.fontStyle = FontStyle.Bold;

            // Stat columns: centered
            for (int c = 1; c < rows[r].Length; c++)
            {
                var cellGO = MakeText("Col" + c, rowBgGO.transform, rows[r][c], 18, Color.white);
                var cellRT = cellGO.GetComponent<RectTransform>();
                cellRT.anchorMin = new Vector2(colMins[c], 0f);
                cellRT.anchorMax = new Vector2(colMaxs[c], 1f);
                cellRT.offsetMin = cellRT.offsetMax = Vector2.zero;
            }
        }

        statsContentGO.SetActive(false);

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
        script.piratShipToBuyCard         = piratToBuyGO;
        script.piratShipBoughtCard        = piratBoughtGO;
        script.buyPiratShipButton         = piratBuyBtnGO.GetComponent<Button>();
        script.piratShipSelectButton      = piratSelectBtnGO.GetComponent<Button>();
        script.piratShipSelectedLabel     = piratSelectedLabelGO.GetComponent<Text>();
        script.piratShipSellButton        = piratSellBtnGO.GetComponent<Button>();
        script.promoCodeInput             = promoInputField;
        script.promoFeedbackText          = promoFeedbackGO.GetComponent<Text>();
        script.statsContent               = statsContentGO;

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
            statsTabGO.GetComponent<Button>().onClick,
            script.ShowStatsTab);
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
            piratBuyBtnGO.GetComponent<Button>().onClick,
            script.OnBuyPiratShipClicked);
        UnityEventTools.AddPersistentListener(
            piratSelectBtnGO.GetComponent<Button>().onClick,
            script.OnSelectPiratShip);
        UnityEventTools.AddPersistentListener(
            piratSellBtnGO.GetComponent<Button>().onClick,
            script.OnSellPiratShipClicked);
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

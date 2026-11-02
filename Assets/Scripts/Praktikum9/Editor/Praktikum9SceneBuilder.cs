using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Menyusun scene Praktikum 9 (Procedural Content Generation - Smart Spawner) lewat menu "Praktikum 9".
/// Arena 40 x 40 m dengan obstacle tetap (dinding, batu, pohon, peti, tong - layer Obstacle), Player,
/// Exit, waypoint + SafePoint untuk enemy Behavior Tree, lalu NavMesh di-bake.
/// Ikut dibuat: prefab 3 jenis enemy (Grunt, Brute, Scout), 3 jenis item (Coin, Potion, Key),
/// 1 legendary item (Treasure Chest), dan 3 aset personality.
/// Enemy dan item TIDAK diletakkan di scene: semuanya muncul dari <see cref="SmartSpawner"/> saat Play.
/// </summary>
public static class Praktikum9SceneBuilder
{
    private const string SceneFolder = "Assets/Scenes/Praktikum9";
    private const string MaterialFolder = SceneFolder + "/Materials";
    private const string PrefabFolder = SceneFolder + "/Prefabs";
    private const string PersonalityFolder = SceneFolder + "/Personalities";
    private const string ScenePath = SceneFolder + "/P09_PCG_SmartSpawner.unity";

    // Animator Enemy dibuat oleh builder Praktikum 6 (Idle/Run + Attack), dipakai ulang di sini.
    private const string EnemyAnimatorPath = "Assets/Scenes/Praktikum6/Animation/EnemyBT.controller";
    private const string SoldierModel = "Assets/Materials/sci-fi-supersoldier-game-ready-animated/source/Idle.fbx";
    private const string PlayerAnimController = "Assets/Animations/PlayerAnimation.controller";

    private const string SyntyChars = "Assets/Synty/PolygonGeneric/Prefabs/Characters/";
    private const string SyntyProps = "Assets/Synty/PolygonGeneric/Prefabs/Props/";
    private const string SyntyStarter = "Assets/Synty/PolygonStarter/Prefabs/";
    private const string PolyRocks = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/";

    private const float ArenaHalf = 20f;
    private static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -3f);
    private static readonly Vector3 ExitPosition = new Vector3(15.5f, 0f, 15.5f);

    [MenuItem("Praktikum 9/Build Scene - PCG Smart Spawner")]
    private static void BuildScene()
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
        {
            EditorUtility.DisplayDialog("Praktikum 9", "Layer 'Obstacle' belum ada. Tambahkan di Project Settings > Tags and Layers.", "OK");
            return;
        }
        if (!PrepareNewScene())
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Material groundMat = GetMaterial("P9_Ground", new Color(0.42f, 0.55f, 0.34f));
        Material wallMat = GetMaterial("P9_StoneWall", new Color(0.52f, 0.5f, 0.47f));
        Material borderMat = GetMaterial("P9_Border", new Color(0.4f, 0.32f, 0.24f));
        Material waypointMat = GetMaterial("P9_Waypoint", new Color(0.2f, 0.85f, 1f), unlit: true);
        Material safeMat = GetMaterial("P9_SafePoint", new Color(0.2f, 1f, 0.45f), unlit: true);
        Material exitMat = GetMaterial("P9_Exit", Color.white, unlit: true);
        Material indicatorMat = GetMaterial("P9_ActionIndicator", Color.white, unlit: true);
        Material barMat = GetMaterial("P9_HealthBar", Color.white, unlit: true);

        // ---------------------------------------------------------------- Prefab
        RuntimeAnimatorController enemyAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyAnimatorPath);
        if (enemyAnimator == null)
            Debug.LogWarning($"[Praktikum 9] {EnemyAnimatorPath} tidak ada. Jalankan menu Praktikum 6 sekali agar enemy beranimasi.");

        EnemyPersonality grunt = Personality("Grunt", new Color(0.85f, 0.85f, 0.85f), flee: 0.3f, chase: 4f, cooldown: 1.5f, damage: 8f, search: 4f);
        EnemyPersonality brute = Personality("Brute", new Color(1f, 0.4f, 0.3f), flee: 0.1f, chase: 3.2f, cooldown: 2f, damage: 15f, search: 6f);
        EnemyPersonality scout = Personality("Scout", new Color(0.5f, 0.75f, 1f), flee: 0.5f, chase: 5.5f, cooldown: 1f, damage: 5f, search: 2f);

        int obstacleMask = 1 << obstacleLayer;
        GameObject gruntPrefab = CreateEnemyPrefab("Enemy_Grunt", SyntyChars + "SM_Gen_Chr_Skeleton_01.prefab", 1f, 100f, grunt,
            obstacleMask, enemyAnimator, indicatorMat, barMat);
        GameObject brutePrefab = CreateEnemyPrefab("Enemy_Brute", SyntyChars + "SM_Gen_Chr_Charred_01.prefab", 1.18f, 160f, brute,
            obstacleMask, enemyAnimator, indicatorMat, barMat);
        GameObject scoutPrefab = CreateEnemyPrefab("Enemy_Scout", SyntyChars + "SM_Gen_Chr_Robot_01.prefab", 0.9f, 60f, scout,
            obstacleMask, enemyAnimator, indicatorMat, barMat);

        GameObject coinPrefab = CreateItemPrefab("Item_Coin", SyntyProps + "SM_Gen_Prop_Coin_Pile_01.prefab", 0.45f, "Coin",
            score: 10, heal: 0f, legendary: false, GetMaterial("P9_Ring_Coin", new Color(1f, 0.85f, 0.2f), unlit: true));
        GameObject potionPrefab = CreateItemPrefab("Item_Potion", SyntyProps + "SM_Gen_Prop_Potion_01.prefab", 0.6f, "Potion",
            score: 5, heal: 30f, legendary: false, GetMaterial("P9_Ring_Potion", new Color(1f, 0.35f, 0.5f), unlit: true));
        GameObject keyPrefab = CreateItemPrefab("Item_Key", SyntyProps + "SM_Gen_Prop_Key_01.prefab", 0.5f, "Key",
            score: 25, heal: 0f, legendary: false, GetMaterial("P9_Ring_Key", new Color(0.3f, 0.9f, 1f), unlit: true));
        GameObject legendaryPrefab = CreateItemPrefab("Item_Legendary_Chest", SyntyProps + "SM_Gen_Prop_Chest_02.prefab", 0.9f, "Legendary",
            score: 100, heal: 50f, legendary: true, GetMaterial("P9_Ring_Legendary", new Color(1f, 0.6f, 0.05f), unlit: true));

        // ---------------------------------------------------------------- Environment
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(ArenaHalf * 0.2f + 0.4f, 1f, ArenaHalf * 0.2f + 0.4f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var env = new GameObject("Environment (Obstacle)").transform;

        var border = new GameObject("Border").transform;
        border.SetParent(env, false);
        float b = ArenaHalf + 0.5f;
        CreateBox(border, "Border_N", new Vector3(0f, 0.75f, b), new Vector3(2 * b + 1f, 1.5f, 1f), obstacleLayer, borderMat);
        CreateBox(border, "Border_S", new Vector3(0f, 0.75f, -b), new Vector3(2 * b + 1f, 1.5f, 1f), obstacleLayer, borderMat);
        CreateBox(border, "Border_E", new Vector3(b, 0.75f, 0f), new Vector3(1f, 1.5f, 2 * b + 1f), obstacleLayer, borderMat);
        CreateBox(border, "Border_W", new Vector3(-b, 0.75f, 0f), new Vector3(1f, 1.5f, 2 * b + 1f), obstacleLayer, borderMat);

        var walls = new GameObject("Walls").transform;
        walls.SetParent(env, false);
        CreateBox(walls, "Wall_NW", new Vector3(-8f, 1.5f, 6f), new Vector3(8f, 3f, 1f), obstacleLayer, wallMat);
        CreateBox(walls, "Wall_E", new Vector3(9f, 1.5f, -4f), new Vector3(1f, 3f, 8f), obstacleLayer, wallMat);
        CreateBox(walls, "Wall_S", new Vector3(-3f, 1.5f, -12f), new Vector3(8f, 3f, 1f), obstacleLayer, wallMat);
        CreateBox(walls, "Wall_N", new Vector3(5f, 1.5f, 10f), new Vector3(6f, 3f, 1f), obstacleLayer, wallMat);

        var props = new GameObject("Props").transform;
        props.SetParent(env, false);
        PlaceProp(props, PolyRocks + "PT_Generic_Rock_01.prefab", new Vector3(-12f, 0f, -6f), 2f, 20f, obstacleLayer, trunk: false);
        PlaceProp(props, PolyRocks + "PT_Menhir_Rock_02.prefab", new Vector3(13f, 0f, 5f), 3.5f, 0f, obstacleLayer, trunk: false);
        PlaceProp(props, PolyRocks + "PT_Generic_Rock_01.prefab", new Vector3(-4f, 0f, 14f), 1.8f, 140f, obstacleLayer, trunk: false);
        PlaceProp(props, PolyRocks + "PT_Ore_Rock_01.prefab", new Vector3(8f, 0f, -15f), 2f, 60f, obstacleLayer, trunk: false);
        PlaceProp(props, SyntyStarter + "SM_Generic_Tree_03.prefab", new Vector3(-16f, 0f, 14f), 7f, 0f, obstacleLayer, trunk: true);
        PlaceProp(props, SyntyStarter + "SM_Generic_Tree_04.prefab", new Vector3(16f, 0f, -16f), 6.5f, 45f, obstacleLayer, trunk: true);
        PlaceProp(props, SyntyStarter + "SM_Generic_Tree_01.prefab", new Vector3(-16f, 0f, -16f), 5.5f, 90f, obstacleLayer, trunk: true);
        PlaceProp(props, SyntyStarter + "SM_Generic_Tree_02.prefab", new Vector3(3f, 0f, 4f), 5.5f, 200f, obstacleLayer, trunk: true);
        PlaceProp(props, SyntyStarter + "SM_Generic_Tree_01.prefab", new Vector3(-10f, 0f, 0f), 5f, 30f, obstacleLayer, trunk: true);
        PlaceProp(props, SyntyProps + "SM_Gen_Prop_Crate_01.prefab", new Vector3(5f, 0f, -8f), 1.2f, 15f, obstacleLayer, trunk: false);
        PlaceProp(props, SyntyProps + "SM_Gen_Prop_Barrel_Wood_01.prefab", new Vector3(-6f, 0f, -7f), 1.3f, 0f, obstacleLayer, trunk: false);
        PlaceProp(props, SyntyProps + "SM_Gen_Prop_Barrel_Wood_02.prefab", new Vector3(15f, 0f, 0f), 1.3f, 0f, obstacleLayer, trunk: false);
        PlaceProp(props, SyntyProps + "SM_Gen_Prop_Crate_02.prefab", new Vector3(-14f, 0f, 8f), 1.2f, 40f, obstacleLayer, trunk: false);

        // ---------------------------------------------------------------- Waypoints & SafePoints (untuk Behavior Tree)
        var wpRoot = new GameObject("Waypoints").transform;
        Vector3[] wpPositions =
        {
            new Vector3(-12f, 0f, 12f),
            new Vector3(11f, 0f, 13f),
            new Vector3(14f, 0f, -9f),
            new Vector3(-12f, 0f, -11f),
        };
        var waypoints = new Transform[wpPositions.Length];
        for (int i = 0; i < wpPositions.Length; i++)
            waypoints[i] = CreateMarker(wpRoot, $"WP_{i + 1}", wpPositions[i], 0.5f, waypointMat).transform;

        var safeRoot = new GameObject("SafePoints").transform;
        SafePoint safeA = CreateSafePoint(safeRoot, "SafePoint_W", new Vector3(-17.5f, 0f, -3f), safeMat);
        SafePoint safeB = CreateSafePoint(safeRoot, "SafePoint_E", new Vector3(17.5f, 0f, -4f), safeMat);

        // ---------------------------------------------------------------- Player & Exit
        Camera cam = Object.FindAnyObjectByType<Camera>();
        GameObject player = CreatePlayer(PlayerStart, cam, barMat);
        ExitGate exit = CreateExit(ExitPosition, exitMat);

        // ---------------------------------------------------------------- Goal, Spawner, Runner, HUD
        var systems = new GameObject("PCG Systems").transform;

        var goal = new GameObject("Game Goal").AddComponent<P9GameGoal>();
        goal.transform.SetParent(systems, false);
        Set(goal, "player", player.transform);
        Set(goal, "playerHealth", player.GetComponent<Health>());
        Set(goal, "exit", exit);
        SetArray(goal, "disableOnEnd", new Object[]
        {
            player.GetComponent<global::PlayerController>(),
            player.GetComponent<PlayerMeleeAttack>(),
        });

        var spawner = new GameObject("Smart Spawner").AddComponent<SmartSpawner>();
        spawner.transform.SetParent(systems, false);
        spawner.Config = new SpawnConfig
        {
            seed = 100,
            enemyCount = 8,
            itemCount = 15,
            areaSize = new Vector2(36f, 36f),
            playerSafeRadius = 5f,
            minSpacing = 1.5f,
            obstacleClearance = 0.7f,
            maxAttempts = 30,
            enemyTypes = new[]
            {
                new SpawnEntry { displayName = "Grunt", prefab = gruntPrefab, weight = 60f },
                new SpawnEntry { displayName = "Brute", prefab = brutePrefab, weight = 30f },
                new SpawnEntry { displayName = "Scout", prefab = scoutPrefab, weight = 10f },
            },
            itemTypes = new[]
            {
                new SpawnEntry { displayName = "Coin", prefab = coinPrefab, weight = 60f },
                new SpawnEntry { displayName = "Potion", prefab = potionPrefab, weight = 30f },
                new SpawnEntry { displayName = "Key", prefab = keyPrefab, weight = 10f },
            },
            legendaryPrefab = legendaryPrefab,
            legendaryChance = 0.08f,
            maxLegendary = 1,
            legendaryMinPlayerDistance = 12f,
            randomRotation = true,
        };
        EditorUtility.SetDirty(spawner);
        Set(spawner, "player", player.transform);
        SetMask(spawner, "obstacleMask", obstacleMask);
        SetArray(spawner, "keepOutPoints", new Object[] { exit.transform });
        Set(spawner, "keepOutRadius", 3f);
        SetArray(spawner, "waypoints", waypoints);
        SetArray(spawner, "safePoints", new Object[] { safeA, safeB });
        Set(spawner, "goal", goal);

        var runner = new GameObject("Experiment Runner").AddComponent<SpawnExperimentRunner>();
        runner.transform.SetParent(systems, false);
        Set(runner, "spawner", spawner);

        var hud = new GameObject("HUD (Spawner Debug)").AddComponent<SpawnerHUD>();
        hud.transform.SetParent(systems, false);
        Set(hud, "spawner", spawner);
        Set(hud, "goal", goal);
        Set(hud, "runner", runner);

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // ---------------------------------------------------------------- Kamera & NavMesh
        cam.transform.SetPositionAndRotation(PlayerStart + new Vector3(0f, 5f, -9f), Quaternion.Euler(15f, 0f, 0f));
        var follow = cam.gameObject.AddComponent<CameraFollow>();
        Set(follow, "target", player.transform);
        Set(follow, "distance", 10f);
        Set(follow, "height", 3f);

        var surfaceObject = new GameObject("NavMeshSurface");
        var surface = surfaceObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Selection.activeGameObject = spawner.gameObject;

        BakeAndSave(surface, scene);
    }

    [MenuItem("Praktikum 9/Run Experiments (scene terbuka)")]
    private static void RunExperiments()
    {
        var runner = Object.FindAnyObjectByType<SpawnExperimentRunner>();
        if (runner == null)
        {
            EditorUtility.DisplayDialog("Praktikum 9", $"SpawnExperimentRunner tidak ditemukan. Buka {ScenePath} dulu.", "OK");
            return;
        }
        runner.RunAll();
    }

    /// <summary>Entry point command line: Unity -batchmode -executeMethod Praktikum9SceneBuilder.BuildSceneBatch</summary>
    public static void BuildSceneBatch()
    {
        BuildScene();
    }

    /// <summary>Entry point command line: Unity -batchmode -executeMethod Praktikum9SceneBuilder.RunExperimentsBatch</summary>
    public static void RunExperimentsBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RunExperiments();
    }

    // ------------------------------------------------------------------ Personality

    private static EnemyPersonality Personality(string name, Color color, float flee, float chase, float cooldown,
        float damage, float search)
    {
        string path = $"{PersonalityFolder}/Personality_{name}.asset";
        var p = AssetDatabase.LoadAssetAtPath<EnemyPersonality>(path);
        if (p == null)
        {
            p = ScriptableObject.CreateInstance<EnemyPersonality>();
            AssetDatabase.CreateAsset(p, path);
        }
        p.displayName = name;
        p.color = color;
        p.fleeThreshold = flee;
        p.chaseSpeed = chase;
        p.attackCooldown = cooldown;
        p.attackDamage = damage;
        p.searchDuration = search;
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    // ------------------------------------------------------------------ Prefab Enemy

    /// <summary>
    /// Prefab Enemy Behavior Tree (Praktikum 6) tanpa referensi scene. Target Player, waypoint, dan SafePoint
    /// diisi SmartSpawner saat Instantiate (EnemyPerception.SetTarget, EnemyBrain.SetRoute).
    /// </summary>
    private static GameObject CreateEnemyPrefab(string name, string modelPath, float scale, float maxHealth,
        EnemyPersonality personality, int obstacleMask, RuntimeAnimatorController animatorController,
        Material indicatorMat, Material barMat)
    {
        var enemy = new GameObject(name);

        var collider = enemy.AddComponent<CapsuleCollider>();
        collider.height = 1.8f * scale;
        collider.radius = 0.35f * scale;
        collider.center = new Vector3(0f, 0.9f * scale, 0f);

        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.baseOffset = 0f;
        agent.radius = 0.4f;
        agent.height = 1.8f * scale;
        agent.speed = 2f;
        agent.angularSpeed = 540f;
        agent.acceleration = 16f;
        agent.autoBraking = true;

        Health health = enemy.AddComponent<Health>();
        Set(health, "maxHealth", maxHealth);
        Set(health, "current", maxHealth);

        var perception = enemy.AddComponent<EnemyPerception>();
        Set(perception, "viewRange", 9f);
        SetMask(perception, "obstacleMask", obstacleMask);

        var brain = enemy.AddComponent<EnemyBrain>();
        Set(brain, "personality", personality);
        Set(brain, "logActions", false);

        GameObject model = InstantiateAsset(modelPath, enemy.transform);
        Animator animator = null;
        if (model != null)
        {
            model.name = "Model";
            model.transform.localScale *= scale;
            animator = GetOrAdd<Animator>(model);
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            foreach (Collider c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
        }

        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = "ActionIndicator";
        Object.DestroyImmediate(indicator.GetComponent<Collider>());
        indicator.transform.SetParent(enemy.transform, false);
        indicator.transform.localPosition = new Vector3(0f, 2.25f * scale, 0f);
        indicator.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
        indicator.GetComponent<Renderer>().sharedMaterial = indicatorMat;

        var visual = enemy.AddComponent<EnemyBrainVisual>();
        Set(visual, "brain", brain);
        Set(visual, "health", health);
        Set(visual, "agent", agent);
        if (animator != null)
            Set(visual, "animator", animator);
        if (model != null)
            Set(visual, "model", model.transform);
        Set(visual, "actionIndicator", indicator.GetComponent<Renderer>());

        var bar = enemy.AddComponent<WorldHealthBar>();
        Set(bar, "health", health);
        Set(bar, "brain", brain);
        Set(bar, "barMaterial", barMat);
        Set(bar, "height", 2.95f * scale);

        return SavePrefab(enemy, name);
    }

    // ------------------------------------------------------------------ Prefab Item

    private static GameObject CreateItemPrefab(string name, string modelPath, float height, string typeName,
        int score, float heal, bool legendary, Material ringMat)
    {
        var item = new GameObject(name);

        GameObject model = InstantiateAsset(modelPath, item.transform);
        if (model != null)
        {
            model.name = "Model";
            Bounds bounds = RenderBounds(model);
            if (bounds.size.y > 0.01f)
                model.transform.localScale *= height / bounds.size.y;
            model.transform.localPosition = Vector3.up * 0.25f;
            foreach (Collider c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
        }

        // Cincin di tanah agar item mudah terlihat dari jauh; warnanya membedakan jenis.
        GameObject ring = CreateMarker(item.transform, "Ring", Vector3.zero, legendary ? 0.8f : 0.5f, ringMat);
        ring.transform.localPosition = Vector3.up * 0.02f;

        Light glow = null;
        if (legendary)
        {
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(item.transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.2f;
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.75f, 0.2f);
            glow.range = 5f;
            glow.intensity = 3f;
        }

        var collectible = item.AddComponent<Collectible>();
        Set(collectible, "typeName", typeName);
        Set(collectible, "score", score);
        Set(collectible, "heal", heal);
        Set(collectible, "legendary", legendary);
        if (model != null)
            Set(collectible, "model", model.transform);
        if (glow != null)
            Set(collectible, "glow", glow);

        return SavePrefab(item, name);
    }

    private static GameObject SavePrefab(GameObject go, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ Player & Exit

    private static GameObject CreatePlayer(Vector3 position, Camera cam, Material barMat)
    {
        var player = new GameObject("Player");
        player.transform.position = position;

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        var controller = player.AddComponent<global::PlayerController>();
        Set(controller, "cameraTransform", cam.transform);

        var health = player.AddComponent<Health>();
        Set(health, "maxHealth", 150f);
        Set(health, "current", 150f);

        var model = InstantiateAsset(SoldierModel, player.transform);
        if (model != null)
        {
            model.name = "Supersoldier";
            // FBX ini ikut membawa Light dan Camera dari file sumbernya; dimatikan supaya tidak silau.
            foreach (Light l in model.GetComponentsInChildren<Light>(true))
                l.gameObject.SetActive(false);
            foreach (Camera c in model.GetComponentsInChildren<Camera>(true))
                c.gameObject.SetActive(false);
            Animator animator = GetOrAdd<Animator>(model);
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimController);
            animator.applyRootMotion = false;
        }

        var driver = player.AddComponent<PlayerAnimatorDriver>();
        if (model != null)
            Set(driver, "animator", model.GetComponent<Animator>());

        var melee = player.AddComponent<PlayerMeleeAttack>();
        if (model != null)
            Set(melee, "model", model.transform);

        var bar = player.AddComponent<WorldHealthBar>();
        Set(bar, "health", health);
        Set(bar, "barMaterial", barMat);
        Set(bar, "height", 2.3f);

        IgnoreFromBake(player);
        return player;
    }

    private static ExitGate CreateExit(Vector3 position, Material mat)
    {
        var root = new GameObject("Exit");
        root.transform.position = position;

        GameObject pad = CreateMarker(root.transform, "Pad", position, 1.5f, mat);
        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "Beacon";
        Object.DestroyImmediate(beacon.GetComponent<Collider>());
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = Vector3.up * 1.5f;
        beacon.transform.localScale = new Vector3(0.25f, 1.5f, 0.25f);
        beacon.GetComponent<Renderer>().sharedMaterial = mat;

        var renderers = new List<Object> { pad.GetComponent<Renderer>(), beacon.GetComponent<Renderer>() };
        foreach (float x in new[] { -1.4f, 1.4f })
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            Object.DestroyImmediate(pillar.GetComponent<Collider>());
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(x, 1.2f, 0f);
            pillar.transform.localScale = new Vector3(0.3f, 2.4f, 0.3f);
            pillar.GetComponent<Renderer>().sharedMaterial = mat;
            renderers.Add(pillar.GetComponent<Renderer>());
        }

        var gate = root.AddComponent<ExitGate>();
        Set(gate, "radius", 1.5f);
        SetArray(gate, "indicators", renderers.ToArray());
        IgnoreFromBake(root);
        return gate;
    }

    // ------------------------------------------------------------------ Waypoint / SafePoint / Prop

    private static GameObject CreateMarker(Transform parent, string name, Vector3 position, float radius, Material mat)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = name;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.transform.SetParent(parent, false);
        marker.transform.position = position + Vector3.up * 0.02f;
        marker.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        marker.GetComponent<Renderer>().sharedMaterial = mat;
        return marker;
    }

    private static SafePoint CreateSafePoint(Transform parent, string name, Vector3 position, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var sp = go.AddComponent<SafePoint>();
        Set(sp, "radius", 1.5f);

        GameObject pad = CreateMarker(go.transform, "Pad", position, 1.5f, mat);
        pad.transform.localScale = new Vector3(3f, 0.02f, 3f);
        return sp;
    }

    /// <summary>
    /// Pasang prefab dekorasi sebagai obstacle: diskalakan ke tinggi tertentu, diberi layer Obstacle,
    /// dan diberi collider agar ikut di-bake ke NavMesh dan terdeteksi validasi spawn.
    /// </summary>
    private static void PlaceProp(Transform parent, string path, Vector3 position, float targetHeight,
        float rotY, int layer, bool trunk)
    {
        GameObject prop = InstantiateAsset(path, parent);
        if (prop == null)
            return;

        prop.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotY, 0f));
        Bounds bounds = RenderBounds(prop);
        if (bounds.size.y > 0.01f)
            prop.transform.localScale *= targetHeight / bounds.size.y;

        foreach (Transform t in prop.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;

        foreach (Collider c in prop.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);

        if (trunk)
        {
            var capsule = prop.AddComponent<CapsuleCollider>();
            float s = prop.transform.lossyScale.y;
            capsule.radius = 0.45f / s;
            capsule.height = 3f / s;
            capsule.center = new Vector3(0f, 1.5f / s, 0f);
        }
        else
        {
            foreach (MeshFilter mf in prop.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        GameObjectUtility.SetStaticEditorFlags(prop, StaticEditorFlags.NavigationStatic);
    }

    private static Bounds RenderBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static GameObject InstantiateAsset(string path, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[Praktikum 9] Aset tidak ditemukan: {path}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    // ------------------------------------------------------------------ NavMesh & scene

    private static void IgnoreFromBake(GameObject go)
    {
        var modifier = go.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }

    private static void BakeAndSave(NavMeshSurface surface, Scene scene)
    {
        surface.BuildNavMesh();

        string dataFolder = ScenePath.Substring(0, ScenePath.Length - ".unity".Length);
        EnsureFolder(dataFolder);
        string assetPath = $"{dataFolder}/NavMesh-{surface.name}.asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Praktikum 9] Scene tersimpan di {ScenePath}, NavMesh sudah di-bake ({assetPath}). Tekan Play.");
    }

    private static bool PrepareNewScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Praktikum 9", $"{ScenePath} sudah ada. Timpa dengan scene baru?", "Timpa", "Batal"))
            return false;

        EnsureFolder(SceneFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(PersonalityFolder);
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static Material GetMaterial(string name, Color color, bool unlit = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, int layer, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.layer = layer;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.NavigationStatic);
        return box;
    }

    private static void AddToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Field script bersifat private [SerializeField], jadi diisi lewat SerializedObject.
    private static void Set(Component target, string property, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogError($"[Praktikum 9] Field '{property}' tidak ditemukan di {target.GetType().Name}.");
            return;
        }

        switch (value)
        {
            case Object obj: prop.objectReferenceValue = obj; break;
            case float f: prop.floatValue = f; break;
            case int i: prop.intValue = i; break;
            case bool bo: prop.boolValue = bo; break;
            case string s: prop.stringValue = s; break;
            default:
                Debug.LogError($"[Praktikum 9] Tipe {value?.GetType().Name} belum didukung untuk '{property}'.");
                return;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Component target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetMask(Component target, string property, int mask)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).intValue = mask;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

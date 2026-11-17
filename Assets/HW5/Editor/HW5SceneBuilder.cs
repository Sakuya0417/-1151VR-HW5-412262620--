using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 一鍵建立作業場景：選單 HW5 ▸ Build Pokemon Scene
/// 1. 起伏地形（中央平坦草地、遠方高山）＋ 草地/岩石貼圖 ＋ 草叢
/// 2. 松樹、闊葉樹、灌木
/// 3. 訓練家、寶可夢、精靈球：若 Assets/Models/Trainer、Pokemon、Pokeball 內有模型就自動使用，否則放替代模型
/// 4. 燈光、霧、天空、攝影機、控制器
/// </summary>
public static class HW5SceneBuilder
{
    const string GenDir = "Assets/HW5/Generated";
    const string ScenePath = "Assets/Scenes/HW5_PokemonCapture.unity";
    const float TerrainSize = 300f;
    const float TerrainHeight = 60f;

    // 精靈球直徑依訓練家身高等比例設定：約身高的 7.5%（1.7 m → 約 13 cm，一手可握）
    const float TrainerHeight = 1.7f;
    const float BallSize = TrainerHeight * 0.075f;

    static readonly Vector3 TrainerPos = new Vector3(-4f, 0f, 0f);
    static readonly Vector3 PokemonPos = new Vector3(4f, 0f, 0f);

    [MenuItem("HW5/Build Pokemon Scene", priority = 0)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(GenDir);
        EnsureFolder("Assets/Scenes");
        // 移除先前試用、已被取代的精靈球模型
        foreach (var old in new[] { "Assets/Models/Pokeball/pokemob.fbx",
                                    "Assets/Models/Pokeball/Textures/Diffuso_pokeball.jpg",
                                    "Assets/Models/Pokeball/Textures/Pokeball_NormalsMap.jpg" })
            if (AssetDatabase.LoadMainAssetAtPath(old) != null) AssetDatabase.DeleteAsset(old);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rnd = new System.Random(1151);

        // ── 環境 ──
        var sunGO = new GameObject("Directional Light");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        RenderSettings.sun = sun;
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.005f;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);

        // ── 地形 ──
        var terrain = BuildTerrain(rnd);

        // ── 樹木與灌木 ──
        var pine = MakePinePrefab();
        var round = MakeRoundTreePrefab();
        var bush = MakeBushPrefab();
        PlaceVegetation(terrain, pine, round, bush, rnd);

        // ── 角色 ──
        var trainer = BuildTrainer();
        var pokemon = BuildPokemon();
        var ball = BuildBall();
        ball.transform.position = TrainerPos + new Vector3(0.3f, 0.9f, -0.3f);

        // ── 攝影機 ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 1000f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 2.0f, -8.5f);
        camGO.transform.LookAt(new Vector3(0f, 1.0f, 0f));
        var rig = camGO.AddComponent<HW5CameraRig>();
        rig.ball = ball.transform;

        // ── 控制器 ──
        var gm = new GameObject("GameManager");
        var ctrl = gm.AddComponent<PokeballThrowController>();
        ctrl.trainer = trainer.GetComponent<TrainerThrowAnimator>();
        ctrl.ball = ball.transform;
        ctrl.pokemon = pokemon.transform;
        gm.AddComponent<ScreenshotTaker>();
        gm.AddComponent<HW5Recorder>();   // 平時不作用，選單 HW5 ▸ Record Video 才會錄影

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild(ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = gm;
        Debug.Log("[HW5] 場景建立完成：" + ScenePath + "　按 Play 開始，Space 拋球、R 重置、F12 截圖。");
    }

    // ═════════════════ 地形 ═════════════════

    static Terrain BuildTerrain(System.Random rnd)
    {
        int res = 257;
        var td = new TerrainData { heightmapResolution = res };
        td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        var h = new float[res, res];
        float ox = (float)rnd.NextDouble() * 100f, oz = (float)rnd.NextDouble() * 100f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = x / (res - 1f) * TerrainSize - TerrainSize / 2f;
                float wz = y / (res - 1f) * TerrainSize - TerrainSize / 2f;
                float d = Mathf.Sqrt(wx * wx + wz * wz);

                float rolling = Mathf.PerlinNoise(ox + x * 0.035f, oz + y * 0.035f) * 0.02f;     // 平緩起伏
                float flat = Mathf.InverseLerp(20f, 8f, d);                                       // 中央遊玩區壓平
                float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 120f, d));           // 往外圍升高成山
                float back = Mathf.InverseLerp(-20f, 120f, wz);                                    // 鏡頭後方的山較高
                float n = Mathf.PerlinNoise(ox + 50f + x * 0.025f, oz + 50f + y * 0.025f);
                float ridge = 1f - Mathf.Abs(2f * n - 1f);                                         // 山脊
                float detail = Mathf.PerlinNoise(ox + x * 0.09f, oz + y * 0.09f) * 0.15f;
                float mountain = mask * (0.3f + 0.7f * back) * (0.35f + 0.55f * ridge + detail);

                h[y, x] = Mathf.Clamp01(rolling * (1f - flat) + mountain);
            }
        }
        td.SetHeights(0, 0, h);

        // 貼圖層：草地 + 岩石
        var grassLayer = MakeLayer("Grass", NoiseTex("GroundGrass", new Color(0.24f, 0.48f, 0.16f), new Color(0.42f, 0.66f, 0.24f), 0.06f), 6f);
        var rockLayer = MakeLayer("Rock", NoiseTex("GroundRock", new Color(0.38f, 0.35f, 0.31f), new Color(0.6f, 0.57f, 0.52f), 0.1f), 14f);
        td.terrainLayers = new[] { grassLayer, rockLayer };
        td.alphamapResolution = 256;
        int ar = td.alphamapResolution;
        var am = new float[ar, ar, 2];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float nx = x / (ar - 1f), ny = y / (ar - 1f);
                float steep = td.GetSteepness(nx, ny);
                float hgt = td.GetInterpolatedHeight(nx, ny);
                float rock = Mathf.Max(Mathf.Clamp01((steep - 22f) / 14f), Mathf.Clamp01((hgt - 30f) / 12f));
                am[y, x, 0] = 1f - rock;
                am[y, x, 1] = rock;
            }
        td.SetAlphamaps(0, 0, am);

        // 草叢 (Detail)
        int dres = 512;
        td.SetDetailResolution(dres, 32);
#if UNITY_2022_2_OR_NEWER
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
#endif
        var dp = new DetailPrototype
        {
            prototypeTexture = GrassBladeTex(),
            renderMode = DetailRenderMode.Grass,
            usePrototypeMesh = false,
            minWidth = 0.5f, maxWidth = 0.9f,
            minHeight = 0.3f, maxHeight = 0.65f,
            noiseSpread = 0.3f,
            healthyColor = new Color(0.55f, 0.85f, 0.35f),
            dryColor = new Color(0.75f, 0.78f, 0.38f)
        };
        td.detailPrototypes = new[] { dp };
        var dens = new int[dres, dres];
        for (int y = 0; y < dres; y++)
            for (int x = 0; x < dres; x++)
            {
                float nx = x / (dres - 1f), ny = y / (dres - 1f);
                float wx = nx * TerrainSize - TerrainSize / 2f, wz = ny * TerrainSize - TerrainSize / 2f;
                float d = Mathf.Sqrt(wx * wx + wz * wz);
                if (d > 90f) continue;
                if (Mathf.Abs(wx) < 7.5f && Mathf.Abs(wz) < 3.5f) continue;   // 主角區域割草，精靈球落地才看得到
                if (td.GetSteepness(nx, ny) > 22f || td.GetInterpolatedHeight(nx, ny) > 15f) continue;
                dens[y, x] = rnd.Next(1, 4);
            }
        td.SetDetailLayer(0, 0, 0, dens);

        AssetDatabase.DeleteAsset(GenDir + "/HW5_Terrain.asset");
        AssetDatabase.CreateAsset(td, GenDir + "/HW5_Terrain.asset");

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.position = new Vector3(-TerrainSize / 2f, 0f, -TerrainSize / 2f);
        var t = go.GetComponent<Terrain>();
        t.detailObjectDistance = 80f;
        t.detailObjectDensity = 1f;
        t.basemapDistance = 1000f;
        t.heightmapPixelError = 3f;
        return t;
    }

    static TerrainLayer MakeLayer(string name, Texture2D tex, float tile)
    {
        string p = GenDir + "/Layer_" + name + ".terrainlayer";
        AssetDatabase.DeleteAsset(p);
        var l = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile) };
        AssetDatabase.CreateAsset(l, p);
        return l;
    }

    static Texture2D NoiseTex(string name, Color a, Color b, float scale)
    {
        int n = 256;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var r = new System.Random(name.GetHashCode());
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // 可無縫拼接的雜訊（四角混合）
                float u = x / (float)n, v = y / (float)n;
                float n1 = Mathf.PerlinNoise(x * scale, y * scale);
                float n2 = Mathf.PerlinNoise((x - n) * scale, y * scale);
                float n3 = Mathf.PerlinNoise(x * scale, (y - n) * scale);
                float n4 = Mathf.PerlinNoise((x - n) * scale, (y - n) * scale);
                float nn = Mathf.Lerp(Mathf.Lerp(n1, n2, u), Mathf.Lerp(n3, n4, u), v);
                float grain = (float)r.NextDouble() * 0.25f;
                t.SetPixel(x, y, Color.Lerp(a, b, Mathf.Clamp01(nn * 0.85f + grain)));
            }
        t.Apply();
        string p = GenDir + "/Tex_" + name + ".asset";
        AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(t, p);
        return t;
    }

    static Texture2D GrassBladeTex()
    {
        int w = 64, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var clear = new Color32[w * h];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(70, 140, 50, 0);
        t.SetPixels32(clear);
        var r = new System.Random(7);
        for (int b = 0; b < 9; b++)
        {
            float x0 = r.Next(6, w - 6);
            float lean = (float)(r.NextDouble() * 2 - 1) * 12f;
            float height = h * (0.55f + (float)r.NextDouble() * 0.45f);
            float width = 2.5f + (float)r.NextDouble() * 2f;
            for (int y = 0; y < height; y++)
            {
                float k = y / height;
                float cx = x0 + lean * k * k;
                float hw = width * (1f - k);
                for (int x = (int)(cx - hw - 1); x <= (int)(cx + hw + 1); x++)
                {
                    if (x < 0 || x >= w) continue;
                    float a = Mathf.Clamp01(hw - Mathf.Abs(x - cx) + 0.5f);
                    if (a <= 0f) continue;
                    var c = Color.Lerp(new Color(0.2f, 0.45f, 0.12f), new Color(0.6f, 0.88f, 0.38f), k);
                    c.a = Mathf.Max(t.GetPixel(x, y).a, a);
                    t.SetPixel(x, y, c);
                }
            }
        }
        t.Apply();
        string p = GenDir + "/Tex_GrassBlade.asset";
        AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(t, p);
        return t;
    }

    // ═════════════════ 植物 ═════════════════

    static GameObject MakePinePrefab()
    {
        var root = new GameObject("PineTree");
        var trunk = Mat("Trunk", new Color(0.36f, 0.24f, 0.14f));
        var leaf = Mat("PineLeaf", new Color(0.12f, 0.36f, 0.16f));
        var cone = ConeMesh();
        Prim(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0, 0.6f, 0), new Vector3(0.28f, 0.6f, 0.28f), trunk);
        MeshObj("Leaves1", root.transform, cone, leaf, new Vector3(0, 0.9f, 0), new Vector3(2.4f, 2.0f, 2.4f));
        MeshObj("Leaves2", root.transform, cone, leaf, new Vector3(0, 1.9f, 0), new Vector3(1.9f, 1.7f, 1.9f));
        MeshObj("Leaves3", root.transform, cone, leaf, new Vector3(0, 2.8f, 0), new Vector3(1.3f, 1.5f, 1.3f));
        return SavePrefab(root);
    }

    static GameObject MakeRoundTreePrefab()
    {
        var root = new GameObject("RoundTree");
        var trunk = Mat("Trunk", new Color(0.36f, 0.24f, 0.14f));
        var leaf = Mat("RoundLeaf", new Color(0.25f, 0.55f, 0.2f));
        Prim(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0, 0.9f, 0), new Vector3(0.32f, 0.9f, 0.32f), trunk);
        Prim(PrimitiveType.Sphere, "Leaves1", root.transform, new Vector3(0, 2.6f, 0), Vector3.one * 2.4f, leaf);
        Prim(PrimitiveType.Sphere, "Leaves2", root.transform, new Vector3(0.7f, 2.2f, 0.3f), Vector3.one * 1.6f, leaf);
        Prim(PrimitiveType.Sphere, "Leaves3", root.transform, new Vector3(-0.6f, 2.3f, -0.4f), Vector3.one * 1.7f, leaf);
        return SavePrefab(root);
    }

    static GameObject MakeBushPrefab()
    {
        var root = new GameObject("Bush");
        var leaf = Mat("Bush", new Color(0.22f, 0.5f, 0.18f));
        Prim(PrimitiveType.Sphere, "B1", root.transform, new Vector3(0, 0.35f, 0), new Vector3(1.1f, 0.8f, 1.0f), leaf);
        Prim(PrimitiveType.Sphere, "B2", root.transform, new Vector3(0.5f, 0.28f, 0.1f), new Vector3(0.8f, 0.6f, 0.8f), leaf);
        Prim(PrimitiveType.Sphere, "B3", root.transform, new Vector3(-0.45f, 0.25f, -0.1f), new Vector3(0.7f, 0.55f, 0.7f), leaf);
        return SavePrefab(root);
    }

    static void PlaceVegetation(Terrain terrain, GameObject pine, GameObject round, GameObject bush, System.Random rnd)
    {
        var parent = new GameObject("Vegetation").transform;
        var td = terrain.terrainData;
        int placed = 0, tries = 0;
        while (placed < 170 && tries < 5000)
        {
            tries++;
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float d = Mathf.Lerp(14f, 85f, Mathf.Sqrt((float)rnd.NextDouble()));
            var p = new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
            if (p.z < 8f && Mathf.Abs(p.x) < 22f) continue;   // 不擋住鏡頭與主角
            float nx = (p.x + TerrainSize / 2f) / TerrainSize, nz = (p.z + TerrainSize / 2f) / TerrainSize;
            if (td.GetSteepness(nx, nz) > 28f) continue;
            p.y = terrain.SampleHeight(p);
            if (p.y > 22f) continue;
            var prefab = rnd.NextDouble() < 0.55 ? pine : round;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.transform.position = p;
            g.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            g.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.6f, (float)rnd.NextDouble());
            placed++;
        }
        // 主角後方的灌木，增加畫面層次
        for (int i = 0; i < 14; i++)
        {
            var p = new Vector3(Mathf.Lerp(-15f, 15f, (float)rnd.NextDouble()), 0f, Mathf.Lerp(4.5f, 12f, (float)rnd.NextDouble()));
            p.y = terrain.SampleHeight(p);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(bush, parent);
            g.transform.position = p;
            g.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            g.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.4f, (float)rnd.NextDouble());
        }
    }

    // ═════════════════ 角色 ═════════════════

    static GameObject BuildTrainer()
    {
        GameObject root;
        var model = FindModel("Assets/Models/Trainer", true);
        TrainerThrowAnimator anim;
        if (model != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root = WrapAndNormalize(inst, "Trainer", TrainerHeight, false);
            anim = root.AddComponent<TrainerThrowAnimator>();
            anim.AutoSetup();
            if (anim.rightUpperArm == null)
                Debug.LogWarning("[HW5] 找不到訓練家模型的右手臂骨骼，請在 Trainer 的 TrainerThrowAnimator 手動指定 Right Upper Arm / Fore Arm / Hand。");
        }
        else
        {
            root = new GameObject("Trainer (Placeholder)");
            var skin = Mat("Skin", new Color(1f, 0.82f, 0.68f));
            var jacket = Mat("Jacket", new Color(0.15f, 0.35f, 0.75f));
            var pants = Mat("Pants", new Color(0.12f, 0.14f, 0.25f));
            var cap = Mat("Cap", new Color(0.85f, 0.15f, 0.15f));
            var t = root.transform;
            Prim(PrimitiveType.Cylinder, "LegL", t, new Vector3(-0.1f, 0.42f, 0), new Vector3(0.14f, 0.42f, 0.14f), pants);
            Prim(PrimitiveType.Cylinder, "LegR", t, new Vector3(0.1f, 0.42f, 0), new Vector3(0.14f, 0.42f, 0.14f), pants);
            Prim(PrimitiveType.Capsule, "Torso", t, new Vector3(0, 1.15f, 0), new Vector3(0.44f, 0.38f, 0.28f), jacket);
            Prim(PrimitiveType.Sphere, "Head", t, new Vector3(0, 1.7f, 0), Vector3.one * 0.28f, skin);
            Prim(PrimitiveType.Sphere, "Cap", t, new Vector3(0, 1.79f, 0), new Vector3(0.3f, 0.14f, 0.3f), cap);
            Prim(PrimitiveType.Cube, "CapBrim", t, new Vector3(0, 1.77f, 0.15f), new Vector3(0.22f, 0.025f, 0.14f), cap);

            anim = root.AddComponent<TrainerThrowAnimator>();
            BuildArm(t, "Right", 1f, jacket, skin, out anim.rightUpperArm, out anim.rightForeArm, out anim.rightHand);
            BuildArm(t, "Left", -1f, jacket, skin, out anim.leftUpperArm, out anim.leftForeArm, out _);
        }
        root.transform.position = TrainerPos;
        root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // 面向 +X（寶可夢）
        GroundByFeet(root);
        return root;
    }

    static void BuildArm(Transform body, string side, float sign, Material sleeve, Material skin,
                         out Transform upper, out Transform fore, out Transform hand)
    {
        upper = new GameObject(side + "UpperArm").transform;
        upper.SetParent(body, false);
        upper.localPosition = new Vector3(0.27f * sign, 1.47f, 0f);
        Prim(PrimitiveType.Capsule, "Mesh", upper, new Vector3(0, -0.15f, 0), new Vector3(0.11f, 0.17f, 0.11f), sleeve);

        fore = new GameObject(side + "ForeArm").transform;
        fore.SetParent(upper, false);
        fore.localPosition = new Vector3(0, -0.3f, 0);
        Prim(PrimitiveType.Capsule, "Mesh", fore, new Vector3(0, -0.14f, 0), new Vector3(0.09f, 0.15f, 0.09f), sleeve);

        hand = new GameObject(side + "Hand").transform;
        hand.SetParent(fore, false);
        hand.localPosition = new Vector3(0, -0.28f, 0);
        Prim(PrimitiveType.Sphere, "Mesh", hand, new Vector3(0, -0.04f, 0), Vector3.one * 0.1f, skin);
    }

    static GameObject BuildPokemon()
    {
        GameObject root;
        var model = FindModel("Assets/Models/Pokemon", false);
        if (model != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root = WrapAndNormalize(inst, "Pokemon", 0.9f, false);
        }
        else
        {
            // 通用替代模型（請換成你下載的寶可夢模型）
            root = new GameObject("Pokemon (Placeholder)");
            var body = Mat("CreatureBody", new Color(0.55f, 0.8f, 0.95f));
            var dark = Mat("CreatureEye", new Color(0.05f, 0.05f, 0.08f));
            var t = root.transform;
            Prim(PrimitiveType.Sphere, "Body", t, new Vector3(0, 0.38f, 0), new Vector3(0.75f, 0.7f, 0.7f), body);
            Prim(PrimitiveType.Sphere, "EyeL", t, new Vector3(-0.13f, 0.5f, 0.3f), Vector3.one * 0.08f, dark);
            Prim(PrimitiveType.Sphere, "EyeR", t, new Vector3(0.13f, 0.5f, 0.3f), Vector3.one * 0.08f, dark);
            Prim(PrimitiveType.Sphere, "FootL", t, new Vector3(-0.18f, 0.05f, 0.1f), new Vector3(0.18f, 0.1f, 0.25f), body);
            Prim(PrimitiveType.Sphere, "FootR", t, new Vector3(0.18f, 0.05f, 0.1f), new Vector3(0.18f, 0.1f, 0.25f), body);
        }
        root.transform.position = PokemonPos;
        root.transform.rotation = Quaternion.Euler(0f, -90f, 0f);  // 面向 -X（訓練家）
        root.AddComponent<WildPokemonIdle>();
        return root;
    }

    static GameObject BuildBall()
    {
        GameObject root;
        var model = FindModel("Assets/Models/Pokeball", false);
        if (model != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            FixMissingTextures(inst, "Assets/Models/Pokeball");
            root = WrapAndNormalize(inst, "Pokeball", BallSize, true);
        }
        else
        {
            root = new GameObject("Pokeball (Placeholder)");
            Prim(PrimitiveType.Sphere, "Ball", root.transform, Vector3.zero, Vector3.one * BallSize, Mat("BallPlaceholder", new Color(0.85f, 0.85f, 0.85f), 0.7f));
        }
        return root;
    }

    /// <summary>在資料夾裡找第一個模型（FBX/OBJ…）或 Prefab。</summary>
    static GameObject FindModel(string folder, bool makeHumanoid)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;
        var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        // 資料夾裡有多個模型時，優先用檔案最大（通常細節最多）的那個
        System.Array.Sort(guids, (a, b) => FileSize(b).CompareTo(FileSize(a)));
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi != null)
            {
                bool dirty = false;
                // 模型檔裡附帶的攝影機 / 燈光會搶走主攝影機畫面，一律不匯入
                if (mi.importCameras) { mi.importCameras = false; dirty = true; }
                if (mi.importLights) { mi.importLights = false; dirty = true; }
                if (makeHumanoid && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
                    && mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    dirty = true;
                }
                if (dirty) mi.SaveAndReimport();
                // 取出 FBX 內嵌貼圖，避免模型變成白色
                string texDir = folder + "/Textures";
                if (!AssetDatabase.IsValidFolder(texDir))
                {
                    EnsureFolder(texDir);
                    if (mi.ExtractTextures(texDir)) AssetDatabase.Refresh();
                    mi.SaveAndReimport();
                }
                go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Debug.Log("[HW5] 使用模型：" + path);
            return go;
        }
        return null;
    }

    /// <summary>模型材質沒有貼到圖時，從資料夾找 Diffuse / BaseColor 貼圖補上。</summary>
    static void FixMissingTextures(GameObject inst, string folder)
    {
        Texture2D diffuse = null, normal = null;
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (n.Contains("normal") || n.Contains("nrm") || n.EndsWith("_n")) { if (normal == null) normal = tex; }
            else if (n.Contains("diff") || n.Contains("albedo") || n.Contains("base") || n.Contains("color") || n.EndsWith("_a") || n.EndsWith("_d")) diffuse = tex;
            else if (diffuse == null && !(n.EndsWith("_m") || n.EndsWith("_r") || n.Contains("rough") || n.Contains("metal") || n.Contains("ao"))) diffuse = tex;
        }
        if (diffuse == null) return;
        if (normal != null)
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }
        int idx = 0;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src != null && src.mainTexture != null) continue;
                var m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.mainTexture = diffuse;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                if (normal != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                string path = GenDir + "/Mat_" + inst.name + "_" + (idx++) + ".mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// 蒙皮模型的 Renderer 外框常比實際身體大，會讓腳懸空。
    /// 改用腳趾 / 腳掌骨骼的最低點，把角色貼齊地面。
    /// </summary>
    static void GroundByFeet(GameObject root)
    {
        float minY = float.MaxValue;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("toe") || n.Contains("foot")) minY = Mathf.Min(minY, t.position.y);
        }
        if (minY == float.MaxValue) return;   // 替代模型沒有腳骨，維持原本的外框對齊
        float ground = root.transform.position.y;
        // 腳趾末端骨骼大約在鞋底上方 1~2 公分
        root.transform.position += Vector3.up * (ground - minY + 0.015f);
    }

    static long FileSize(string guid)
    {
        var p = AssetDatabase.GUIDToAssetPath(guid);
        return System.IO.File.Exists(p) ? new System.IO.FileInfo(p).Length : 0;
    }

    /// <summary>包一層空物件，縮放到目標尺寸並把樞紐放在腳底（或中心）。</summary>
    static GameObject WrapAndNormalize(GameObject inst, string name, float targetSize, bool pivotAtCenter)
    {
        var root = new GameObject(name);
        inst.transform.SetParent(root.transform, false);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        var b = GetModelBounds(inst);
        float size = pivotAtCenter ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : b.size.y;
        if (size > 1e-4f) inst.transform.localScale *= targetSize / size;
        b = GetModelBounds(inst);
        Vector3 anchor = pivotAtCenter ? b.center : new Vector3(b.center.x, b.min.y, b.center.z);
        inst.transform.position -= anchor;
        return root;
    }

    static Bounds GetModelBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // ═════════════════ 工具 ═════════════════

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    static Material Mat(string name, Color c, float smooth = 0.15f)
    {
        string p = GenDir + "/Mat_" + name + ".mat";
        if (matCache.TryGetValue(p, out var cached) && cached != null) return cached;
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader s = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p);
        matCache[p] = m;
        return m;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 lp, Vector3 ls, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = ls;
        var col = g.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material m, Vector3 lp, Vector3 ls)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = ls;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    static Mesh coneCache;

    static Mesh ConeMesh(int seg = 18)
    {
        if (coneCache != null) return coneCache;
        string p = GenDir + "/Mesh_Cone.asset";
        AssetDatabase.DeleteAsset(p);
        var v = new List<Vector3> { new Vector3(0, 1, 0) };
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f));
        }
        v.Add(Vector3.zero);
        int c = seg + 1;
        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            int a = 1 + i, b = 1 + (i + 1) % seg;
            tris.AddRange(new[] { 0, b, a });
            tris.AddRange(new[] { c, a, b });
        }
        var mesh = new Mesh { name = "Cone" };
        mesh.SetVertices(v);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, p);
        coneCache = mesh;
        return mesh;
    }

    static GameObject SavePrefab(GameObject go)
    {
        string p = GenDir + "/" + go.name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, p);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void AddSceneToBuild(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == path)) list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}

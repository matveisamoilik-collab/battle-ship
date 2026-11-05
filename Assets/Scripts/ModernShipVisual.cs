using System.Collections.Generic;
using UnityEngine;

// Builds the "shark" player ship at runtime: a modern grey metal warship (pointed-bow hull mesh,
// superstructure, mast with spinning radar, bow gun, hangar + helipad) with a shark picture
// painted on both sides of the hull. Everything is generated in code — no model or texture assets.
// Ship-local axes: +Z = bow (torpedo spawn at z = 7.5), hull spans z ≈ -6 … 6.3 like the box ships.
public static class ModernShipVisual
{
    public static readonly Color MetalColor = new Color(0.56f, 0.60f, 0.64f);
    static readonly Color LightMetal = new Color(0.68f, 0.71f, 0.74f);
    static readonly Color DarkMetal  = new Color(0.34f, 0.37f, 0.41f);
    static readonly Color DeckColor  = new Color(0.28f, 0.30f, 0.32f);
    static readonly Color GlassColor = new Color(0.05f, 0.08f, 0.12f);
    static readonly Color WhiteColor = new Color(0.90f, 0.91f, 0.92f);

    const float Metallic   = 0.55f;
    static readonly Color PadColor = new Color(0.12f, 0.12f, 0.13f);
    const float PadMetallic   = 0.2f;
    const float PadSmoothness = 0.3f;
    const float Smoothness = 0.55f;

    // Right half of the hull outline, stern → bow: (z, half-beam). Segment 1→2 is straight (shark picture goes there).
    static readonly Vector2[] Outline =
    {
        new Vector2(-6.0f, 1.85f),
        new Vector2(-3.0f, 2.10f),
        new Vector2( 3.0f, 2.05f),
        new Vector2( 4.6f, 1.40f),
        new Vector2( 5.6f, 0.65f),
        new Vector2( 6.3f, 0.00f),
    };
    const float KeelY        = -0.4f;
    const float KeelNarrow   = 0.7f;  // keel half-beam = deck half-beam × this (V-shaped hull)
    const float KeelTipZ     = 5.7f;  // raked bow: keel ends behind the deck tip

    static float DeckY(float z) => Mathf.Lerp(1.8f, 2.1f, Mathf.InverseLerp(-6f, 6.3f, z)); // sheer: bow sits higher

    // ---------------------------------------------------------------------------------------------
    public static GameObject Build(Transform shipRoot, Material template)
    {
        var root = new GameObject("SharkShipVisual");
        root.transform.SetParent(shipRoot, false);

        Material metal = Mat(template, MetalColor, Metallic, Smoothness);
        Material light = Mat(template, LightMetal, Metallic, Smoothness);
        Material dark  = Mat(template, DarkMetal,  Metallic, 0.4f);
        Material deck  = Mat(template, DeckColor,  0.2f, 0.25f);
        Material glass = Mat(template, GlassColor, 0.3f, 0.9f);
        Material white = Mat(template, WhiteColor, 0.1f, 0.4f);
        Material black = Mat(template, PadColor, PadMetallic, PadSmoothness);

        // Shark decal uses the same metallic/smoothness as the hull so its metal background blends in seamlessly
        Material shark = Mat(template, Color.white, Metallic, Smoothness);
        shark.SetTexture("_BaseMap", SharkTexture);
        shark.mainTexture = SharkTexture;

        BuildHull(root.transform, metal, deck, shark);

        float d;
        // Bow gun
        d = DeckY(3.7f);
        Part(root.transform, "GunBase",   PrimitiveType.Cylinder, new Vector3(0f, d + 0.15f, 3.7f), new Vector3(1.2f, 0.15f, 1.2f), light);
        Part(root.transform, "GunHouse",  PrimitiveType.Cube,     new Vector3(0f, d + 0.50f, 3.6f), new Vector3(0.95f, 0.5f, 1.2f), light);
        Part(root.transform, "GunBarrel", PrimitiveType.Cylinder, new Vector3(0f, d + 0.55f, 5.0f), new Vector3(0.14f, 0.8f, 0.14f), dark,
             new Vector3(90f, 0f, 0f));

        // Vertical launch cells + angled missile canisters
        d = DeckY(2.3f);
        Part(root.transform, "VLS", PrimitiveType.Cube, new Vector3(0f, d + 0.06f, 2.3f), new Vector3(1.6f, 0.12f, 1.1f), dark);
        d = DeckY(1.5f);
        Part(root.transform, "MissilesL", PrimitiveType.Cube, new Vector3(-0.95f, d + 0.3f, 1.5f), new Vector3(0.45f, 0.35f, 1.2f), dark, new Vector3(-15f, 0f, 0f));
        Part(root.transform, "MissilesR", PrimitiveType.Cube, new Vector3( 0.95f, d + 0.3f, 1.5f), new Vector3(0.45f, 0.35f, 1.2f), dark, new Vector3(-15f, 0f, 0f));

        // Superstructure + bridge
        d = DeckY(-0.3f);
        float blockTop  = d + 1.6f;
        float bridgeTop = blockTop + 0.9f;
        Part(root.transform, "Superstructure", PrimitiveType.Cube, new Vector3(0f, d + 0.8f, -0.3f), new Vector3(3.0f, 1.6f, 3.6f), light);
        Part(root.transform, "Bridge",         PrimitiveType.Cube, new Vector3(0f, blockTop + 0.45f, 0.5f), new Vector3(2.5f, 0.9f, 1.8f), light);
        Part(root.transform, "BridgeWindows",  PrimitiveType.Cube, new Vector3(0f, blockTop + 0.55f, 1.41f), new Vector3(2.3f, 0.22f, 0.04f), glass);

        // Mast, yardarm and spinning radar
        Part(root.transform, "Mast",    PrimitiveType.Cube,     new Vector3(0f, bridgeTop + 0.9f, 0.1f), new Vector3(0.55f, 1.8f, 0.55f), light);
        Part(root.transform, "MastTop", PrimitiveType.Cylinder, new Vector3(0f, bridgeTop + 2.3f, 0.1f), new Vector3(0.08f, 0.5f, 0.08f), dark);
        Part(root.transform, "Yardarm", PrimitiveType.Cube,     new Vector3(0f, bridgeTop + 1.3f, 0.1f), new Vector3(1.8f, 0.06f, 0.06f), dark);
        var radarPivot = new GameObject("RadarPivot");
        radarPivot.transform.SetParent(root.transform, false);
        radarPivot.transform.localPosition = new Vector3(0f, bridgeTop + 1.9f, 0.1f);
        radarPivot.AddComponent<RadarSpin>();
        Part(radarPivot.transform, "Radar", PrimitiveType.Cube, new Vector3(0f, 0f, 0.12f), new Vector3(1.6f, 0.3f, 0.1f), dark);

        // Funnel
        Part(root.transform, "Funnel",    PrimitiveType.Cube, new Vector3(0f, blockTop + 0.55f, -1.5f), new Vector3(1.3f, 1.1f, 1.2f), metal);
        Part(root.transform, "FunnelCap", PrimitiveType.Cube, new Vector3(0f, blockTop + 1.15f, -1.5f), new Vector3(1.32f, 0.1f, 1.22f), black);

        // Hangar with close-in weapon dome
        d = DeckY(-2.8f);
        float hangarTop = d + 1.2f;
        Part(root.transform, "Hangar",     PrimitiveType.Cube,     new Vector3(0f, d + 0.6f, -2.8f), new Vector3(2.6f, 1.2f, 1.8f), light);
        Part(root.transform, "HangarDoor", PrimitiveType.Cube,     new Vector3(0f, d + 0.5f, -3.71f), new Vector3(1.8f, 0.9f, 0.04f), dark);
        Part(root.transform, "CIWSBase",   PrimitiveType.Cylinder, new Vector3(0f, hangarTop + 0.2f, -2.6f), new Vector3(0.5f, 0.2f, 0.5f), white);
        Part(root.transform, "CIWSDome",   PrimitiveType.Sphere,   new Vector3(0f, hangarTop + 0.55f, -2.6f), new Vector3(0.5f, 0.5f, 0.5f), white);

        // Helipad with a 3D shark statue on it (head to starboard, so the chase camera sees it side-on)
        d = DeckY(-4.9f);
        Part(root.transform, "Helipad", PrimitiveType.Cube, new Vector3(0f, d + 0.02f, -4.9f), new Vector3(3.2f, 0.04f, 2.0f), black);
        BuildHelipadShark(root.transform, template, new Vector3(0f, d + 0.04f, -4.9f), dark);

        return root;
    }

    static Material Mat(Material template, Color c, float metallic, float smoothness)
    {
        var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat,
                           Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>()); // visual only — the hidden Hull BoxCollider takes torpedo hits
        go.transform.SetParent(parent, false);
        go.transform.localPosition    = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale       = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ---------------------------------------------------------------------------------------------
    // 3D shark statue for the helipad. Shark-local: +X = nose, +Y = up, origin = pad surface centre.
    const float SharkLen = 2.6f;
    const int   SharkRings = 28, SharkSegs = 18;

    static Vector3 SharkSpine(float t) =>
        new Vector3((t - 0.5f) * SharkLen, 0.62f + 0.10f * Mathf.Sin(Mathf.PI * t), 0.06f * Mathf.Sin(2f * Mathf.PI * t));

    static float SharkRadius(float t) => 0.85f * t * Mathf.Pow(Mathf.Max(0f, 1f - t), 0.55f); // max ≈ 0.31, thick near the head

    // Point on the body surface: h = height around the body (-1 belly … +1 back), side = +1 (+Z) or -1 (-Z)
    static Vector3 SharkSurface(float t, float h, float side)
    {
        float r = SharkRadius(t);
        return SharkSpine(t) + new Vector3(0f, h * r, side * Mathf.Sqrt(Mathf.Max(0f, 1f - h * h)) * r * 0.8f);
    }

    static void BuildHelipadShark(Transform parent, Material template, Vector3 localPos, Material standMat)
    {
        var root = new GameObject("HelipadShark3D");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;

        Material skin = Mat(template, Color.white, 0.1f, 0.7f);
        skin.SetTexture("_BaseMap", SharkSkinTexture);
        skin.mainTexture = SharkSkinTexture;
        Material finMat = Mat(template, Color.white, 0.1f, 0.6f);
        finMat.SetTexture("_BaseMap", SharkFinTexture);
        finMat.mainTexture = SharkFinTexture;
        Material iris  = Mat(template, EyeIris, 0.0f, 0.8f);
        Material pupil = Mat(template, Color.black, 0.0f, 0.9f);

        // Body: lofted rings with smooth (shared-vertex) normals
        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();
        for (int i = 0; i <= SharkRings; i++)
        {
            float t = (float)i / SharkRings;
            float r = SharkRadius(t);
            for (int j = 0; j <= SharkSegs; j++)
            {
                float a = (float)j / SharkSegs * Mathf.PI * 2f;  // 0 = belly, π/2 = +Z side, π = back
                verts.Add(SharkSpine(t) + new Vector3(0f, -Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f));
                uvs.Add(new Vector2(t, (float)j / SharkSegs));
            }
        }
        for (int i = 0; i < SharkRings; i++)
        for (int j = 0; j < SharkSegs; j++)
        {
            int a = i * (SharkSegs + 1) + j, b = a + 1, c = a + SharkSegs + 1, e = c + 1;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(e);
        }
        var body = new Mesh();
        body.SetVertices(verts);
        body.SetUVs(0, uvs);
        body.SetTriangles(tris, 0);
        body.RecalculateNormals();
        body.RecalculateBounds();
        MeshObject(root.transform, "Body", body, skin);

        // Fins (thin wedges; UV v = 0 at the base → 1 at the tip, so tips turn orange)
        var fins = new MeshBuilder();
        Vector3 up = Vector3.up;
        // Dorsal — swept back
        Fin(fins, SharkSurface(0.62f, 0.9f, 1f) - up * 0.04f, SharkSurface(0.45f, 0.9f, 1f) - up * 0.04f,
            SharkSurface(0.44f, 1f, 1f) + new Vector3(-0.15f, 0.55f, 0f), 0.045f);
        // Second dorsal + anal fin near the tail
        Fin(fins, SharkSurface(0.25f, 0.9f, 1f) - up * 0.02f, SharkSurface(0.17f, 0.9f, 1f) - up * 0.02f,
            SharkSurface(0.16f, 1f, 1f) + new Vector3(-0.06f, 0.16f, 0f), 0.03f);
        Fin(fins, SharkSurface(0.27f, -0.9f, 1f) + up * 0.02f, SharkSurface(0.19f, -0.9f, 1f) + up * 0.02f,
            SharkSurface(0.18f, -1f, 1f) + new Vector3(-0.06f, -0.14f, 0f), 0.03f);
        // Tail — upper (bigger) and lower lobes
        Vector3 tail = SharkSpine(0f);
        Fin(fins, SharkSpine(0.08f) + up * 0.05f, tail + new Vector3(0.02f, -0.03f, 0f), tail + new Vector3(-0.38f, 0.62f, 0f), 0.04f);
        Fin(fins, SharkSpine(0.07f) - up * 0.04f, tail + new Vector3(0.02f, 0.03f, 0f), tail + new Vector3(-0.26f, -0.36f, 0f), 0.04f);
        // Pectoral fins on both sides, angled down and out
        for (int s = -1; s <= 1; s += 2)
            Fin(fins, SharkSurface(0.74f, -0.45f, s), SharkSurface(0.63f, -0.45f, s),
                SharkSurface(0.6f, -0.45f, s) + new Vector3(-0.28f, -0.30f, s * 0.42f), 0.035f);
        MeshObject(root.transform, "Fins", fins.ToMesh(), finMat);

        // Eyes — yellow iris with a black pupil on each side
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 eye = SharkSurface(0.9f, 0.3f, s) + new Vector3(0f, 0f, s * 0.01f);
            Part(root.transform, "Eye",   PrimitiveType.Sphere, eye, Vector3.one * 0.075f, iris);
            Part(root.transform, "Pupil", PrimitiveType.Sphere, eye + new Vector3(0.008f, 0.004f, s * 0.026f), Vector3.one * 0.04f, pupil);
        }

        // Display stand: base disc + pole up to the belly
        float poleX  = SharkSpine(0.55f).x;
        float bellyY = SharkSurface(0.55f, -1f, 1f).y;
        Part(root.transform, "StandBase", PrimitiveType.Cylinder, new Vector3(poleX, 0.03f, 0f), new Vector3(0.7f, 0.03f, 0.7f), standMat);
        Part(root.transform, "StandPole", PrimitiveType.Cylinder, new Vector3(poleX, bellyY * 0.5f + 0.03f, 0f),
             new Vector3(0.1f, bellyY * 0.5f, 0.1f), standMat);
    }

    // Thin triangular fin with thickness: two faces + three edge strips
    static void Fin(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 tip, float thickness)
    {
        Vector3 n = Vector3.Cross(b - a, tip - a).normalized * (thickness * 0.5f);
        Vector3 centre = (a + b + tip) / 3f;
        Vector2 ub = new Vector2(0.5f, 0f), ut = new Vector2(0.5f, 1f);

        mb.Tri(a + n, b + n, tip + n,  n, ub, ub, ut);
        mb.Tri(a - n, b - n, tip - n, -n, ub, ub, ut);
        mb.Quad(a + n, b + n, b - n, a - n, (a + b) * 0.5f - centre, ub, ub, ub, ub);
        mb.Quad(b + n, tip + n, tip - n, b - n, (b + tip) * 0.5f - centre, ub, ut, ut, ub);
        mb.Quad(tip + n, a + n, a - n, tip - n, (tip + a) * 0.5f - centre, ut, ub, ub, ut);
    }

    // Body skin: u = nose-to-tail position t, v = angle around the body (0 = belly, 0.5 = back)
    static Texture2D s_skinTex, s_finTex;
    static Texture2D SharkSkinTexture
    {
        get
        {
            if (s_skinTex != null) return s_skinTex;
            const int W = 256, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            tex.wrapModeU  = TextureWrapMode.Clamp;
            tex.wrapModeV  = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float t   = (x + 0.5f) / W;
                float ang = (y + 0.5f) / H * Mathf.PI * 2f;
                px[y * W + x] = SkinColor(t, -Mathf.Cos(ang), Mathf.Sin(ang));
            }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return s_skinTex = tex;
        }
    }

    static Color SkinColor(float t, float h, float side)
    {
        // Open red mouth with white teeth on the lower front
        if (t > 0.83f && t < 0.985f)
        {
            float k    = Mathf.InverseLerp(0.83f, 0.985f, t);
            float hTop = Mathf.Lerp(-0.12f, -0.05f, k);
            float hBot = Mathf.Lerp(-0.12f, -0.70f, k);
            if (h < hTop && h > hBot)
            {
                float tooth = 0.09f * (1f - Mathf.Abs(Mathf.Repeat(t * 70f, 1f) * 2f - 1f));
                return hTop - h < tooth ? Color.white : SharkMouth;
            }
        }
        // Gill slits on both flanks
        if (Mathf.Abs(h) < 0.35f && Mathf.Abs(side) > 0.3f)
            for (int g = 0; g < 3; g++)
                if (Mathf.Abs(t - (0.735f + g * 0.025f + h * 0.01f)) < 0.004f) return SharkOutline;

        float bellyLine = -0.3f + 0.04f * Mathf.Sin(t * 20f);
        if (h < bellyLine) return SharkBelly;
        if (h < bellyLine + 0.12f) return SharkStripe;
        return Color.Lerp(SharkFlank, SharkBack, Mathf.Pow(Mathf.InverseLerp(bellyLine + 0.12f, 0.9f, h), 0.7f));
    }

    static Texture2D SharkFinTexture
    {
        get
        {
            if (s_finTex != null) return s_finTex;
            const int W = 4, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                Color c = Color.Lerp(SharkBack, FinTip, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, (y + 0.5f) / H)));
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return s_finTex = tex;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Hull mesh
    static void BuildHull(Transform parent, Material hullMat, Material deckMat, Material sharkMat)
    {
        int n = Outline.Length;
        var dR = new Vector3[n];
        var kR = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float z = Outline[i].x, w = Outline[i].y;
            dR[i] = new Vector3(w, DeckY(z), z);
            kR[i] = new Vector3(w * KeelNarrow, KeelY, i == n - 1 ? KeelTipZ : z);
        }

        // Sides, transom and bottom
        var hull = new MeshBuilder();
        for (int i = 0; i < n - 1; i++)
        {
            hull.Quad(dR[i], dR[i + 1], kR[i + 1], kR[i], Vector3.right);
            hull.Quad(L(dR[i]), L(dR[i + 1]), L(kR[i + 1]), L(kR[i]), Vector3.left);
        }
        hull.Quad(dR[0], L(dR[0]), L(kR[0]), kR[0], Vector3.back);
        hull.Fan(Ring(kR), Vector3.down);
        MeshObject(parent, "HullMesh", hull.ToMesh(), hullMat);

        // Deck
        var deck = new MeshBuilder();
        deck.Fan(Ring(dR), Vector3.up);
        MeshObject(parent, "DeckMesh", deck.ToMesh(), deckMat);

        // Shark picture on both sides (straight segment 1→2); u = 0 at stern end, 1 at bow end, so the shark faces the bow on each side
        var decal = new MeshBuilder();
        AddSideDecal(decal, dR[1], dR[2], kR[1], kR[2], 1f);
        AddSideDecal(decal, L(dR[1]), L(dR[2]), L(kR[1]), L(kR[2]), -1f);
        MeshObject(parent, "SharkDecal", decal.ToMesh(), sharkMat);
    }

    static void AddSideDecal(MeshBuilder mb, Vector3 d1, Vector3 d2, Vector3 k1, Vector3 k2, float side)
    {
        const float s0 = 0.03f, s1 = 0.99f;   // almost the whole straight segment (z ≈ -2.8 … 2.9, ~5.8 units)
        const float yBottom = 0.0f, topGap = 0.06f; // waterline up to just under the deck edge (~1.9 units)

        Vector3 P(float s, bool top)
        {
            Vector3 deckPt = Vector3.Lerp(d1, d2, s);
            Vector3 keelPt = Vector3.Lerp(k1, k2, s);
            float y = top ? deckPt.y - topGap : yBottom;
            float h = Mathf.InverseLerp(keelPt.y, deckPt.y, y);
            return Vector3.Lerp(keelPt, deckPt, h);
        }

        Vector3 a = P(s0, false), b = P(s1, false), c = P(s1, true), e = P(s0, true);
        Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
        if (normal.x * side < 0f) normal = -normal;
        Vector3 off = normal * 0.02f; // lift off the hull to avoid z-fighting

        mb.Quad(a + off, b + off, c + off, e + off, Vector3.right * side,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
    }

    static Vector3 L(Vector3 p) => new Vector3(-p.x, p.y, p.z);

    // Closed polygon: right side stern→bow tip, then left side bow→stern
    static List<Vector3> Ring(Vector3[] right)
    {
        var ring = new List<Vector3>(right);
        for (int i = right.Length - 2; i >= 0; i--) ring.Add(L(right[i]));
        return ring;
    }

    static void MeshObject(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    class MeshBuilder
    {
        readonly List<Vector3> v  = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>     t  = new List<int>();

        // Winding is chosen so the face normal points along 'outward' (Unity front faces are clockwise)
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward,
                        Vector2 ua = default, Vector2 ub = default, Vector2 uc = default)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                (b, c)   = (c, b);
                (ub, uc) = (uc, ub);
            }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward,
                         Vector2 ua = default, Vector2 ub = default, Vector2 uc = default, Vector2 ud = default)
        {
            Tri(a, b, c, outward, ua, ub, uc);
            Tri(a, c, d, outward, ua, uc, ud);
        }

        public void Fan(List<Vector3> ring, Vector3 outward)
        {
            Vector3 centre = Vector3.zero;
            foreach (var p in ring) centre += p;
            centre /= ring.Count;
            for (int i = 0; i < ring.Count; i++)
                Tri(centre, ring[i], ring[(i + 1) % ring.Count], outward);
        }

        public Mesh ToMesh()
        {
            var m = new Mesh();
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals(); // vertices aren't shared → flat-shaded panels
            m.RecalculateBounds();
            return m;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Shark picture — drawn procedurally once and cached. Head points to u = 1 (right).
    // Background is the hull metal colour so the decal edges are invisible on the hull.
    const int   TexW = 384, TexH = 128;
    const float Aspect = (float)TexW / TexH;   // 3.0 — matches the decal's world-space proportions (5.8 × 1.9)
    // Zoom: the texture shows this much of the design space vertically, so the shark (≈ 0.6 tall in
    // design units, centred at v ≈ 0.55) fills ~85 % of the picture height.
    const float Zoom    = 0.7f;
    const float CentreV = 0.55f;

    static Texture2D s_sharkTex;
    public static Texture2D SharkTexture
    {
        get
        {
            if (s_sharkTex == null) s_sharkTex = DrawShark(MetalColor, SharkOutline, true);
            return s_sharkTex;
        }
    }

    // Colourful palette: royal-blue back fading to bright cyan flanks, white belly with an orange racing
    // stripe, orange fin tips, yellow eye, red mouth — jumping out of cyan waves with white foam.
    static readonly Color SharkBack    = new Color(0.04f, 0.22f, 0.75f);
    static readonly Color SharkFlank   = new Color(0.10f, 0.72f, 0.98f);
    static readonly Color SharkBelly   = new Color(0.98f, 0.98f, 1.00f);
    static readonly Color SharkStripe  = new Color(1.00f, 0.55f, 0.05f);
    static readonly Color FinTip       = new Color(1.00f, 0.40f, 0.05f);
    static readonly Color SharkOutline = new Color(0.03f, 0.06f, 0.22f);
    static readonly Color SharkMouth   = new Color(0.92f, 0.08f, 0.15f);
    static readonly Color EyeIris      = new Color(1.00f, 0.85f, 0.05f);
    static readonly Color WaveLight    = new Color(0.08f, 0.70f, 0.95f);
    static readonly Color WaveDeep     = new Color(0.02f, 0.32f, 0.72f);
    const float C = 0.5f; // body centreline (v)

    static Texture2D DrawShark(Color background, Color outline, bool waves)
    {
        var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, true);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        var px = new Color[TexW * TexH];
        float[] sub = { 0.25f, 0.75f };

        for (int y = 0; y < TexH; y++)
        for (int x = 0; x < TexW; x++)
        {
            Color acc = Color.clear;
            foreach (float sx in sub)
            foreach (float sy in sub)
            {
                // Design space: u ∈ [0,2] spans the shark nose-to-tail; zoomed in so it fills the picture
                float u = ((x + sx) / TexW - 0.5f) * Aspect * Zoom + 1f;
                float v = ((y + sy) / TexH - 0.5f) * Zoom + CentreV;
                acc += SampleShark(u, v, background, outline, waves);
            }
            px[y * TexW + x] = acc / 4f;
        }

        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    static Color SampleShark(float u, float v, Color background, Color outline, bool waves)
    {
        if (!InSilhouette(u, v))
        {
            const float o = 0.016f;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI * 0.25f;
                if (InSilhouette(u + Mathf.Cos(ang) * o, v + Mathf.Sin(ang) * o)) return outline;
            }
            return waves ? Background(u, v, background) : background;
        }

        // Eye: yellow iris, black pupil, white glint
        float eyeSq = Sq(u - 1.66f) + Sq(v - (C + 0.045f));
        if (eyeSq < Sq(0.032f))
        {
            if (Sq(u - 1.672f) + Sq(v - (C + 0.055f)) < Sq(0.008f)) return Color.white;
            if (eyeSq < Sq(0.017f)) return Color.black;
            return eyeSq < Sq(0.027f) ? EyeIris : SharkOutline;
        }

        // Mouth with teeth
        Vector2 m0 = new Vector2(1.56f, C - 0.06f), m1 = new Vector2(1.84f, C - 0.045f), m2 = new Vector2(1.68f, C - 0.12f);
        if (InTri(u, v, m0, m1, m2))
        {
            float topLine = Mathf.Lerp(m0.y, m1.y, Mathf.InverseLerp(m0.x, m1.x, u));
            float tooth   = 0.03f * (1f - Mathf.Abs(Mathf.Repeat(u * 30f, 1f) * 2f - 1f));
            return topLine - v < tooth ? Color.white : SharkMouth;
        }

        if (InBody(u, v))
        {
            // Gill slits
            if (Mathf.Abs(v - C) < 0.065f)
                for (int g = 0; g < 3; g++)
                    if (Mathf.Abs(u - (1.44f + g * 0.05f + (v - C) * 0.2f)) < 0.008f) return SharkOutline;

            float h         = BodyHalf(u);
            float bellyLine = C - h * 0.2f + 0.012f * Mathf.Sin(u * 9f);
            if (v < bellyLine) return SharkBelly;
            if (v < bellyLine + 0.014f) return SharkStripe;                     // orange racing stripe
            float t = Mathf.InverseLerp(bellyLine, C + h, v);
            return Color.Lerp(SharkFlank, SharkBack, Mathf.Pow(t, 0.7f));       // cyan flank → royal-blue back
        }

        // Fins: blue at the base, orange towards the tips
        float outside = Mathf.Abs(v - C) - BodyHalf(u);
        return Color.Lerp(SharkBack, FinTip, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.2f, outside)));
    }

    // Behind the shark: cyan waves with a white foam crest along the bottom; plain background above
    static Color Background(float u, float v, Color background)
    {
        float crest = 0.29f + 0.022f * Mathf.Sin(u * 11f) + 0.010f * Mathf.Sin(u * 23f + 1f);
        if (v > crest) return background;
        if (v > crest - 0.014f) return Color.white;
        return Color.Lerp(WaveDeep, WaveLight, Mathf.InverseLerp(crest - 0.09f, crest - 0.014f, v));
    }

    static float BodyHalf(float u)
    {
        float t = Mathf.InverseLerp(0.24f, 1.86f, u);
        if (t <= 0f || t >= 1f) return 0f;
        return 0.44f * t * Mathf.Pow(1f - t, 0.55f); // thick near the head, tapering to the tail
    }

    static bool InBody(float u, float v) => Mathf.Abs(v - C) < BodyHalf(u);

    static bool InSilhouette(float u, float v)
    {
        if (InBody(u, v)) return true;
        // Dorsal fin
        if (InTri(u, v, new Vector2(1.20f, C + 0.10f), new Vector2(0.94f, C + 0.11f), new Vector2(0.88f, C + 0.36f))) return true;
        // Pectoral fin
        if (InTri(u, v, new Vector2(1.34f, C - 0.07f), new Vector2(1.12f, C - 0.07f), new Vector2(1.00f, C - 0.28f))) return true;
        // Tail — upper and lower lobes
        if (InTri(u, v, new Vector2(0.36f, C + 0.03f), new Vector2(0.08f, C + 0.36f), new Vector2(0.22f, C - 0.02f))) return true;
        if (InTri(u, v, new Vector2(0.32f, C - 0.02f), new Vector2(0.12f, C - 0.24f), new Vector2(0.22f, C + 0.03f))) return true;
        // Small second dorsal + anal fins near the tail
        if (InTri(u, v, new Vector2(0.56f, C + 0.04f), new Vector2(0.46f, C + 0.04f), new Vector2(0.44f, C + 0.12f))) return true;
        if (InTri(u, v, new Vector2(0.60f, C - 0.04f), new Vector2(0.50f, C - 0.04f), new Vector2(0.47f, C - 0.11f))) return true;
        return false;
    }

    static float Sq(float x) => x * x;

    static bool InTri(float u, float v, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Edge(u, v, a, b), d2 = Edge(u, v, b, c), d3 = Edge(u, v, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    static float Edge(float u, float v, Vector2 a, Vector2 b) => (u - b.x) * (a.y - b.y) - (a.x - b.x) * (v - b.y);
}

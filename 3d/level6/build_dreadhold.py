"""
Level 6 "Dreadhold" model generator (Blender 4.5, headless).

Run from the project root:
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" -b --factory-startup --python 3d/level6/build_dreadhold.py

Builds every Level 6 model procedurally (seeded, so the output is reproducible), saves
3d/level6/dreadhold.blend and exports five FBX files next to this script:
  DreadholdIsland.fbx  - rocky island with an overhanging rim (+ bushes, waterline boulders)
  DreadholdCastle.fbx  - keep, towers, spire, hexagonal curtain wall with 6 edge towers (one cannon each)
  DreadholdPillar.fbx  - Base, Drum_0..4, Drum_0..4_Damaged, Capital, Chunk, GlowRing, Marker
  DreadholdCannon.fbx  - Carriage + Barrel (pivot at the trunnion, points +Z in Unity)
  DreadholdBoat.fbx    - small enemy gunboat, 8 units long, bow +Z in Unity
  DreadholdSeagull.fbx - Body, WingL/WingR (pivot at the shoulder), WingTipL/WingTipR (pivot at
                         the wrist); beak +Z, wings along X. Seagull.cs flaps the wings in code.

All geometry is written in UNITY coordinates (Y up, +Z forward, metres = Unity units) and
converted with U() to Blender space. Unity imports the FBX with bakeAxisConversion = true.
Layout numbers (radii, heights, angles) must match the constants in GameSetup.BuildDreadhold().
Every FBX must stay under 2 MB - the script fails loudly if one does not.
"""

import bpy
import bmesh
import math
import os
import random
import sys

try:
    OUT_DIR = os.path.dirname(os.path.realpath(__file__))
except NameError:
    OUT_DIR = os.path.join(os.getcwd(), "3d", "level6")

MAX_BYTES = 2 * 1024 * 1024

# ---------------------------------------------------------------------------
# Shared layout constants (keep in sync with GameSetup.cs)
# ---------------------------------------------------------------------------
PILLAR_ANGLES   = (180.0, 60.0, 300.0)   # degrees, measured from +Z toward +X
PILLAR_RADIUS   = 26.5
CANNON_ANGLES   = (0.0, 60.0, 120.0, 180.0, 240.0, 300.0)   # 6 edge towers at the hex wall vertices
WALL_RADIUS     = 13.5                    # castle hex wall / turret ring radius
TURRET_TOP      = 6.3                     # edge tower top = cannon mount height (castle local)
PLATEAU_Y       = 10.0                    # castle base height on the island
PLATEAU_RADIUS  = 16.0

MAT_COLORS = {
    "L6_Rock":        (0.36, 0.33, 0.30),
    "L6_Grass":       (0.33, 0.50, 0.18),
    "L6_CastleStone": (0.72, 0.68, 0.60),
    "L6_Roof":        (0.22, 0.25, 0.36),
    "L6_Window":      (0.06, 0.05, 0.05),
    "L6_Wood":        (0.40, 0.25, 0.12),
    "L6_PillarStone": (0.82, 0.74, 0.56),
    "L6_Rune":        (1.00, 0.55, 0.10),
    "L6_Iron":        (0.12, 0.12, 0.13),
    "L6_BoatHull":    (0.35, 0.12, 0.10),
    "L6_BoatDeck":    (0.55, 0.40, 0.22),
    "L6_Flag":        (0.08, 0.08, 0.08),
    "L6_GullWhite":   (0.95, 0.95, 0.93),
    "L6_GullGrey":    (0.58, 0.62, 0.68),
    "L6_GullBlack":   (0.05, 0.05, 0.06),
    "L6_GullBeak":    (0.98, 0.78, 0.12),
    "L6_GullLeg":     (0.95, 0.55, 0.35),
    "L6_GullRed":     (0.85, 0.12, 0.08),
}


def U(x, y, z):
    """Unity (x, y, z) -> Blender (x, y, z) for an FBX exported -Z forward / Y up."""
    return (-x, -z, y)


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        c = MAT_COLORS[name]
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (c[0], c[1], c[2], 1.0)
            bsdf.inputs["Roughness"].default_value = 0.9
        m.diffuse_color = (c[0], c[1], c[2], 1.0)
    return m


# ---------------------------------------------------------------------------
# Mesh builder (Unity coordinates)
# ---------------------------------------------------------------------------
class MB:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.fmats = []
        self.mats = []

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def v(self, p):
        self.verts.append(tuple(p))
        return len(self.verts) - 1

    def f(self, idx, mat):
        self.faces.append(tuple(idx))
        self.fmats.append(self.mat(mat))

    # --- primitives -------------------------------------------------------
    def box(self, c, size, mat, yaw=0.0, pitch=0.0):
        """Axis box centred at c, size (sx, sy, sz); rotated yaw (deg, about Y), then pitch about local X."""
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        cy, sy_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
        ids = []
        for dx, dy, dz in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                           (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)):
            x, y, z = dx * sx, dy * sy, dz * sz
            y, z = y * cp - z * sp, y * sp + z * cp          # pitch about X
            x, z = x * cy + z * sy_, -x * sy_ + z * cy        # yaw about Y
            ids.append(self.v((c[0] + x, c[1] + y, c[2] + z)))
        a, b, cc, d, e, f_, g, h = ids
        for q in ((a, b, cc, d), (e, h, g, f_), (a, e, f_, b), (b, f_, g, cc), (cc, g, h, d), (d, h, e, a)):
            self.f(q, mat)

    def tube(self, p0, p1, r0, r1, seg, mat, rmul=None, cap0=True, cap1=True, phase=0.0):
        """Cylinder / truncated cone from p0 to p1. r1 == 0 makes a pointed cone."""
        ax = [p1[i] - p0[i] for i in range(3)]
        ln = math.sqrt(sum(a * a for a in ax)) or 1e-6
        ax = [a / ln for a in ax]
        ref = (0.0, 1.0, 0.0) if abs(ax[1]) < 0.9 else (1.0, 0.0, 0.0)
        u = [ax[1] * ref[2] - ax[2] * ref[1], ax[2] * ref[0] - ax[0] * ref[2], ax[0] * ref[1] - ax[1] * ref[0]]
        ul = math.sqrt(sum(a * a for a in u))
        u = [a / ul for a in u]
        w = [ax[1] * u[2] - ax[2] * u[1], ax[2] * u[0] - ax[0] * u[2], ax[0] * u[1] - ax[1] * u[0]]

        def ring(p, r):
            out = []
            for j in range(seg):
                a = 2 * math.pi * j / seg + phase
                m = rmul[j] if rmul else 1.0
                out.append(self.v([p[i] + (u[i] * math.cos(a) + w[i] * math.sin(a)) * r * m for i in range(3)]))
            return out

        b = ring(p0, r0)
        if r1 <= 1e-6:
            tip = self.v(p1)
            for j in range(seg):
                self.f((b[j], b[(j + 1) % seg], tip), mat)
        else:
            t = ring(p1, r1)
            for j in range(seg):
                k = (j + 1) % seg
                self.f((b[j], b[k], t[k], t[j]), mat)
            if cap1:
                self.f(tuple(t), mat)
        if cap0:
            self.f(tuple(reversed(b)), mat)

    def annulus(self, y0, y1, rin, rout, seg, mat):
        ib, ob, it, ot = [], [], [], []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            s, c = math.sin(a), math.cos(a)
            ib.append(self.v((s * rin, y0, c * rin)))
            ob.append(self.v((s * rout, y0, c * rout)))
            it.append(self.v((s * rin, y1, c * rin)))
            ot.append(self.v((s * rout, y1, c * rout)))
        for j in range(seg):
            k = (j + 1) % seg
            self.f((it[j], ot[j], ot[k], it[k]), mat)   # top
            self.f((ib[j], ib[k], ob[k], ob[j]), mat)   # bottom
            self.f((ob[j], ob[k], ot[k], ot[j]), mat)   # outer
            self.f((ib[j], it[j], it[k], ib[k]), mat)   # inner

    def rock(self, c, radius, mat, rng, squash=0.7, subdiv=1):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
        base = len(self.verts)
        jitter = {}
        for vert in bm.verts:
            k = rng.uniform(0.75, 1.2)
            x, y, z = vert.co.x * k, vert.co.z * k * squash, vert.co.y * k
            self.v((c[0] + x * radius, c[1] + y * radius, c[2] + z * radius))
        for face in bm.faces:
            self.f([base + vv.index for vv in face.verts], mat)
        bm.free()

    # --- finish -------------------------------------------------------------
    def build(self, name, origin=(0.0, 0.0, 0.0)):
        bverts = [U(p[0] - origin[0], p[1] - origin[1], p[2] - origin[2]) for p in self.verts]
        me = bpy.data.meshes.new(name)
        me.from_pydata(bverts, [], self.faces)
        me.update()
        for mname in self.mats:
            me.materials.append(get_mat(mname))
        for i, poly in enumerate(me.polygons):
            poly.material_index = self.fmats[i]
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new(name, me)
        obj.location = U(*origin)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def ang_dist(a, b):
    d = abs((a - b + 180.0) % 360.0 - 180.0)
    return d


# ---------------------------------------------------------------------------
# Island
# ---------------------------------------------------------------------------
ISLAND_PROFILE = [  # (y, radius) from below the waterline up to the castle plateau
    (-3.0, 21.5), (0.0, 22.5), (2.5, 23.2), (4.5, 24.3), (5.8, 25.6), (6.6, 26.4),
    (7.3, 26.0), (7.8, 23.5), (8.6, 20.5), (9.5, 17.8), (PLATEAU_Y, PLATEAU_RADIUS),
]
CORBEL = {4.5: 0.8, 5.8: 2.4, 6.6: 3.2, 7.3: 2.8}  # extra rim reach over each pillar
RIM_RING = 5  # index of (6.6, 26.4)


def island_noise(a, y, rng_val):
    return (0.045 * math.sin(3 * a + 1.3) + 0.035 * math.sin(7 * a + y * 0.8)
            + 0.025 * math.sin(13 * a + 2.1 + y * 1.7) + rng_val)


def surface_height(r):
    """Approximate island top height at radius r (top profile, ignoring noise)."""
    top = ISLAND_PROFILE[RIM_RING:]
    if r <= PLATEAU_RADIUS:
        return PLATEAU_Y
    pts = sorted([(rr, yy) for yy, rr in top])
    for (ra, ya), (rb, yb) in zip(pts, pts[1:]):
        if ra <= r <= rb:
            t = (r - ra) / (rb - ra)
            return ya + (yb - ya) * t
    return pts[-1][1]


def build_island():
    rng = random.Random(6006)
    mb = MB()
    seg = 96
    rings = []
    for k, (y, r) in enumerate(ISLAND_PROFILE):
        ring = []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            deg = math.degrees(a)
            rr = r
            if y < PLATEAU_Y - 0.1:
                rr = r * (1.0 + island_noise(a, y, rng.uniform(-0.02, 0.02)))
            else:
                rr = r * (1.0 + 0.02 * math.sin(5 * a))
            lip = CORBEL.get(y)
            if lip:
                for pa in PILLAR_ANGLES:
                    wgt = max(0.0, 1.0 - ang_dist(deg, pa) / 14.0)
                    rr += lip * (wgt ** 1.5)
            yy = y + (rng.uniform(-0.25, 0.25) if 0 < k < len(ISLAND_PROFILE) - 1 else 0.0)
            ring.append(mb.v((math.sin(a) * rr, yy, math.cos(a) * rr)))
        rings.append(ring)

    for k in range(len(rings) - 1):
        for j in range(seg):
            jn = (j + 1) % seg
            a = 2 * math.pi * (j + 0.5) / seg
            grass = k >= RIM_RING + 1 and (math.sin(4 * a + k) + math.sin(9 * a)) > -1.3
            grass = grass or (k == RIM_RING and math.sin(6 * a + 0.7) > 0.4)
            mb.f((rings[k][j], rings[k][jn], rings[k + 1][jn], rings[k + 1][j]),
                 "L6_Grass" if grass else "L6_Rock")
    top_c = mb.v((0.0, PLATEAU_Y, 0.0))
    bot_c = mb.v((0.0, ISLAND_PROFILE[0][0], 0.0))
    last, first = rings[-1], rings[0]
    for j in range(seg):
        jn = (j + 1) % seg
        mb.f((last[j], last[jn], top_c), "L6_Grass")
        mb.f((first[jn], first[j], bot_c), "L6_Rock")

    # Waterline boulders - kept clear of the pillars' torpedo lanes
    placed = 0
    while placed < 16:
        deg = rng.uniform(0, 360)
        if min(ang_dist(deg, pa) for pa in PILLAR_ANGLES) < 22:
            continue
        a = math.radians(deg)
        r = rng.uniform(22.0, 24.0)
        mb.rock((math.sin(a) * r, rng.uniform(-0.6, 0.6), math.cos(a) * r),
                rng.uniform(1.6, 3.0), "L6_Rock", rng, squash=0.75)
        placed += 1

    # Rock outcrops + bushes on the grassy rim
    for i in range(10):
        deg = rng.uniform(0, 360)
        a = math.radians(deg)
        r = rng.uniform(17.5, 23.0)
        mb.rock((math.sin(a) * r, surface_height(r) + 0.2, math.cos(a) * r),
                rng.uniform(1.0, 1.8), "L6_Rock", rng, squash=0.9)
    for i in range(26):
        deg = rng.uniform(0, 360)
        a = math.radians(deg)
        r = rng.uniform(16.8, 24.0)
        mb.rock((math.sin(a) * r, surface_height(r) + 0.35, math.cos(a) * r),
                rng.uniform(0.7, 1.4), "L6_Grass", rng, squash=0.8)
    return mb.build("Island")


# ---------------------------------------------------------------------------
# Castle (origin = castle base centre, sits on the plateau at PLATEAU_Y)
# ---------------------------------------------------------------------------
def merlons_along(mb, a, b, y, outward, mat, step=1.5, size=(0.7, 0.8, 0.45)):
    dx, dz = b[0] - a[0], b[2] - a[2]
    ln = math.hypot(dx, dz)
    yaw = math.degrees(math.atan2(dx, dz))
    n = max(1, int(ln / step))
    for i in range(n):
        t = (i + 0.5) / n
        c = (a[0] + dx * t + outward[0], y + size[1] / 2, a[2] + dz * t + outward[2])
        mb.box(c, (size[2], size[1], size[0]), mat, yaw=yaw)


def build_castle():
    mb = MB()
    S, R, W, I = "L6_CastleStone", "L6_Roof", "L6_Window", "L6_Iron"
    wall_h, wall_t = 4.6, 1.4

    # Hexagonal curtain wall with a turret at every vertex
    vtx = [(math.sin(math.radians(d)) * WALL_RADIUS, 0.0, math.cos(math.radians(d)) * WALL_RADIUS)
           for d in CANNON_ANGLES]
    for k in range(6):
        a, b = vtx[k], vtx[(k + 1) % 6]
        dx, dz = b[0] - a[0], b[2] - a[2]
        ln = math.hypot(dx, dz)
        yaw = math.degrees(math.atan2(dx, dz))
        mid = ((a[0] + b[0]) / 2, wall_h / 2, (a[2] + b[2]) / 2)
        mb.box(mid, (wall_t, wall_h, ln - 3.2), S, yaw=yaw)
        mr = math.hypot(mid[0], mid[2])
        out = (mid[0] / mr * 0.45, 0.0, mid[2] / mr * 0.45)
        inset_a = (a[0] + dx / ln * 2.2, 0, a[2] + dz / ln * 2.2)
        inset_b = (b[0] - dx / ln * 2.2, 0, b[2] - dz / ln * 2.2)
        merlons_along(mb, inset_a, inset_b, wall_h, out, S)
        # arrow slits on the outer face
        for t in (0.3, 0.7):
            p = (a[0] + dx * t + out[0] * 1.6, 2.4, a[2] + dz * t + out[2] * 1.6)
            mb.box(p, (0.12, 1.0, 0.35), W, yaw=yaw)

    for d, p in zip(CANNON_ANGLES, vtx):
        mb.tube((p[0], 0.0, p[2]), (p[0], TURRET_TOP - 0.3, p[2]), 2.1, 2.1, 16, S)
        mb.tube((p[0], TURRET_TOP - 0.3, p[2]), (p[0], TURRET_TOP, p[2]), 2.45, 2.45, 16, S)

    # Gate on the wall facing roughly south-west of the player spawn
    ga = math.radians(210.0)
    gr = WALL_RADIUS * math.cos(math.radians(30.0)) + 0.75
    mb.box((math.sin(ga) * gr, 1.5, math.cos(ga) * gr), (2.6, 3.0, 0.3), "L6_Wood", yaw=210.0 + 90.0)

    # Keep
    kh = 8.5
    mb.box((0.0, kh / 2, 0.0), (10.0, kh, 10.0), S)
    for x0, z0, x1, z1, ox, oz in ((-5, -5, 5, -5, 0, -0.35), (5, -5, 5, 5, 0.35, 0),
                                   (5, 5, -5, 5, 0, 0.35), (-5, 5, -5, -5, -0.35, 0)):
        merlons_along(mb, (x0, 0, z0), (x1, 0, z1), kh, (ox, 0, oz), S, step=1.6)
    for side in range(4):
        for row in (2.5, 5.5):
            for col in (-2.5, 0.0, 2.5):
                if side == 0:
                    c, yaw = (col, row, -5.04), 0.0
                elif side == 1:
                    c, yaw = (5.04, row, col), 90.0
                elif side == 2:
                    c, yaw = (col, row, 5.04), 0.0
                else:
                    c, yaw = (-5.04, row, col), 90.0
                mb.box(c, (0.6, 1.1, 0.1), W, yaw=yaw)

    # Corner towers with conical roofs
    for x, z in ((-5, -5), (5, -5), (5, 5), (-5, 5)):
        mb.tube((x, 0.0, z), (x, 12.0, z), 1.75, 1.75, 14, S)
        mb.tube((x, 12.0, z), (x, 16.5, z), 2.2, 0.0, 14, R)
        mb.box((x * 1.0 + (0.0), 9.5, z + (-1.76 if z < 0 else 1.76)), (0.45, 0.9, 0.1), W)

    # Secondary tower
    mb.box((2.4, kh + 3.0, -2.4), (3.0, 6.0, 3.0), S)
    mb.tube((2.4, kh + 6.0, -2.4), (2.4, kh + 9.5, -2.4), 2.3, 0.0, 4, R, phase=math.pi / 4)

    # Central great tower with spire (top ~ y 30 -> ~40 in the world)
    mb.box((-0.8, kh + 5.25, 0.8), (4.4, 10.5, 4.4), S)
    merlons_along(mb, (-3.0, 0, -1.4), (1.4, 0, -1.4), kh + 10.5, (0, 0, -0.2), S, step=1.4)
    merlons_along(mb, (1.4, 0, 3.0), (-3.0, 0, 3.0), kh + 10.5, (0, 0, 0.2), S, step=1.4)
    for row in (11.5, 14.5, 17.0):
        mb.box((-0.8, row, -1.44), (0.6, 1.2, 0.1), W)
        mb.box((-0.8, row, 3.04), (0.6, 1.2, 0.1), W)
    mb.tube((-0.8, kh + 10.5, 0.8), (-0.8, kh + 15.5, 0.8), 3.0, 0.0, 4, R, phase=math.pi / 4)
    mb.tube((-0.8, kh + 14.5, 0.8), (-0.8, kh + 21.5, 0.8), 0.7, 0.0, 8, R)
    mb.tube((-0.8, kh + 21.3, 0.8), (-0.8, kh + 22.3, 0.8), 0.05, 0.05, 4, I)
    return mb.build("Castle")


# ---------------------------------------------------------------------------
# Pillar (origin = pillar base centre at sea level)
# ---------------------------------------------------------------------------
DRUM_COUNT  = 5
DRUM_BOTTOM = 0.4
DRUM_H      = 0.92
CAPITAL_Y   = DRUM_BOTTOM + DRUM_COUNT * DRUM_H   # 5.0


def drum_radius(i):
    return 2.05 - 0.05 * i


def build_drum(i, damaged, rng):
    mb = MB()
    seg = 16
    cy = DRUM_BOTTOM + DRUM_H * (i + 0.5)
    r = drum_radius(i)
    rmul = None
    if damaged:
        rmul = [1.0] * seg
        for _ in range(2):
            s0 = rng.randrange(seg)
            depth = rng.uniform(0.62, 0.8)
            for d in range(-1, 2):
                rmul[(s0 + d) % seg] = min(rmul[(s0 + d) % seg], depth + 0.12 * abs(d))
    mb.tube((0, cy - DRUM_H / 2, 0), (0, cy + DRUM_H / 2 - 0.02, 0), r, r * 0.99, seg, "L6_PillarStone", rmul=rmul)
    mb.tube((0, cy - 0.12, 0), (0, cy + 0.12, 0), r + 0.09, r + 0.09, seg, "L6_Rune", rmul=rmul)
    if damaged:
        for _ in range(3):
            a = rng.uniform(0, 2 * math.pi)
            p = (math.sin(a) * (r + 0.02), cy + rng.uniform(-0.2, 0.2), math.cos(a) * (r + 0.02))
            mb.box(p, (0.09, rng.uniform(0.5, 0.85), 0.06), "L6_Window",
                   yaw=math.degrees(a) + 90.0, pitch=rng.uniform(-35, 35))
    name = "Drum_%d%s" % (i, "_Damaged" if damaged else "")
    return mb.build(name, origin=(0.0, cy, 0.0))


def build_pillar():
    rng = random.Random(66)
    objs = []
    mb = MB()
    mb.tube((0, -1.5, 0), (0, DRUM_BOTTOM - 0.15, 0), 2.75, 2.55, 8, "L6_PillarStone", phase=math.pi / 8)
    mb.tube((0, DRUM_BOTTOM - 0.15, 0), (0, DRUM_BOTTOM, 0), 2.35, 2.2, 16, "L6_PillarStone")
    objs.append(mb.build("Base"))
    for i in range(DRUM_COUNT):
        objs.append(build_drum(i, False, rng))
        objs.append(build_drum(i, True, rng))

    mb = MB()
    mb.tube((0, CAPITAL_Y, 0), (0, CAPITAL_Y + 0.45, 0), 1.85, 2.4, 16, "L6_PillarStone")
    mb.box((0, CAPITAL_Y + 0.63, 0), (4.8, 0.36, 4.8), "L6_PillarStone")
    mb.tube((0, CAPITAL_Y + 0.2, 0), (0, CAPITAL_Y + 0.32, 0), 2.18, 2.3, 16, "L6_Rune")
    objs.append(mb.build("Capital", origin=(0.0, CAPITAL_Y, 0.0)))

    mb = MB()
    mb.rock((0, 0, 0), 0.45, "L6_PillarStone", rng, squash=0.8)
    objs.append(mb.build("Chunk"))

    mb = MB()
    mb.annulus(0.0, 0.05, 3.0, 3.55, 40, "L6_Rune")
    objs.append(mb.build("GlowRing", origin=(0.0, 0.0, 0.0)))

    mb = MB()
    mb.tube((0, 0.9, 0), (0, 0.0, 0), 0.75, 0.0, 12, "L6_Rune")
    mb.tube((0, 0.9, 0), (0, 2.1, 0), 0.28, 0.28, 10, "L6_Rune")
    objs.append(mb.build("Marker"))
    return objs


# ---------------------------------------------------------------------------
# Cannon (origin = mount base; barrel pivot at the trunnion, points +Z)
# ---------------------------------------------------------------------------
TRUNNION_Y = 1.05
MUZZLE_Z   = 1.85


def build_cannon():
    objs = []
    mb = MB()
    mb.box((0, 0.42, 0), (1.3, 0.55, 1.7), "L6_Wood")
    for sx in (-0.55, 0.55):
        mb.box((sx, 0.95, -0.1), (0.22, 0.6, 1.2), "L6_Wood")
        for z in (-0.6, 0.6):
            mb.tube((sx * 1.25, 0.32, z), (sx * 1.25 + (0.12 if sx > 0 else -0.12), 0.32, z), 0.32, 0.32, 10, "L6_Iron")
    objs.append(mb.build("Carriage"))

    mb = MB()
    o = (0.0, TRUNNION_Y, 0.0)
    mb.tube((0, TRUNNION_Y, -0.7), (0, TRUNNION_Y, MUZZLE_Z - 0.25), 0.37, 0.28, 14, "L6_Iron")
    mb.tube((0, TRUNNION_Y, MUZZLE_Z - 0.3), (0, TRUNNION_Y, MUZZLE_Z), 0.35, 0.33, 14, "L6_Iron")
    mb.tube((0, TRUNNION_Y, -0.95), (0, TRUNNION_Y, -0.7), 0.16, 0.3, 10, "L6_Iron")
    mb.tube((-0.5, TRUNNION_Y, 0.0), (0.5, TRUNNION_Y, 0.0), 0.12, 0.12, 8, "L6_Iron")
    objs.append(mb.build("Barrel", origin=o))
    return objs


# ---------------------------------------------------------------------------
# Enemy gunboat (origin = waterline centre, bow +Z, length 8)
# ---------------------------------------------------------------------------
def build_boat():
    mb = MB()
    H, D = "L6_BoatHull", "L6_BoatDeck"
    sections = [  # (z, half-width, deck y, keel y)
        (-4.0, 1.20, 1.00, -0.25), (-3.0, 1.35, 1.00, -0.40), (-1.0, 1.42, 1.00, -0.45),
        (1.0, 1.40, 1.02, -0.45), (2.5, 1.10, 1.10, -0.38), (3.5, 0.55, 1.22, -0.22), (4.0, 0.04, 1.32, 0.10),
    ]
    rows = []
    for z, w, yd, yk in sections:
        pts = [(-w, yd), (-w * 0.88, 0.25), (-w * 0.35, yk), (w * 0.35, yk), (w * 0.88, 0.25), (w, yd)]
        rows.append([mb.v((x, y, z)) for x, y in pts])
    n = len(rows[0])
    for k in range(len(rows) - 1):
        a, b = rows[k], rows[k + 1]
        for j in range(n - 1):
            mb.f((a[j], a[j + 1], b[j + 1], b[j]), H)
        mb.f((a[n - 1], a[0], b[0], b[n - 1]), D)   # deck strip
    mb.f(tuple(rows[0]), H)                          # transom
    mb.f(tuple(reversed(rows[-1])), H)               # bow tip
    # Cabin, bow gun, mast and flag
    mb.box((0, 1.55, -1.3), (1.7, 1.1, 1.9), "L6_CastleStone")
    mb.box((0, 2.17, -1.3), (1.95, 0.14, 2.2), H)
    mb.box((-0.5, 1.55, -0.34), (0.35, 0.3, 0.05), "L6_Window")
    mb.box((0.5, 1.55, -0.34), (0.35, 0.3, 0.05), "L6_Window")
    mb.tube((0, 1.0, 2.1), (0, 1.35, 2.1), 0.42, 0.38, 10, "L6_Iron")
    mb.tube((0, 1.32, 2.1), (0, 1.32, 3.6), 0.12, 0.1, 8, "L6_Iron")
    mb.tube((0, 2.2, -1.6), (0, 4.4, -1.6), 0.07, 0.06, 6, "L6_Wood")
    mb.box((0, 3.95, -2.15), (0.05, 0.7, 1.05), "L6_Flag")
    return mb.build("Boat")


# ---------------------------------------------------------------------------
# Seagull (origin = body centre, beak +Z, wings along X, ~1.5 long, ~3 wingspan)
# The wings are separate objects so Seagull.cs can flap them: WingL/WingR pivot at the
# shoulder, WingTipL/WingTipR pivot at the wrist (Unity re-parents the tips at runtime).
# ---------------------------------------------------------------------------
GULL_SHOULDER_X = 0.12
GULL_WRIST_X    = 0.80
GULL_WING_Y     = 0.10


def slab(mb, outline, y, thick, top, bottom, side):
    """Extrude a convex XZ outline (Unity coords) by thick, centred on y."""
    n = len(outline)
    lo = [mb.v((x, y - thick / 2, z)) for x, z in outline]
    hi = [mb.v((x, y + thick / 2, z)) for x, z in outline]
    mb.f(tuple(hi), top)
    mb.f(tuple(reversed(lo)), bottom)
    for j in range(n):
        k = (j + 1) % n
        mb.f((lo[j], lo[k], hi[k], hi[j]), side)


def loft(mb, rings, seg, mat_top, mat_bottom, tip_front, tip_back):
    """Body of revolution-ish: rings = [(z, yc, rx, ry)], tail -> head, pointed ends."""
    ids = []
    for z, yc, rx, ry in rings:
        row = []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            row.append(mb.v((math.cos(a) * rx, yc + math.sin(a) * ry, z)))
        ids.append(row)

    def mat_for(j):  # upper half of the ring = back (top) colour
        a = 2 * math.pi * (j + 0.5) / seg
        return mat_top if math.sin(a) > 0.35 else mat_bottom

    for r in range(len(ids) - 1):
        a, b = ids[r], ids[r + 1]
        for j in range(seg):
            k = (j + 1) % seg
            mb.f((a[j], a[k], b[k], b[j]), mat_for(j))
    back, front = mb.v(tip_back), mb.v(tip_front)
    for j in range(seg):
        k = (j + 1) % seg
        mb.f((ids[0][k], ids[0][j], back), mat_for(j))
        mb.f((ids[-1][j], ids[-1][k], front), mat_for(j))


def build_seagull():
    W, G, K = "L6_GullWhite", "L6_GullGrey", "L6_GullBlack"
    objs = []

    mb = MB()
    loft(mb, [  # (z, y centre, half-width, half-height)
        (-0.58, 0.06, 0.08, 0.05), (-0.42, 0.04, 0.15, 0.11), (-0.20, 0.01, 0.21, 0.18),
        (0.05, 0.02, 0.21, 0.19), (0.24, 0.07, 0.15, 0.14), (0.34, 0.13, 0.115, 0.115),
        (0.44, 0.18, 0.13, 0.13), (0.55, 0.19, 0.11, 0.11), (0.63, 0.18, 0.065, 0.07),
    ], 12, W, W, tip_front=(0.0, 0.18, 0.67), tip_back=(0.0, 0.07, -0.64))
    # grey mantle on the back between the wings
    slab(mb, [(-0.14, 0.22), (0.14, 0.22), (0.12, -0.42), (-0.12, -0.42)], 0.19, 0.03, G, G, G)
    # tail fan (white with black band)
    slab(mb, [(-0.07, -0.50), (0.07, -0.50), (0.17, -0.80), (-0.17, -0.80)], 0.07, 0.03, W, W, W)
    slab(mb, [(-0.17, -0.80), (0.17, -0.80), (0.18, -0.86), (-0.18, -0.86)], 0.07, 0.03, K, K, K)
    # beak with the red gonys spot, eyes, tucked legs + feet
    mb.tube((0, 0.165, 0.62), (0, 0.145, 0.90), 0.045, 0.012, 8, "L6_GullBeak")
    mb.box((0, 0.125, 0.82), (0.03, 0.035, 0.04), "L6_GullRed")
    for sx in (-1, 1):
        mb.box((sx * 0.105, 0.23, 0.52), (0.03, 0.045, 0.045), K)
        mb.tube((sx * 0.07, -0.13, -0.05), (sx * 0.07, -0.17, -0.36), 0.025, 0.02, 6, "L6_GullLeg")
        mb.box((sx * 0.07, -0.17, -0.42), (0.10, 0.02, 0.12), "L6_GullLeg")
    objs.append(mb.build("Body"))

    for side, tag in ((1, "R"), (-1, "L")):
        sx = lambda pts: [(side * x, z) for x, z in (pts if side > 0 else list(reversed(pts)))]
        # inner wing: shoulder -> wrist (grey top, white underside, white trailing edge)
        mb = MB()
        slab(mb, sx([(GULL_SHOULDER_X, 0.24), (GULL_WRIST_X, 0.19), (GULL_WRIST_X, -0.20),
                     (GULL_SHOULDER_X, -0.30)]), GULL_WING_Y, 0.05, G, W, G)
        slab(mb, sx([(GULL_SHOULDER_X, -0.30), (GULL_WRIST_X, -0.20), (GULL_WRIST_X, -0.26),
                     (GULL_SHOULDER_X, -0.36)]), GULL_WING_Y, 0.04, W, W, W)
        objs.append(mb.build("Wing" + tag, origin=(side * GULL_SHOULDER_X, GULL_WING_Y, 0.0)))
        # wing tip: grey hand, black primaries with a white mirror spot
        mb = MB()
        slab(mb, sx([(GULL_WRIST_X, 0.19), (1.12, 0.12), (1.12, -0.24), (GULL_WRIST_X, -0.26)]),
             GULL_WING_Y, 0.04, G, W, G)
        slab(mb, sx([(1.12, 0.12), (1.50, -0.03), (1.42, -0.18), (1.12, -0.24)]),
             GULL_WING_Y, 0.035, K, K, K)
        slab(mb, sx([(1.30, -0.02), (1.38, -0.05), (1.36, -0.11), (1.28, -0.10)]),
             GULL_WING_Y + 0.02, 0.01, W, W, W)
        objs.append(mb.build("WingTip" + tag, origin=(side * GULL_WRIST_X, GULL_WING_Y, 0.0)))
    return objs


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
def export(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False,
        mesh_smooth_type="FACE", add_leaf_bones=False, use_mesh_modifiers=True,
        path_mode="AUTO", embed_textures=False)
    size = os.path.getsize(path)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
    print("[Dreadhold] %-22s %8.1f KB  %6d tris  %d objects" % (filename, size / 1024.0, tris, len(objs)))
    return path, size


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    results = [
        export([build_island()], "DreadholdIsland.fbx"),
        export([build_castle()], "DreadholdCastle.fbx"),
        export(build_pillar(), "DreadholdPillar.fbx"),
        export(build_cannon(), "DreadholdCannon.fbx"),
        export([build_boat()], "DreadholdBoat.fbx"),
        export(build_seagull(), "DreadholdSeagull.fbx"),
    ]
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "dreadhold.blend"), compress=True)

    too_big = [(p, s) for p, s in results if s > MAX_BYTES]
    if too_big:
        for p, s in too_big:
            print("[Dreadhold] ERROR: %s is %.2f MB (limit 2 MB)" % (p, s / 1048576.0))
        sys.exit(1)
    print("[Dreadhold] All models exported, every FBX under 2 MB.")


main()

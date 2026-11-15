"""
Level 7 "Kraken's Throne" model generator (Blender 4.5, headless).

Run from the project root:
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" -b --factory-startup --python Tools/Blender/build_kraken.py

Builds every Level 7 model procedurally (seeded, so the output is reproducible), saves
3d/level7/kraken.blend and exports these FBX files to 3d/level7/ (GameSetup.CopyModels imports
them into Assets/Models/Kraken/):

  KrakenTentacle.fbx  - rigged (bones T0..T7 + tip marker T8, base at the origin, 36 m tall,
                        faces +Z). One timeline, split into clips by KrakenTentacle.clips.json:
                        Idle (loop), Rise, Windup (loop), Slam, Recover, Throw, Sink.
  KrakenBody.fbx      - Phase 1 kraken: mantle, eyes, 8 arm stubs. Eyes face +Z.
  KrakenThrone.fbx    - rock + coral throne (seat top at y = 9).
  KrakenKing.fbx      - the Phase 2 kraken sitting on the throne (same origin), with a crown.
  KrakenWhirlpool.fbx - concave funnel disc, radius 30, planar UVs for the Whirlpool shader.
  Sunfish.fbx         - mola mola, upright, faces +Z, body centre at the origin.
  Jellyfish.fbx       - bell (rim at y = 0) + hanging tentacles.
  KrakenRock.fbx      - throwing rock, radius ~1.5.
  InkBlob.fbx         - ink projectile, radius ~1.
  Dolphin.fbx         - rigged (D0..D4 spine bottom -> top, FlipL / FlipR), modelled UPRIGHT
                        (tail at y = 0, head up, belly +Z). Clips from Dolphin.clips.json:
                        Jump, Applaud (loop).

All geometry is written in UNITY coordinates (Y up, +Z forward, metres = Unity units) and
converted with U() to Blender space. Unity imports the FBX with bakeAxisConversion = true.
Every FBX must stay under 2 MB and every mesh under MAX_TRIS triangles - the script fails
loudly if one does not.
"""

import bpy
import bmesh
import json
import math
import os
import random
import sys
from mathutils import Quaternion, Vector

try:
    _HERE = os.path.dirname(os.path.realpath(__file__))
    ROOT = os.path.dirname(os.path.dirname(_HERE))
except NameError:
    ROOT = os.getcwd()
OUT_DIR = os.path.join(ROOT, "3d", "level7")

MAX_BYTES = 2 * 1024 * 1024
MAX_TRIS = 3000
FPS = 30

# Tentacle layout (keep in sync with TentacleAttacker / GameSetup.BuildKrakensThrone)
TENTACLE_LENGTH = 36.0
TENTACLE_BONES = 8
SEG = TENTACLE_LENGTH / TENTACLE_BONES

MAT_COLORS = {
    "L7_KrakenSkin":   (0.45, 0.10, 0.22),
    "L7_KrakenBelly":  (0.88, 0.50, 0.48),
    "L7_Eye":          (1.00, 0.82, 0.10),
    "L7_Pupil":        (0.03, 0.03, 0.03),
    "L7_Gold":         (0.95, 0.72, 0.15),
    "L7_Gem":          (0.10, 0.80, 0.75),
    "L7_ThroneRock":   (0.30, 0.33, 0.36),
    "L7_Coral":        (0.95, 0.45, 0.40),
    "L7_Seaweed":      (0.15, 0.45, 0.25),
    "L7_SunfishSkin":  (0.60, 0.65, 0.70),
    "L7_SunfishBelly": (0.88, 0.89, 0.86),
    "L7_JellyBell":    (0.85, 0.45, 0.95),
    "L7_JellyGlow":    (1.00, 0.75, 1.00),
    "L7_Rock":         (0.42, 0.38, 0.34),
    "L7_Ink":          (0.05, 0.03, 0.10),
    "L7_Whirlpool":    (0.10, 0.35, 0.60),
    "L7_DolphinSkin":  (0.40, 0.55, 0.70),
    "L7_DolphinBelly": (0.85, 0.90, 0.95),
}


def U(x, y, z):
    """Unity (x, y, z) -> Blender (x, y, z) for an FBX exported -Z forward / Y up."""
    return (-x, -z, y)


def B2U(v):
    """Blender vector -> Unity (x, y, z)."""
    return (-v[0], v[2], -v[1])


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        c = MAT_COLORS[name]
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (c[0], c[1], c[2], 1.0)
            bsdf.inputs["Roughness"].default_value = 0.8
        m.diffuse_color = (c[0], c[1], c[2], 1.0)
    return m


def lerp(a, b, t):
    return a + (b - a) * t


def clamp(x, a, b):
    return max(a, min(b, x))


# ---------------------------------------------------------------------------
# Mesh builder (Unity coordinates)
# ---------------------------------------------------------------------------
class MB:
    def __init__(self):
        self.verts = []
        self.faces = []
        self.fmats = []
        self.mats = []
        self.tags = []      # per-vertex tag (used for skinning parts)
        self.tag = None

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def v(self, p):
        self.verts.append(tuple(p))
        self.tags.append(self.tag)
        return len(self.verts) - 1

    def f(self, idx, mat):
        self.faces.append(tuple(idx))
        self.fmats.append(self.mat(mat))

    # --- primitives -------------------------------------------------------
    def frame(self, ax):
        """Two unit vectors perpendicular to axis ax."""
        ref = (0.0, 1.0, 0.0) if abs(ax[1]) < 0.9 else (1.0, 0.0, 0.0)
        u = [ax[1] * ref[2] - ax[2] * ref[1], ax[2] * ref[0] - ax[0] * ref[2], ax[0] * ref[1] - ax[1] * ref[0]]
        ul = math.sqrt(sum(a * a for a in u))
        u = [a / ul for a in u]
        w = [ax[1] * u[2] - ax[2] * u[1], ax[2] * u[0] - ax[0] * u[2], ax[0] * u[1] - ax[1] * u[0]]
        return u, w

    def tube(self, p0, p1, r0, r1, seg, mat, cap0=True, cap1=True, phase=0.0):
        """Cylinder / truncated cone from p0 to p1. r1 == 0 makes a pointed cone."""
        self.path([p0, p1], [r0, r1], seg, mat, cap0=cap0, cap1=cap1, phase=phase)

    def path(self, pts, radii, seg, mat, cap0=True, cap1=True, phase=0.0, sx=1.0, matfn=None):
        """Tube along a polyline; radii per point (last radius 0 = pointed tip).
        sx flattens the cross-section along the first frame axis. matfn(j, i) may override the
        material per quad (j = segment around, i = ring index)."""
        rings = []
        n = len(pts)
        for i in range(n):
            if i == 0:
                d = [pts[1][k] - pts[0][k] for k in range(3)]
            elif i == n - 1:
                d = [pts[-1][k] - pts[-2][k] for k in range(3)]
            else:
                d = [pts[i + 1][k] - pts[i - 1][k] for k in range(3)]
            ln = math.sqrt(sum(a * a for a in d)) or 1e-6
            d = [a / ln for a in d]
            u, w = self.frame(d)
            r = radii[i]
            if r <= 1e-6:
                rings.append([self.v(pts[i])])
                continue
            ring = []
            for j in range(seg):
                a = 2 * math.pi * j / seg + phase
                ring.append(self.v([pts[i][k] + (u[k] * math.cos(a) * sx + w[k] * math.sin(a)) * r for k in range(3)]))
            rings.append(ring)
        for i in range(n - 1):
            a, b = rings[i], rings[i + 1]
            for j in range(seg):
                k = (j + 1) % seg
                m = matfn(j, i) if matfn else mat
                if len(b) == 1:
                    self.f((a[j], a[k], b[0]), m)
                elif len(a) == 1:
                    self.f((a[0], b[k], b[j]), m)
                else:
                    self.f((a[j], a[k], b[k], b[j]), m)
        if cap0 and len(rings[0]) > 1:
            self.f(tuple(reversed(rings[0])), mat)
        if cap1 and len(rings[-1]) > 1:
            self.f(tuple(rings[-1]), mat)

    def lathe(self, profile, seg, mat, centre=(0.0, 0.0, 0.0), rmul=None, offset=None,
              sx=1.0, sz=1.0, matfn=None, close_top=True, close_bottom=True):
        """Surface of revolution about the Unity Y axis. profile = [(y, r), ...] bottom -> top.
        offset(y) -> (dx, dz) shifts each ring; rmul(j, y) scales the radius per segment."""
        rings = []
        for (y, r) in profile:
            dx, dz = offset(y) if offset else (0.0, 0.0)
            if r <= 1e-6:
                rings.append([self.v((centre[0] + dx, centre[1] + y, centre[2] + dz))])
                continue
            ring = []
            for j in range(seg):
                a = 2 * math.pi * j / seg
                m = rmul(j, y) if rmul else 1.0
                ring.append(self.v((centre[0] + dx + math.sin(a) * r * m * sx,
                                    centre[1] + y,
                                    centre[2] + dz + math.cos(a) * r * m * sz)))
            rings.append(ring)
        for i in range(len(rings) - 1):
            a, b = rings[i], rings[i + 1]
            for j in range(seg):
                k = (j + 1) % seg
                m = matfn(j, i) if matfn else mat
                if len(b) == 1:
                    self.f((a[j], a[k], b[0]), m)
                elif len(a) == 1:
                    self.f((a[0], b[k], b[j]), m)
                else:
                    self.f((a[j], a[k], b[k], b[j]), m)
        if close_bottom and len(rings[0]) > 1:
            self.f(tuple(reversed(rings[0])), mat)
        if close_top and len(rings[-1]) > 1:
            self.f(tuple(rings[-1]), mat)

    def sphere(self, c, r, mat, seg=12, rings=8, scale=(1.0, 1.0, 1.0)):
        prof = []
        for i in range(rings + 1):
            t = math.pi * i / rings
            prof.append((-math.cos(t) * r * scale[1], math.sin(t) * r))
        self.lathe(prof, seg, mat, centre=c, sx=scale[0], sz=scale[2])

    def box(self, c, size, mat, yaw=0.0, pitch=0.0):
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        cy, sy_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
        ids = []
        for dx, dy, dz in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                           (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)):
            x, y, z = dx * sx, dy * sy, dz * sz
            y, z = y * cp - z * sp, y * sp + z * cp
            x, z = x * cy + z * sy_, -x * sy_ + z * cy
            ids.append(self.v((c[0] + x, c[1] + y, c[2] + z)))
        a, b, cc, d, e, f_, g, h = ids
        for q in ((a, b, cc, d), (e, h, g, f_), (a, e, f_, b), (b, f_, g, cc), (cc, g, h, d), (d, h, e, a)):
            self.f(q, mat)

    def slab(self, pts2d, y_or_plane, thick, mat, plane="xz"):
        """Extruded polygon. plane 'xz': pts are (x, z) at height y; 'yz': pts are (y, z) at x;
        'xy': pts are (x, y) at z."""
        def P(p, off):
            if plane == "xz":
                return (p[0], y_or_plane + off, p[1])
            if plane == "yz":
                return (y_or_plane + off, p[0], p[1])
            return (p[0], p[1], y_or_plane + off)
        n = len(pts2d)
        top = [self.v(P(p, thick / 2)) for p in pts2d]
        bot = [self.v(P(p, -thick / 2)) for p in pts2d]
        self.f(tuple(top), mat)
        self.f(tuple(reversed(bot)), mat)
        for i in range(n):
            k = (i + 1) % n
            self.f((bot[i], bot[k], top[k], top[i]), mat)

    def rock(self, c, radius, mat, rng, squash=0.8, subdiv=1):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
        base = len(self.verts)
        for vert in bm.verts:
            k = rng.uniform(0.78, 1.18)
            x, y, z = vert.co.x * k, vert.co.z * k * squash, vert.co.y * k
            self.v((c[0] + x * radius, c[1] + y * radius, c[2] + z * radius))
        for face in bm.faces:
            self.f([base + vv.index for vv in face.verts], mat)
        bm.free()

    # --- finish -------------------------------------------------------------
    def build(self, name, merge=True, smooth=False, uv=None):
        """uv(unity_xyz) -> (u, v) adds a UV layer."""
        bverts = [U(*p) for p in self.verts]
        me = bpy.data.meshes.new(name)
        me.from_pydata(bverts, [], self.faces)
        me.update()
        for mname in self.mats:
            me.materials.append(get_mat(mname))
        for i, poly in enumerate(me.polygons):
            poly.material_index = self.fmats[i]
        if uv is not None:
            layer = me.uv_layers.new(name="UVMap")
            for poly in me.polygons:
                for li in poly.loop_indices:
                    vi = me.loops[li].vertex_index
                    layer.data[li].uv = uv(self.verts[vi])
        bm = bmesh.new()
        bm.from_mesh(me)
        if merge:
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        for poly in me.polygons:
            poly.use_smooth = smooth
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ---------------------------------------------------------------------------
# Rigging helpers
# ---------------------------------------------------------------------------
def make_armature(name, bones):
    """bones: [(bone_name, head_unity, tail_unity, parent_name_or_None), ...]"""
    data = bpy.data.armatures.new(name + "Data")
    arm = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = data.edit_bones
    for bname, head, tail, parent in bones:
        b = eb.new(bname)
        b.head = U(*head)
        b.tail = U(*tail)
        b.roll = 0.0
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


VERTEX_TAGS = {}   # mesh object name -> builder tag per vertex (meshes built with merge=False)


def skin(mesh_obj, arm, weight_fn):
    """weight_fn(unity_xyz, tag) -> {bone_name: weight}. Mesh must be built with merge=False so
    the vertex order matches the builder's tag list (VERTEX_TAGS[mesh_obj.name])."""
    tags = VERTEX_TAGS[mesh_obj.name]
    groups = {}
    for b in arm.data.bones:
        groups[b.name] = mesh_obj.vertex_groups.new(name=b.name)
    for v in mesh_obj.data.vertices:
        p = B2U(v.co)
        for bname, w in weight_fn(p, tags[v.index]).items():
            if w > 1e-4:
                groups[bname].add([v.index], w, "REPLACE")
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def bend_quat(pb, axis_blender, deg):
    """Rotation of deg degrees about a Blender ARMATURE-space axis, expressed in the bone's rest frame."""
    local = pb.bone.matrix_local.to_3x3().inverted() @ Vector(axis_blender)
    return Quaternion(local.normalized(), math.radians(deg))


# Positive angle about Blender +X bends a vertical bone toward Unity +Z (forward);
# negative angle about Blender +Y bends it toward Unity +X (right); about Blender +Z it yaws.
AX_FWD = (1.0, 0.0, 0.0)
AX_SIDE = (0.0, 1.0, 0.0)
AX_YAW = (0.0, 0.0, 1.0)


def key_pose(arm, frame, rots):
    """rots: {bone: [(axis, deg), ...]} - unspecified bones reset to identity."""
    for pb in arm.pose.bones:
        q = Quaternion()
        for axis, deg in rots.get(pb.name, []):
            q = bend_quat(pb, axis, deg) @ q
        pb.rotation_quaternion = q
        pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)


def write_clips(filename, clips, meta):
    data = {"fps": FPS, "clips": [{"name": n, "first": a, "last": b, "loop": lp} for n, a, b, lp in clips],
            "meta": meta}
    path = os.path.join(OUT_DIR, filename)
    with open(path, "w") as fh:
        json.dump(data, fh, indent=2)
    print("[Kraken] wrote %s" % path)


# ---------------------------------------------------------------------------
# Tentacle (rigged)
# ---------------------------------------------------------------------------
def tentacle_radius(y):
    t = clamp(y / TENTACLE_LENGTH, 0.0, 1.0)
    return 1.9 * (1.0 - t) ** 0.85 + 0.12


def build_tentacle():
    mb = MB()
    sides = 10
    n_rings = 24
    pts, radii = [], []
    for i in range(n_rings + 1):
        y = TENTACLE_LENGTH * i / n_rings - 2.0 * (1 if i == 0 else 0)  # base dips below the water
        pts.append((0.0, max(y, -2.0), 0.0))
        radii.append(tentacle_radius(max(y, 0.0)))
    pts.append((0.0, TENTACLE_LENGTH + 0.8, 0.0))
    radii.append(0.0)

    def mat_for(j, i):
        return "L7_KrakenBelly" if face_front[j] else "L7_KrakenSkin"

    # work out which segments face +Z (the sucker side) using the same frame the path uses
    u, w = mb.frame((0.0, 1.0, 0.0))
    face_front = []
    for j in range(sides):
        a = 2 * math.pi * (j + 0.5) / sides
        d = [u[k] * math.cos(a) + w[k] * math.sin(a) for k in range(3)]
        face_front.append(d[2] > 0.55)
    mb.path(pts, radii, sides, "L7_KrakenSkin", cap0=True, cap1=False, matfn=mat_for)

    # suckers: two rows on the +Z side
    for i in range(11):
        y = 2.5 + i * 2.9
        if y > TENTACLE_LENGTH - 3.0:
            break
        r = tentacle_radius(y)
        sr = clamp(r * 0.32, 0.12, 0.55)
        for sx in (-1, 1):
            a = math.radians(28 * sx)
            nx, nz = math.sin(a), math.cos(a)
            base = (nx * r * 0.9, y, nz * r * 0.9)
            top = (nx * (r * 0.9 + sr * 0.6), y, nz * (r * 0.9 + sr * 0.6))
            mb.tube(base, top, sr, sr * 0.75, 6, "L7_KrakenBelly")

    obj = mb.build("Tentacle", merge=False, smooth=True)
    VERTEX_TAGS[obj.name] = mb.tags

    bones = []
    for i in range(TENTACLE_BONES):
        bones.append(("T%d" % i, (0, i * SEG, 0), (0, (i + 1) * SEG, 0), "T%d" % (i - 1) if i else None))
    bones.append(("T8", (0, TENTACLE_LENGTH, 0), (0, TENTACLE_LENGTH + 0.5, 0), "T7"))
    arm = make_armature("Armature", bones)

    def weights(p, tag):
        y = p[1]
        t = y / SEG - 0.5
        i0 = int(math.floor(t))
        f = t - i0
        if i0 < 0:
            return {"T0": 1.0}
        if i0 >= TENTACLE_BONES - 1:
            return {"T7": 1.0}
        return {"T%d" % i0: 1.0 - f, "T%d" % (i0 + 1): f}

    skin(obj, arm, weights)
    return arm, obj


TENT_IDLE = [-4, -3, 2, 5, 8, 11, 14, 16]
TENT_CURL = [22, 30, 36, 42, 46, 48, 48, 48]
TENT_WIND = [-14, -10, -6, 6, 18, 26, 30, 30]
TENT_SLAM = [86, 1, 1, 1, 1, 1, 1, 1]
TENT_BACK = [-22, -16, -10, 0, 14, 24, 30, 30]
TENT_FLING = [28, 18, 10, 4, 0, -4, -6, -6]

TENTACLE_CLIPS = [  # name, first, last, loop
    ("Idle", 0, 59, True),
    ("Rise", 60, 95, False),
    ("Windup", 96, 135, True),
    ("Slam", 136, 145, False),
    ("Recover", 146, 179, False),
    ("Throw", 180, 209, False),
    ("Sink", 210, 239, False),
]


def tentacle_pose(fwd, sway_f=0.0, sway_s=0.0, phase=0.0):
    rots = {}
    for i in range(TENTACLE_BONES):
        sf = sway_f * math.sin(phase + i * 0.55)
        ss = sway_s * math.sin(phase * 1.0 + 1.3 + i * 0.45)
        rots["T%d" % i] = [(AX_FWD, fwd[i] + sf), (AX_SIDE, ss)]
    return rots


def mix(a, b, t):
    return [lerp(a[i], b[i], t) for i in range(len(a))]


def animate_tentacle(arm):
    arm.animation_data_create()
    act = bpy.data.actions.new("TentacleAnim")
    arm.animation_data.action = act
    # Idle loop (sway)
    for f in range(0, 60, 6):
        ph = 2 * math.pi * f / 59.0
        key_pose(arm, f, tentacle_pose(TENT_IDLE, 3.0, 4.0, ph))
    key_pose(arm, 59, tentacle_pose(TENT_IDLE, 3.0, 4.0, 2 * math.pi))
    # Rise: curled -> idle
    key_pose(arm, 60, tentacle_pose(TENT_CURL))
    key_pose(arm, 78, tentacle_pose(mix(TENT_CURL, TENT_IDLE, 0.7)))
    key_pose(arm, 95, tentacle_pose(TENT_IDLE))
    # Windup loop: reared back, menacing sway
    for f in range(96, 136, 8):
        ph = 2 * math.pi * (f - 96) / 39.0
        key_pose(arm, f, tentacle_pose(TENT_WIND, 4.0, 3.0, ph))
    key_pose(arm, 135, tentacle_pose(TENT_WIND, 4.0, 3.0, 2 * math.pi))
    # Slam: windup -> flat on the water along +Z (impact on the last frame)
    key_pose(arm, 136, tentacle_pose(TENT_WIND))
    key_pose(arm, 140, tentacle_pose(mix(TENT_WIND, TENT_SLAM, 0.45)))
    key_pose(arm, 145, tentacle_pose(TENT_SLAM))
    # Recover: flat -> idle
    key_pose(arm, 146, tentacle_pose(TENT_SLAM))
    key_pose(arm, 160, tentacle_pose(mix(TENT_SLAM, TENT_IDLE, 0.6)))
    key_pose(arm, 179, tentacle_pose(TENT_IDLE))
    # Throw: idle -> reared back -> fling forward (release ~frame 195) -> idle
    key_pose(arm, 180, tentacle_pose(TENT_IDLE))
    key_pose(arm, 190, tentacle_pose(TENT_BACK))
    key_pose(arm, 197, tentacle_pose(TENT_FLING))
    key_pose(arm, 209, tentacle_pose(TENT_IDLE))
    # Sink: idle -> curled
    key_pose(arm, 210, tentacle_pose(TENT_IDLE))
    key_pose(arm, 239, tentacle_pose(TENT_CURL))
    return act


def bone_tip_unity(arm, bone, frame):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    pb = arm.pose.bones[bone]
    return B2U(arm.matrix_world @ pb.head)


# ---------------------------------------------------------------------------
# Kraken (Phase 1)
# ---------------------------------------------------------------------------
def kraken_arm(mb, a_deg, r0, h0, reach, rise, thick, rng, mat="L7_KrakenSkin"):
    a = math.radians(a_deg)
    dx, dz = math.sin(a), math.cos(a)
    px, pz = math.cos(a), -math.sin(a)   # sideways
    pts, radii = [], []
    n = 7
    curl = rng.uniform(-0.25, 0.25)
    for i in range(n):
        t = i / (n - 1)
        r = r0 + reach * math.sin(t * math.pi * 0.62)
        y = h0 + rise * math.sin(t * math.pi * 0.9) - 0.8 * t
        side = curl * reach * t * t
        pts.append((dx * r + px * side, y, dz * r + pz * side))
        radii.append(thick * (1.0 - t) ** 0.8 + (0.12 if i < n - 1 else 0.0))
    radii[-1] = 0.0
    mb.path(pts, radii, 8, mat, cap0=False)


def build_kraken_body():
    mb = MB()
    rng = random.Random(71)

    def tilt(y):
        return (0.0, -max(0.0, y - 4.0) * 0.38)

    def wob(j, y):
        a = 2 * math.pi * j / 20
        return 1.0 + 0.04 * math.sin(3 * a + y * 0.3) + 0.03 * math.sin(5 * a + 1.7)

    prof = [(-6, 12.0), (0, 13.0), (4, 13.6), (8, 13.0), (12, 11.6), (16, 9.8), (20, 7.6),
            (24, 4.8), (27, 1.8), (28.2, 0.0)]
    mb.lathe(prof, 20, "L7_KrakenSkin", rmul=wob, offset=tilt, close_bottom=True)
    # belly / face plate (lighter) on the front
    mb.sphere((0, 4.5, 9.8), 6.0, "L7_KrakenBelly", seg=12, rings=6, scale=(1.3, 0.8, 0.6))
    # eyes with slit pupils and brow ridges
    for sx in (-1, 1):
        mb.sphere((sx * 5.6, 11.0, 10.4), 3.0, "L7_Eye", seg=12, rings=8)
        mb.box((sx * 5.9, 11.0, 13.3), (0.9, 3.4, 0.7), "L7_Pupil")
        mb.tube((sx * 2.5, 14.3, 11.0), (sx * 8.8, 13.6, 9.6), 1.2, 0.6, 6, "L7_KrakenSkin")
    # beak hint
    mb.tube((0, 2.6, 13.0), (0, 1.0, 14.6), 1.4, 0.0, 6, "L7_Pupil")
    # 8 arm stubs curling out over the water (skip the face front)
    for k in range(8):
        a = 22.5 + 45 * k
        kraken_arm(mb, a, 10.5, 1.0, 11.0 + rng.uniform(-1.5, 1.5), 6.5 + rng.uniform(-1, 1.5), 2.8, rng)
    return mb.build("KrakenBody", smooth=True)


# ---------------------------------------------------------------------------
# Throne + Kraken King (Phase 2)
# ---------------------------------------------------------------------------
SEAT_Y = 9.0


def build_throne():
    mb = MB()
    rng = random.Random(72)

    def wob(j, y):
        a = 2 * math.pi * j / 24
        return 1.0 + 0.05 * math.sin(4 * a + y) + 0.04 * math.sin(9 * a + 0.6)

    mb.lathe([(-6, 13.0), (0, 13.6), (2.2, 13.0), (3.6, 11.4), (4.2, 10.0)], 24, "L7_ThroneRock", rmul=wob)
    # seat block + gold rim
    mb.box((0, 6.6, -0.5), (13.0, 4.8, 10.5), "L7_ThroneRock")
    mb.box((0, 9.05, 4.6), (13.4, 0.5, 0.6), "L7_Gold")
    # armrests
    for sx in (-1, 1):
        mb.box((sx * 6.8, 10.4, -0.5), (1.8, 3.2, 9.0), "L7_ThroneRock")
        mb.box((sx * 6.8, 12.15, -0.5), (2.0, 0.4, 9.4), "L7_Gold")
        mb.sphere((sx * 6.8, 12.9, 4.0), 0.9, "L7_Gem", seg=8, rings=5)
    # backrest slab with spires
    mb.box((0, 18.0, -6.2), (14.0, 18.0, 2.4), "L7_ThroneRock", pitch=-6)
    mb.box((0, 26.6, -7.1), (14.6, 0.6, 2.8), "L7_Gold", pitch=-6)
    for sx, h, r in ((0, 36.0, 2.0), (-4.5, 32.0, 1.6), (4.5, 32.0, 1.6), (-7.2, 28.5, 1.3), (7.2, 28.5, 1.3)):
        mb.tube((sx, 26.0, -7.2), (sx * 1.05, h, -8.6), r, 0.0, 6, "L7_ThroneRock")
    mb.sphere((0, 24.0, -4.8), 1.4, "L7_Gem", seg=10, rings=6)
    # coral clusters and seaweed around the base
    for k in range(9):
        a = math.radians(k * 40 + rng.uniform(-12, 12))
        r = 12.0 + rng.uniform(-0.5, 1.0)
        bx, bz = math.sin(a) * r, math.cos(a) * r
        for b in range(3):
            ang = math.radians(rng.uniform(-35, 35))
            h = rng.uniform(2.5, 4.5)
            mb.tube((bx, 1.5, bz), (bx + math.sin(a + ang) * 1.6, 1.5 + h, bz + math.cos(a + ang) * 1.6),
                    0.45, 0.12, 5, "L7_Coral")
        mb.tube((bx * 0.96, 0.0, bz * 0.96), (bx * 1.02, 3.2, bz * 1.02), 0.5, 0.0, 4, "L7_Seaweed")
    return mb.build("KrakenThrone")


def build_kraken_king():
    mb = MB()
    rng = random.Random(73)

    def tilt(y):
        return (0.0, -1.0 - max(0.0, y - 14.0) * 0.30)

    def wob(j, y):
        a = 2 * math.pi * j / 18
        return 1.0 + 0.04 * math.sin(3 * a + y * 0.4)

    prof = [(SEAT_Y - 0.3, 5.6), (SEAT_Y + 3, 6.4), (SEAT_Y + 7, 6.8), (SEAT_Y + 10, 6.2),
            (SEAT_Y + 13, 5.0), (SEAT_Y + 16, 3.4), (SEAT_Y + 18.5, 1.4), (SEAT_Y + 19.5, 0.0)]
    mb.lathe(prof, 18, "L7_KrakenSkin", rmul=wob, offset=tilt)
    mb.sphere((0, SEAT_Y + 3.5, 4.2), 3.8, "L7_KrakenBelly", seg=10, rings=6, scale=(1.2, 0.9, 0.6))
    for sx in (-1, 1):
        mb.sphere((sx * 2.9, SEAT_Y + 8.0, 4.6), 1.7, "L7_Eye", seg=10, rings=6)
        mb.box((sx * 3.05, SEAT_Y + 8.0, 6.25), (0.5, 2.0, 0.4), "L7_Pupil")
        mb.tube((sx * 1.2, SEAT_Y + 10.0, 5.0), (sx * 4.8, SEAT_Y + 9.6, 4.2), 0.7, 0.35, 6, "L7_KrakenSkin")
    # crown: gold band + 7 spikes + gems
    cy, cz = SEAT_Y + 13.6, -3.4
    mb.lathe([(cy - 0.8, 4.4), (cy + 0.8, 4.6)], 16, "L7_Gold", centre=(0, 0, cz),
             close_top=False, close_bottom=False)
    for k in range(7):
        a = math.radians(-90 + k * 30)
        px, pz = math.sin(a) * 4.5, cz + math.cos(a) * 4.5
        mb.tube((px, cy + 0.6, pz), (px * 1.05, cy + 3.6, pz + 0.2), 0.7, 0.0, 5, "L7_Gold")
        if k % 2 == 0:
            mb.sphere((px * 1.08, cy, pz + math.cos(a) * 0.3), 0.5, "L7_Gem", seg=6, rings=4)
    # arms: draped over the armrests and down the front of the throne into the water
    for k in range(8):
        a = -150 + k * 300 / 7.0
        kraken_arm(mb, a, 5.0, SEAT_Y, 9.0 + rng.uniform(-1.0, 1.0), -7.5, 1.7, rng)
    return mb.build("KrakenKing", smooth=True)


# ---------------------------------------------------------------------------
# Whirlpool
# ---------------------------------------------------------------------------
WHIRL_R = 30.0


def build_whirlpool():
    mb = MB()
    rings, seg = 16, 48
    ids = []
    for i in range(rings + 1):
        t = i / rings
        r = WHIRL_R * (t ** 1.15)
        y = -7.0 * (1.0 - t) ** 2.2 + (0.12 if i == rings else 0.0)
        row = []
        for j in range(seg):
            a = 2 * math.pi * j / seg + t * 1.4   # slight spiral in the topology
            row.append(mb.v((math.sin(a) * r, y, math.cos(a) * r)) if i > 0 else None)
        ids.append(row)
    centre = mb.v((0.0, -7.0, 0.0))
    for j in range(seg):
        k = (j + 1) % seg
        mb.f((centre, ids[1][k], ids[1][j]), "L7_Whirlpool")
    for i in range(1, rings):
        for j in range(seg):
            k = (j + 1) % seg
            mb.f((ids[i][j], ids[i][k], ids[i + 1][k], ids[i + 1][j]), "L7_Whirlpool")
    return mb.build("Whirlpool", uv=lambda p: (p[0] / (2 * WHIRL_R) + 0.5, p[2] / (2 * WHIRL_R) + 0.5))


# ---------------------------------------------------------------------------
# Sunfish
# ---------------------------------------------------------------------------
def build_sunfish():
    mb = MB()
    # body: flattened ellipsoid, length along Z
    prof = []
    rings = 10
    for i in range(rings + 1):
        t = math.pi * i / rings
        prof.append((-math.cos(t) * 3.0, math.sin(t) * 3.0))
    # build about Y then the lathe sx/sz flatten: X thickness 0.55, Z length 1.05, Y height 0.95
    def body_mat(j, i):
        return "L7_SunfishBelly" if i < 3 else "L7_SunfishSkin"
    mb.lathe([(y * 0.95, r) for (y, r) in prof], 16, "L7_SunfishSkin", sx=0.30, sz=1.05, matfn=body_mat)
    # dorsal + anal fins (tall, swept back), thin slabs in the YZ plane
    mb.slab([(2.0, 1.0), (7.4, -1.2), (6.9, -1.9), (2.2, -1.9)], 0.0, 0.3, "L7_SunfishSkin", plane="yz")
    mb.slab([(-2.0, 1.0), (-7.4, -1.2), (-6.9, -1.9), (-2.2, -1.9)], 0.0, 0.3, "L7_SunfishBelly", plane="yz")
    # clavus (truncated scalloped tail)
    pts = []
    for k in range(9):
        a = math.radians(-80 + k * 20)
        rr = 2.6 + (0.35 if k % 2 else 0.0)
        pts.append((math.sin(a) * rr, -2.6 - math.cos(a) * 1.1))
    mb.slab(pts, 0.0, 0.4, "L7_SunfishSkin", plane="yz")
    # eyes, mouth, pectoral fins
    for sx in (-1, 1):
        mb.sphere((sx * 0.72, 1.0, 1.9), 0.42, "L7_SunfishBelly", seg=8, rings=5)
        mb.sphere((sx * 0.86, 1.0, 1.98), 0.24, "L7_Pupil", seg=8, rings=5)
        mb.slab([(0.0, 0.6), (0.9, 0.2), (0.7, -0.4), (-0.3, 0.0)], sx * 0.95, 0.15, "L7_SunfishSkin", plane="yz")
    mb.tube((0, -0.3, 2.9), (0, -0.35, 3.35), 0.38, 0.30, 8, "L7_SunfishBelly")
    return mb.build("Sunfish", smooth=False)


# ---------------------------------------------------------------------------
# Jellyfish
# ---------------------------------------------------------------------------
def build_jellyfish():
    mb = MB()
    seg = 16

    def scallop(j, y):
        return 1.0 + (0.08 if (j % 2 == 0 and y < 0.2) else 0.0)

    prof = [(0.0, 2.0), (0.5, 2.05), (1.2, 1.85), (1.8, 1.4), (2.2, 0.7), (2.35, 0.0)]
    mb.lathe(prof, seg, "L7_JellyBell", rmul=scallop, close_bottom=False)
    # inner glow dome (visible through the rim)
    mb.lathe([(0.1, 1.3), (0.8, 1.15), (1.4, 0.6), (1.6, 0.0)], 10, "L7_JellyGlow", close_bottom=True)
    rng = random.Random(74)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        x0, z0 = math.sin(a) * 1.8, math.cos(a) * 1.8
        pts = []
        for i in range(6):
            t = i / 5.0
            sway = 0.35 * math.sin(t * 5 + k)
            pts.append((x0 * (1 - 0.2 * t) + math.cos(a) * sway, -t * 4.2, z0 * (1 - 0.2 * t) - math.sin(a) * sway))
        mb.path(pts, [0.12, 0.10, 0.09, 0.07, 0.05, 0.0], 4, "L7_JellyBell", cap0=False)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        x0, z0 = math.sin(a) * 0.5, math.cos(a) * 0.5
        pts = [(x0 * (1 + 0.6 * t) + 0.3 * math.sin(t * 6 + k), -t * 3.0, z0 * (1 + 0.6 * t)) for t in
               (0.0, 0.25, 0.5, 0.75, 1.0)]
        mb.path(pts, [0.35, 0.32, 0.26, 0.18, 0.0], 5, "L7_JellyGlow", cap0=False, sx=0.4)
    return mb.build("Jellyfish", smooth=True)


# ---------------------------------------------------------------------------
# Rock + ink blob
# ---------------------------------------------------------------------------
def build_rock():
    mb = MB()
    mb.rock((0, 0, 0), 1.5, "L7_Rock", random.Random(75), squash=0.85, subdiv=2)
    return mb.build("KrakenRock")


def build_ink():
    mb = MB()
    prof = []
    for i in range(9):
        t = math.pi * i / 8
        r = math.sin(t) * (1.0 - 0.25 * (i / 8.0))
        prof.append((-math.cos(t) * 1.0 + (0.5 if i == 8 else 0.0), r))
    mb.lathe(prof, 12, "L7_Ink")
    for p, r in (((0.7, 0.3, -0.9), 0.35), ((-0.6, -0.4, -1.1), 0.28), ((0.1, 0.8, -1.3), 0.22)):
        mb.sphere(p, r, "L7_Ink", seg=6, rings=4)
    return mb.build("InkBlob", smooth=True)


# ---------------------------------------------------------------------------
# Dolphin (rigged, upright)
# ---------------------------------------------------------------------------
DOLPHIN_BONES = [  # name, head, tail, parent
    ("D0", (0, 0.0, 0), (0, 0.9, 0), None),
    ("D1", (0, 0.9, 0), (0, 1.8, 0), "D0"),
    ("D2", (0, 1.8, 0), (0, 2.7, 0), "D1"),
    ("D3", (0, 2.7, 0), (0, 3.6, 0), "D2"),
    ("D4", (0, 3.6, 0), (0, 4.5, 0), "D3"),
    ("FlipL", (-0.45, 2.95, 0.12), (-1.2, 2.45, 0.35), "D3"),
    ("FlipR", (0.45, 2.95, 0.12), (1.2, 2.45, 0.35), "D3"),
]


def build_dolphin():
    mb = MB()
    mb.tag = "body"

    def belly(j, i):
        a = 2 * math.pi * (j + 0.5) / 14
        return "L7_DolphinBelly" if math.cos(a) > 0.45 and 1 <= i <= 7 else "L7_DolphinSkin"

    prof = [(0.42, 0.0), (0.5, 0.12), (0.62, 0.15), (1.2, 0.30), (1.8, 0.46), (2.5, 0.56), (3.1, 0.54),
            (3.6, 0.45), (3.95, 0.34), (4.12, 0.15), (4.16, 0.0)]
    mb.lathe(prof, 14, "L7_DolphinSkin", sz=0.9, matfn=belly)
    # rostrum (beak) forward-up, eyes, dorsal fin on the back (-Z)
    mb.tube((0, 3.8, 0.22), (0, 4.0, 0.82), 0.16, 0.07, 8, "L7_DolphinBelly")
    for sx in (-1, 1):
        mb.sphere((sx * 0.33, 3.72, 0.24), 0.07, "L7_Pupil", seg=6, rings=4)
    mb.slab([(2.3, -0.4), (2.9, -0.45), (2.1, -1.15), (2.0, -0.95)], 0.0, 0.1, "L7_DolphinSkin", plane="yz")
    # tail flukes (horizontal) at the bottom
    mb.slab([(0.0, -0.12), (1.0, -0.32), (0.8, 0.12), (0.0, 0.16), (-0.8, 0.12), (-1.0, -0.32)],
            0.5, 0.1, "L7_DolphinSkin", plane="xz")
    # flippers (separately skinned)
    for sx, tag in ((-1, "FlipL"), (1, "FlipR")):
        mb.tag = tag
        mb.path([(sx * 0.42, 2.95, 0.12), (sx * 0.85, 2.7, 0.25), (sx * 1.25, 2.42, 0.36)],
                [0.18, 0.13, 0.0], 6, "L7_DolphinSkin", sx=0.35)
    mb.tag = "body"
    obj = mb.build("Dolphin", merge=False, smooth=True)
    VERTEX_TAGS[obj.name] = mb.tags
    arm = make_armature("Armature", DOLPHIN_BONES)

    def weights(p, tag):
        if tag in ("FlipL", "FlipR"):
            return {tag: 1.0}
        y = p[1]
        t = y / 0.9 - 0.5
        i0 = int(math.floor(t))
        f = t - i0
        if i0 < 0:
            return {"D0": 1.0}
        if i0 >= 4:
            return {"D4": 1.0}
        return {"D%d" % i0: 1.0 - f, "D%d" % (i0 + 1): f}

    skin(obj, arm, weights)
    return arm, obj


DOLPHIN_CLIPS = [
    ("Jump", 0, 29, False),
    ("Applaud", 30, 59, True),
]


def animate_dolphin(arm):
    arm.animation_data_create()
    act = bpy.data.actions.new("DolphinAnim")
    arm.animation_data.action = act

    def pose(arch, clap, nod=0.0, wiggle=0.0):
        r = {}
        for i in range(5):
            r["D%d" % i] = [(AX_FWD, arch * (0.6 if i < 2 else 1.0)), (AX_SIDE, wiggle * (1.0 if i < 2 else 0.2))]
        r["D4"].append((AX_FWD, nod))
        # flippers swing forward (+Z) and inward to meet in front of the chest
        r["FlipR"] = [(AX_YAW, clap)]
        r["FlipL"] = [(AX_YAW, -clap)]
        return r

    # Jump: arched back while flying, straightens, flippers out
    key_pose(arm, 0, pose(-12, -15))
    key_pose(arm, 10, pose(-18, -20))
    key_pose(arm, 20, pose(6, 10))
    key_pose(arm, 29, pose(0, 0))
    # Applaud loop: 2 claps per second, head nods, tail wiggles (tail-walking)
    for f in range(30, 60, 4):
        t = (f - 30) / 30.0
        clap = 35 + 35 * math.sin(2 * math.pi * 2 * t - math.pi / 2)   # 0 .. 70 degrees
        key_pose(arm, f, pose(2, clap, nod=6 * math.sin(2 * math.pi * 2 * t), wiggle=6 * math.sin(2 * math.pi * t)))
    key_pose(arm, 59, pose(2, 0, 0, 0))
    return act


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
results = []


def export(objs, filename, rigged=False, frame_range=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    scene = bpy.context.scene
    if frame_range:
        scene.frame_start, scene.frame_end = frame_range
    path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        object_types={"MESH", "ARMATURE"} if rigged else {"MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False,
        mesh_smooth_type="FACE", add_leaf_bones=False, use_mesh_modifiers=True,
        use_armature_deform_only=False,
        bake_anim=rigged, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        path_mode="AUTO", embed_textures=False)
    size = os.path.getsize(path)
    meshes = [o for o in objs if o.type == "MESH"]
    tris = max(tri_count(o) for o in meshes)
    print("[Kraken] %-22s %8.1f KB  %6d tris (largest mesh)  %d objects" % (filename, size / 1024.0, tris, len(objs)))
    results.append((path, size, tris))
    return path


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    os.makedirs(OUT_DIR, exist_ok=True)

    # --- tentacle --------------------------------------------------------
    arm, mesh = build_tentacle()
    animate_tentacle(arm)
    slam_tip = bone_tip_unity(arm, "T8", 145)
    idle_tip = bone_tip_unity(arm, "T8", 0)
    throw_tip = bone_tip_unity(arm, "T8", 195)
    reach = math.hypot(slam_tip[0], slam_tip[2])
    print("[Kraken] Tentacle tip: idle %s  slam %s  throw-release %s" %
          (tuple(round(c, 2) for c in idle_tip), tuple(round(c, 2) for c in slam_tip),
           tuple(round(c, 2) for c in throw_tip)))
    if not (slam_tip[2] > 0.8 * TENTACLE_LENGTH and abs(slam_tip[1]) < 3.0):
        print("[Kraken] ERROR: slam pose does not lay the tentacle flat along +Z: %s" % (slam_tip,))
        sys.exit(1)
    write_clips("KrakenTentacle.clips.json", TENTACLE_CLIPS, {
        "slamReach": round(reach, 3),
        "slamImpactTime": round((145 - 136) / FPS, 4),
        "throwReleaseTime": round((195 - 180) / FPS, 4),
        "length": TENTACLE_LENGTH,
        "tipBone": "T8",
    })
    export([arm, mesh], "KrakenTentacle.fbx", rigged=True, frame_range=(0, 239))

    # --- dolphin ---------------------------------------------------------
    darm, dmesh = build_dolphin()
    animate_dolphin(darm)
    clap_r = bone_tip_unity(darm, "FlipR", 38)
    print("[Kraken] Dolphin right flipper head at clap: %s" % (tuple(round(c, 2) for c in clap_r),))
    write_clips("Dolphin.clips.json", DOLPHIN_CLIPS, {"height": 4.16})
    export([darm, dmesh], "Dolphin.fbx", rigged=True, frame_range=(0, 59))

    # --- static models ---------------------------------------------------
    export([build_kraken_body()], "KrakenBody.fbx")
    export([build_throne()], "KrakenThrone.fbx")
    export([build_kraken_king()], "KrakenKing.fbx")
    export([build_whirlpool()], "KrakenWhirlpool.fbx")
    export([build_sunfish()], "Sunfish.fbx")
    export([build_jellyfish()], "Jellyfish.fbx")
    export([build_rock()], "KrakenRock.fbx")
    export([build_ink()], "InkBlob.fbx")

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "kraken.blend"), compress=True)

    failed = False
    for p, s, t in results:
        if s > MAX_BYTES:
            print("[Kraken] ERROR: %s is %.2f MB (limit 2 MB)" % (p, s / 1048576.0))
            failed = True
        if t > MAX_TRIS:
            print("[Kraken] ERROR: %s has a %d-triangle mesh (limit %d)" % (p, t, MAX_TRIS))
            failed = True
    if failed:
        sys.exit(1)
    print("[Kraken] All models exported: every FBX under 2 MB, every mesh under %d triangles." % MAX_TRIS)


main()

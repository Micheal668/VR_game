"""Convert NASA's public SLS Block 1 display STL to the stage meshes used in Unity.

Run Blender in background with --python this_file -- path/to/downloaded.stl
The source and its SHA-256 are in the credit alongside the baked model.
"""
from collections import defaultdict
import hashlib
from pathlib import Path
import struct
import sys

import bpy
import numpy as np


SOURCE_HASH = "d3af75412a54f1d15d644cce56e895fe6813452b49dc0558b4b2ddfb484844ac"
OUT = Path(__file__).resolve().parents[1] / "Assets/_LunarEscape/Art/Artemis/Block1.slsmesh"
PIVOTS = {
    "Core": (0, 32, 0), "LeftBooster": (-6.28, 25.5, 0), "RightBooster": (6.28, 25.5, 0),
    "UpperStage": (0, 71.5, 0), "Orion": (0, 82, 0), "EscapeTower": (0, 90, 0),
}
# Indices into the importer's shared material palette.
IVORY, ORANGE, COPPER, DARK, STEEL, SILVER, BLACK = range(7)


def load_stl(path):
    if hashlib.sha256(path.read_bytes()).hexdigest() != SOURCE_HASH:
        raise ValueError("Expected NASA Space Launch System (SLS) Block 1.stl")
    facets = np.fromfile(path, offset=84, dtype=[("normal", "<f4", 3), ("p", "<f4", (3, 3)), ("attr", "<u2")])
    points, faces = np.unique(np.round(facets["p"].reshape(-1, 3), 4), axis=0, return_inverse=True)
    faces = faces.reshape(-1, 3)
    parent = list(range(len(points)))

    def root(index):
        while index != parent[index]:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    for a, b, c in faces:
        a, b, c = root(a), root(b), root(c)
        parent[b] = parent[c] = a
    _, groups = np.unique([root(i) for i in faces[:, 0]], return_inverse=True)
    # One Unity unit is a metre. Discard the rectangular printable stand.
    scale = 98.0 / (198.74088 - 4.99)
    points = points[:, [0, 2, 1]].copy()
    points[:, 1] -= 4.99
    points *= scale
    return points, faces[:, [0, 2, 1]], groups


def classify(points, faces, groups):
    centers = points[faces].mean(axis=1)
    x, y, z = centers.T
    radius = np.hypot(x, z)
    angle = np.arctan2(z, x)
    sections = np.full(len(faces), "Core", dtype="U12")
    shades = np.full(len(faces), STEEL)
    keep = np.ones(len(faces), dtype=bool)
    for group in np.unique(groups):
        selection = groups == group
        bounds = points[faces[selection]].reshape(-1, 3)
        lo, hi = bounds.min(axis=0), bounds.max(axis=0)
        center = (lo + hi) * .5
        if hi[1] < 1 and hi[0] - lo[0] > 20:
            keep[selection] = False
        elif abs(center[0]) > 5 and hi[1] - lo[1] > 35:
            sections[selection] = "LeftBooster" if center[0] < 0 else "RightBooster"
            shades[selection] = IVORY
            shades[selection & (y < 1.2)] = DARK
            around = np.arctan2(z, x - np.sign(center[0]) * 6.28)
            fiducial = (((y > 14) & (y < 16)) | ((y > 40.2) & (y < 42.2))) & (np.cos(around * 4) > .25)
            shades[selection & fiducial] = BLACK
        elif lo[1] > 64:
            sections[selection & (y < 77.47)] = "UpperStage"
            sections[selection & (y < 72.05)] = "Core"
            sections[selection & (y >= 77.47) & (y < 82.12)] = "Orion"
            sections[selection & (y >= 82.12)] = "EscapeTower"
            shades[selection] = IVORY
            shades[selection & (y < 72)] = ORANGE
            shades[selection & (y >= 72) & (y < 73.2)] = SILVER
            shades[selection & (y >= 76.2) & (y < 77.47)] = STEEL
            # Clear the cabin window and crew hatch through the exterior cover.
            access = (np.abs(angle) < .36) & (y > 81.92) & (y < 84.15)
            window = (np.abs(angle - np.pi * .5) < .66) & (y > 82.5) & (y < 84.65)
            keep[selection & (access | window)] = False
            # Omit the print's sealed dummy hatch on the opposite side.
            if lo[1] > 81 and hi[1] < 84 and np.hypot(*center[[0, 2]]) > 2:
                keep[selection] = False
        elif hi[0] - lo[0] > 8 and hi[1] > 60:
            shades[selection] = ORANGE
            shades[selection & (y < 8)] = COPPER
            shades[selection & (y < 4.3)] = DARK
            shades[selection & (y > 44.6) & (y < 49.7) & (radius > 4.21)] = COPPER
        elif hi[1] < 4.5:
            shades[selection] = DARK
    return sections, shades, keep


def reveal_surfaces(stage):
    """Surfaces exposed when the printable display is opened for boarding or staging."""
    points, faces, colors = [], [], []
    def shell(rings, finish, apertures=False):
        count = 64
        for band, ((y0, r0), (y1, r1)) in enumerate(zip(rings, rings[1:])):
            for i in range(count):
                t0, t1 = np.pi * 2 * np.array([i, i + 1]) / count
                angle = (t0 + t1) * .5
                if apertures:
                    height = (y0 + y1) * .5 - 82
                    if abs(np.arctan2(np.sin(angle), np.cos(angle))) < .37 and -.1 < height < 2.12:
                        continue
                    if abs(angle - np.pi / 2) < .65 and .5 < height < 2:
                        continue
                index = len(points)
                for h, r, t in ((y0,r0,t0),(y0,r0,t1),(y1,r1,t1),(y1,r1,t0)):
                    points.append((r*np.cos(t), h, r*np.sin(t)))
                faces.extend([(index,index+3,index+2),(index,index+2,index+1)])
                colors.extend([finish[band]]*2)
    if stage == "Orion":
        shell([(81.82,2.57),(81.98,2.58),(82.49,2.49),(84.0,1.54),(84.12,1.46),(85.14,.81),(85.20,.78),(85.20,.012)],
              [BLACK,SILVER,SILVER,DARK,SILVER,DARK,STEEL], True)
        exterior_end = len(faces)
        shell([(81.82,2.52),(81.98,2.53),(82.49,2.44),(84,1.49),(84.12,1.41),(85.14,.76),(85.20,.73)],
              [DARK,STEEL,STEEL,DARK,STEEL,DARK], True)
        faces[exterior_end:] = [tuple(reversed(f)) for f in faces[exterior_end:]]
    elif stage == "UpperStage":
        shell([(66.55,.79),(66.59,.82),(68.2,.67),(70.9,.24),(71.23,.57),(72.18,.85)],
              [SILVER,DARK,DARK,STEEL,STEEL])
        shell([(72.13,2.48),(72.10,.73),(72.08,.02)], [STEEL,DARK])
        shell([(77.45,2.69),(77.48,2.58),(77.50,.03)], [DARK,STEEL])
    if not faces:
        return None
    return np.array(points), np.asarray(faces), np.asarray(colors)


def write_section(stream, name, points, faces, shades):
    # Decimate as a single welded shell per stage, retaining the source silhouette.
    used, indices = np.unique(faces, return_inverse=True)
    pivot = np.asarray(PIVOTS[name])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata((points[used] - pivot).tolist(), [], indices.reshape(-1, 3).tolist())
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for mat in palette:
        mesh.materials.append(mat)
    mesh.polygons.foreach_set("material_index", np.asarray(shades, dtype=np.int32))
    simplify = obj.modifiers.new("VR display mesh", "DECIMATE")
    simplify.ratio = .24 if len(faces) > 30000 else .64
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    result = evaluated.to_mesh()
    result.calc_loop_triangles()
    by_material = defaultdict(list)
    positions = np.array([v.co[:] for v in result.vertices], dtype="f4")
    normals = np.array([v.normal[:] for v in result.vertices], dtype="f4")
    for tri in result.loop_triangles:
        by_material[tri.material_index].extend(tri.vertices[:])
    stream.write(struct.pack("<B", len(name)) + name.encode("ascii"))
    stream.write(struct.pack("<3fii", *pivot, len(positions), len(by_material)))
    stream.write(positions.tobytes() + normals.tobytes())
    for material, triangles in sorted(by_material.items()):
        stream.write(struct.pack("<ii", material, len(triangles)))
        stream.write(np.asarray(triangles, dtype="<i4").tobytes())
    print(name, len(positions), len(result.loop_triangles))
    evaluated.to_mesh_clear()
    bpy.data.objects.remove(obj, do_unlink=True)


if __name__ == "__main__":
    source = Path(sys.argv[sys.argv.index("--") + 1])
    points, faces, groups = load_stl(source)
    sections, shades, keep = classify(points, faces, groups)
    palette = [bpy.data.materials.new(str(i)) for i in range(7)]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("wb") as stream:
        stream.write(b"SLS1" + struct.pack("<i", len(PIVOTS)))
        for name in PIVOTS:
            selection = (sections == name) & keep
            selected_faces, selected_shades = faces[selection], shades[selection]
            extra = reveal_surfaces(name)
            if extra:
                detail_points, detail_faces, detail_shades = extra
                write_section(stream, name, np.vstack((points, detail_points)),
                              np.vstack((selected_faces, detail_faces + len(points))),
                              np.hstack((selected_shades, detail_shades)))
            else:
                write_section(stream, name, points, selected_faces, selected_shades)

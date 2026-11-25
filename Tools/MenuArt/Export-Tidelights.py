"""Bake the approved single-atlas Cubism keyforms into MTGB's menu mesh and 30 Hz poses."""
import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path


AXES = ("ParamBreath", "ParamSettleGrip", "ParamHeadIncline", "ParamBreeze", "ParamBreezeTips")
LIDS = ("BlinkHalf", "BlinkNarrow", "BlinkClosed")
RIB_TIPS = ((228, 428), (282, 495), (336, 541), (1291, 291), (1377, 219), (1486, 77))


def sample(curve, time):
    segments = curve["Segments"]
    t, value = segments[:2]
    k = 2
    while k < len(segments):
        kind = segments[k]
        if kind == 1:
            t1, v1, t2, v2, end, new = segments[k+1:k+7]
            step = (end-t)/3
            if abs(t1-t-step) > .00001 or abs(end-t2-step) > .00001:
                raise ValueError("Unrestricted Bezier timing")
            if time < end:
                u = (time-t)/(end-t)
                return (1-u)**3*value + 3*(1-u)**2*u*v1 + 3*(1-u)*u*u*v2 + u**3*new
            k += 7
        elif kind in (0, 2):
            end, new = segments[k+1:k+3]
            if time < end:
                return value if kind == 2 else value+(new-value)*(time-t)/(end-t)
            k += 3
        else:
            raise ValueError(f"Unsupported segment {kind}")
        t, value = end, new
    return value


def keyforms(layer):
    size = len(layer["vertices"])
    result = [[0.] * size for _ in AXES]
    lid = LIDS.index(layer["id"])+1 if layer["id"] in LIDS else 0
    for axis in layer["axes"]:
        keys = axis["keys"]
        if axis["id"] == "ParamBlink":
            if not lid or any(k.get("opacity") != (i == lid) or any(k["delta"])
                              for i, k in enumerate(keys)):
                raise ValueError("Unexpected eyelid keyforms")
            continue
        delta = next(k["delta"] for k in keys if k["value"] == 1)
        if any(abs(actual - k["value"]*end) > .001 for k in keys
               for actual, end in zip(k["delta"], delta, strict=True)):
            raise ValueError(f"Nonlinear geometry in {layer['id']}/{axis['id']}")
        result[AXES.index(axis["id"])] = delta
    return result, lid


def rib_attachment(layer, tip):
    points = list(zip(layer["vertices"][::2], layer["vertices"][1::2]))
    triangles = layer["triangles"]
    for k in range(0, len(triangles), 3):
        ids = triangles[k:k+3]
        (ax, ay), (bx, by), (cx, cy) = (points[i] for i in ids)
        det = (bx-ax)*(cy-ay) - (by-ay)*(cx-ax)
        u = ((tip[0]-ax)*(cy-ay) - (tip[1]-ay)*(cx-ax))/det
        v = ((bx-ax)*(tip[1]-ay) - (by-ay)*(tip[0]-ax))/det
        if min(1-u-v, u, v) >= -.00001:
            return ids, (1-u-v, u, v)
    raise ValueError(f"Rib tip outside the authored canopy mesh: {tip}")


def verify_inputs(receipt, inputs):
    # The atlas and topology can both load successfully when accidentally mixed
    # across revisions. Bind them to the same independently verified export.
    recorded = json.loads(receipt.read_text(encoding="utf-8-sig"))["verifiedRuntimeSha256"]
    for path, suffix in inputs:
        expected, = [h for key, h in recorded.items() if key.endswith(suffix)]
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError(f"File differs from the verified model: {path}")


def export(rig_file, model_file, destination, receipt=None):
    rig = json.loads(rig_file.read_text(encoding="utf-8-sig"))
    refs = json.loads(model_file.read_text(encoding="utf-8-sig"))["FileReferences"]
    atlas, = refs["Textures"]
    motion_name = refs["Motions"]["Idle"][0]["File"]
    if receipt:
        verify_inputs(receipt, [(rig_file, "-atlas-rig.json"), (model_file, "/" + model_file.name),
            *((model_file.parent / name, "/" + name) for name in (refs["Moc"], atlas, motion_name))])
    motion = json.loads((model_file.parent / motion_name).read_text(encoding="utf-8-sig"))
    curves = {c["Id"]: c for c in motion["Curves"]}
    width, height = rig["width"], rig["height"]
    fps = int(motion["Meta"]["Fps"])
    count = round(motion["Meta"]["Duration"]*fps)
    layers = rig["layers"]
    total = sum(len(l["vertices"])//2 for l in layers)
    if total > 65535 or fps != 30 or any(l["page"] != 0 for l in layers):
        raise ValueError("Expected a single 16-bit index mesh and a 30 Hz motion")
    indices = []
    out = bytearray(struct.pack("<4s6i", b"TDM1", width, height, total,
                                sum(len(l["triangles"]) for l in layers), count, fps))
    start = 0
    for layer in layers:
        offsets, lid = keyforms(layer)
        vertices, uv = layer["vertices"], layer["uv"]
        for i in range(0, len(vertices), 2):
            values = [num for axis in offsets for num in axis[i:i+2]]
            out.extend(struct.pack("<15f", *vertices[i:i+2], uv[i], 1-uv[i+1], *values, lid))
        indices.extend(start+n for n in layer["triangles"])
        start += len(vertices)//2
    out.extend(struct.pack(f"<{len(indices)}H", *indices))
    canopy = next(l for l in layers if l["id"] == "Parasol")
    delta, _ = keyforms(canopy)
    ribs = [rib_attachment(canopy, tip) for tip in RIB_TIPS]
    for i in range(count):
        weights = [sample(curves[axis], i/fps) for axis in AXES]
        blink = sample(curves["ParamBlink"], i/fps)
        lid = next(n for n, key in enumerate((0, .34, .68, 1)) if abs(blink-key) < .0001)
        out.extend(struct.pack("<6f", *weights, lid))
        for tip, (ids, bary) in zip(RIB_TIPS, ribs):
            for c in (0, 1):
                offset = sum(w*b*d[2*j+c] for w, d in zip(weights, delta) for j, b in zip(ids, bary))
                out.extend(struct.pack("<f", tip[c]+offset))
    destination.mkdir(parents=True, exist_ok=True)
    (destination / "TideIdle.bytes").write_bytes(out)
    shutil.copyfile(model_file.parent / atlas, destination / "TideIdleAtlas.png")
    print(f"{total} vertices, {len(indices)//3} triangles, {count} poses; mesh and motion {len(out):,} bytes")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ("atlas_rig", "model", "destination"):
        parser.add_argument(arg, type=Path)
    parser.add_argument("--verified-by", type=Path, help="Authoring archive reproduction.json")
    args = parser.parse_args()
    export(args.atlas_rig, args.model, args.destination, args.verified_by)

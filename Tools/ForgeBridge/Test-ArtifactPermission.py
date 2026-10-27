"""Real Chiss-Goria exile permission: affinity proposals, payment and X resolution.
Usage: python -X utf8 Tools/ForgeBridge/Test-ArtifactPermission.py --case serpent
"""
import argparse, json, os, queue, re, subprocess, tempfile, threading, time, hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "ForgeRuntime"
FIXTURE = ROOT / "Tools/ForgeBridge/tests/ArtifactPermissionFixture.java"

def run(case, output):
    output = output.resolve()
    folder = output / case / str(time.time_ns())
    folder.mkdir(parents=True)
    classes = output / "classes"
    classes.mkdir(exist_ok=True)
    cfg = json.loads((RUNTIME / "runtime.json").read_text(encoding="utf-8-sig"))
    subprocess.run(["javac", "-encoding", "UTF-8", "-cp", str(RUNTIME / "classes") + os.pathsep + str(RUNTIME / "lib/*"), "-d", str(classes), str(FIXTURE)], check=True)
    desired = {"ballista": "Walking Ballista", "serpent": "Stonecoil Serpent", "wurmcoil": "Wurmcoil Engine"}[case]
    x = 8 if case == "serpent" else 4
    spent = 1 if case == "wurmcoil" else 3
    stack_seen = False
    cast = attacked = False
    answered = set()
    checks = set()
    numeric_views = {}
    def check(ok, label):
        assert ok, label
        checks.add(label)
    with tempfile.TemporaryDirectory(dir=folder) as tmp, (folder / "engine.log").open("w", encoding="utf-8") as log, (folder / "trace.jsonl").open("w", encoding="utf-8") as trace:
        deck = {"cards": [{"setCode": "10E", "collectorNumber": "376", "count": 60}], "sideboard": []}
        config = dict(session="affinity", artifactCase=case, seed=4, firstPlayerSeat=1, startingLife=20, decks=[deck, deck])
        start = Path(tmp) / "start.json"
        start.write_text(json.dumps(config), encoding="utf-8")
        proc = subprocess.Popen([str(RUNTIME / cfg["java"]), "-Xmx768m", "-XX:CICompilerCount=2", "-Dmtgb.bridge.lazyCatalog=true", "-Djava.awt.headless=true", "-Djava.io.tmpdir=" + tmp, "-cp", os.pathsep.join(map(str, [classes, RUNTIME / "classes", RUNTIME / "lib/*"])), "mtgb.bridge.ArtifactPermissionFixture", str(RUNTIME / cfg["assets"]), str(start)], cwd=RUNTIME / cfg["assets"], env=dict(os.environ, APPDATA=tmp, LOCALAPPDATA=tmp, TEMP=tmp, TMP=tmp), stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding="utf-8")
        messages = queue.Queue()
        def reader():
            for line in proc.stdout:
                messages.put(json.loads(line))
            messages.put(None)
        threading.Thread(target=reader, daemon=True).start()
        try:
            for step in range(160):
                message = messages.get(timeout=40)
                check(message is not None and message["type"] not in ["fatal", "rejected", "ended"], "native session remains available")
                trace.write(json.dumps(message) + "\n")
                trace.flush()
                if message["type"] != "request":
                    continue
                request = message["request"]
                if request["requestId"] in answered:
                    continue
                answered.add(request["requestId"])
                view = message["views"][0]
                own = view["players"][0]
                options = [o for o in request["options"] if not o.get("disabled")]
                ids = [o["id"] for o in options]
                kind = request["kind"]
                for entry in view.get("stackItems", []):
                    if entry.get("source", {}).get("name") == desired:
                        stack_seen = True
                        check(len(re.findall(r"\{[^}]+\}", entry["payment"]["spentMana"])) == spent, "actual mana spent matches affinity")
                        check(entry["source"]["castingManaCost"] == "{" + str(spent) + "}", "announced cost matches native payment")
                        (folder / "stack.json").write_text(json.dumps(entry, ensure_ascii=False), encoding="utf-8")
                resolved = next((c for c in own["battlefield"] if c["name"] == desired), None)
                if cast and stack_seen and resolved and not view.get("stackItems") and kind == "InputPassPriority":
                    if case != "wurmcoil":
                        check(resolved["power"] == x and resolved["toughness"] == x, "X resolves into the chosen counters and stats")
                    result = dict(status="PASS", case=case, runtimeHash=cfg["runtimeHash"], fixtureSha256=hashlib.sha256(FIXTURE.read_bytes()).hexdigest(), assertions=sorted(checks), manaSpent=spent, x=x if case != "wurmcoil" else None)
                    (folder / "resolved.json").write_text(json.dumps(view, ensure_ascii=False), encoding="utf-8")
                    (folder / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
                    return result
                exile = own.get("exileCards", [])
                if not cast and len(exile) == 5 and kind == "InputPassPriority" and request["seat"] == 1 and view["phase"] == "MAIN2":
                    costs = {c["name"]: c.get("previewManaCost") for c in exile}
                    check(costs["Wurmcoil Engine"] == "{1}" and costs["Cityscape Leveler"] == "{3}" and costs["Sol Ring"] == "{0}" and costs["Portal to Phyrexia"] == "{4}", "exile proposals include all five artifact reductions")
                    available = {o.get("label", "").split(" [")[0] for o in options if o["id"].startswith("card:") and not o.get("requiresPaymentCheck")}
                    check("Wurmcoil Engine" in available and "Cityscape Leveler" in available and "Portal to Phyrexia" not in available, "priority options agree with three available mana")
                    check(len([c for c in own["battlefield"] if c["name"] == "Darksteel Relic"]) == 5 and all(not c.get("tapped") for c in own["battlefield"] if c["land"]), "forecast leaves real artifacts and lands unchanged")
                    (folder / "exile.json").write_text(json.dumps(view, ensure_ascii=False), encoding="utf-8")
                    pick = "card:" + str(next(c["id"] for c in exile if c["name"] == desired))
                    cast = True
                elif "Mulligan" in kind:
                    pick = "ok"
                elif kind == "InputAttack":
                    pick = "attack-all" if not attacked else "ok"
                    attacked = True
                elif kind == "InputPassPriority":
                    pick = "ok"
                elif kind == "ChooseNumber":
                    key = request["contextId"]
                    snapshot = json.dumps(message["views"], sort_keys=True)
                    check(snapshot == numeric_views.setdefault(key, snapshot), "numeric draft preserves both seat projections")
                    check("decisionViews" not in request, "internal modal snapshot stays off the wire")
                    pick = "number-plus" if request["selectedValue"] < x else "confirm-number"
                elif "PayMana" in kind:
                    payer = message["views"][request["seat"] - 1]["players"][request["seat"] - 1]
                    pick = "ok" if "ok" in ids else next((i for i in ids if i.startswith("mana:")), None) or next("card:" + str(c["id"]) for c in payer["battlefield"] if c["land"] and not c.get("tapped") and "card:" + str(c["id"]) in ids)
                else:
                    pick = "ok" if "ok" in ids else "confirm-order" if "confirm-order" in ids else ids[0]
                check(pick in ids, "responses use original enabled option IDs")
                proc.stdin.write(json.dumps(dict(type="respond", session="affinity", seat=request["seat"], requestId=request["requestId"], option=pick)) + "\n")
                proc.stdin.flush()
            raise AssertionError("native step budget")
        finally:
            proc.kill()
            proc.wait()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--case", choices=["all", "ballista", "serpent", "wurmcoil"], default="all")
    parser.add_argument("--output", type=Path, default=ROOT / ".tools/artifact-permission-1027")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for case in ["ballista", "serpent", "wurmcoil"] if args.case == "all" else [args.case]:
        print(json.dumps(run(case, args.output), ensure_ascii=False))

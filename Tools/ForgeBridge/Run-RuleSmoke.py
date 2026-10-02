"""Exercise a real Lightning Bolt from a saved deck using the packaged bridge."""
import json
import argparse
from pathlib import Path
import os
import queue
import subprocess
import tempfile
import threading
import time

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--seed", type=int, default=4)
parser.add_argument("--deck", type=Path, default=root / "Decks/Starter.mdk")
parser.add_argument("--mulligans", type=int, default=0, choices=range(0, 4))
parser.add_argument("--auto-bottom", action="store_true")
args = parser.parse_args()
runtime = root / "ForgeRuntime"
config = json.loads((runtime / "runtime.json").read_text(encoding="utf-8-sig"))
deck = {"cards": [], "sideboard": []}
section = "cards"
for line in args.deck.read_text(encoding="utf-8-sig").splitlines():
    if line == "!side":
        section = "sideboard"
    elif line and line[0].isdigit():
        count, key = line.split()
        edition, collector = key.split("/", 1)
        deck[section].append({"count": int(count), "setCode": edition, "collectorNumber": collector})
output = root / ".tools/forge-migration/rules"
output.mkdir(parents=True, exist_ok=True)
messages = queue.Queue()
with tempfile.TemporaryDirectory(prefix="mtgb-forge-") as temp:
    start = Path(temp) / "start.json"
    start.write_text(json.dumps({"session": "rule-smoke", "seed": args.seed, "startingLife": 20,
                                "firstPlayerSeat": 1, "decks": [deck, deck]}), encoding="utf-8")
    environment = dict(os.environ, APPDATA=temp, LOCALAPPDATA=temp)
    with (output / "engine.log").open("w", encoding="utf-8") as errors:
        process = subprocess.Popen([str(runtime / config["java"]), "-Djava.awt.headless=true", "-cp",
                                    str(runtime / "classes") + ";" + str(runtime / "lib/*"),
                                    "mtgb.bridge.ForgeBridge", str(runtime / config["assets"]), str(start)],
                                   cwd=runtime / config["assets"], env=environment,
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors,
                                   text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW)
        def read():
            for line in process.stdout:
                messages.put(json.loads(line))
        threading.Thread(target=read, daemon=True).start()
        try:
            deadline = time.monotonic() + 60
            count = 0
            answered = set()
            mulligans = {1: 0, 2: 0}
            toggled = set()
            kept = set()
            while time.monotonic() < deadline:
                try:
                    message = messages.get(timeout=5)
                except queue.Empty:
                    if process.poll() is not None:
                        raise RuntimeError("Forge exited; inspect engine.log")
                    continue
                if message["type"] == "rejected" and message.get("error") == "stale-request":
                    continue
                if message["type"] in ("fatal", "rejected"):
                    raise RuntimeError(message.get("error"))
                if message['type'] == 'ended':
                    raise RuntimeError('Game ended before the expected rule chain')
                if message["type"] != "request":
                    continue
                request = message["request"]
                if request["requestId"] in answered:
                    continue
                answered.add(request["requestId"])
                view = message["views"][request["seat"] - 1]
                if args.mulligans and view['turn'] > 0:
                    expected = 7 - args.mulligans
                    for player in view['players']:
                        assert player['handCount'] == expected, (player['seat'], player['handCount'], expected)
                    assert all(n == args.mulligans for n in mulligans.values()), mulligans
                    print(f'PASS: both seats mulligan {args.mulligans} times, bottom cards, start turn with {expected} cards; no premature finish', flush=True)
                    break
                if view["players"][1]["life"] == 17:
                    print("PASS: saved deck, land, Lightning Bolt, target, mana and resolution 20 -> 17", flush=True)
                    (output / "result.json").write_text(json.dumps({"success": True, "requests": count,
                        "detail": "Saved deck Lightning Bolt resolved against seat 2: 20 -> 17"}), encoding="utf-8")
                    break
                options = request["options"]
                opponent = message['views'][request['seat'] - 1]['players'][2 - request['seat']]
                assert not opponent['hand'], 'Opponent hand leaked'
                if count == 4:
                    print("Opening hand:", [c["name"] for c in view["players"][0]["hand"]], flush=True)
                pick = next((o for o in options if o["id"] == "ok"), options[0])
                if args.mulligans and request['kind'] == 'InputConfirmMulligan' and mulligans[request['seat']] < args.mulligans:
                    pick = next(o for o in options if o['id'] == 'cancel')
                    mulligans[request['seat']] += 1
                if request['kind'] == 'InputLondonMulligan':
                    assert request['seat'] in kept, 'Bottom selection appeared before Keep'
                    cards = [o for o in options if o['id'].startswith('card:')]
                    own_ids = {c['id'] for c in view['players'][request['seat'] - 1]['hand']}
                    assert {int(o['id'][5:]) for o in cards} == own_ids
                    selected = [o for o in cards if o['label'].startswith('[已选回牌库底]')]
                    if args.auto_bottom:
                        pick = next(o for o in options if o['id'] == 'cancel')
                    elif selected and request['seat'] not in toggled:
                        pick = selected[0]  # Verify deselection before completing the choice.
                        toggled.add(request['seat'])
                    elif not any(o['id'] == 'ok' for o in options):
                        pick = next(o for o in cards if o not in selected)
                if request["seat"] == 1:
                    player = view["players"][0]
                    if request["kind"] == "InputPassPriority" and "MAIN" in view["phase"].upper():
                        desired = "Mountain [Hand]" if not player["battlefield"] else "Lightning Bolt [Hand]"
                        pick = next((o for o in options if o["label"] == desired), pick)
                    elif request["kind"] == "InputSelectTargets":
                        if not any(o['id'] == 'ok' for o in options):
                            pick = next((o for o in options if o["label"] == "Player 2"), pick)
                    elif request["kind"].startswith("InputPayMana"):
                        available = {c["id"] for c in player["battlefield"] if not c["tapped"]}
                        pick = next((o for o in options if o["id"] == "mana:R" or
                                     o["id"].startswith("card:") and int(o["id"][5:]) in available), pick)
                if request['kind'] == 'InputConfirmMulligan' and pick['id'] == 'ok':
                    kept.add(request['seat'])
                print(count, request["seat"], request["kind"], view["phase"], pick["label"], flush=True)
                process.stdin.write(json.dumps({"type": "respond", "session": "rule-smoke", "seat": request["seat"],
                                              "requestId": request["requestId"], "option": pick["id"]}) + "\n")
                process.stdin.flush()
                count += 1
                if count > 100:
                    raise RuntimeError("Expected rule chain did not complete in 100 requests")
            else:
                raise RuntimeError("Rule smoke timed out")
        finally:
            if process.poll() is None:
                process.stdin.write('{"type":"shutdown"}\n'); process.stdin.flush()
                try:
                    process.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()


"""Play YSOS/30 through Forge's input protocol and inspect both seat views."""
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import tempfile
import threading

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / "ForgeRuntime"
OUT = ROOT / ".tools/mystical-archive-ysos30"
TOOLS = Path(__file__).parent
NAME = "The Mystical Archive"
DECK = {"cards": [{"setCode": "YSOS", "collectorNumber": "30", "count": 1},
                  {"setCode": "10E", "collectorNumber": "335", "count": 59}]}


def run(scenario, classes):
    with tempfile.TemporaryDirectory(dir=OUT) as tmp, (OUT / (scenario + ".log")).open("w") as log:
        session = "archive-" + scenario
        start = Path(tmp) / "start.json"
        start.write_text(json.dumps(dict(session=session, fixture=scenario, decks=[DECK, DECK],
                                         firstPlayerSeat=1, startingLife=20, seed=4)))
        assets = RT / "assets"
        cp = os.pathsep.join(map(str, [classes, RT / "classes", RT / "lib/*"]))
        proc = subprocess.Popen([str(RT / "jre/bin/java.exe"), "-Xmx768m", "-XX:ActiveProcessorCount=4",
                                 "-Djava.awt.headless=true", "-cp", cp, "mtgb.bridge.MysticalArchiveFixture",
                                 str(assets), str(start)], cwd=assets, env=dict(os.environ, APPDATA=tmp, LOCALAPPDATA=tmp),
                                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding="utf8")
        messages = queue.Queue()

        def read():
            try:
                for line in proc.stdout:
                    messages.put(json.loads(line))
            finally:
                messages.put(None)

        threading.Thread(target=read, daemon=True).start()
        stage = 0
        drafted = None
        try:
            for _ in range(160):
                msg = messages.get(timeout=60)
                assert msg and msg["type"] not in ("fatal", "ended", "rejected"), msg
                if msg["type"] != "request":
                    continue
                r = msg["request"]
                view, other = msg["views"]
                own = view["players"][0]
                options = [o for o in r["options"] if not o.get("disabled")]
                ids = [o["id"] for o in options]
                kind = r["kind"]
                pick = "ok"

                def save(label):
                    snapshot = dict(view, request=r, awaitingInput=True)
                    (OUT / (scenario + "-" + label + ".json")).write_text(json.dumps(snapshot), encoding="utf8")

                def card_option(card):
                    return "card:" + str(card["id"])

                if kind == "InputMulligan":
                    pick = "keep"
                elif kind == "InputPassPriority" and r["seat"] == 1 and view["phase"] == "MAIN1" and not view["stackItems"]:
                    land = next(c for c in own["hand"] + own["battlefield"] if c["name"] == NAME)
                    if stage == 0:
                        archive_id = land["id"]
                        pick = card_option(land)
                        stage = 1
                    elif scenario in ("draft", "other-archive"):
                        if stage == 1:
                            assert drafted and len(own["exileCards"]) == 1
                            assert own["exileCards"][0]["faceDown"]
                            remembered = own["exileCards"][0]
                            assert not remembered.get("knownFacePrintingKey")
                            assert (remembered["draftedFaceName"], remembered["draftedFacePrintingKey"]) == (drafted["name"], drafted["printingKey"])
                            hidden = other["players"][0]["exileCards"][0]
                            assert hidden["faceDown"] and not hidden.get("printingKey") and hidden["name"] != drafted["name"]
                            assert not hidden.get("draftedFaceName") and not hidden.get("draftedFacePrintingKey") and not hidden.get("knownFaceName")
                            for projected in (own, other["players"][0]):
                                sources = [c for c in projected["battlefield"] if c.get("temporaryExileIds")]
                                assert len(sources) == 1 and sources[0]["id"] == archive_id and sources[0]["temporaryExileIds"] == [drafted["id"]]
                            save("exiled")
                            (OUT / (scenario + "-exiled-opponent.json")).write_text(json.dumps(other), encoding="utf8")
                            archives = [c for c in own["battlefield"] if c["name"] == NAME]
                            source = max(archives, key=lambda c: c["id"]) if scenario == "other-archive" else land
                            pick = card_option(source)
                            stage = 2
                        elif scenario == "other-archive":
                            assert len(own["exileCards"]) == 1 and own["exileCards"][0]["id"] == drafted["id"]
                            assert all(c["id"] != drafted["id"] for c in own["hand"])
                            assert sum(c["tapped"] for c in own["battlefield"]) == 3
                            save("still-exiled")
                            return
                        else:
                            assert not own["exileCards"] and any(c["id"] == drafted["id"] and c["name"] == drafted["name"] for c in own["hand"])
                            assert not any(c.get("temporaryExileIds") for c in own["battlefield"])
                            assert not other["players"][0]["hand"]
                            assert all(c["tapped"] for c in own["battlefield"] if c["land"])
                            save("returned")
                            return
                    elif scenario == "colorless":
                        assert own["manaPool"] == [0, 0, 0, 0, 0, 1] and land["tapped"]
                        save("colorless")
                        return
                    elif stage == 1:
                        assert own["manaPool"] == [1, 1, 0, 0, 0, 0], own["manaPool"]
                        original, generated = sorted((c for c in own["hand"] if c["name"] == "Mind Stone"), key=lambda c: c["id"])
                        assert card_option(original) not in ids and card_option(generated) in ids
                        tome = next(c for c in own["battlefield"] if c["name"] == "Jalum Tome")
                        assert card_option(tome) not in ids
                        save("restricted")
                        pick = card_option(generated)
                        stage = 2
                    else:
                        assert any(c["id"] == generated["id"] for c in own["battlefield"])
                        assert any(c["id"] == original["id"] for c in own["hand"])
                        assert sum(own["manaPool"]) == 0
                        save("paid")
                        return
                elif kind == "SelectCards":
                    if r.get("selectedCount"):
                        pick = "confirm-cards"
                    else:
                        offered = [o for o in options if o.get("card")]
                        if stage == 1:
                            assert len({o["card"]["name"] for o in offered}) == 3
                            assert all(o["card"].get("printingKey") and o["card"].get("currentRules") for o in offered)
                            drafted = offered[-1]["card"]
                            save("draft")
                        pick = offered[-1]["id"]
                elif kind in ("dialog", "Option", "ChooseColor"):
                    if r["message"] == "getAbilityToPlay":
                        match = {"draft": "owner's hand", "other-archive": "owner's hand",
                                 "colorless": "Add {C}", "mana": "any combination"}[scenario]
                        pick = next(o["id"] for o in options if match in o["label"])
                    else:
                        raise AssertionError(r)
                elif kind == "AssignAmount":
                    pick = {2: "amount-add:0", 1: "amount-add:1", 0: "confirm-amount"}[r["remainingCount"]]
                elif "PayMana" in kind:
                    mana = [i for i in ids if i.startswith("mana:")]
                    lands = [card_option(c) for c in own["battlefield"] if c["name"] == "Wastes" and not c["tapped"]]
                    pick = next(i for i in mana + lands if i in ids)
                elif kind not in ("InputPassPriority", "InputConfirmMulligan", "InputConfirm"):
                    raise AssertionError((stage, kind, r))
                assert pick in ids, (stage, kind, pick, ids)
                proc.stdin.write(json.dumps(dict(type="respond", session=session, seat=r["seat"],
                                                 requestId=r["requestId"], option=pick)) + "\n")
                proc.stdin.flush()
            raise AssertionError("No terminal position")
        except Exception:
            if "msg" in locals() and msg:
                (OUT / (scenario + "-failure.json")).write_text(json.dumps(msg), encoding="utf8")
            raise
        finally:
            proc.kill()
            proc.wait()


if __name__ == "__main__":
    classes = OUT / "classes"
    classes.mkdir(parents=True, exist_ok=True)
    subprocess.run(["javac", "-encoding", "UTF-8", "-cp", os.pathsep.join(map(str, [RT / "classes", RT / "lib/*"])),
                    "-d", str(classes), str(TOOLS / "tests/RoyalStateFixture.java"),
                    str(TOOLS / "tests/MysticalArchiveFixture.java")], check=True)
    for case in sys.argv[1:] or ["draft", "other-archive", "mana", "colorless"]:
        run(case, classes)
        print("PASS", case, flush=True)

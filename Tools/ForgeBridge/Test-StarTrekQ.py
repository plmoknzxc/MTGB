"""Exercise Star Trek's granted, free-cast and assimilate abilities with native Forge."""

import json
import os
from pathlib import Path
import queue
import shutil
import subprocess
import sys
import tempfile
import threading

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / "ForgeRuntime"
OUT = ROOT / ".tools/star-trek-zh-1120"
STACKING = "Q, Stacking the Deck"
HOST = "Q, Your Illustrious Host"
QUEEN = "The Borg Queen"
LAND = "Q Continuum"


def run(case, classes):
    copy = case in ("stacking", "stacking-paid")
    paid_copy = case == "stacking-paid"
    copied_name = "Grizzly Bears" if paid_copy else "Memnite"
    deck = {
        "cards": [
            {
                "setCode": "10E" if paid_copy else "SOM",
                "collectorNumber": "268" if paid_copy else "174",
                "count": 60,
            }
        ]
    }
    faeries = ["Cloud Sprite", "Zephyr Sprite", "Faerie Seer"]
    own = (
        {"Hand": [STACKING, "Island"]}
        if copy
        else {
            "host": {
                "Battlefield": [HOST, *faeries],
                "Library": ["Mountain", "Shivan Dragon"],
            },
            "queen": {"Hand": [QUEEN]},
            "continuum": {"Hand": [LAND], "Battlefield": ["Grizzly Bears"]},
        }[case]
    )
    other = {"Library": ["Mountain", "Grizzly Bears"]} if case == "queen" else {}
    folder = OUT / case
    folder.mkdir(exist_ok=True)
    with (
        tempfile.TemporaryDirectory(dir=OUT) as tmp,
        (folder / "engine.log").open("w") as log,
    ):
        spec = dict(
            actor=1,
            session=case,
            seed=4,
            decks=[deck, deck],
            firstPlayerSeat=1,
            startingLife=20,
            own=own,
            other=other,
            auditDir=tmp,
        )
        start = Path(tmp) / "start.json"
        start.write_text(json.dumps(spec), encoding="utf8")
        proc = subprocess.Popen(
            [
                str(RT / "jre/bin/java.exe"),
                "-Xmx512m",
                "-XX:ActiveProcessorCount=2",
                "-Djava.awt.headless=true",
                "-cp",
                os.pathsep.join(map(str, [classes, RT / "classes", RT / "lib/*"])),
                "mtgb.bridge.StateBranchFixture",
                str(RT / "assets"),
                str(start),
            ],
            cwd=RT / "assets",
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=log,
            text=True,
            encoding="utf8",
            env=dict(os.environ, APPDATA=tmp, LOCALAPPDATA=tmp),
        )
        messages = queue.Queue()

        def read():
            try:
                for line in proc.stdout:
                    messages.put(json.loads(line))
            finally:
                messages.put(None)

        threading.Thread(target=read, daemon=True).start()
        stage = 0
        original = payment = None
        tapped_before_copy = 0
        with (folder / "inputs.txt").open("w", encoding="utf8") as trace:
            try:
                for step in range(100):
                    m = messages.get(timeout=45)
                    assert m and m["type"] not in ("rejected", "ended", "fatal"), m
                    if m["type"] != "request":
                        continue
                    req = m["request"]
                    view = m["views"][0]
                    p = view["players"][0]
                    kind = req["kind"]
                    field = p["battlefield"]
                    exiled = p["exileCards"]
                    opts = [o for o in req["options"] if not o.get("disabled")]
                    ids = [o["id"] for o in opts]

                    def pickcard(c):
                        return "card:" + str(c["id"])

                    def named(cards, name):
                        return next(c for c in cards if c["name"] == name)

                    summary = [
                        (o["id"], o["label"])
                        for o in opts
                        if not o["label"].startswith(
                            (
                                "Plains [",
                                "Mountain [",
                                "Swamp [",
                                "Forest [",
                                "Island [",
                            )
                        )
                    ]
                    print(
                        step,
                        stage,
                        kind,
                        req["seat"],
                        view["phase"],
                        str(req["message"]),
                        summary,
                        file=trace,
                        flush=True,
                    )
                    if req["seat"] == 1 and stage > 0:
                        (folder / f"frame-{step}.json").write_text(
                            json.dumps(dict(view, request=req, awaitingInput=True)),
                            encoding="utf8",
                        )
                    pick = "ok"
                    if (
                        kind == "InputPassPriority"
                        and req["seat"] == 1
                        and view["phase"] == "MAIN1"
                        and not view.get("stackItems")
                    ):
                        if stage == 0:
                            source_name = (
                                STACKING
                                if copy
                                else {"host": HOST, "queen": QUEEN, "continuum": LAND}[
                                    case
                                ]
                            )
                            pick = pickcard(
                                named(
                                    field if case == "host" else p["hand"], source_name
                                )
                            )
                            payment = named(p["hand"], "Island")["id"] if copy else None
                            stage = 1
                        elif copy and stage == 1:
                            original = named(exiled, copied_name)["id"]
                            tapped_before_copy = sum(
                                c["land"] and c["tapped"] for c in field
                            )
                            pick = "card:" + str(original)
                            assert any(c["name"] == STACKING for c in field)
                            stage = 2
                        elif copy:
                            clone = named(field, copied_name)
                            assert clone["id"] != original and {original, payment} <= {
                                c["id"] for c in exiled
                            }
                            assert sum(
                                c["land"] and c["tapped"] for c in field
                            ) == tapped_before_copy + (2 if paid_copy else 0)
                            print(
                                f"PASS: exiled a hand card, cast a copy of {copied_name} for its mana cost, original remained exiled"
                            )
                            return
                        elif case == "host":
                            assert (
                                sum(c["name"] in faeries and c["tapped"] for c in field)
                                == 3
                            )
                            assert any(
                                c["name"] == "Shivan Dragon" for c in field
                            ) and any(c["name"] == "Mountain" for c in exiled)
                            assert not any(c["land"] and c["tapped"] for c in field)
                            print(
                                "PASS: tap three distinct Faeries, exile land and dragon, cast dragon without spending mana"
                            )
                            return
                        elif case == "continuum":
                            bear = named(field, "Grizzly Bears")
                            assert not named(field, LAND)["tapped"]
                            assert (
                                str(bear["power"]) == "3"
                                and str(bear["toughness"]) == "3"
                            )
                            assert "VIGILANCE" in bear["keywords"], bear["keywords"]
                            print(
                                "PASS: Q Continuum entered untapped, chose pump mode, 2/2 target became 3/3 with vigilance"
                            )
                            return
                        else:
                            bear = named(field, "Grizzly Bears")
                            assert (
                                "Borg" in bear["currentType"]
                                and "Artifact" in bear["currentType"]
                                and "Bear" not in bear["currentType"]
                            )
                            assert (
                                str(bear["power"]) == "5"
                                and str(bear["toughness"]) == "5"
                            ), bear
                            assert any(
                                counter["kind"] == "P1P1" and counter["count"] == 1
                                for counter in bear["counters"]
                            )
                            print(
                                "PASS: target opponent, assimilate their 2/2, control a 5/5 Borg artifact with one +1/+1 counter"
                            )
                            return
                    elif kind == "ChooseModes" and "confirm-modes" in ids:
                        pick = "confirm-modes"
                    elif kind == "ChooseModes" and any(
                        "gets +1/+1" in o["label"] for o in opts
                    ):
                        pick = next(o["id"] for o in opts if "gets +1/+1" in o["label"])
                    elif (
                        case == "continuum"
                        and kind == "InputSelectTargets"
                        and not req.get("selectedCount")
                    ):
                        pick = pickcard(named(field, "Grizzly Bears"))
                    elif kind.startswith("InputSelect") or kind == "dialog":
                        if (
                            copy
                            and "card:" + str(payment) in ids
                            and req.get("selectedCount", 0) == 0
                        ):
                            pick = "card:" + str(payment)
                        elif (
                            case == "queen"
                            and "player:2" in ids
                            and not req.get("selectedCount")
                        ):
                            pick = "player:2"
                        elif case == "host":
                            available = [
                                pickcard(c)
                                for c in field
                                if c["name"] in faeries
                                and pickcard(c) in ids
                                and not next(
                                    o for o in opts if o["id"] == pickcard(c)
                                ).get("selected")
                            ]
                            if available and req.get("selectedCount", 0) < 3:
                                pick = available[0]
                            elif "ok" not in ids:
                                pick = next(x for x in ids if x != "cancel")
                        elif "ok" not in ids:
                            pick = next(x for x in ids if x != "cancel")
                    elif kind == "InputConfirm" and "yes" in ids:
                        pick = "yes"
                    elif kind.startswith("InputMulligan"):
                        pick = "keep" if "keep" in ids else "ok"
                    elif "ok" not in ids:
                        pick = next(x for x in ids if x not in ("cancel", "no"))
                    assert pick in ids, (kind, pick, ids)
                    proc.stdin.write(
                        json.dumps(
                            dict(
                                type="respond",
                                session=case,
                                seat=req["seat"],
                                requestId=req["requestId"],
                                option=pick,
                            )
                        )
                        + "\n"
                    )
                    proc.stdin.flush()
                raise AssertionError("request budget exhausted")
            finally:
                proc.kill()
                proc.wait()


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=OUT) as classes:
        javac = shutil.which("javac") or "C:/Program Files/Java/jdk-17/bin/javac.exe"
        subprocess.run(
            [
                javac,
                "--release",
                "17",
                "-encoding",
                "UTF-8",
                "-cp",
                os.pathsep.join(map(str, [RT / "classes", RT / "lib/*"])),
                "-d",
                classes,
                str(ROOT / "Tools/ForgeBridge/tests/StateBranchFixture.java"),
            ],
            check=True,
        )
        for case in sys.argv[1:] or [
            "stacking",
            "stacking-paid",
            "host",
            "queen",
            "continuum",
        ]:
            run(case, classes)

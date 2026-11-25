"""Exercise puzzle victory conditions against the installed Forge runtime."""
import argparse
import json
import os
import queue
import subprocess
import tempfile
import threading
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EXAMPLE = json.loads((ROOT / "Puzzles/Examples/clear-and-protect.json").read_text(encoding="utf-8"))
CLEAR, KEEP = EXAMPLE["goal"]["conditions"]
DAMAGE = dict(metric="damageTakenThisTurn", player="opponent", op="atLeast", value=3)
LIFE = dict(metric="life", player="opponent", op="atMost", value=17)
IMPOSSIBLE = dict(metric="life", player="you", op="atLeast", value=90)


@dataclass
class Case:
    name: str
    mode: str
    conditions: list
    target: str = ""
    opponent_life: int = 20
    winner: int = 1
    timing: str = "state"


CASES = [
    Case("clear-and-protect", "all", [dict(CLEAR, cardType="Creature"), KEEP], "bear"),
    Case("damage-and-life", "all", [DAMAGE, LIFE], "player"),
    Case("one-alternative", "any", [IMPOSSIBLE, CLEAR], "bear"),
    Case("one-insufficient", "all", [CLEAR, IMPOSSIBLE], "bear", winner=2),
    Case("opening-state", "all", [KEEP]),
    Case("time-limit", "any", [DAMAGE, IMPOSSIBLE], winner=2),
    Case("lethal-with-goal", "all", [DAMAGE, LIFE], "player", 3),
    Case("lethal-without-goal", "all", [CLEAR, KEEP], "player", 3, 2),
    Case("win-required", "all", [KEEP], winner=2, timing="win"),
    Case("win-and-protect", "all", [KEEP], "player", 3, timing="win"),
    Case("survive-and-protect", "all", [KEEP], timing="survive"),
    Case("survive-insufficient", "all", [IMPOSSIBLE], winner=2, timing="survive"),
    Case("survival-early-win", "all", [KEEP], "player", 3, timing="survive"),
    Case("original-win", "win", [], "player", 3),
    Case("original-survive", "survive", []),
]


def config_for(case):
    puzzle = {
        "title": case.name, "goal": "Survive" if case.mode == "survive" else "Win", "turns": 1,
        "state": ["turn=1", "activeplayer=human", "activephase=MAIN1", "humanlife=20",
                  f"ailife={case.opponent_life}", "humanhand=Lightning Bolt",
                  "humanbattlefield=Mountain|NoETBTrigs;Wind Drake|NoETBTrigs",
                  "aibattlefield=Grizzly Bears|NoETBTrigs", "ailibrary=Forest;Forest;Forest",
                  "humanlibrary=Island;Island;Island"],
        "attachments": [],
    }
    if case.mode in ("all", "any"):
        puzzle["objective"] = {"mode": case.mode, "conditions": case.conditions, "checkAt": case.timing}
    return {"session": case.name, "seed": 11431, "startingLife": 20, "formatId": "none",
            "firstPlayerSeat": 1, "aiSeat": 2, "localPractice": True,
            "decks": [{"cards": [], "sideboard": []} for _ in range(2)], "localPuzzle": json.dumps(puzzle)}


def read_protocol(stream, inbox):
    try:
        for line in stream:
            inbox.put(line)
    finally:
        inbox.put(None)


def exercise(case, process):
    inbox = queue.Queue()
    threading.Thread(target=read_protocol, args=(process.stdout, inbox), daemon=True).start()
    cast = targeted = False
    for _ in range(240):
        line = inbox.get(timeout=40)
        if line is None:
            raise RuntimeError("Forge quit before sending a result")
        message = json.loads(line)
        if message["type"] == "ended":
            assert message["winnerSeat"] == case.winner, message["winnerSeat"]
            assert not case.target or (cast and targeted), "Ended before the specified action"
            if case.timing == "survive" and not case.target:
                final = message["views"][0]
                assert final["turn"] == 2 and final["phase"] == "UPKEEP", (final["turn"], final["phase"])
            return
        if message["type"] in ("fatal", "rejected"):
            raise RuntimeError(message)
        if message["type"] != "request":
            continue
        request = message["request"]
        players = message["views"][0]["players"]
        options = [o["id"] for o in request["options"] if not o["disabled"]]
        if request["kind"] == "InputPassPriority" and case.target and not cast:
            bolt = next(c for c in players[0]["hand"] if c["name"] == "Lightning Bolt")
            pick = f"card:{bolt['id']}"
            cast = True
        elif request["kind"] == "InputSelectTargets" and not targeted:
            pick = (f"card:{players[1]['battlefield'][0]['id']}" if case.target == "bear"
                    else f"player:{players[1]['targetId']}")
            targeted = True
        else:
            pick = "ok"
        assert pick in options, (request["kind"], pick, options)
        response = {"session": case.name, "type": "respond", "seat": request["seat"],
                    "requestId": request["requestId"], "option": pick}
        process.stdin.write(json.dumps(response) + "\n")
        process.stdin.flush()
    raise RuntimeError("No puzzle result")


def run(runtime, case):
    with tempfile.TemporaryDirectory(prefix="mtgb-puzzle-") as temp:
        config = Path(temp) / "match.json"
        config.write_text(json.dumps(config_for(case)), encoding="utf-8")
        command = [str(runtime / "jre/bin/java"), "-Xmx320m", "-XX:ActiveProcessorCount=2",
                   "-Dmtgb.bridge.lazyCatalog=true", "-Djava.awt.headless=true", "-Dfile.encoding=UTF-8",
                   f"-Duser.home={temp}", "-cp", os.pathsep.join(map(str, [runtime/"classes", runtime/"lib/*"])),
                   "mtgb.bridge.ForgeBridge", str(runtime/"assets"), str(config)]
        with (Path(temp) / "forge.log").open("w+", encoding="utf-8") as log:
            with subprocess.Popen(command, cwd=runtime/"assets", stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=log, encoding="utf-8") as process:
                try:
                    exercise(case, process)
                except Exception as error:
                    log.seek(0)
                    raise RuntimeError(f"{case.name}: {log.read()[-1000:]}") from error
                finally:
                    process.kill()
                    process.wait()
        print("PASS", case.name, flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runtime", nargs="?", type=Path, default=ROOT / "ForgeRuntime")
    runtime = parser.parse_args().runtime.resolve()
    with ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(lambda case: run(runtime, case), CASES))

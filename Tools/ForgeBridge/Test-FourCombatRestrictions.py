"""Exercise declaration constraints through real four-seat requests; save Unity-replayable states."""
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / "ForgeRuntime"
OUT = ROOT / ".tools/four-seat-combat-rules" / str(time.time_ns())
DECK = {"cards": [{"setCode": "10E", "collectorNumber": "364", "count": 99}],
        "commanders": [{"setCode": "CHK", "collectorNumber": "19", "count": 1}], "sideboard": []}


class Match:
    def __init__(self, process, name, folder):
        self.process, self.name, self.folder = process, name, folder
        self.messages = queue.Queue()
        def read():
            try:
                for line in process.stdout:
                    self.messages.put(json.loads(line))
            finally:
                self.messages.put(None)
        threading.Thread(target=read, daemon=True).start()
        self.next()

    def next(self):
        while True:
            msg = self.messages.get(timeout=35)
            assert msg and msg["type"] not in ("fatal", "ended", "rejected"), msg
            if msg["type"] == "request":
                self.request = msg["request"]
                self.views = msg["views"]
                self.view = self.views[self.request["seat"] - 1]
                self.ids = {o["id"] for o in self.request["options"] if not o.get("disabled")}
                return

    def pick(self, option, group=None):
        assert option in self.ids, (option, self.ids)
        response = dict(type="respond", session=self.name, seat=self.request["seat"],
                        requestId=self.request["requestId"], option=option, order=group or [])
        self.process.stdin.write(json.dumps(response) + "\n")
        self.process.stdin.flush()
        self.next()

    def until(self, predicate):
        for _ in range(100):
            if predicate():
                return
            if self.request["kind"] == "AssignAmount" and "ok" not in self.ids:
                options = [o["id"] for o in self.request["options"] if not o.get("disabled")]
                self.pick("confirm-amount" if "confirm-amount" in options else next(o for o in options if o.startswith("amount-add:")))
            else:
                self.pick("ok")
        raise AssertionError("Did not reach expected phase")

    def save(self, name):
        state = dict(self.view, awaitingInput=True, request=self.request)
        (self.folder / (name + ".json")).write_text(json.dumps(state, ensure_ascii=False), encoding="utf8")

    def issue(self, code=None, card=None):
        rules = self.request["combatDeclaration"]
        assert rules.get("issue") == code, (code, rules)
        if card is not None:
            assert rules["cardId"] == card, rules
        return rules

    def card(self, seat, name, index=0):
        return [c["id"] for c in self.view["players"][seat-1]["battlefield"] if c["name"] == name][index]

    def attack(self, card, seat=2):
        return f"attack:{card}:player:{self.view['players'][seat-1]['targetId']}"

    def begin_blocks(self):
        self.pick("ok")
        self.until(lambda: self.request["kind"] == "InputBlock" and self.request["seat"] == 2)


def required(m):
    ram, pack = m.card(1, "Juggernaut"), m.card(1, "Mogg Flunkies")
    wall, idle = m.card(1, "Wall of Wood"), m.card(1, "Flowering Lumberknot")
    assert all(not opt.startswith((f"attack:{wall}:", f"attack:{idle}:")) for opt in m.ids)
    m.issue("mustAttack", ram)
    m.save("must-attack")
    m.pick(m.attack(pack))
    m.issue("NOT_ALONE", pack)
    m.save("attack-together")
    m.pick(m.attack(ram))
    m.issue()
    m.begin_blocks()
    screen, bear = m.card(2, "Razorgrass Screen"), m.card(2, "Grizzly Bears")
    assert f"block:{screen}:{ram}" not in m.ids and f"block:{screen}:{pack}" in m.ids
    m.issue("mustBlock", screen)
    m.save("must-block")
    # Even without the Unity guard, invalid OK must reoffer the declaration.
    m.pick("ok")
    m.until(lambda: m.request["kind"] == "InputBlock")
    m.issue("mustBlock", screen)
    m.pick(f"block:{screen}:{pack}")
    m.issue()
    m.pick(f"block:{bear}:{ram}")
    m.issue()
    m.pick("ok")


def ranges(m):
    names = ["Boggart Brute", "Phyrexian Colossus", "Charging Rhino", "Tormented Soul", "Dread Warlock", "Wind Drake"]
    brute, colossus, rhino, soul, warlock, drake = [m.card(1, name) for name in names]
    for attacker in (brute, colossus, rhino, soul, warlock, drake):
        m.pick(m.attack(attacker))
    m.begin_blocks()
    bears = [m.card(2, "Grizzly Bears", i) for i in range(3)]
    black, tern, unable = [m.card(2, name) for name in ("Walking Corpse", "Welkin Tern", "Tormented Soul")]
    limits = {l["id"]: l for l in m.issue()["limits"]}
    assert limits[brute]["min"] == 2 and limits[colossus]["min"] == 3 and limits[rhino]["max"] == 1
    assert limits[soul]["unblockable"] and all(not o.startswith(f"block:{unable}:") for o in m.ids)
    assert all(not o.endswith(f":{soul}") for o in m.ids if o.startswith("block:"))
    assert f"block:{black}:{warlock}" in m.ids and f"block:{bears[0]}:{warlock}" not in m.ids
    assert f"block:{tern}:{drake}" in m.ids and f"block:{tern}:{brute}" not in m.ids
    m.save("pairs")
    for i, bear in enumerate(bears):
        m.pick(f"block:{bear}:{colossus}")
        if i < 2:
            assert m.issue("blockMin", colossus)["amount"] == 3
            m.save("three-needs-" + str(2-i))
        else:
            m.issue()
    for bear in bears:
        m.pick(f"block-remove:{bear}")
    m.issue()  # Minimum is conditional on choosing to block.
    m.pick(f"block:{bears[0]}:{brute}")
    assert m.issue("blockMin", brute)["amount"] == 2
    m.pick(f"block:{bears[1]}:{brute}")
    m.issue()
    m.pick(f"block:{bears[2]}:{rhino}")
    assert f"block:{black}:{rhino}" not in m.ids
    m.pick(f"block:{black}:{warlock}")
    m.pick(f"block:{tern}:{drake}")
    m.issue()
    m.save("ranges-legal")
    m.pick("ok")


def together(m):
    target = m.card(1, "Hill Giant")
    m.pick(m.attack(target))
    m.begin_blocks()
    pack, bear = m.card(2, "Mogg Flunkies"), m.card(2, "Grizzly Bears")
    m.pick(f"block:{pack}:{target}")
    m.issue("blockTogether", pack)
    m.save("block-together")
    m.pick(f"block:{bear}:{target}")
    m.issue()
    m.pick("ok")


def caps(m):
    attackers = [m.card(1, "Hill Giant", i) for i in range(3)]
    assert m.issue()["seatLimit"] == 2
    for attacker in attackers:
        m.pick(m.attack(attacker))
    m.issue("attackMax")
    m.pick(f"attack-remove:{attackers[-1]}")
    m.issue()
    m.begin_blocks()
    bears = [m.card(2, "Grizzly Bears", i) for i in range(3)]
    assert m.issue()["seatLimit"] == 2
    m.pick(f"block:{bears[0]}:{attackers[0]}", bears[:2])
    assert all(not o.startswith(f"block:{bears[2]}:") for o in m.ids)
    m.save("two-blocker-cap")
    m.pick(f"block-remove:{bears[1]}")
    m.pick(f"block:{bears[2]}:{attackers[1]}")
    m.issue()
    m.pick("ok")


def protected(m):
    attacker = m.card(1, "Grizzly Bears")
    assert m.attack(attacker, 2) not in m.ids and m.attack(attacker, 3) in m.ids and m.attack(attacker, 4) in m.ids
    victim = m.view["players"][1]["targetId"]
    assert {"id":victim,"kind":"player"} in m.issue()["destinations"]
    m.save("protected-player")
    m.pick(m.attack(attacker, 3))
    m.pick("ok")


def competing(m):
    first, second = (m.card(1,"Juggernaut",i) for i in range(2))
    assert not m.issue("attackRequirement").get("cardId")
    for card in (first, second):
        m.pick(m.attack(card))
        m.issue()
        m.pick(f"attack-remove:{card}")
    m.pick(m.attack(second))
    m.pick("ok")


def unable_to_fulfil(m):
    brute, screen = m.card(1,"Boggart Brute"), m.card(2,"Razorgrass Screen")
    m.pick(m.attack(brute))
    m.begin_blocks()
    m.issue()  # A lone mandatory blocker cannot legally meet menace by itself.
    m.save("no-blocks-legal")
    if f"block:{screen}:{brute}" in m.ids:
        m.pick(f"block:{screen}:{brute}")
        m.issue("blockMin", brute)
        m.pick(f"block-remove:{screen}")
    m.issue()
    m.pick("ok")


def blocker_capacity(m):
    a, b, c = [m.card(1,"Goblin Piker",i) for i in range(3)]
    for attacker in (a,b,c):
        m.pick(m.attack(attacker))
    m.begin_blocks()
    giant=m.card(2,"Two-Headed Giant of Foriys")
    m.pick(f"block:{giant}:{a}")
    m.pick(f"block:{giant}:{b}")
    assert f"block:{giant}:{c}" not in m.ids
    m.save("two-attackers-blocked")
    m.pick(f"block:{giant}:{a}")  # Release only this pairing, keeping the second.
    m.pick(f"block:{giant}:{c}")
    creature=next(c for c in m.view["players"][1]["battlefield"] if c["id"]==giant)
    assert set(creature["blockedAttackerIds"])=={b,c}
    m.issue()
    m.pick("ok")


def conflicting_limits(m):
    rhino, ordinary = m.card(1,"Charging Rhino"), m.card(1,"Grizzly Bears")
    m.pick(m.attack(rhino))
    m.pick(m.attack(ordinary))
    m.begin_blocks()
    limits = {l["id"]: l for l in m.issue()["limits"]}
    assert (limits[rhino]["min"],limits[rhino]["max"],limits[rhino]["unblockable"]) == (2,1,True)
    assert all(not o.endswith(f":{rhino}") for o in m.ids if o.startswith("block:"))
    blockers=[m.card(2,"Grizzly Bears",i) for i in range(2)]
    m.pick(f"block:{blockers[0]}:{ordinary}",blockers)
    m.issue()
    m.save("conflicting-limits")
    m.pick("ok")


def forced_defence(m):
    gorm = m.card(1,"Gorm the Great")
    m.pick(m.attack(gorm))
    m.begin_blocks()
    rules=m.issue("blockRequirement")
    assert next(l for l in rules["limits"] if l["id"]==gorm)["requiredIfAble"]=="two"
    bears=[c["id"] for c in m.view["players"][1]["battlefield"] if c["name"]=="Grizzly Bears"]
    m.pick(f"block:{bears[0]}:{gorm}")
    if len(bears)>1:
        m.issue("blockRequirement")
        m.save("forced-second-blocker")
        m.pick(f"block:{bears[1]}:{gorm}")
    m.issue()
    m.save("forced-legal")
    m.pick("ok")


CASES = [(required, ["Juggernaut","Mogg Flunkies","Wall of Wood","Flowering Lumberknot"], ["Razorgrass Screen","Grizzly Bears"]),
         (ranges, ["Boggart Brute","Phyrexian Colossus","Charging Rhino","Tormented Soul","Dread Warlock","Wind Drake"],
          ["Grizzly Bears"]*3+["Walking Corpse","Welkin Tern","Tormented Soul"]),
         (together, ["Hill Giant"], ["Mogg Flunkies","Grizzly Bears"]),
         (caps, ["Hill Giant"]*3+["Caverns of Despair"], ["Grizzly Bears"]*3),
         (protected, ["Grizzly Bears"], ["Blazing Archon"]),
         (competing, ["Juggernaut","Juggernaut","Dueling Grounds"], []),
         (unable_to_fulfil, ["Boggart Brute"], ["Razorgrass Screen"]),
         (blocker_capacity, ["Goblin Piker"]*3, ["Two-Headed Giant of Foriys"]),
         (conflicting_limits, ["Charging Rhino","Grizzly Bears","Goblin War Drums"], ["Grizzly Bears"]*2),
         (forced_defence, ["Gorm the Great"], ["Grizzly Bears"]*3),
         (forced_defence, ["Gorm the Great"], ["Grizzly Bears"])]


def run():
    OUT.mkdir(parents=True)
    print(OUT, flush=True)
    classes = OUT / "classes"
    classes.mkdir()
    cp = os.pathsep.join(map(str,[RT/"classes",RT/"lib/*"]))
    fixtures = ROOT/"Tools/ForgeBridge/tests"
    subprocess.run(["javac","-encoding","UTF-8","-cp",cp,"-d",str(classes),
                    str(fixtures/"StateBranchFixture.java"),str(fixtures/"CombatKeywordFixture.java")],check=True)
    passed = []
    for check, own, other in CASES:
        case = check.__name__ + ("-one" if check==forced_defence and len(other)==1 else "")
        folder = OUT/case
        folder.mkdir()
        audit = folder/"audit"
        audit.mkdir()
        start = folder/"start.json"
        start.write_text(json.dumps(dict(session=check.__name__, formatId="commander", auditDir=str(audit),
            seed=4,startingLife=40,firstPlayerSeat=1,decks=[DECK]*4,own={"Battlefield":own},other={"Battlefield":other})), encoding="utf8")
        with tempfile.TemporaryDirectory() as tmp, (folder/"engine.log").open("w",encoding="utf8") as log:
            with subprocess.Popen([str(RT/"jre/bin/java"),"-Xmx512m","-XX:ActiveProcessorCount=4","-Djava.awt.headless=true",
                    "-cp",str(classes)+os.pathsep+cp,"mtgb.bridge.CombatKeywordFixture",str(RT/"assets"),str(start)],
                    cwd=RT/"assets",env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp),
                    stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding="utf8") as p:
                try:
                    match=Match(p,check.__name__,folder)
                    match.until(lambda: match.request["kind"]=="InputAttack")
                    check(match)
                    match.until(lambda: match.view["phase"]=="MAIN2" and match.request["kind"]=="InputPassPriority")
                    match.save("resolved")
                    passed.append(case)
                    print("PASS",case,flush=True)
                finally:
                    if p.poll() is None:
                        p.stdin.write('{"type":"shutdown"}\n')
                        p.stdin.flush()
                        try: p.wait(5)
                        except subprocess.TimeoutExpired: p.kill()
    runtime=json.loads((RT/"runtime.json").read_text(encoding="utf-8-sig"))
    (OUT/"result.json").write_text(json.dumps(dict(runtimeHash=runtime["runtimeHash"],passed=passed),indent=2),encoding="utf8")

if __name__ == "__main__":
    run()

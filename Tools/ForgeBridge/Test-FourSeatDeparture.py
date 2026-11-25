"""Keep seat identities through a concession, a targeted spell and trample damage."""
import json
import os
from pathlib import Path
import runpy
import subprocess
import tempfile

combat = runpy.run_path(str(Path(__file__).with_name("Test-FourCombatRestrictions.py")))
RT, ROOT, Match = (combat[key] for key in ("RT", "ROOT", "Match"))


def exercise(m):
    m.until(lambda: m.request["seat"] == 2)
    m.process.stdin.write(json.dumps(dict(type="concede", session=m.name, seat=2)) + "\n")
    m.process.stdin.flush()
    m.next()
    m.until(lambda: m.view["phase"] == "MAIN1" and m.request["seat"] == 1)
    players = m.view["players"]
    fourth = players[3]["targetId"]
    bolt = next(c["id"] for c in players[0]["hand"] if c["name"] == "Lightning Bolt")
    m.pick(f"card:{bolt}")
    m.pick(f"player:{fourth}")
    m.until(lambda: bool(m.view.get("stackItems")) and m.request["kind"] == "InputPassPriority")
    target = m.view["stackItems"][0]["targets"][0]
    assert target["id"] == fourth and target["playerSeat"] == 4, target
    assert [p["seat"] for p in m.view["players"] if p.get("eliminated")] == [2]
    m.save("target-seat-four")
    frames = [dict(time=0, views=m.views)]
    m.until(lambda: m.request["kind"] == "InputAttack")
    assert m.view["players"][3]["life"] == 37
    dinosaur = m.card(1, "Colossal Dreadmaw")
    bear = m.card(4, "Grizzly Bears")
    m.pick(m.attack(dinosaur, 4))
    m.pick("ok")
    m.until(lambda: m.request["kind"] == "InputBlock" and m.request["seat"] == 4)
    m.pick(f"block:{bear}:{dinosaur}")
    m.pick("ok")
    m.until(lambda: m.request["kind"] == "AssignAmount")
    allocations = m.request["allocations"]
    defender = next(row for row in allocations if row["targetKind"] == "player")
    blocker = next(row for row in allocations if row["targetKind"] == "card")
    assert defender["targetId"] == fourth and defender["playerSeat"] == 4, defender
    m.save("allocation-seat-four")
    frames.append(dict(time=.25, views=m.views))
    for _ in range(2):
        m.pick(f"amount-add:{blocker['id']}")
    for _ in range(4):
        m.pick(f"amount-add:{defender['id']}")
    m.pick("confirm-amount")
    m.until(lambda: m.view["phase"] == "MAIN2")
    assert [p["life"] for p in m.view["players"]] == [40, 40, 40, 33]
    assert all(c["id"] != bear for c in m.view["players"][3]["battlefield"])
    m.save("resolved")
    frames.append(dict(time=.25, views=m.views))
    (m.folder / "frames.json").write_text(json.dumps(frames), encoding="utf8")


def run():
    folder = combat["OUT"] / "seat-departure"
    folder.mkdir(parents=True)
    print(folder, flush=True)
    classes, audit = folder / "classes", folder / "audit"
    classes.mkdir()
    audit.mkdir()
    fixtures = Path(__file__).with_name("tests")
    cp = os.pathsep.join(map(str, [RT / "classes", RT / "lib/*"]))
    subprocess.run(["javac", "-encoding", "UTF-8", "-cp", cp, "-d", str(classes),
                    str(fixtures / "StateBranchFixture.java"), str(fixtures / "CombatKeywordFixture.java")], check=True)
    name = "seat-departure"
    config = dict(session=name, formatId="commander", seed=4, startingLife=40, firstPlayerSeat=1,
                  auditDir=str(audit), decks=[combat["DECK"]] * 4, positions=[
                      {"Hand": ["Lightning Bolt"], "Battlefield": ["Mountain", "Colossal Dreadmaw"]},
                      {}, {}, {"Battlefield": ["Grizzly Bears"]}])
    start = folder / "start.json"
    start.write_text(json.dumps(config), encoding="utf8")
    with tempfile.TemporaryDirectory() as tmp, (folder / "engine.log").open("w", encoding="utf8") as log:
        with subprocess.Popen([str(RT / "jre/bin/java"), "-Xmx512m", "-XX:ActiveProcessorCount=4",
                               "-Djava.awt.headless=true", "-cp", str(classes) + os.pathsep + cp,
                               "mtgb.bridge.CombatKeywordFixture", str(RT / "assets"), str(start)],
                              cwd=RT / "assets", env=dict(os.environ, APPDATA=tmp, LOCALAPPDATA=tmp),
                              stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding="utf8") as process:
            try:
                exercise(Match(process, name, folder))
            finally:
                if process.poll() is None:
                    process.stdin.write('{"type":"shutdown"}\n')
                    process.stdin.flush()
                    try:
                        process.wait(5)
                    except subprocess.TimeoutExpired:
                        process.kill()
    print("PASS: seat 2 conceded, seat 4 took 3 spell damage and 4 trample damage")


if __name__ == "__main__":
    run()

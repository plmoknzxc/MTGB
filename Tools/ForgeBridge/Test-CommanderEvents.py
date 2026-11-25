"""Four-seat Magic Presents regression on the installed Forge bridge. Uses only temporary files."""
import json
import os
from pathlib import Path
import runpy
import subprocess
import tempfile

combat = runpy.run_path(str(Path(__file__).with_name("Test-FourCombatRestrictions.py")))
runtime = combat["RT"]


def capture_event(name, view):
    folder = os.environ.get("MTGB_EVENT_CAPTURE")
    if folder:
        (Path(folder) / (name + ".json")).write_text(json.dumps(view, ensure_ascii=False), encoding="utf8")


def loki(match, block):
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    assert match.request["seat"] == match.view["lokiSeat"] == 1
    assert [p["life"] for p in match.view["players"]] == [60] * 4
    choices = match.request["options"]
    assert len([o for o in choices if o["id"].startswith("select:")]) == 10
    match.pick(next(o["id"] for o in choices if "壮丽幻象" in o["label"]))
    match.pick(next(k for k in match.ids if k == "ok" or "confirm" in k))
    match.until(lambda: match.request["kind"] == "InputAttack")
    creatures = match.view["players"][0]["battlefield"]
    assert sum(c["name"].startswith("Illusion") and str(c["power"]) == str(c["toughness"]) == "3" for c in creatures) == 3
    bear = match.card(1, "Grizzly Bears")
    match.pick(match.attack(bear, 2))
    match.pick("ok")
    if block:
        match.until(lambda: match.request["kind"] == "InputBlock" and match.request["seat"] == 3)
        match.pick(f"block:{match.card(3, 'Grizzly Bears')}:{bear}")
        match.pick("ok")
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    assert match.view["turn"] == 3
    assert [p["life"] for p in match.view["players"][1:]] == ([60] if block else [58]) * 3
    assert not any("壮丽幻象" in o["label"] for o in match.request["options"])


def first_turn_draws(match, loki_seat):
    def hands():
        return [[c["name"] for c in view["players"][i]["hand"]] for i, view in enumerate(match.views)]

    match.until(lambda: match.request["kind"] == "ChooseOptions")
    opening = hands()
    assert opening == [["Slaughter Pact"] if i == loki_seat else [] for i in range(1, 5)], opening
    # Subversion affects each opponent individually, then gives Loki the combined life loss.
    life = [p["life"] for p in match.view["players"]]
    assert life == [63 if i == loki_seat else 57 for i in range(1, 5)], life
    match.pick(next(o["id"] for o in match.request["options"] if "壮丽幻象" in o["label"]))
    match.pick("confirm-cards")
    match.until(lambda: match.view["turn"] == 2 and match.view["phase"] == "MAIN1"
                and match.request["kind"] == "InputPassPriority")
    assert hands() == [["Slaughter Pact"]] * 4, hands()
    first = match.request["seat"]
    match.pick("ok")
    caster = match.request["seat"]
    assert caster not in (first, loki_seat)
    spell = match.views[caster-1]["players"][caster-1]["hand"][0]
    victim = match.card(loki_seat, "Grizzly Bears")
    match.pick(f"card:{spell['id']}")
    assert match.request["seat"] == caster and f"card:{victim}" in match.ids, (match.request["kind"], match.ids)
    match.pick(f"card:{victim}")
    match.until(lambda: "Grizzly Bears" in match.view["players"][loki_seat-1]["graveyard"])


def trick_board(match, loki_seat):
    def board(view):
        return next(m["lokiTricks"] for m in view["players"][loki_seat-1]["effectMarkers"] if m["name"] == "洛基的诡计")

    def check(marked, capture):
        expected = board(match.view)
        assert len({t["name"] for t in expected}) == len(expected) == 10
        assert all(t["rules"].strip() and type(t["marked"]) is bool for t in expected)
        assert {t["name"] for t in expected if t["marked"]} == marked
        assert all(board(v) == expected for v in match.views)
        capture_event(capture,match.views[0])

    def choose(name):
        match.pick(next(o["id"] for o in match.request["options"] if name in o["label"]))
        match.pick("confirm-cards")
        match.until(lambda: bool(match.view["stackItems"]))

    match.until(lambda: match.request["kind"] == "ChooseOptions")
    check(set(),"Loki-board-initial")
    choose("壮丽幻象")
    check({"壮丽幻象"},"Loki-board-marked")
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    assert not any("壮丽幻象" in o["label"] for o in match.request["options"])
    choose("时间戏法")
    check({"壮丽幻象","时间戏法"},"Loki-board-reset-pending")
    match.until(lambda: not match.view["stackItems"])
    check(set(),"Loki-board-cleared")
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    assert len([o for o in match.request["options"] if o["id"].startswith("select:")]) == 10


TRICK_CASES = {
    "clone": "无序之主",
    "destroy": "择乱而行",
    "destroy none": "择乱而行",
    "discount": "秘法欺瞒",
    "sweep": "祸乱降临",
    "steal": "混乱使者",
    "destroy after transfer": "择乱而行",
    "steal after transfer": "混乱使者",
    "reveal": "幻境揭幕",
    "discard instant": "谋士洞见",
    "discard two": "谋士洞见",
    "stun": "随兴织法",
}


def trick_position(case):
    seats = [
        {"Battlefield": ["Grizzly Bears", "Llanowar Elves", "Sol Ring", "Darksteel Ingot"]} for _ in range(4)
    ]
    seats[0]["Battlefield"].append("Isamaru, Hound of Konda")
    seats[0]["Library"] = ["Mountain", "Island", "Lightning Bolt", "Forest", "Swamp", "Plains"]
    if case == "discount":
        seats[0]["Hand"] = ["Pithing Needle", "Soul-Guide Lantern"]
    if case in ("steal", "stun"):
        seats[1]["Tapped"] = ["Grizzly Bears"]
    if case.endswith("after transfer"):
        seats[1]["Battlefield"].append("Merieke Ri Berit")
        for p in seats[2:]:
            p["Battlefield"].append("Ornithopter")
    return seats


def trick_effect(match, case):
    def cards(seat, zone="battlefield"):
        return match.views[seat - 1]["players"][seat - 1][zone]

    def priority(turn=1):
        return (
            match.view["turn"] == turn
            and match.view["phase"] == "MAIN1"
            and match.request["kind"] == "InputPassPriority"
        )

    def finish():
        match.until(lambda: priority() and not match.view["stackItems"])

    match.until(lambda: match.request["kind"] == "ChooseOptions")
    bears = [match.card(s, "Grizzly Bears") for s in range(1, 5)]
    rings = [match.card(s, "Sol Ring") for s in range(1, 5)]
    ingot = match.card(2, "Darksteel Ingot")
    original = match.card(1, "Isamaru, Hound of Konda")
    if case in ("steal", "stun"):
        assert next(c for c in cards(2) if c["id"] == bears[1])["tapped"]
    match.pick(next(o["id"] for o in match.request["options"] if TRICK_CASES[case] in o["label"]))
    match.pick("confirm-cards")
    if case.endswith("after transfer"):
        match.until(lambda: match.request["kind"] == "InputSelectTargets")
        moved, stable = (match.card(s, "Ornithopter") for s in (3, 4))
        match.pick(f"card:{moved}")
        match.pick(f"card:{stable}")
        match.pick("ok")
        match.until(lambda: priority() and match.request["seat"] == 2 and len(match.view["stackItems"]) == 1)
        match.pick("card:" + str(match.card(2, "Merieke Ri Berit")))
        match.until(lambda: match.request["kind"] == "InputSelectTargets")
        match.pick(f"card:{moved}")
        match.until(lambda: len(match.view["stackItems"]) == 1 and any(c["id"] == moved for c in cards(2)))
        finish()
        skipped = next((c for c in cards(2) if c["id"] == moved), None)
        assert skipped is not None, "The target who changed controllers must be skipped"
        assert "Villain" not in skipped["currentType"] and "HASTE" not in skipped["keywords"]
        assert (
            any(c["id"] == stable for c in cards(1))
            if case.startswith("steal")
            else "Ornithopter" in cards(4, "graveyard")
        )
        return
    if case in ("clone", "destroy", "destroy none", "steal", "sweep"):
        match.until(lambda: match.request["kind"] == "InputSelectTargets")
        assert match.request["seat"] == 1
        if case == "clone":
            assert f"card:{bears[1]}" not in match.ids
            match.pick(f"card:{original}")
        elif case == "sweep":
            match.pick("player:" + str(match.view["players"][2]["targetId"]))
        elif case == "destroy none":
            match.pick("ok")
        else:
            chosen = rings if case == "destroy" else bears
            assert f"card:{chosen[0]}" not in match.ids
            match.pick(f"card:{chosen[1]}")
            sibling = ingot if case == "destroy" else match.card(2, "Llanowar Elves")
            assert f"card:{sibling}" not in match.ids, (case, match.ids)
            capture_event(
                "Loki-" + case + "-selected", dict(match.view, awaitingInput=True, request=match.request)
            )
            match.pick(f"card:{chosen[1]}")
            assert f"card:{sibling}" in match.ids
            capture_event(
                "Loki-" + case + "-cleared", dict(match.view, awaitingInput=True, request=match.request)
            )
            match.pick(f"card:{chosen[1]}")
            match.pick(f"card:{chosen[2]}")
            match.pick(f"card:{chosen[3]}")
    match.until(lambda: bool(match.view["stackItems"]))
    if case.startswith("discard"):
        match.until(lambda: match.request["kind"] == "InputSelectEntitiesFromList")
        hand = cards(1, "hand")
        assert len(hand) == 6 and "ok" not in match.ids
        names = ["Lightning Bolt"] if case == "discard instant" else ["Mountain", "Island"]
        for i, name in enumerate(names):
            if i:
                assert "ok" not in match.ids
            match.pick("card:" + str(next(c["id"] for c in hand if c["name"] == name)))
        if match.request["kind"] == "InputSelectEntitiesFromList":
            match.pick("ok")
        finish()
        assert sorted(cards(1, "graveyard")) == sorted(names) and len(cards(1, "hand")) == 6 - len(names)
        return
    finish()
    if case == "clone":
        field = cards(1)
        clones = [c for c in field if c["name"] == "Isamaru, Hound of Konda"]
        assert len(clones) == 3 and all(
            ("Legendary" in c["currentType"]) == (c["id"] == original) for c in clones
        ), [(c["name"], c["currentType"]) for c in field]
        match.until(lambda: priority(2))
        assert match.card(1, "Grizzly Bears") == bears[0]
    elif case in ("destroy", "destroy none"):
        assert [r in [c["id"] for c in cards(i)] for i, r in enumerate(rings, 1)] == (
            [True] * 4 if case == "destroy none" else [True, False, False, False]
        )
        assert all(any(c["name"] == "Darksteel Ingot" for c in cards(i)) for i in range(1, 5))
    elif case == "sweep":
        assert [sum("Creature" in c["currentType"] for c in cards(s)) for s in range(1, 5)] == [3, 2, 0, 2]
    elif case == "steal":
        stolen = [c for c in cards(1) if c["id"] in bears[1:]]
        assert len(stolen) == 3 and all(
            "Villain" in c["currentType"] and "HASTE" in c["keywords"] and not c["tapped"] for c in stolen
        ), [(c["currentType"], c["keywords"]) for c in stolen]
        match.until(lambda: priority(2))
        for i in range(2, 5):
            c = next(c for c in cards(i) if c["id"] == bears[i - 1])
            assert "Villain" not in c["currentType"] and "HASTE" not in c["keywords"]
    elif case == "reveal":
        assert all(any(c["name"] == name for c in cards(1)) for name in ("Island", "Forest"))
        assert sorted(c["name"] for c in cards(1, "hand")) == ["Lightning Bolt", "Mountain"]
    elif case == "stun":

        def stuns(seat):
            return [
                (c["tapped"], sum(x["count"] for x in c["counters"] if x["kind"].lower() == "stun"))
                for c in cards(seat)
                if "Creature" in c["currentType"]
            ]

        assert stuns(1) == [(False, 0)] * 3
        assert all(stuns(i) == [(True, 1)] * 2 for i in range(2, 5)), [stuns(i) for i in range(2, 5)]
        match.until(lambda: priority(2))
        assert all(stuns(i) == [(True, 0)] * 2 for i in range(2, 5))
    elif case == "discount":
        spell = next(c for c in cards(1, "hand") if c["name"] == "Soul-Guide Lantern")
        match.until(lambda: priority() and match.request["seat"] == 1)
        match.pick(f"card:{spell['id']}")
        match.until(lambda: any(c["id"] == spell["id"] for c in cards(1)))
        assert all(not c["tapped"] for c in cards(1) if c["name"] in ("Sol Ring","Darksteel Ingot","Llanowar Elves"))


def heroes_attack(match, loki_seat):
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    match.pick(next(o["id"] for o in match.request["options"] if "壮丽幻象" in o["label"]))
    match.pick("confirm-cards")
    match.until(lambda: match.view["turn"] == 2 and match.request["kind"] == "InputAttack")
    heroes = set(range(1, 5)) - {loki_seat}
    declared = set()
    attackers = []
    for _ in heroes:
        actor = match.request["seat"]
        assert actor in heroes - declared and match.request["kind"] == "InputAttack"
        bear = match.card(actor, "Grizzly Bears")
        assert {int(k.split(":")[1]) for k in match.ids if k.startswith("attack:")} == {bear}
        declared.add(actor)
        attackers.append(bear)
        match.pick(match.attack(bear, loki_seat))
        match.pick("ok")
    match.until(lambda: match.request["kind"] == "InputBlock")
    assert match.request["seat"] == loki_seat
    blocker = match.card(loki_seat, "Grizzly Bears")
    assert all(f"block:{blocker}:{attacker}" in match.ids for attacker in attackers)
    match.pick(f"block:{blocker}:{attackers[0]}")
    match.pick("ok")
    match.until(lambda: match.view["players"][loki_seat - 1]["life"] < 60)
    assert all(v["players"][loki_seat - 1]["life"] == 56 for v in match.views)


def team_priority(match, loki_seat):
    """Check empty-stack rotation and a fresh clockwise response round for every exposed item."""
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    match.pick(next(o["id"] for o in match.request["options"] if "壮丽幻象" in o["label"]))
    match.pick(next(k for k in match.ids if k == "ok" or "confirm" in k))
    match.until(lambda: match.view["turn"] == 2 and match.view["phase"] == "MAIN1"
                and match.request["kind"] == "InputPassPriority")
    heroes = set(range(1, 5)) - {loki_seat}

    def at_priority():
        assert match.request["kind"] == "InputPassPriority", match.request["kind"]
        assert match.view["turn"] == 2 and match.view["phase"] == "MAIN1"
        return match.request["seat"]

    def cast(name):
        seat = at_priority()
        source = next(c for c in match.view["players"][seat-1]["hand"] if c["name"] == name)
        match.pick(f"card:{source['id']}")
        match.until(lambda: match.request["kind"] == "InputPassPriority")

    first = at_priority()
    # An unconfirmed hero may pass the action seat around the table, even a
    # full lap, without sending team priority to Loki or ending the main phase.
    ordered = sorted(heroes)
    for _ in range(3):
        actor = at_priority()
        match.process.stdin.write(json.dumps(dict(type="respond", session=match.name, seat=actor,
            requestId=match.request["requestId"], option="ok", heroHandoff=True)) + "\n")
        match.process.stdin.flush()
        match.next()
        assert at_priority() == ordered[(ordered.index(actor) + 1) % len(ordered)]
    assert at_priority() == first
    match.pick("ok")
    second = at_priority()
    assert first != second and {first, second} <= heroes
    cast("Forest")
    # A land doesn't use the stack. Everyone on the team, including the player
    # who previously declined, can take the team's next sorcery-speed action.
    before_loki = set()
    while at_priority() != loki_seat and len(before_loki) < 4:
        seat = at_priority()
        assert seat not in before_loki
        before_loki.add(seat)
        if len(before_loki) == 3:
            break
        match.pick("ok")
    assert before_loki == heroes, (before_loki, heroes, at_priority())
    opener = at_priority()
    ordered_heroes = sorted(heroes)
    successor = ordered_heroes[(ordered_heroes.index(opener) + 1) % len(ordered_heroes)]
    cast("Ornithopter")
    after_clear = successor
    def expect(seat, stack):
        assert at_priority() == seat, (at_priority(), seat, stack)
        for view in match.views:
            assert view["prioritySeat"] == seat and view["heroAfterStackSeat"] == after_clear
            assert [(x["id"], x["controllerSeat"]) for x in view["stackItems"]] == stack

    def items():
        return [(x["id"], x["controllerSeat"]) for x in match.view["stackItems"]]

    def pass_round(order, stack):
        for seat in order:
            expect(seat, stack)
            match.pick("ok")

    third = (heroes - {opener, successor}).pop()
    original = items()
    expect(loki_seat, original)
    match.pick("ok")
    expect(successor, original)
    cast("Pact of the Titan")
    nested = items()
    assert len(nested) == 2 and nested[1:] == original
    pass_round([loki_seat, third, opener, successor], nested)
    # Loki already passed on the original. The new round permits a real response.
    expect(loki_seat, original)
    cast("Pact of the Titan")
    loki_stack = items()
    assert len(loki_stack) == 2 and loki_stack[1:] == original and loki_stack[0] != nested[0]
    pass_round([((loki_seat - 1 + i) % 4) + 1 for i in range(1, 5)], loki_stack)
    pass_round([loki_seat, successor, third, opener], original)
    assert at_priority() == successor
    assert all(v["prioritySeat"] == successor and v["heroAfterStackSeat"] == 0 for v in match.views)

    # Three different heroes add items; the two Pacts have the same name.
    after_clear = third
    cast("Ornithopter")
    bottom = items()
    pass_round([loki_seat], bottom)
    expect(third, bottom)
    cast("Pact of the Titan")
    middle = items()
    pass_round([loki_seat], middle)
    expect(opener, middle)
    cast("Pact of the Titan")
    top = items()
    assert len(top) == 3 and top[1:] == middle and middle[1:] == bottom
    pass_round([loki_seat, successor, third, opener], top)
    pass_round([loki_seat, opener, successor, third], middle)
    pass_round([loki_seat, third, opener, successor], bottom)
    assert at_priority() == third and not items()


def private_cards(match, loki_seat, shared):
    match.until(lambda: all(len(p["exileCards"]) >= 2 for p in match.view["players"]))
    def visible(viewer, holder):
        return viewer == holder or (shared and viewer != loki_seat and holder != loki_seat)
    for viewer, view in enumerate(match.views, 1):
        for holder, player in enumerate(view["players"], 1):
            own_hand = match.views[holder-1]["players"][holder-1]["hand"]
            assert own_hand
            assert [c["id"] for c in player["hand"]] == ([c["id"] for c in own_hand] if visible(viewer,holder) else [])
            originals = {c["knownFaceName"] for c in player["exileCards"] if c.get("knownFaceName")}
            expected = {"Lightning Bolt"} if visible(viewer,holder) else set()
            if holder == 2 and visible(viewer,3):
                expected.add("Counterspell")
            assert originals == expected, (viewer,holder,originals,expected)
            for card in player["exileCards"]:
                assert card["faceDown"] and card["name"] == "Face-down card"
                assert card["id"] not in player["usableSourceIds"]
                if not card.get("knownFaceName"):
                    assert not card.get("knownFaceRules") and not card.get("knownFacePrintingKey")
        assert not view["playableExile"]


def team_cast_timing(match, same_caster):
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    match.pick(next(o["id"] for o in match.request["options"] if "壮丽幻象" in o["label"]))
    match.pick(next(k for k in match.ids if k == "ok" or "confirm" in k))
    match.until(lambda: match.view["turn"] == 2 and match.view["phase"] == "MAIN1"
                and match.request["kind"] == "InputPassPriority")
    first = match.request["seat"]
    second = next(seat for seat in range(1, 5) if seat not in (first, 2))
    def spell(seat, name):
        return "card:" + str(next(c["id"] for c in match.views[seat-1]["players"][seat-1]["hand"] if c["name"] == name))
    first_spell, second_spell = spell(first, "Ornithopter"), spell(second, "Pact of the Titan")
    # Deliver two separate seats' choices for the same request in one pipe write.
    # Run the out-of-turn choice first: even arrival order cannot grant priority.
    decision = match.request["requestId"]
    choices = [(second, second_spell), (first, first_spell)]
    match.process.stdin.write("".join(json.dumps(dict(type="respond", session=match.name, seat=seat,
        requestId=decision, option=option, order=[]))+"\n" for seat, option in choices))
    match.process.stdin.flush()
    msg = match.messages.get(timeout=35)
    assert msg["type"] == "rejected" and msg["error"] == "wrong-seat", msg.get("type")
    match.next()
    caster = first if same_caster else second
    match.until(lambda: match.request["kind"] == "InputPassPriority" and match.request["seat"] == caster
                and len(match.view["stackItems"]) == 1)
    assert not any(c["name"] == "Ornithopter" for c in match.view["players"][first-1]["battlefield"])
    if caster != first:
        assert spell(caster, "Ornithopter") not in match.ids
    if same_caster:
        capture_event("Shared-before-pact", match.views[first-1])
    match.pick(spell(caster, "Pact of the Titan"))
    match.until(lambda: len(match.view["stackItems"]) == 2)
    match.until(lambda: any("Giant" in c["currentType"] for c in match.view["players"][caster-1]["battlefield"]))
    assert len(match.view["stackItems"]) == 1
    assert not any(c["name"] == "Ornithopter" for c in match.view["players"][first-1]["battlefield"])
    match.until(lambda: any(c["name"] == "Ornithopter" for c in match.view["players"][first-1]["battlefield"]))
    assert not match.view["stackItems"] and match.view["dayTime"] == "night"
    if same_caster:
        capture_event("Shared-night", match.views[first-1])
    match.until(lambda: match.view["turn"] == 3 and match.view["phase"] == "MAIN1")
    if same_caster:
        capture_event("Shared-day", match.views[first-1])
    assert match.view["dayTime"] == ("day" if same_caster else "night"), (first, caster, match.view["dayTime"])
    if same_caster:
        # Loki takes the next turn without casting; the next team starts at night.
        match.until(lambda: match.request["kind"] == "ChooseOptions")
        match.pick(next(o["id"] for o in match.request["options"] if "秘法欺瞒" in o["label"]))
        match.pick(next(k for k in match.ids if k == "ok" or "confirm" in k))
        match.until(lambda: match.view["turn"] == 4 and match.view["dayTime"] == "night")
        assert match.view["turnSeat"] != 2
        capture_event("Shared-next-night", match.views[first-1])
        def pact_on_stack():
            return any(item.get("source", {}).get("name") == "Pact of the Titan" for item in match.view["stackItems"])
        match.until(pact_on_stack)
        assert not any(m["name"] == "Pact of the Titan" for m in match.view["players"][first-1]["effectMarkers"])
        capture_event("Shared-pact-triggered", match.views[first-1])



def nauctis(match):
    # Seat 1 starts with 19 permanent cards and many instants, seat 2 with 20.
    match.until(lambda: match.view["phase"] == "MAIN1" and match.request["kind"] == "InputPassPriority")
    def names(seat):
        return [m["name"] for m in match.view["players"][seat-1]["effectMarkers"]]
    assert "广袤海域" in names(1) and "雄伟大洋" in names(2), (names(1), names(2))
    assert all("雄伟大洋" not in names(s) for s in (3, 4))
    guide = next(m for m in match.view["players"][1]["effectMarkers"] if m["name"] == "雄伟大洋")
    assert "落坟20" in guide["originalRules"] and "撒饵诱鱼" in guide["originalRules"]
    match.until(lambda: match.view["turn"] == 1 and match.view["phase"] == "MAIN1"
                and match.request["kind"] == "InputPassPriority" and match.request["seat"] == 2
                and any("Shark" in c["currentType"] for c in match.view["players"][1]["battlefield"]))
    shark = next(c for c in match.view["players"][1]["battlefield"] if "Shark" in c["currentType"])
    assert "Shark" in shark["name"] and str(shark["power"]) == str(shark["toughness"]) == "3"
    assert shark["currentColors"] == 8, shark
    capture_event("Nauctis-effect", dict(match.view, awaitingInput=True, request=match.request))
    def cast(name):
        hand = match.view["players"][1]["hand"]
        match.pick("card:" + str(next(c["id"] for c in hand if c["name"] == name)))
    cast("Ornithopter")
    for _ in range(45):
        if match.request["kind"] != "InputPassPriority":
            break
        match.pick("ok")
    # Devour must be optional and let the player actually sacrifice this Shark.
    if match.request["kind"] == "InputConfirm":
        match.pick("ok")
    assert "card:" + str(shark["id"]) in match.ids, (match.request["kind"], match.ids)
    match.pick("card:" + str(shark["id"]))
    for _ in range(18):
        if "深潜" in names(2):
            break
        assert match.view["turn"] == 1 and match.view["phase"] == "MAIN1", (names(2), [(c["name"],c["currentRules"]) for c in match.view["players"][1]["battlefield"]])
        match.pick("ok")
    assert "深潜" in names(2), names(2)
    thopter = next(c for c in match.view["players"][1]["battlefield"] if c["name"] == "Ornithopter")
    assert str(thopter["power"]) == "3" and str(thopter["toughness"]) == "5"
    match.until(lambda: match.view["phase"] == "MAIN1" and match.request["seat"] == 2 and not match.view["stack"])
    cast("Memnite")
    match.until(lambda: any(c["name"] == "Memnite" for c in match.view["players"][1]["battlefield"]))
    assert not any("Shark" in c["currentType"] for c in match.view["players"][1]["battlefield"])
    memnite = next(c for c in match.view["players"][1]["battlefield"] if c["name"] == "Memnite")
    assert not any("Devour" in k for k in memnite["keywords"])
    match.until(lambda: match.request["kind"] == "InputAttack")
    match.pick(match.attack(thopter["id"], 3))
    match.pick(match.attack(memnite["id"], 3))
    match.pick("ok")
    match.until(lambda: match.request["kind"] == "InputBlock" and match.request["seat"] == 3)
    angel = match.card(3, "Serra Angel")
    assert f"block:{angel}:{thopter['id']}" not in match.ids
    assert f"block:{angel}:{memnite['id']}" in match.ids
    match.pick("ok")
    match.until(lambda: match.view["phase"] == "MAIN2")
    assert match.view["players"][2]["life"] == 36 and "深潜" not in names(2)


def nauctis_threshold(match):
    match.until(lambda: match.view["phase"] == "MAIN1" and match.request["kind"] == "InputPassPriority")
    def region():
        return [m["name"] for m in match.view["players"][0]["effectMarkers"]]
    assert "广袤海域" in region() and "雄伟大洋" not in region()
    capture_event("Nauctis-before", match.views[0])
    crypt = match.card(1, "Tormod's Crypt")
    match.pick(f"card:{crypt}")
    match.until(lambda: match.request["kind"] == "InputSelectTargets")
    match.pick("player:" + str(match.view["players"][0]["targetId"]))
    match.until(lambda: len(match.view["players"][0]["graveyard"]) == 0)
    assert "雄伟大洋" in region() and "广袤海域" not in region()
    capture_event("Nauctis-after", match.views[0])
    assert not any("Shark" in c["currentType"] for c in match.view["players"][0]["battlefield"])



def arkenstone(match):
    for active, target in [(1, 2), (2, 1)]:
        match.until(lambda: match.request["kind"] == "InputAttack" and match.request["seat"] == active)
        match.pick(match.attack(match.card(active, "Grizzly Bears"), target))
        match.pick("ok")
        match.until(lambda: match.view["phase"] == "MAIN2")
        players = match.view["players"]
        assert [c["name"] for c in players[active - 1]["emblems"]] == ["阿肯宝石"]
        assert not players[target - 1]["emblems"]
        assert int(players[active - 1]["battlefield"][0]["power"]) == 3
        assert int(players[target - 1]["battlefield"][0]["power"]) == 2
    cards_before = match.view["players"][1]["handCount"]
    match.until(lambda: match.view["turn"] == 3 and match.view["phase"] == "MAIN1")
    assert match.view["players"][1]["handCount"] == cards_before + 1
    for turn, points in [(6, 1), (10, 2)]:
        while match.view["turn"] < turn:
            match.pick("ok")
        match.until(lambda: match.view["phase"] == "MAIN1")
        markers = match.view["players"][1]["effectMarkers"]
        assert any(m.get("markerStatus") == f"胜利点 {points}/3" for m in markers)
    assert finish_with_passes(match) == 2


def finish_with_passes(match):
    # The shared fixture stops at decisions; keep the final authority message here.
    for _ in range(500):
        request = match.request
        assert "ok" in match.ids
        match.process.stdin.write(json.dumps(dict(type="respond", session=match.name, seat=request["seat"],
                                                 requestId=request["requestId"], option="ok", order=[])) + "\n")
        match.process.stdin.flush()
        message = match.messages.get(timeout=35)
        while message and message["type"] not in ("ended", "request", "rejected", "fatal"):
            message = match.messages.get(timeout=35)
        assert message and message["type"] in ("ended", "request"), message
        if message["type"] == "ended":
            winner = message["winnerSeat"]
            assert all(v["commanderEvent"] == match.name and v.get("lokiSeat") == match.view.get("lokiSeat")
                       for v in message["views"])
            return winner
        match.request = message["request"]
        match.ids = {o["id"] for o in match.request["options"] if not o.get("disabled")}
    raise AssertionError("expected a terminal game result")


def loki_finish(match, loki_seat, heroes_win, attacker_name):
    hero = loki_seat % 4 + 1
    match.until(lambda: match.request["kind"] == "ChooseOptions")
    assert match.request["seat"] == match.view["lokiSeat"] == loki_seat
    assert [p["life"] for p in match.view["players"]] == [60] * 4
    match.pick(next(o["id"] for o in match.request["options"] if "壮丽幻象" in o["label"]))
    match.pick(next(k for k in match.ids if k == "ok" or "confirm" in k))
    seat, target = (hero, loki_seat) if heroes_win else (loki_seat, hero)
    match.until(lambda: match.request["kind"] == "InputAttack" and match.request["seat"] == seat)
    attacker = match.card(seat, attacker_name)
    match.pick(match.attack(attacker, target))
    if not heroes_win and attacker_name != "Serra Avatar":
        match.until(lambda: any(p.get("eliminated") and p["seat"] == target for p in match.view["players"]))
        assert [p["seat"] for p in match.view["players"] if p.get("eliminated")] == [target]
        assert all(p["life"] > 0 for p in match.view["players"] if not p.get("eliminated"))
        match.until(lambda: match.view["phase"] == "MAIN1" and match.request["seat"] not in (target, loki_seat))
        return
    winner = finish_with_passes(match)
    assert (winner in range(1, 5) and winner != loki_seat) if heroes_win else winner == loki_seat


def main():
    cp = os.pathsep.join(str(p) for p in (runtime / "classes", runtime / "lib/*"))
    with tempfile.TemporaryDirectory() as temporary:
        folder = Path(temporary)
        fixtures = Path(__file__).with_name("tests")
        subprocess.run(["javac", "--release", "17", "-encoding", "UTF-8", "-cp", cp, "-d", temporary,
                        str(fixtures / "StateBranchFixture.java"), str(fixtures / "CombatKeywordFixture.java")], check=True)
        for mode, case, first in [("heart-of-the-mountain", "stone", 1), ("god-of-mischief", "ally block", 1),
                                  ("god-of-mischief", "shared life", 1), ("god-of-mischief", "heroes win", 2),
                                  ("god-of-mischief", "first turn draws", 4),
                                  ("god-of-mischief", "three heroes attack", 2),
                                  ("god-of-mischief", "trick board", 2),
                                  ("god-of-mischief", "Loki wins", 3), ("god-of-mischief", "hero loses to spell effect", 4),
                                  ("god-of-mischief", "individual poison", 4),
                                  ("god-of-mischief", "team priority", 2),
                                  ("god-of-mischief", "hero private cards", 2),
                                  ("heart-of-the-mountain", "ordinary private cards", 2),
                                  ("nauctis-grand-marine-preview", "nauctis", 2),
                                  ("nauctis-grand-marine-preview", "nauctis threshold", 1),
                                  ("god-of-mischief", "two hero casts", 2), ("god-of-mischief", "one hero twice", 2),
                                  *(("god-of-mischief", trick, 1) for trick in TRICK_CASES)]:
            if os.environ.get("MTGB_EVENT_CASE") and case != os.environ["MTGB_EVENT_CASE"]:
                continue
            positions = [{"Battlefield": ["Grizzly Bears"]} for _ in range(4)]
            if case in TRICK_CASES:
                positions = trick_position(case)
            if case == "first turn draws":
                positions[first-1]["Battlefield"].append("Subversion")
                for position in positions:
                    position["Library"] = ["Slaughter Pact"]
            if case == "nauctis":
                positions[0]["Graveyard"] = ["Island"] * 19 + ["Opt"] * 4
                positions[1]["Graveyard"] = ["Forest"] * 20
                positions[1]["Hand"] = ["Ornithopter", "Memnite"]
                positions[1]["Battlefield"].append("Fervor")
                positions[2]["Battlefield"].append("Serra Angel")
            if case == "nauctis threshold":
                positions[0]["Graveyard"] = ["Forest"] * 19 + ["Opt"] * 2
                positions[0]["Battlefield"].append("Tormod's Crypt")
            if case in ("team priority", "two hero casts", "one hero twice"):
                for position in positions:
                    position["Hand"] = ["Forest", "Ornithopter", "Pact of the Titan"]
            if case.endswith("private cards"):
                for position, name in zip(positions, ["Mountain","Forest","Island","Plains"]):
                    position["Hand"] = [name]
            if case == "heroes win":
                positions[first % 4]["Battlefield"].append("Phage the Untouchable")
            if case == "Loki wins":
                positions[first-1]["Battlefield"].append("Serra Avatar")
            if case == "hero loses to spell effect":
                positions[first-1]["Battlefield"].append("Phage the Untouchable")
                positions[(first + 1) % 4]["Battlefield"].append("Platinum Angel")
            if case == "individual poison":
                positions[first-1]["Battlefield"].append("Blightsteel Colossus")
            config = dict(session=mode, formatId="commander", commanderEvent=mode, firstPlayerSeat=first, seed=4,
                          startingLife=40, auditDir=temporary, decks=[combat["DECK"]] * 4,
                          positions=positions)
            if case.endswith("private cards"):
                config["privateExileFixture"] = True
            if case in ("two hero casts", "one hero twice"):
                config["startAtNight"] = True
            start = folder / "start.json"
            start.write_text(json.dumps(config), encoding="utf8")
            with (folder / "engine.log").open("w", encoding="utf8") as log, subprocess.Popen(
                [str(runtime / "jre/bin/java"), "-Xmx400m", "-XX:ActiveProcessorCount=3", "-Djava.awt.headless=true",
                 "-cp", temporary + os.pathsep + cp, "mtgb.bridge.CombatKeywordFixture", str(runtime / "assets"), str(start)],
                cwd=runtime / "assets", env=dict(os.environ, APPDATA=temporary, LOCALAPPDATA=temporary),
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, encoding="utf8", text=True
            ) as process:
                try:
                    match = combat["Match"](process, mode, folder)
                    if case in TRICK_CASES:
                        trick_effect(match, case)
                    elif case == "nauctis":
                        nauctis(match)
                    elif case == "nauctis threshold":
                        nauctis_threshold(match)
                    elif case in ("two hero casts", "one hero twice"):
                        team_cast_timing(match, case == "one hero twice")
                    elif case == "first turn draws":
                        first_turn_draws(match, first)
                    elif case == "trick board":
                        trick_board(match, first)
                    elif case.endswith("private cards"):
                        private_cards(match, first, case == "hero private cards")
                    elif case == "team priority":
                        team_priority(match, first)
                    elif case == "three heroes attack":
                        heroes_attack(match, first)
                    elif case == "stone":
                        arkenstone(match)
                    elif case == "individual poison":
                        loki_finish(match, first, False, "Blightsteel Colossus")
                    elif case in ("heroes win", "Loki wins", "hero loses to spell effect"):
                        loki_finish(match, first, case == "heroes win",
                                    "Serra Avatar" if case == "Loki wins" else "Phage the Untouchable")
                    else:
                        loki(match, case == "ally block")
                except Exception:
                    log.flush()
                    print((folder / "engine.log").read_text("utf8", errors="replace")[-4500:], flush=True)
                    raise
                finally:
                    if process.poll() is None:
                        process.kill()
            print("PASS", mode, case, flush=True)


if __name__ == "__main__":
    main()

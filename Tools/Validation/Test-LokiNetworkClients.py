"""Drive four independent opt-in Windows Unity players through a LAN Loki match."""

import argparse
import heapq
import json
import os
import select
import socket
import subprocess
import threading
import time
from pathlib import Path

NAMES = ("one", "two", "three", "four")
THOPTER, PACT = "Ornithopter", "Pact of the Titan"
DISPLAY_NAMES = {THOPTER: "扑翼机", PACT: "泰坦条约"}


class DelayedLink:
    """Local UDP path between one real Unity Transport client and the LAN host."""
    def __init__(self, host_port, delay_ms):
        self.host = ("127.0.0.1", host_port)
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.socket.bind(("127.0.0.1", 0))
        self.port = self.socket.getsockname()[1]
        self.delay = delay_ms / 1000
        self.stopping = threading.Event()
        self.offline = threading.Event()
        self.forwarded = self.dropped = 0
        self.thread = threading.Thread(target=self.forward, daemon=True)
        self.thread.start()

    def forward(self):
        pending, client, serial = [], None, 0
        while not self.stopping.is_set():
            now = time.monotonic()
            while pending and pending[0][0] <= now:
                _, _, address, packet = heapq.heappop(pending)
                if self.offline.is_set():
                    self.dropped += 1
                    continue
                self.socket.sendto(packet, address)
                self.forwarded += 1
            wait = min(.02, max(0, pending[0][0] - now)) if pending else .02
            if select.select([self.socket], [], [], wait)[0]:
                try:
                    packet, sender = self.socket.recvfrom(65535)
                except ConnectionResetError:  # ICMP from a Unity socket closed during reconnect
                    continue
                if self.offline.is_set():
                    self.dropped += 1
                    continue
                if sender != self.host:
                    client = sender
                destination = client if sender == self.host else self.host
                if destination:
                    serial += 1
                    heapq.heappush(pending, (time.monotonic() + self.delay, serial, destination, packet))

    def close(self):
        self.stopping.set()
        self.thread.join()
        self.socket.close()


class Table:
    def __init__(self, player, folder, port, joined_heroes, teammate_lead_ms=0):
        self.player, self.folder, self.port = player, folder, port
        self.joined_heroes = joined_heroes
        self.teammate_lead_ms = teammate_lead_ms
        self.processes, self.commands, self.checkpoints = {}, {}, []
        self.last_request = 0
        self.track_passes = False
        self.priority_passes = []
        self.links = {}

    def launch(self, name):
        dest = self.folder / name
        dest.mkdir(parents=True)
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
        self.processes[name] = subprocess.Popen(
            [
                str(self.player),
                "-screen-fullscreen",
                "0",
                "-screen-width",
                "960",
                "-screen-height",
                "540",
                "-force-d3d11",
                "-noaudio",
                "-logFile",
                str(dest / "player.log"),
                "-loki-network-test",
                str(self.folder),
                name,
                str(self.links[name].port if name in self.links else self.port),
            ],
            cwd=self.player.parent,
            startupinfo=startup,
        )
        self.commands[name] = 0

    def state(self, name):
        if self.processes[name].poll() is not None:
            raise RuntimeError(f"{name} exited: {self.processes[name].returncode}")
        for _ in range(5):
            try:
                state = json.loads((self.folder / name / "state.json").read_bytes())
                break
            except FileNotFoundError:
                return {}
            except (OSError, ValueError):
                time.sleep(.01)
        else:
            return {}
        if state.get("error"):
            raise RuntimeError(f"{name}: {state['error']}")
        return state

    def wait(self, condition, what, timeout=45):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            result = condition()
            if result:
                return result
            time.sleep(0.035)
        summaries = [
            {
                k: s.get(k)
                for k in (
                    "local",
                    "lobby",
                    "decks",
                    "ready",
                    "turn",
                    "phase",
                    "kind",
                    "request",
                    "priority",
                    "status",
                    "actionResult",
                    "stack",
                    "rendered",
                    "options",
                    "scene",
                )
            }
            for s in [self.state(n) for n in self.processes]
        ]
        raise RuntimeError(f"Timed out: {what} {summaries}")

    def command(self, name, action, value="", request=0, at=0):
        self.commands[name] += 1
        number = self.commands[name]
        path = self.folder / name / "command.json"
        tmp = path.with_suffix(".tmp")
        tmp.write_text(
            json.dumps({"number": number, "action": action, "value": value, "request": request, "at": at}),
            encoding="utf8",
        )
        for attempt in range(30):
            try:
                os.replace(tmp, path)
                return number
            except PermissionError:
                if attempt == 29:
                    raise
                time.sleep(0.01)

    def completed(self, name, number):
        def read():
            s = self.state(name)
            return s if s.get("command", 0) >= number else None

        return self.wait(read, f"{name} command {number}")

    def interrupt_responder(self, actor, state):
        absent = actor
        if self.outage == "other":
            caster = state["stack"][0]["controller"]
            absent = next(n for n in self.links if n != actor and NAMES.index(n)+1 not in (state["loki"],caster))
        missing_seat = NAMES.index(absent) + 1
        link = self.links[absent]
        bit = 1 << (missing_seat - 1)
        others = [n for n in NAMES if n != absent]
        def signature(s):
            return (s.get("match"), s.get("priority"), s.get("sequence"),
                    [(item["id"],item["controller"]) for item in s.get("stack") or []])
        before = signature(state)
        start = time.monotonic()
        link.offline.set()
        self.wait(lambda: self.state("one").get("connectedSeats") == 15 ^ bit,
                  "server registered physical link loss", 55)
        detected = time.monotonic()
        for _ in range(12):
            for n in others:
                peer = self.wait(lambda: self.state(n) or None, "read connected peer snapshot", 2)
                assert signature(peer) == before and peer["canAct"] == (n == actor), (n, peer)
            time.sleep(.15)
        info = {"seat":missing_seat, "respondingSeat":state["local"], "heldRequest":state["request"], "originalStack":before[-1],
                "detectionSeconds":round(detected - start, 2)}
        if self.outage in ("expire","other"):
            def survivors():
                peers = [self.state(n) for n in others]
                return peers if all(any(p["seat"] == missing_seat and p["eliminated"] for p in s.get("players") or [])
                                    and not s["ended"] for s in peers) else None
            peers = self.wait(survivors, "only missing hero eliminated", 55)
            for s in peers:
                assert sum(p["eliminated"] for p in s["players"]) == 1
            if self.outage == "other":
                assert all(s["priority"] == state["local"] for s in peers)
            info["forfeitSecondsAfterDetection"] = round(time.monotonic()-detected,2)
            passed = []
            for _ in range(12):
                def renewed():
                    peers = [self.state(n) for n in others]
                    if not all(peers) or len({s["sequence"] for s in peers}) != 1:
                        return None
                    return next(((n,s) for n,s in zip(others,peers) if s["gate"] and s["canAct"]
                                 and s["request"] > self.last_request), None)
                name, prompt = self.wait(renewed, "survivors can act after forfeit")
                if not prompt["stack"]:
                    assert any(c["name"]==THOPTER for p in prompt["players"] for c in p["field"])
                    info["survivorPasses"] = passed
                    info["nextSeat"] = prompt["local"]
                    return info
                assert signature(prompt)[-1]==before[-1]
                passed.append(prompt["local"])
                self.submit_option(name,prompt,"ok")
            raise AssertionError("Original spell failed to resolve after the responder left")
        link.offline.clear()
        def restored():
            s = self.state(actor)
            return s if (s.get("connectedSeats")==15 and s.get("gate") and s.get("request")==state["request"]
                         and signature(s)==before and self.everyone(lambda p:p.get("connectedSeats")==15)) else None
        restored_state = self.wait(restored,"four clients restored the same pending response",55)
        assert restored_state["options"]==state["options"] and restored_state["canAct"]
        info["restoredSecondsAfterDetection"]=round(time.monotonic()-detected,2)
        info["droppedDatagrams"]=link.dropped
        assert link.dropped and link.forwarded
        return info

    def everyone(self, predicate):
        states = [self.state(n) for n in NAMES]
        return states if all(predicate(s) for s in states) else None

    def decision(self, include_result=False):
        def available():
            states = [self.state(n) for n in NAMES]
            if include_result and any(s.get("ended") for s in states):
                if any(s.get("aborted") for s in states):
                    raise AssertionError("Match aborted: " + str([(s.get("turn"), s.get("phase"), s.get("status")) for s in states]))
                if all(s.get("ended") and s.get("resultTitle") for s in states):
                    return None, states
                return None
            if len({s.get("sequence") for s in states}) != 1:
                return None
            for n, s in zip(NAMES, states):
                if s.get("request", 0) > self.last_request and s.get("gate") and s.get("canAct"):
                    return n, s

        return self.wait(available, "new actionable request")

    def vote(self, seat, ready, window, mask, locked=False):
        name = NAMES[seat - 1]
        self.wait(
            lambda: (
                s
                if (s := self.state(name)).get("voteWindow") == window
                and s.get("voteEnabled")
                and s.get("gate")
                and (s.get("voteButton") == "取消结束") != ready
                else None
            ),
            "vote button " + name,
        )
        self.completed(name, self.command(name, "vote" if ready else "unvote"))

        def projected(s):
            bit = 1 << (s.get("local", 1) - 1)
            if s.get("voteWindow") != window or s.get("votes") != mask:
                return False
            if not (s["voters"] & bit):
                return not s.get("voteButton")
            if s["advanceVisible"]:
                return False
            if locked:
                return s.get("voteButton") == "全员已确认" and not s.get("voteEnabled")
            return (s.get("voteButton") == "取消结束") == bool(mask & bit) and s.get(
                "priorityHint"
            ) == f"英雄确认结束 {mask.bit_count()} / {s['voters'].bit_count()}"

        self.wait(lambda: self.everyone(projected), f"all show vote {mask} in {window}")

    def vote_all(self, state):
        mask, window, eligible = state["votes"], state["voteWindow"], state["voters"]
        seats = sorted(range(1, 5), key=lambda seat: seat == state["local"])
        self.last_request = state["request"]
        for seat in seats:
            bit = 1 << (seat - 1)
            if eligible & bit and not mask & bit:
                mask |= bit
                self.vote(seat, True, window, mask, mask == eligible)

    def pick(self, name, state):
        if state["phase"] == "CLEANUP" and not self.track_passes:
            # Forge can complete a cleanup prompt while the client is publishing the next turn.
            # Give automatic choices time to settle before driving this setup-only step.
            end = time.monotonic() + 0.6
            while time.monotonic() < end:
                latest = self.state(name)
                if latest and (latest.get("request") != state["request"] or not latest.get("canAct")):
                    self.last_request = state["request"]
                    return
                time.sleep(0.04)
        if state["voteWindow"] and state["local"] != state["loki"]:
            self.vote_all(state)
            return
        options = dict(x.split("|", 1) for x in state["options"])
        choice = next((k for k in ("keep", "confirm-cards", "ok") if k in options), None)
        if state["kind"] == "ChooseOptions" and not any("confirm" in k for k in options):
            choice = next((k for k, v in options.items() if "壮丽幻象" in v), choice)
        if choice is None:
            choice = next(iter(options))
        if self.track_passes and state["kind"] == "InputPassPriority" and choice == "ok":
            expected = [(x["id"], x["controller"]) for x in state["stack"]]

            def agreed(s):
                return (
                    s.get("sequence") == state["sequence"]
                    and s.get("priority") == state["local"]
                    and [(x["id"], x["controller"]) for x in s.get("stack", [])] == expected
                    and s.get("request") == (state["request"] if s.get("local") == state["local"] else 0)
                )

            self.wait(lambda: self.everyone(agreed), f"four agree before seat {state['local']} passes")
            self.priority_passes.append(
                {
                    "sequence": state["sequence"],
                    "seat": state["local"],
                    "request": state["request"],
                    "stack": [x["id"] for x in state["stack"]],
                }
            )
        self.last_request = state["request"]
        use_button = self.track_passes and state["kind"] == "InputPassPriority" and choice == "ok"
        self.completed(name, self.command(name, "pass" if use_button else "pick", choice, state["request"]))

    def until(self, stop, limit=160):
        for _ in range(limit):
            name, s = self.decision()
            if stop(name, s):
                return name, s
            self.pick(name, s)
        raise RuntimeError("Too many decisions")

    def finish_match(self, match_id, loki, expect_heroes):
        """Play through the Pact upkeep and verify the result screen on every seat."""
        decisions = []
        losses = []
        for _ in range(400):
            actor, state = self.decision(include_result=True)
            if actor is None:
                winners = {s["winner"] for s in state}
                assert len(winners) == 1 and next(iter(winners)) in range(1, 5), winners
                winner = next(iter(winners))
                assert (winner != loki) == expect_heroes, (winner, loki, expect_heroes)
                title = "英雄阵营" if expect_heroes else "洛基"
                assert len({(s["sequence"], s["turn"], s["phase"]) for s in state}) == 1
                for s in state:
                    assert s["endMatchId"] == match_id and not s["aborted"]
                    assert s["seriesFinished"] and s["seriesWinner"] == winner
                    assert title in s["resultTitle"] and "获胜" in s["resultTitle"], s["resultTitle"]
                if not expect_heroes:
                    assert [len(group) for group in losses] == [1,2], losses
                return {"winnerSeat": winner, "lokiSeat": loki,
                        "titles": [s["resultTitle"] for s in state], "individualLosses":losses, "decisions": decisions}
            eliminated = [p["seat"] for p in state["players"] if p["eliminated"]]
            if eliminated and (not losses or eliminated != losses[-1]):
                self.wait(lambda: self.everyone(lambda s:
                    [p["seat"] for p in s.get("players") or [] if p["eliminated"]] == eliminated),
                    "four clients agree on individual loss")
                assert state["local"] not in eliminated
                losses.append(eliminated)
            decisions.append([state["turn"], state["phase"], state["local"], state["kind"]])
            if not expect_heroes and state["local"] != loki and state["kind"] == "InputPassPriority":
                # Put each remaining hero under a real Pact obligation before the next upkeep.
                hand = state["players"][state["local"] - 1]["hand"]
                ids = {o.split("|")[0] for o in state["options"]}
                if any(c["name"] == PACT and f"card:{c['id']}" in ids for c in hand):
                    self.last_request = state["request"]
                    assert self.completed(actor, self.command(actor, "drag", PACT))["actionResult"] == "cast drag"
                    continue
            self.pick(actor, state)
        raise AssertionError("Match did not reach a result after the Pact upkeep")

    def checkpoint(self, label, actor, choice, expected, want_field=None, after_stack=0):
        def matches(s):
            if (s.get("turn"), s.get("phase"), s.get("priority")) != (
                choice["turn"],
                choice["phase"],
                choice["priority"],
            ):
                return False
            if s.get("sequence") != choice["sequence"] or s.get("scene") != "IntegratedBattlefieldCommander4":
                return False
            if s.get("voteWindow") and s["local"] != s["loki"] and s["advanceVisible"]:
                return False
            if expected and s["local"] == choice["priority"] and not s["advanceVisible"]:
                return False
            hint = (
                f"英雄确认结束 {s['votes'].bit_count()} / {s['voters'].bit_count()}"
                if s.get("voteWindow")
                else ("清栈后 · " + NAMES[after_stack - 1])
                if after_stack
                else ("全员让过后结算栈顶" if expected else "全员让过后推进步骤")
            )
            if (s.get("afterStack"), s.get("priorityLabel"), s.get("priorityHint")) != (
                after_stack,
                "优先权 · " + NAMES[choice["priority"] - 1],
                hint,
            ):
                return False
            if [(x["name"], x["controller"]) for x in s.get("stack", [])] != expected:
                return False
            rendered = s.get("rendered") or []
            if len(rendered) != len(expected):
                return False
            for pos, ((name, seat), row) in enumerate(zip(expected, rendered), 1):
                if (
                    not row.startswith(f"{pos:02}|")
                    or DISPLAY_NAMES[name] not in row.split("|")
                    or f">{NAMES[seat - 1]}</color>" not in row
                    or "施放" not in row
                ):
                    return False
            if (s.get("request", 0), s.get("canAct")) != (
                (choice["request"], True) if s.get("local") == choice["local"] else (0, False)
            ):
                return False
            return not want_field or want_field(s)

        states = self.wait(lambda: self.everyone(matches), label)
        assert len({s["match"] for s in states}) == 1 and len({s["pid"] for s in states}) == 4
        assert choice["priority"] == choice["local"], (label, choice["priority"], choice["local"])
        self.checkpoints.append(
            {
                "label": label,
                "privateDecisionSeat": choice["local"],
                "clients": [
                    {
                        k: s.get(k)
                        for k in (
                            "pid",
                            "local",
                            "sequence",
                            "phase",
                            "priority",
                            "request",
                            "afterStack",
                            "priorityLabel",
                            "priorityHint",
                            "voteWindow",
                            "votes",
                            "voters",
                            "voteButton",
                            "advanceVisible",
                            "combatSeat",
                            "combatChoice",
                            "stack",
                            "rendered",
                        )
                    }
                    for s in states
                ],
            }
        )
        print(label, "seat", choice["local"], expected, flush=True)
        return states

    def capture_all(self, label=""):
        filename = (label or "stack") + ".png"
        for name in NAMES:
            self.command(name, "capture", label)

        def complete(file):
            if not file.is_file() or file.stat().st_size < 12:
                return False
            with file.open("rb") as stream:
                stream.seek(-8, os.SEEK_END)
                return stream.read(4) == b"IEND"

        self.wait(lambda: all(complete(self.folder / name / filename) for name in NAMES), filename)

    def combat_checkpoint(self, label, actor, decision):
        kind = decision["combatChoice"]
        title = {"attack": "宣告攻击", "block": "宣告阻挡", "damage": "分配伤害"}[kind]
        seat = decision["local"]
        states = self.wait(
            lambda: self.everyone(
                lambda s: (
                    s.get("combatSeat") == seat
                    and s.get("priorityLabel") == title + " · " + actor
                    and s.get("priorityHint") == "等待战斗选择完成"
                    and not s.get("voteWindow")
                    and not s.get("voteButton")
                    and s.get("request") == (decision["request"] if s.get("local") == seat else 0)
                )
            ),
            label,
        )
        self.checkpoints.append(
            {
                "label": label,
                "clients": [
                    {
                        k: s[k]
                        for k in (
                            "pid",
                            "local",
                            "turn",
                            "phase",
                            "request",
                            "combatSeat",
                            "priorityLabel",
                            "priorityHint",
                        )
                    }
                    for s in states
                ],
            }
        )
        print(label, "seat", seat, flush=True)

    def submit_option(self, name, state, option):
        self.last_request = state["request"]
        self.completed(name, self.command(name, "pick", option, state["request"]))

    def test_hero_attack(self, actor, state, heroes):
        match = state["match"]
        cast = set()
        for _ in heroes:
            seat = state["local"]
            assert seat in set(heroes) - cast and state["phase"] == "MAIN1"
            self.last_request = state["request"]
            assert self.completed(actor, self.command(actor, "drag", THOPTER))["actionResult"] == "cast drag"
            cast.add(seat)
            actor, state = self.until(
                lambda _, s, seat=seat: (
                    s["kind"] == "InputPassPriority"
                    and s["voteWindow"]
                    and any(c["name"] == THOPTER for c in s["players"][seat - 1]["field"])
                )
            )
        # Summoning sickness expires on the next shared turn. Loki chooses a
        # noncombat trick and passes through his intervening combat.
        actor, state = self.until(lambda _, s: s["turn"] == 4 and s["kind"] == "InputAttack", limit=240)
        declared = set()
        for _ in heroes:
            seat = state["local"]
            assert seat in set(heroes) - declared
            self.combat_checkpoint("Hero declares own attacker", actor, state)
            if not declared:
                self.capture_all("combat")
            own = next(c["id"] for c in state["players"][seat - 1]["field"] if c["name"] == THOPTER)
            options = [o.split("|")[0] for o in state["options"] if o.startswith("attack:")]
            assert len(options) == 1 and options[0].startswith(f"attack:{own}:player:")
            self.submit_option(actor, state, options[0])
            actor, state = self.decision()
            assert state["kind"] == "InputAttack" and state["local"] == seat
            self.pick(actor, state)
            declared.add(seat)
            actor, state = self.decision()
        actor, state = self.until(lambda _, s: s["kind"] == "InputBlock")
        self.combat_checkpoint("Loki can block all three heroes' attackers", actor, state)
        sources = {int(o.split("|")[0].split(":")[2]) for o in state["options"] if o.startswith("block:")}
        expected = {
            c["id"] for seat in heroes for c in state["players"][seat - 1]["field"] if c["name"] == THOPTER
        }
        assert state["local"] == state["loki"] and sources == expected and len(expected) == 3
        report = {"match": match, "checkpoints": self.checkpoints, "declaredSeats": sorted(declared)}
        if self.full_match:
            loki = state["loki"]
            blocker = next(o.split("|")[0] for o in state["options"] if o.startswith("block:"))
            self.submit_option(actor, state, blocker)
            report["result"] = self.finish_match(match, loki, expect_heroes=False)
        return report

    def test_combat(self, loki, heroes):
        actor, p = self.until(lambda _, s: s["turn"] == 3 and s["kind"] == "InputAttack")
        self.combat_checkpoint("Loki declares attackers; vote controls hidden", actor, p)
        self.capture_all("combat")
        opt = next(o.split("|")[0] for o in p["options"] if o.startswith("attack:") and ":player:" in o)
        attack_id = int(opt.split(":")[1])
        self.submit_option(actor, p, opt)
        actor, p = self.decision()
        self.pick(actor, p)
        blocked = []
        while len(blocked) < 2:
            actor, p = self.until(
                lambda _, s: (
                    s["kind"] == "InputBlock"
                    and s["local"] not in blocked
                    and any(
                        o.startswith("block:") and o.split("|")[0].endswith(":" + str(attack_id))
                        for o in s["options"]
                    )
                )
            )
            assert p["local"] in heroes
            self.combat_checkpoint("Hero declares blocker for team", actor, p)
            opt = next(
                o.split("|")[0]
                for o in p["options"]
                if o.startswith("block:") and o.split("|")[0].endswith(":" + str(attack_id))
            )
            blocked.append(p["local"])
            self.submit_option(actor, p, opt)
            actor, p = self.decision()
            self.pick(actor, p)
        actor, p = self.until(lambda _, s: s.get("combatChoice") == "damage")
        self.combat_checkpoint("Loki allocates damage between allies' blockers", actor, p)
        assert p["local"] == loki
        for _ in range(12):
            opts = [o.split("|")[0] for o in p["options"]]
            choice = (
                "confirm-amount"
                if "confirm-amount" in opts
                else next(o for o in opts if o.startswith("amount-add:"))
            )
            self.submit_option(actor, p, choice)
            actor, p = self.decision()
            if not p.get("combatChoice"):
                break
        assert p["kind"] == "InputPassPriority" and p["turn"] == 3
        self.checkpoint("combat damage resolved after declarations and allocation", actor, p, [])

    def run(self):
        for name in NAMES:
            self.launch(name)
            self.wait(lambda name=name: self.state(name).get("local", 0) > 0, "join " + name, 120)
        self.wait(lambda: self.everyone(lambda s: s.get("lobby") == 4), "all joined")
        for n in NAMES:
            self.command(n, "deck")
        self.wait(lambda: self.everyone(lambda s: s.get("decks") == 4), "decks accepted", 90)
        for n in NAMES:
            self.command(n, "ready")
        self.wait(lambda: self.everyone(lambda s: s.get("ready") == 4), "four ready")
        self.command("one", "start")
        opening = self.wait(
            lambda: self.everyone(lambda s: s.get("turn", 0) > 0 or len(s.get("players") or []) == 4),
            "start",
            120,
        )
        for seat, s in enumerate(opening, 1):
            assert s["local"] == seat
            for player in s["players"]:
                holder = player["seat"]
                own_hand = opening[holder - 1]["players"][holder - 1]["hand"]
                shared = holder == seat or (seat != s["loki"] and holder != s["loki"])
                assert player.get("hand") == (own_hand if shared else []), (seat, holder, shared)
            names = [c["name"] for c in s["players"][s["local"] - 1]["hand"]]
            assert THOPTER in names and PACT in names, (s["local"], names)
        visible_hands = [[len(player["hand"]) for player in s["players"]] for s in opening]
        if self.hero_attack:
            # Give Loki a flyer that can legally block each hero's Ornithopter.
            name, state = self.until(
                lambda _, s: (
                    s["turn"] == 1
                    and s["phase"] == "MAIN1"
                    and s["kind"] == "InputPassPriority"
                    and s["local"] == s["loki"]
                    and not s.get("stack")
                )
            )
            self.last_request = state["request"]
            assert self.completed(name, self.command(name, "drag", THOPTER))["actionResult"] == "cast drag"
        a, s = self.until(
            lambda n, s: (
                s["turn"] == 2
                and s["phase"] == "MAIN1"
                and s["kind"] == "InputPassPriority"
                and s["local"] != s["loki"]
            )
        )
        if self.joined_heroes and a == "one":
            self.last_request = s["request"]
            window, host = s["voteWindow"], s["local"]
            self.vote(host, True, window, 1 << (host - 1))
            a, s = self.decision()
            assert a != "one" and s["phase"] == "MAIN1" and s["turn"] == 2
            self.vote(host, False, window, 0)
            a, s = self.decision()
        seat_a = s["local"]
        loki = s["loki"]
        heroes = [seat for seat in range(1, 5) if seat != loki]

        if self.hero_attack:
            return self.test_hero_attack(a, s, heroes)

        def successor(seat):
            return heroes[(heroes.index(seat) + 1) % len(heroes)]

        next_hero = successor(seat_a)
        other_heroes = [
            n
            for n in NAMES
            if NAMES.index(n) + 1 not in (seat_a, loki) and (not self.joined_heroes or n != "one")
        ]
        # Prefer a responder other than the designated next hero to expose a
        # client that accidentally hands the window to the last responder.
        b = next((n for n in other_heroes if NAMES.index(n) + 1 != next_hero), other_heroes[0])
        if self.outage == "other" and 1 in heroes and seat_a != 1:
            b = "one"  # Keep the remaining non-caster hero remote for the outage.
        if self.reoffer_response and not self.outage:
            b = NAMES[next_hero - 1]
        seat_b = NAMES.index(b) + 1
        self.checkpoint("shared priority before simultaneous drags", a, s, [])
        self.last_request = s["request"]
        when = int(time.time() * 1000) + 900
        cb = self.command(b, "drag", PACT, at=when - self.teammate_lead_ms)
        ca = self.command(a, "drag", THOPTER, at=when)
        ra = self.completed(a, ca)
        rb = self.completed(b, cb)
        delta = abs(ra["actedAt"] - rb["actedAt"])
        assert (
            delta < 150 and ra["actionResult"] == "cast drag" and rb["actionResult"] == "unavailable drag"
        ), (delta, ra["actionResult"], rb["actionResult"])
        self.race = {
            "firstSeat": seat_a,
            "secondSeat": seat_b,
            "loki": loki,
            "deltaMs": delta,
            "firstUtcMs": ra["actedAt"],
            "secondUtcMs": rb["actedAt"],
        }
        actor, p = self.until(
            lambda n, s: s["kind"] == "InputPassPriority" and len(s.get("stack") or []) == 1
        )
        assert p["local"] == loki
        original_id = p["stack"][0]["id"]
        self.checkpoint("Loki offered the original spell first", actor, p, [(THOPTER, seat_a)], after_stack=next_hero)
        self.pick(actor, p)
        actor, p = self.until(
            lambda n, s: s["kind"] == "InputPassPriority" and n == b and len(s.get("stack") or []) == 1
        )
        self.checkpoint(
            "teammate may respond; first spell still pending",
            actor,
            p,
            [(THOPTER, seat_a)],
            after_stack=next_hero,
        )
        assert any(c["name"] == PACT for c in p["players"][seat_b - 1]["hand"])
        if self.outage:
            self.outage_result = self.interrupt_responder(b, p)
            print("physical outage", self.outage_result, flush=True)
            if self.outage in ("expire","other"):
                return {"match":p["match"],"outage":self.outage_result,"checkpoints":self.checkpoints}
        self.last_request = p["request"]
        assert self.completed(b, self.command(b, "drag", PACT))["actionResult"] == "cast drag"
        actor, p = self.until(
            lambda n, s: s["kind"] == "InputPassPriority" and len(s.get("stack") or []) == 2
        )
        # Forge projections enumerate the stack in resolution order.
        response_id = p["stack"][0]["id"]
        self.checkpoint(
            "both spells on stack", actor, p, [(PACT, seat_b), (THOPTER, seat_a)], after_stack=next_hero
        )
        self.capture_all()

        def giant(s):
            return any("Giant" in c["name"] for c in s["players"][seat_b - 1]["field"])

        def only_giant(s):
            return giant(s) and all(c["name"] != THOPTER for c in s["players"][seat_a - 1]["field"])

        self.track_passes = True
        self.pick(actor, p)
        if self.outage == "reconnect":
            actor, p = self.until(lambda name, s: name in self.links and s["local"] != loki
                                  and s["kind"] == "InputPassPriority" and len(s["stack"]) == 2)
            self.checkpoint("hero can respond with both spells pending", actor, p,
                            [(PACT, seat_b), (THOPTER, seat_a)], after_stack=next_hero)
            self.outage_result["nested"] = self.interrupt_responder(actor, p)
            self.pick(actor,p)
        actor, p = self.until(
            lambda n, s: s["kind"] == "InputPassPriority" and len(s.get("stack") or []) == 1 and giant(s)
        )
        self.checkpoint(
            "pact resolved; thopter still pending", actor, p, [(THOPTER, seat_a)], only_giant, next_hero
        )
        assert p["local"] == loki and p["stack"][0]["id"] == original_id
        if self.reoffer_response:
            self.last_request = p["request"]
            assert self.completed(actor, self.command(actor, "drag", PACT))["actionResult"] == "cast drag"
            actor, p = self.until(lambda _, s: s["kind"] == "InputPassPriority" and len(s["stack"]) == 2)
            self.checkpoint("Loki responds on second offer of the original spell", actor, p,
                            [(PACT, loki), (THOPTER, seat_a)], only_giant, next_hero)
            for offset in range(1, 5):
                assert p["local"] == (loki - 1 + offset) % 4 + 1 and len(p["stack"]) == 2
                self.pick(actor, p)
                actor, p = self.decision()
            assert p["local"] == loki and [item["id"] for item in p["stack"]] == [original_id]
            self.checkpoint("Loki's response resolved; original offered again", actor, p, [(THOPTER, seat_a)],
                            lambda s: only_giant(s) and any("Giant" in c["name"] for c in s["players"][loki-1]["field"]), next_hero)
        self.pick(actor, p)

        def both(s):
            return giant(s) and any(c["name"] == THOPTER for c in s["players"][seat_a - 1]["field"])

        actor, p = self.until(
            lambda n, s: s["kind"] == "InputPassPriority" and not s.get("stack") and both(s)
        )
        self.checkpoint("thopter resolved; stack empty", actor, p, [], both)
        assert p["phase"] == "MAIN1" and p["turn"] == 2 and p["local"] == next_hero, p
        self.capture_all("next-hero")
        for count, caster in ((2, seat_b), (1, seat_a)):
            passed = [v["seat"] for v in self.priority_passes if len(v["stack"]) == count and
                      (count == 1 or v["stack"][0] == response_id)]
            assert passed == [loki, successor(caster), successor(successor(caster)), caster], (count, passed)

        # Start another stack at sorcery speed, this time with no response.
        self.last_request = p["request"]
        assert self.completed(actor, self.command(actor, "drag", THOPTER))["actionResult"] == "cast drag"
        actor, p = self.until(
            lambda n, s: s["kind"] == "InputPassPriority" and len(s.get("stack") or []) == 1
        )
        end_seat = successor(next_hero)
        self.checkpoint(
            "second hero opens a fresh stack", actor, p, [(THOPTER, next_hero)], after_stack=end_seat
        )
        self.capture_all("unanswered")
        for expected_seat in (loki, end_seat, successor(end_seat), next_hero):
            assert len(p["stack"]) == 1 and p["phase"] == "MAIN1" and p["local"] == expected_seat
            self.pick(actor, p)
            actor, p = self.decision()

        def second_thopter(s):
            return both(s) and any(c["name"] == THOPTER for c in s["players"][next_hero - 1]["field"])

        assert p["local"] == end_seat and p["phase"] == "MAIN1" and p["turn"] == 2
        self.checkpoint("unanswered spell resolved; next hero acts", actor, p, [], second_thopter)

        if self.reoffer_response and not self.full_match:
            return {"match": p["match"], "race": self.race, "visibleHands": visible_hands,
                    "checkpoints": self.checkpoints, "priorityPasses": self.priority_passes}

        window = p["voteWindow"]
        first, second = successor(end_seat), successor(successor(end_seat))
        bit = lambda seat: 1 << (seat - 1)
        self.vote(first, True, window, bit(first))
        self.vote(second, True, window, bit(first) | bit(second))
        actor, p = self.decision()
        self.checkpoint("two heroes ready; third can still act", actor, p, [], second_thopter)
        self.capture_all("votes")
        self.vote(second, False, window, bit(first))
        actor, p = self.decision()
        self.checkpoint("hero cancels; still same main phase", actor, p, [], second_thopter)
        self.vote(end_seat, True, window, bit(first) | bit(end_seat))
        self.wait(lambda: self.state(NAMES[second - 1]).get("canAct"), "unconfirmed teammate gets priority")
        self.vote(second, True, window, p["voters"], locked=True)
        actor, p = self.until(lambda n, s: s["local"] == loki and s["kind"] == "InputPassPriority")
        assert p["phase"] == "MAIN1" and p["votes"] == p["voters"]
        self.checkpoint("all heroes confirmed; Loki gets priority", actor, p, [], second_thopter)
        if self.loki_response:
            self.last_request = p["request"]
            assert self.completed(actor, self.command(actor, "drag", PACT))["actionResult"] == "cast drag"
            actor, p = self.until(
                lambda n, s: s["kind"] == "InputPassPriority" and len(s.get("stack") or []) == 1
            )
            assert not p["voteWindow"]
            self.checkpoint("Loki responds after unanimity", actor, p, [(PACT, loki)])
            actor, p = self.until(
                lambda n, s: s["kind"] == "InputPassPriority" and s["voteWindow"] and not s.get("stack")
            )
            assert p["phase"] == "MAIN1" and p["votes"] == 0 and p["voteWindow"] != window
            self.checkpoint("Loki response resolves; all heroes can act again", actor, p, [])
            self.vote_all(p)
            actor, p = self.until(lambda n, s: s["local"] == loki and s["kind"] == "InputPassPriority")
        self.pick(actor, p)
        actor, p = self.decision()
        assert p["phase"] != "MAIN1" and p["turn"] == 2 and not p["stack"]
        assert not p["votes"]
        self.checkpoint("Loki passes; combat begins with fresh votes", actor, p, [], second_thopter)
        if not self.loki_response and not self.reoffer_response:
            self.test_combat(loki, heroes)
        report = {
            "race": self.race,
            "match": p["match"],
            "visibleHands": visible_hands,
            "checkpoints": self.checkpoints,
            "priorityPasses": self.priority_passes,
        }
        if self.full_match:
            report["result"] = self.finish_match(p["match"], loki, self.loki_response or self.reoffer_response)
        return report

    def close(self):
        for name, proc in self.processes.items():
            if proc.poll() is None:
                self.command(name, "quit")
        for proc in self.processes.values():
            try:
                proc.wait(timeout=12)
            except subprocess.TimeoutExpired:
                proc.terminate()
                proc.wait(timeout=8)
        for link in self.links.values():
            link.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("player", type=Path)
    parser.add_argument("run_dir", type=Path)
    parser.add_argument("--port", type=int, default=18791)
    parser.add_argument("--outage", choices=("reconnect","expire","other"))
    parser.add_argument("--latency-ms", type=int, default=0, help="One-way UDP delay for each remote Unity player.")
    parser.add_argument("--full-match", action="store_true", help="Continue through a natural win and verify all four result screens.")
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--loki-response", action="store_true")
    modes.add_argument("--reoffer-response", action="store_true", help="Loki passes, then responds when the original spell is offered again.")
    modes.add_argument("--hero-attack", action="store_true")
    parser.add_argument(
        "--joined-heroes",
        action="store_true",
        help="Both casting players are remote clients of the LAN host.",
    )
    parser.add_argument(
        "--teammate-lead-ms",
        type=int,
        choices=range(81),
        metavar="0..80",
        default=0,
        help="Start the hero without priority slightly earlier in the same open decision.",
    )
    args = parser.parse_args()
    args.run_dir.mkdir(parents=True, exist_ok=False)
    table = Table(
        args.player.resolve(), args.run_dir.resolve(), args.port, args.joined_heroes, args.teammate_lead_ms
    )
    table.hero_attack = args.hero_attack
    table.loki_response = args.loki_response
    table.reoffer_response = args.reoffer_response
    table.full_match = args.full_match
    table.outage = args.outage
    if args.outage:
        table.joined_heroes = True
    if args.outage or args.latency_ms:
        table.links = {n: DelayedLink(args.port,args.latency_ms) for n in NAMES[1:]}
    try:
        result = table.run()
        if args.outage:
            result["outage"] = table.outage_result
        if table.links:
            result["udp"] = {"oneWayMs":args.latency_ms,
                             "forwarded":{n:link.forwarded for n,link in table.links.items()}}
        (table.folder / "result.json").write_text(
            json.dumps(result, ensure_ascii=False, indent=2), encoding="utf8"
        )
        print("PASS", table.folder, flush=True)
    finally:
        table.close()

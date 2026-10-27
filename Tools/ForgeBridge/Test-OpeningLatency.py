"""Measure real per-match openings and exercise London mulligans; no whole-game coverage."""
import argparse, hashlib, json, os, pathlib, queue, statistics, subprocess, threading, time

ROOT = pathlib.Path(__file__).resolve().parents[2]


def read_deck(path):
    deck = dict(cards=[], sideboard=[], commanders=[])
    section = None
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        if line.startswith('#main-order '):
            deck['mainOrder'] = line.split(' ', 1)[1].split(',')
        elif line == '!side':
            section = 'sideboard'
        elif line.startswith('#'):
            section = {'#main': 'cards', '#sideboard': 'sideboard', '#commander': 'commanders'}.get(line)
        elif section and line.strip():
            count, key = line.split()
            code, number = key.split('/', 1)
            deck[section].append(dict(setCode=code, collectorNumber=number, count=int(count)))
    return deck


def run(runtime, override, decks, folder, ai, format_id, java_options=()):
    folder.mkdir(parents=True)
    config = dict(session='opening-latency', formatId=format_id, seed=42, startingLife=20,
                  firstPlayerSeat=1, aiSeat=ai, decks=decks)
    start = folder / 'start.json'
    start.write_text(json.dumps(config), encoding='utf-8')
    cp = os.pathsep.join(map(str, [*([override] if override else []), runtime / 'classes', runtime / 'lib/*']))
    java = runtime / ('jre/bin/java.exe' if os.name == 'nt' else 'jre/bin/java')
    pending, trace, answered, mulliganed = queue.Queue(), [], set(), set()
    began = time.perf_counter()
    with (folder / 'stderr.txt').open('w', encoding='utf-8') as error:
        process = subprocess.Popen([str(java), *java_options, '-Dmtgb.bridge.lazyCatalog=true', '-Djava.awt.headless=true',
                                    '-Dfile.encoding=UTF-8', '-Duser.home=' + str(folder), '-cp', cp,
                                    'mtgb.bridge.ForgeBridge', str(runtime / 'assets'), str(start)],
                                   cwd=runtime / 'assets', env=dict(os.environ, APPDATA=str(folder), LOCALAPPDATA=str(folder)),
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=error, text=True, encoding='utf-8',
                                   creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))

        def receive():
            for line in process.stdout:
                pending.put((time.perf_counter() - began, line))

        threading.Thread(target=receive, daemon=True).start()

        def send(message):
            process.stdin.write(json.dumps(dict(session=config['session'], **message)) + '\n')
            process.stdin.flush()

        opening, priority, ended, bottoms = None, False, False, set()
        try:
            deadline = time.monotonic() + 90
            while time.monotonic() < deadline:
                try:
                    elapsed, line = pending.get(timeout=.25)
                except queue.Empty:
                    if process.poll() is not None:
                        raise RuntimeError('Native worker exited: ' + str(process.returncode))
                    continue
                message = json.loads(line)
                trace.append(dict(seconds=elapsed, message=message))
                if message['type'] in ('fatal', 'rejected'):
                    raise RuntimeError(message)
                if message['type'] == 'ended':
                    ended = True
                    break
                if message['type'] != 'request':
                    continue
                request = message['request']
                identity = request['requestId']
                if identity in answered:
                    continue
                answered.add(identity)
                seat, kind = request['seat'], request['kind']
                if opening is None:
                    hands = [view['players'][i]['hand'] for i, view in enumerate(message['views'])]
                    assert [len(hand) for hand in hands] == [7, 7], 'Opening hand size'
                    opening = dict(seconds=elapsed, hands=[[card['name'] for card in hand] for hand in hands])
                if kind == 'InputConfirmMulligan':
                    # Both humans mulligan once; native AI makes its own decisions.
                    option = 'ok' if seat in mulliganed else 'cancel'
                    mulliganed.add(seat)
                elif kind == 'InputLondonMulligan':
                    bottoms.add(seat)
                    options = request['options']
                    option = next(o['id'] for o in options if o['id'] == 'cancel' and not o.get('disabled'))
                elif kind == 'InputPassPriority':
                    priority = True
                    assert mulliganed == ({1} if ai == 2 else {1, 2}), 'Missing mulligan seat'
                    if format_id != 'brawl':
                        assert bottoms == mulliganed, 'Missing London bottom choices'
                        assert len(message['views'][0]['players'][0]['hand']) == 6, 'Human did not keep six'
                    send(dict(type='concede', seat=1))
                    continue
                else:
                    raise RuntimeError('Unexpected opening request: ' + kind)
                send(dict(type='respond', seat=seat, requestId=identity, option=option))
            assert opening and priority and ended, 'Opening did not reach priority and end cleanly'
            return dict(status='PASS', **opening, mulliganSeats=sorted(mulliganed), bottomSeats=sorted(bottoms))
        finally:
            if process.poll() is None:
                try:
                    send(dict(type='shutdown'))
                except (BrokenPipeError, OSError):
                    pass
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait()
            (folder / 'trace.json').write_text(json.dumps(trace), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--runtime', type=pathlib.Path, default=ROOT / 'ForgeRuntime')
    parser.add_argument('--override', type=pathlib.Path)
    parser.add_argument('--deck', type=pathlib.Path)
    parser.add_argument('--opponent', type=pathlib.Path)
    parser.add_argument('--ai-seat', type=int, choices=(0, 2), default=0)
    parser.add_argument('--format', choices=('none', 'brawl', 'duelcommander'), default='none')
    parser.add_argument('--runs', type=int, default=3)
    parser.add_argument('--max-opening-seconds', type=float)
    parser.add_argument('--baseline', type=pathlib.Path)
    parser.add_argument('--check-index', action='store_true', help='Also compile/run the prepared-index API fixture')
    parser.add_argument('--java-option', action='append', default=[], help='Isolated JVM experiment; does not change production launch options')
    args = parser.parse_args()
    assert 1 <= args.runs <= 5
    assert bool(args.deck) == bool(args.opponent), 'Supply both decks or neither'
    runtime = args.runtime.resolve()
    override = args.override.resolve() if args.override else None
    out = ROOT / '.tools/opening-latency-1026' / str(time.time_ns())
    out.mkdir(parents=True)
    if args.check_index:
        fixture = out / 'fixture'
        fixture.mkdir()
        cp = os.pathsep.join(map(str, [fixture, *([override] if override else []), runtime / 'classes', runtime / 'lib/*']))
        subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', cp, '-d', str(fixture),
                        str(ROOT / 'Tools/ForgeBridge/tests/PreparedCardNameIndexFixture.java')], check=True)
        java = runtime / ('jre/bin/java.exe' if os.name == 'nt' else 'jre/bin/java')
        with (out / 'index-check.log').open('w', encoding='utf-8') as log:
            subprocess.run([str(java), '-Djava.awt.headless=true', '-cp', cp,
                            'mtgb.bridge.PreparedCardNameIndexFixture', str(out / 'index')],
                           stdout=log, stderr=log, timeout=20, check=True)
    if args.deck:
        decks = [read_deck(args.deck), read_deck(args.opponent)]
    else:
        names = {}
        for line in (runtime / 'catalog.jsonl').open(encoding='utf-8'):
            row = json.loads(line)
            if row['name'] in ('Forest', 'Island', 'Kinnan, Bonder Prodigy') and not row.get('ambiguous'):
                names.setdefault(row['name'], row['key'])
        def entry(name, count):
            code, number = names[name].split('/', 1)
            return dict(setCode=code, collectorNumber=number, count=count)
        deck = dict(cards=[entry('Forest', 30), entry('Island', 30)], sideboard=[], commanders=[])
        if args.format != 'none':
            deck = dict(cards=[entry('Forest', 99)], sideboard=[], commanders=[entry('Kinnan, Bonder Prodigy', 1)])
        decks = [deck, deck]
    results = []
    summary = dict(status='RUNNING', results=results, runtime=str(runtime),
                   override=str(override), runtimeHash=json.loads((runtime / 'runtime.json').read_text())['runtimeHash'],
                   inputSha256=hashlib.sha256(json.dumps(decks, sort_keys=True).encode()).hexdigest())
    summary['preparedIndexChecks'] = args.check_index
    summary.update(formatId=args.format, aiSeat=args.ai_seat, seed=42, javaOptions=args.java_option)
    summary['overrideClasses'] = {p.relative_to(override).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                                  for p in sorted(override.rglob('*.class'))} if override else {}
    try:
        for index in range(args.runs):
            result = run(runtime, override, decks, out / str(index), args.ai_seat, args.format, args.java_option)
            results.append(result)
            print(json.dumps(result), flush=True)
        median = statistics.median(r['seconds'] for r in results)
        summary.update(status='PASS', medianOpeningSeconds=median)
        if args.baseline:
            baseline = json.loads(args.baseline.read_text(encoding='utf-8'))
            assert summary['inputSha256'] == baseline['inputSha256'], 'Different decks'
            assert all(summary[key] == baseline[key] for key in ('formatId', 'aiSeat', 'seed')), 'Different match settings'
            assert results[0]['hands'] == baseline['results'][0]['hands'], 'Opening cards changed'
            assert median < baseline['medianOpeningSeconds'] * .75, 'Less than 25% improvement'
        if args.max_opening_seconds:
            assert median < args.max_opening_seconds, 'Opening is too slow: ' + str(median)
    except Exception as error:
        summary.update(status='FAIL', error=repr(error))
        raise
    finally:
        (out / 'result.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
        print('Evidence: ' + str(out / 'result.json'), flush=True)


if __name__ == '__main__':
    main()

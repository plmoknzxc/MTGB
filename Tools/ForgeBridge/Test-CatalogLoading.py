"""Bounded native catalog regression/performance probe. Does not claim card-effect coverage."""
import argparse, hashlib, json, os, pathlib, statistics, subprocess, time

ROOT = pathlib.Path(__file__).resolve().parents[2]
RT = ROOT / 'ForgeRuntime'

def main():
    global RT
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--override', type=pathlib.Path)
    parser.add_argument('--runtime', type=pathlib.Path, default=RT)
    parser.add_argument('--baseline', type=pathlib.Path)
    parser.add_argument('--runs', type=int, default=3)
    args = parser.parse_args()
    RT = args.runtime.resolve()
    assert 1 <= args.runs <= 10
    out = ROOT / '.tools/catalog-loading-1016' / str(time.time_ns())
    out.mkdir(parents=True)
    classes = out / 'classes'
    cp = os.pathsep.join(map(str, [classes, *([args.override.resolve()] if args.override else []), RT / 'classes', RT / 'lib/*']))
    source = ROOT / 'Tools/ForgeBridge/tests/CatalogLoadingFixture.java'
    subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', cp, '-d', str(classes), str(source)], check=True)
    deck = dict(cards=[dict(setCode='10E', collectorNumber='380', count=60)], sideboard=[], attractions=[], contraptions=[])
    start = out / 'start.json'
    start.write_text(json.dumps(dict(session='catalog-loading', seed=42, startingLife=20, firstPlayerSeat=1, useAnte=False, decks=[deck, deck])), encoding='utf8')
    results = []
    for i in range(args.runs):
        result_path = out / f'run-{i}.json'
        command = [str(RT / 'jre/bin/java.exe'), '-Xms32m', '-Xmx384m', '-XX:ActiveProcessorCount=2', '-Dmtgb.bridge.lazyCatalog=true', '-Djava.awt.headless=true', '-Dfile.encoding=UTF-8', '-Duser.home=' + str(out / 'home'), '-Dcatalog.output=' + str(result_path), '-Dcatalog.verifyIndex=' + str(i == 0).lower(), '-cp', cp, 'mtgb.bridge.CatalogLoadingFixture', str(RT / 'assets'), str(start)]
        with (out / f'run-{i}.log').open('w', encoding='utf8') as log:
            process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=log, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            try:
                code = process.wait(timeout=90)
                assert code == 0, f'Native probe failed: {out / f"run-{i}.log"}'
            finally:
                if process.poll() is None: process.kill(); process.wait(timeout=10)
        result = json.loads(result_path.read_text(encoding='utf8'))
        assert result['success']
        results.append(result)
    assert len({r['catalogSha256'] for r in results}) == 1, 'Nondeterministic catalog'
    baseline = json.loads(args.baseline.read_text(encoding='utf8')) if args.baseline else None
    median = statistics.median(r['loadMs'] for r in results)
    if baseline:
        assert results[0]['catalogSha256'] == baseline['catalogSha256'], 'Catalog changed'
        assert median < baseline['loadMs'] * .7, 'Full load improved by less than 30%'
    summary = dict(status='PASS', scope='Native catalog APIs only; not card-effect coverage', maxHeapMiB=384, medianLoadMs=median, baseline=baseline, results=results, fixtureSha256=hashlib.sha256(source.read_bytes()).hexdigest(), runtimeHash=json.loads((RT / 'runtime.json').read_text(encoding='utf-8-sig'))['runtimeHash'], override=str(args.override) if args.override else None)
    (out / 'result.json').write_text(json.dumps(summary, indent=2), encoding='utf8')
    print(json.dumps(dict(status='PASS', medianLoadMs=median, result=str(out / 'result.json'))), flush=True)

if __name__ == '__main__': main()

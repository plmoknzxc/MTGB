"""Exercise native pool publication under concurrent mutation (not a rule-effect test).

Optional --overlay supplies an isolated candidate build before ForgeRuntime.
"""
import argparse
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / 'ForgeRuntime'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--overlay', type=Path)
    args = parser.parse_args()
    out = ROOT / '.tools/gameplay-audit-1022' / ('snapshot-' + str(time.time_ns()))
    out.mkdir(parents=True)
    classes = out / 'classes'
    classes.mkdir()
    dependencies = ([str(args.overlay.resolve())] if args.overlay else []) + [str(RT / 'classes'), str(RT / 'lib/*')]
    subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', ';'.join(dependencies), '-d', str(classes),
                    str(ROOT / 'Tools/ForgeBridge/tests/ManaSnapshotFixture.java')], check=True)
    cfg = json.loads((RT / 'runtime.json').read_text('utf-8-sig'))
    result = out / 'result.txt'
    deck = {'cards': [{'setCode': '10E', 'collectorNumber': '376', 'count': 60}], 'sideboard': []}
    with tempfile.TemporaryDirectory(dir=out) as temp, (out / 'engine.log').open('w', encoding='utf8') as log:
        start = Path(temp) / 'start.json'
        start.write_text(json.dumps(dict(session='snapshot', seed=4, firstPlayerSeat=1, startingLife=20,
                                        decks=[deck, deck], snapshotResult=str(result))), 'utf8')
        proc = subprocess.Popen([str(RT / cfg['java']), '-Xmx384m', '-Djava.awt.headless=true',
                                 '-cp', ';'.join([str(classes), *dependencies]), 'mtgb.bridge.ManaSnapshotFixture',
                                 str(RT / cfg['assets']), str(start)], cwd=RT / cfg['assets'],
                                env=dict(os.environ, APPDATA=temp, LOCALAPPDATA=temp, TEMP=temp, TMP=temp),
                                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding='utf8')
        messages = queue.Queue()

        def read():
            try:
                for line in proc.stdout:
                    messages.put(json.loads(line))
            finally:
                messages.put(None)

        threading.Thread(target=read, daemon=True).start()
        try:
            for _ in range(100):
                message = messages.get(timeout=45)
                assert message is not None and message['type'] not in ('fatal', 'ended'), message
                if result.exists():
                    print(result.read_text('utf8').strip())
                    print(out)
                    return
                if message['type'] == 'request':
                    request = message['request']
                    options = [o['id'] for o in request['options']]
                    option = 'keep' if 'keep' in options else 'ok'
                    assert option in options, request
                    proc.stdin.write(json.dumps(dict(type='respond', session='snapshot', seat=request['seat'],
                                                     requestId=request['requestId'], option=option)) + '\n')
                    proc.stdin.flush()
            raise AssertionError('Snapshot fixture did not complete')
        except BaseException as error:
            (out / 'failure.txt').write_text(repr(error), 'utf8')
            raise
        finally:
            if proc.poll() is None:
                proc.kill()
            proc.wait()


if __name__ == '__main__':
    main()

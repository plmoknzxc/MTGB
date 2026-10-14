"""R21 focused regression: native exert flow plus read-only grouping gates.

Reuses the existing actual attack-two consumer; does not add an effect first.
"""
import importlib.util, json, subprocess, os, queue, tempfile, threading, time, hashlib, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '.tools/group-safety-1007'
OUT.mkdir(parents=True, exist_ok=True)
spec = importlib.util.spec_from_file_location('exert_group_driver', ROOT / 'Tools/ForgeBridge/Test-ExertBranches.py')
driver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(driver)
driver.OUT = OUT / 'native'
driver.OUT.mkdir(exist_ok=True)
driver.CLASSES = driver.OUT / 'classes'
driver.CLASSES.mkdir(exist_ok=True)

# Only observation changes. Initial positions, native choices and assertions
# remain the original actual attack-two flow, including both later untaps.
source = (ROOT / 'Tools/ForgeBridge/tests/ExertBranchFixture.java').read_text(encoding='utf-8-sig')
needle = 'cards.add(Map.of("id",card.getId(),"name",card.getName(),"tapped",card.isTapped(),"exertedThisTurn",card.getExertedThisTurn(),"waitingUntapSeats",waiting));'
replacement = '''Map<String,Object> gates=new LinkedHashMap<>();
                                gates.put("changedTraits",!card.getChangedCardTraits().isEmpty());
                                gates.put("changedKeywords",!card.getChangedCardKeywords().isEmpty());
                                gates.put("activatedThisTurn",!card.getAbilityActivatedThisTurn().isEmpty());
                                gates.put("activatedThisGame",!card.getAbilityActivatedThisGame().isEmpty());
                                gates.put("remembered",card.hasRemembered());
                                gates.put("cloned",card.isCloned());
                                gates.put("attached",card.isAttachedToEntity());
                                cards.add(Map.of("id",card.getId(),"name",card.getName(),"tapped",card.isTapped(),"exertedThisTurn",card.getExertedThisTurn(),"waitingUntapSeats",waiting,"gates",gates));'''
assert source.count(needle) == 1
fixture = OUT / 'ExertBranchFixture.java'
fixture.write_text(source.replace(needle, replacement), encoding='utf8')
driver.FIXTURES = [ROOT / 'Tools/ForgeBridge/tests/StateBranchFixture.java', fixture]
subprocess.run(['javac', '-encoding', 'UTF-8', '-cp', str(driver.RT / 'classes') + ';' + str(driver.RT / 'lib/*'), '-d', str(driver.CLASSES), *map(str, driver.FIXTURES)], check=True)
# Bound heap/CPU and keep temporary files on the workspace drive.
original_popen = driver.subprocess.Popen
def limited_popen(args, **kwargs):
    args = list(args)
    args[1:1] = ['-Xmx768m', '-XX:ActiveProcessorCount=4']
    return original_popen(args, **kwargs)
driver.subprocess.Popen = limited_popen
if "--land-only" not in sys.argv:
    try:
        result = driver.run('attack-two')
    finally:
        driver.subprocess.Popen = original_popen
    folder = ROOT / result['folder']
    assert result['status'] == 'PASS', result
    observations = []
    for label in ['before', 'effect', 'pending-after-eot', 'next-untap', 'following-untap']:
        view = json.loads((folder / (label + '.json')).read_text('utf8'))
        audit = json.loads((folder / ('audit-' + str(view['request']['requestId']) + '.json')).read_text('utf8'))
        cards = [c for c in view['players'][0]['battlefield'] if not c['land']]
        assert len(cards) == len(audit['cards']) == 2
        for native in audit['cards']:
            projected = next(c for c in cards if c['id'] == native['id'])
            blocked = [k for k,v in native['gates'].items() if v]
            if label == 'before':
                assert not blocked and projected['stackablePermanent']
            else:
                assert blocked and not projected['stackablePermanent'], (label, native, projected)
            observations.append(dict(label=label, id=native['id'], gates=blocked, stackable=projected['stackablePermanent']))
    value = dict(status='PASS', classification='actual-existing-flow-regression-with-new-readonly-adapter-assertions', native=result, observations=observations)
    (OUT / 'gate-result.json').write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(json.dumps(value, ensure_ascii=False))

# One new actual identity boundary: second of two identical lands, not a
# direct-state adapter. Other exert assertions above remain a regression.
land = OUT / 'land' / str(time.time_ns())
land.mkdir(parents=True)
def save(m, label):
    for seat,v in enumerate(m['views'],1):
        v=dict(v, request=m['request'], awaitingInput=True)
        (land/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
with tempfile.TemporaryDirectory(dir=OUT, prefix='land-temp-') as tmp, (land/'engine.log').open('w',encoding='utf8') as log, (land/'trace.jsonl').open('w',encoding='utf8') as trace:
    start=Path(tmp)/'start.json'
    audit=Path(tmp)/'audit';audit.mkdir()
    start.write_text(json.dumps(dict(session='grouped-land',auditDir=str(audit),own={'Battlefield':['Mountain','Mountain'],'Library':['Mountain']},other={'Library':['Mountain']},seed=4,firstPlayerSeat=1,startingLife=20,decks=[driver.DECK,driver.DECK])),encoding='utf8')
    proc=subprocess.Popen([str(driver.RT/driver.RUNTIME['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[driver.CLASSES,driver.RT/'classes',driver.RT/'lib/*'])),'mtgb.bridge.ExertBranchFixture',str(driver.RT/driver.RUNTIME['assets']),str(start)],cwd=driver.RT/driver.RUNTIME['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
    messages=queue.Queue()
    def read():
        try:
            for line in proc.stdout:messages.put(json.loads(line))
        finally:messages.put(None)
    threading.Thread(target=read,daemon=True).start()
    selected=None
    try:
        for _ in range(80):
            m=messages.get(timeout=35);assert m is not None,'Native exit'
            trace.write(json.dumps(m)+'\n');trace.flush()
            assert m['type'] not in ['fatal','ended','rejected'],m
            if m['type']!='request':continue
            r=m['request'];ids=[o['id'] for o in r['options']];v=m['views'][0]
            if r['kind']=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
                cards=v['players'][0]['battlefield']
                if selected is None:
                    assert len(cards)==2 and all(c['name']=='Mountain' and not c['tapped'] and c['stackablePermanent'] for c in cards)
                    assert v['players'][0]['manaPool']==[0]*6
                    selected=cards[1]['id'];pick='card:'+str(selected);assert pick in ids
                    save(m,'before')
                else:
                    assert [c['id'] for c in cards if c['tapped']]==[selected]
                    assert v['players'][0]['manaPool']==[0,0,0,1,0,0]
                    assert {c['id'] for c in m['views'][1]['players'][0]['battlefield'] if c['tapped']}=={selected}
                    save(m,'resolved');break
            else:
                assert 'ok' in ids,(r['kind'],ids);pick='ok'
            proc.stdin.write(json.dumps(dict(type='respond',session='grouped-land',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
        else:raise AssertionError('No grouped-land result')
    finally:
        if proc.poll() is None:proc.kill();proc.wait()
result=dict(status='PASS',case='grouped-land',folder=land.relative_to(ROOT).as_posix(),runtimeHash=driver.RUNTIME['runtimeHash'],driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),selectedId=selected,result='PASS grouped-land: actual second identical Mountain ID tapped once, first untouched, red pool1, both seat projections and main priority recover')
(land/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
(OUT/'grouped-land-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(result['result'])

"""Bounded native AI strategy proof; no Unity, installed player or server changes."""
import hashlib,json,os,queue,subprocess,threading,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
RUNTIME=ROOT/'ForgeRuntime'
WORK=ROOT/'.tools/ai-strategy-1028'

def main():
    WORK.mkdir(exist_ok=True)
    (WORK/'classes').mkdir(exist_ok=True)
    subprocess.run(['javac','-encoding','UTF-8','-cp',os.pathsep.join(map(str,[RUNTIME/'classes',RUNTIME/'lib/*'])),
        '-d',str(WORK/'classes'),str(ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java'),
        str(ROOT/'Tools/ForgeBridge/tests/AiStrategyFixture.java')],check=True)
    folder=WORK/time.strftime('run-%Y%m%d-%H%M%S');folder.mkdir()
    names={}
    for line in (RUNTIME/'catalog.jsonl').open(encoding='utf8'):
        c=json.loads(line)
        if not c.get('ambiguous'):names.setdefault(c['name'],c['key'])
    def entry(name,count):
        s,c=names[name].split('/',1);return dict(setCode=s,collectorNumber=c,count=count)
    deck=lambda pairs:dict(cards=[entry(n,c) for n,c in pairs],sideboard=[],commanders=[])
    config=dict(session='strategy',formatId='none',seed=812,startingLife=20,firstPlayerSeat=1,aiSeat=2,
        decks=[deck([('Island',60)]),deck([('Island',24),('Opt',12),('Counterspell',12),('Concentrate',12)])],
        strategyResult=str(folder/'result.json'))
    path=folder/'start.json';path.write_text(json.dumps(config),encoding='utf8')
    cp=os.pathsep.join(map(str,[WORK/'classes',RUNTIME/'classes',RUNTIME/'lib/*']))
    command=[str(RUNTIME/'jre/bin/java.exe'),'-Xmx384m','-XX:ActiveProcessorCount=2',
        '-Dmtgb.bridge.lazyCatalog=true','-Djava.awt.headless=true','-Dfile.encoding=UTF-8',f'-Duser.home={folder}',
        '-cp',cp,'mtgb.bridge.AiStrategyFixture',str(RUNTIME/'assets'),str(path)]
    q=queue.Queue();env=dict(os.environ,APPDATA=str(folder),LOCALAPPDATA=str(folder))
    with (folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'wire.jsonl').open('w',encoding='utf8') as wire:
        proc=subprocess.Popen(command,cwd=RUNTIME/'assets',env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
        def reader():
            for line in proc.stdout:q.put(line)
            q.put(None)
        threading.Thread(target=reader,daemon=True).start()
        start=time.monotonic()
        try:
            while time.monotonic()-start<100:
                try:line=q.get(timeout=.5)
                except queue.Empty:continue
                if line is None:break
                wire.write(line);wire.flush()
                m=json.loads(line)
                if m['type']=='fatal':raise RuntimeError(str(m))
                if m['type']!='request':continue
                r=m['request'];opts=[o for o in r['options'] if not o.get('disabled')]
                option=next((o for o in opts if o['id']=='ok'),None) or next((o for o in opts if o['id']=='cancel'),None) or opts[0]
                proc.stdin.write(json.dumps(dict(type='respond',session='strategy',seat=r['seat'],requestId=r['requestId'],option=option['id']))+'\n');proc.stdin.flush()
            code=proc.wait(timeout=3)
        finally:
            if proc.poll() is None:proc.kill();proc.wait()
    result=json.loads((folder/'result.json').read_text(encoding='utf8'))
    result['seconds']=round(time.monotonic()-start,3)
    result['processExit']=code
    result['identity']={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in [
        ROOT/'Tools/ForgeBridge/java/mtgb/bridge/BridgeAiStrategy.java',ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',
        ROOT/'Tools/ForgeBridge/NativePatches.json',RUNTIME/'classes/mtgb/bridge/BridgeAiStrategy.class']}
    (folder/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(dict(status=result['status'],checks=len(result['checks']),seconds=result['seconds'],result=str(folder/'result.json'),error=result.get('error')),ensure_ascii=False))
    return result['status']!='PASS' or code!=0

if __name__=='__main__':raise SystemExit(main())

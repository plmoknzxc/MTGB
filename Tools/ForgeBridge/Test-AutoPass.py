"""Real Forge response availability; Unity's ForgeControlModeValidation checks auto-pass policy."""
import concurrent.futures, json, os, queue, subprocess, tempfile, threading
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/AutoPass';out.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(root/'.tools/auto-pass/classes'),str(root/'Tools/ForgeBridge/tests/AutoPassFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
cases={'none':False,'tapped':False,'restricted-land':False,'unaffordable-ability':False,'land':False,'mana-creature':False,'instant':True,'spell-with-mana':True,'activated':True,'flashback':True}
def run(case):
    with tempfile.TemporaryDirectory(prefix='mtgb-auto-pass-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log:
        start=Path(temp)/'start.json';start.write_text(json.dumps({'session':case,'fixture':case,'seed':4,'startingLife':20,'firstPlayerSeat':1,'decks':[deck,deck]}),encoding='utf-8')
        p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(root/'.tools/auto-pass/classes')+';'+str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.AutoPassFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue();threading.Thread(target=lambda:[messages.put(json.loads(line)) for line in p.stdout],daemon=True).start()
        own=0;kept=set();opponent=False;requests=[]
        try:
            for _ in range(100):
                m=messages.get(timeout=50)
                assert m['type'] not in ('fatal','rejected','ended'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][r['seat']-1];requests.append({'request':r,'view':v})
                if 'Mulligan' in r['kind']:kept.add(r['seat'])
                if r['kind']=='InputPassPriority':
                    if r['seat']==2:
                        opponent=bool(v['canRespond'])
                        assert opponent==cases[case],f'{case}: wrong response availability: {opponent}'
                        assert v['turnSeat']==1
                        if opponent:assert any(o['id'].startswith('card:') for o in r['options']),r
                        break
                    own+=1
                    if own==3:
                        assert not cases[case],f'{case}: legitimate action was auto-passed'
                        break
                p.stdin.write(json.dumps({'type':'respond','session':case,'seat':r['seat'],'requestId':r['requestId'],'option':'ok'})+'\n');p.stdin.flush()
            else:raise AssertionError('No priority outcome')
            assert kept=={1,2},'Mulligan was auto-answered'
            assert own>0 and opponent==cases[case]
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()
            (out/(case+'.json')).write_text(json.dumps(requests),encoding='utf-8')
        return 'PASS '+case+(': legal response reported' if opponent else ': no response reported for Unity AUTO policy')

try:
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        results=list(executor.map(run,cases))
    text='\n'.join(results);(out/'result.txt').write_text(text,encoding='utf-8');print(text)
except Exception as e:
    (out/'result.txt').write_text('FAIL '+repr(e),encoding='utf-8');raise

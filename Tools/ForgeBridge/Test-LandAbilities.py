"""Direct land clicks: no Mountain confirmation; Shivan Reef choices retain source identity."""
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time

root=Path(__file__).resolve().parents[2]
runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/LandInteraction'
out.mkdir(parents=True,exist_ok=True)
for name,number in [('Mountain','376'),('Shivan Reef','357')]:
    result=out/(number+'-result.txt');result.write_text('RUNNING',encoding='utf-8')
    with tempfile.TemporaryDirectory(prefix='mtgb-land-') as temp, (out/(number+'-engine.log')).open('w',encoding='utf-8') as log:
        deck={'cards':[{'setCode':'10E','collectorNumber':number,'count':60}],'sideboard':[]}
        start=Path(temp)/'start.json';start.write_text(json.dumps({'session':'land','seed':4,'startingLife':20,'firstPlayerSeat':1,'decks':[deck,deck]}),encoding='utf-8')
        p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.ForgeBridge',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue();threading.Thread(target=lambda:[messages.put(json.loads(line)) for line in p.stdout],daemon=True).start()
        seen=set();activated=False;saw_choices=False;land_id=0;deadline=time.monotonic()+60
        try:
            while time.monotonic()<deadline:
                msg=messages.get(timeout=15)
                if msg['type']=='rejected' and msg.get('error')=='stale-request':continue
                assert msg['type'] not in ('fatal','rejected','ended'),msg
                if msg['type']!='request':continue
                req=msg['request'];view=msg['views'][req['seat']-1]
                if req['requestId'] in seen:continue
                seen.add(req['requestId']);options=req['options'];assert options
                pick=next((o for o in options if o['id']=='ok'),options[0])
                if req['seat']==1:
                    own=view['players'][0]
                    if req['kind']=='InputPassPriority' and view['phase']=='MAIN1':
                        if not own['battlefield']:pick=next(o for o in options if o['label']==name+' [Hand]')
                        elif not activated:
                            land_id=own['battlefield'][0]['id'];pick=next(o for o in options if o['id']=='card:'+str(land_id));activated=True
                        else:
                            assert own['battlefield'][0]['tapped']
                            assert sum(own['manaPool'])==1,(own,req)
                            assert own['life']==20
                            if name=='Mountain':assert own['redMana']==1 and not saw_choices
                            else:assert saw_choices and own['manaPool'][5]==1
                            text='PASS: '+name+' direct click; '+('no dialog, tapped and red mana +1' if name=='Mountain' else 'multiple source-linked choices; selected colorless, tapped and mana +1')
                            result.write_text(text,encoding='utf-8');print(text,flush=True);break
                    elif req['kind']=='dialog':
                        assert name!='Mountain','Single-ability Mountain still requests confirmation'
                        if not activated:
                            pick=next(o for o in options if o['label']=='Play land')
                            p.stdin.write(json.dumps({'type':'respond','session':'land','seat':req['seat'],'requestId':req['requestId'],'option':pick['id']})+'\n');p.stdin.flush();continue
                        assert activated and req['sourceCardId']==land_id,(land_id,req)
                        assert len(options)>1
                        saw_choices=True;pick=next(o for o in options if '{C}' in o['label'])
                p.stdin.write(json.dumps({'type':'respond','session':'land','seat':req['seat'],'requestId':req['requestId'],'option':pick['id']})+'\n');p.stdin.flush()
            else:raise RuntimeError('Land test timeout')
        except Exception as e:
            result.write_text('FAIL: '+repr(e),encoding='utf-8');raise
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()

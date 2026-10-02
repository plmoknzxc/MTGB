"""Real Forge: two lands cannot propose Hill Giant; tap/undo; four lands can cast it."""
import json, os, queue, subprocess, tempfile, threading, time
from pathlib import Path

root=Path(__file__).resolve().parents[2]
runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/Affordability';out.mkdir(parents=True,exist_ok=True)
result=out/'result.txt';result.write_text('RUNNING',encoding='utf-8')
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':40},
               {'setCode':'10E','collectorNumber':'212','count':20}],'sideboard':[]}
with tempfile.TemporaryDirectory(prefix='mtgb-afford-') as temp, (out/'engine.log').open('w',encoding='utf-8') as log, (out/'trace.jsonl').open('w',encoding='utf-8') as trace:
    start=Path(temp)/'start.json'
    start.write_text(json.dumps({'session':'afford','seed':4,'startingLife':20,'firstPlayerSeat':1,'decks':[deck,deck]}),encoding='utf-8')
    p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.ForgeBridge',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
    messages=queue.Queue();threading.Thread(target=lambda:[messages.put(json.loads(line)) for line in p.stdout],daemon=True).start()
    seen=set();tap_stage=0;checked_two=False;cast=False;passes=[];deadline=time.monotonic()+100
    try:
        while time.monotonic()<deadline:
            msg=messages.get(timeout=20)
            if msg['type']=='rejected' and msg.get('error')=='stale-request':continue
            assert msg['type'] not in ('fatal','rejected','ended'),msg
            if msg['type']!='request':continue
            req=msg['request']
            if req['requestId'] in seen:continue
            seen.add(req['requestId']);v=msg['views'][req['seat']-1];own=v['players'][0];opts=req['options']
            trace.write(json.dumps({'request':req,'view':v})+'\n');trace.flush()
            pick=next((o for o in opts if o['id']=='ok'),opts[0])
            if req['kind']=='InputPassPriority' and v['stackCount'] and cast:
                passes.append(req['seat'])
                assert len(v['stackItems'])==v['stackCount']
                assert v['stackItems'][0]['source']['name']=='Hill Giant'
                assert v['stackItems'][0]['source']['printingKey']=='10E/212'
            if req['seat']==1 and any(c['name']=='Hill Giant' for c in own['battlefield']):
                assert checked_two and tap_stage==3 and cast and passes==[1,2],(checked_two,tap_stage,passes)
                text='PASS: 2 lands reject Hill Giant proposal; both tap and undo restore zero mana/untapped; 4 lands cast through payment and both priority passes.'
                result.write_text(text,encoding='utf-8');print(text);break
            lands=[c for c in own['battlefield'] if c['land']]
            if req['kind']=='InputPassPriority' and v['phase']=='MAIN1' and not v['stackCount']:
                giant=next((o for o in opts if o['label']=='Hill Giant [Hand]'),None)
                if req['seat']==1 and len(lands)==2:
                    assert any(c['name']=='Hill Giant' for c in own['hand']), 'Fixture missing Hill Giant'
                    assert giant is None, 'Two-land Hill Giant is still offered'
                    checked_two=True
                    if tap_stage==0:tap_stage=1
                    if tap_stage==1:
                        untapped=next((c for c in lands if not c['tapped']),None)
                        if untapped:pick=next(o for o in opts if o['id']=='card:'+str(untapped['id']))
                        else:tap_stage=2
                    if tap_stage==2:
                        if any(c['tapped'] for c in lands):pick=next(o for o in opts if o['id']=='cancel')
                        else:
                            assert sum(own['manaPool'])==0
                            tap_stage=3
                if req['seat']==1 and len(lands)>=4 and not cast:
                    assert giant is not None,'Affordable Hill Giant not offered'
                    pick=giant;cast=True
                elif not (req['seat']==1 and tap_stage in (1,2)):
                    pick=next((o for o in opts if o['label']=='Mountain [Hand]'),pick)
            elif req['kind'].startswith('InputPayMana') and req['seat']==1:
                ids={'card:'+str(c['id']) for c in lands if not c['tapped']}
                pick=next((o for o in opts if o['id']=='mana:R' or o['id'] in ids),pick)
            p.stdin.write(json.dumps({'type':'respond','session':'afford','seat':req['seat'],'requestId':req['requestId'],'option':pick['id']})+'\n');p.stdin.flush()
        else:raise RuntimeError('Affordability timeout')
    except Exception as e:
        result.write_text('FAIL: '+repr(e),encoding='utf-8');raise
    finally:
        if p.poll() is None:
            p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
            try:p.wait(timeout=3)
            except subprocess.TimeoutExpired:p.kill();p.wait()

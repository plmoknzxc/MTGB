"""Native Foretell/Plot special actions, delayed exile casts, taxed costs and privacy.
Usage: python -X utf8 Tools/ForgeBridge/Test-ExileAlternativeCosts.py foretell
"""
import argparse,json,os,queue,subprocess,tempfile,threading,time,re,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';W=R/'.tools/exile-alternative-1027';W.mkdir(exist_ok=True)
p=argparse.ArgumentParser();p.add_argument('case',choices=['foretell','plot','external-foretell']);p.add_argument('--tax',type=int,choices=[0,1],default=1);p.add_argument('--classes');p.add_argument('--out',default='before');a=p.parse_args();out=W/(a.out+'-'+a.case+'-'+str(time.time_ns()));out.mkdir(exist_ok=True)
(W/'classes').mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+os.pathsep+str(RT/'lib/*'),'-d',str(W/'classes'),str(R/'Tools/ForgeBridge/tests/ExileAlternativeFixture.java')],check=True)
name="Sarulf's Packmate" if a.case=='foretell' else 'Grizzly Bears' if a.case=='external-foretell' else 'Slickshot Show-Off';stage=0;active=False;known=None;cast=None
with tempfile.TemporaryDirectory(dir=out) as tmp,(out/'engine.log').open('w',encoding='utf-8') as log,(out/'trace.jsonl').open('w',encoding='utf-8') as trace:
 cfg=dict(session='known',case=a.case,tax=a.tax,seed=4,startingLife=20,firstPlayerSeat=1,decks=[dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])]*2);start=Path(tmp)/'start.json';start.write_text(json.dumps(cfg));q=queue.Queue()
 cp=os.pathsep.join(([str(Path(a.classes).resolve())] if a.classes else [])+[str(W/'classes'),str(RT/'classes'),str(RT/'lib/*')])
 proc=subprocess.Popen([str(RT/'jre/bin/java.exe'),'-Xmx768m','-XX:CICompilerCount=2','-Dmtgb.bridge.lazyCatalog=true','-Djava.awt.headless=true','-cp',cp,'mtgb.bridge.ExileAlternativeFixture',str(RT/'assets'),str(start.resolve())],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
 threading.Thread(target=lambda:[q.put(json.loads(s)) for s in proc.stdout],daemon=True).start()
 try:
  for step in range(320):
   m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected','ended'],m
   if m['type']!='request':continue
   r=m['request'];v=m['views'][0];own=v['players'][0];opts=[o for o in r['options'] if not o.get('disabled')];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
   for e in v['stackItems']:
    if e.get('kind')=='spell' and e['source']['name']==name:cast=e
   if kind=='InputPassPriority':
    ex=next((c for c in own['exileCards'] if c.get('name')==name or c.get('knownFaceName')==name),None)
    if ex and stage==0:
     stage=1;active=False;(out/'exiled.json').write_text(json.dumps(v));assert not any(c.get('knownFaceName')==name for c in m['views'][1]['players'][0]['exileCards'])
    if stage==1 and v['turn']<3:
     assert 'card:'+str(ex['id']) not in ids,'Exile alternative offered before a later turn'
    if ex and a.case!='plot':
     hidden=next(c for c in m['views'][1]['players'][0]['exileCards'] if c['id']==ex['id'])
     assert hidden.get('name')=='Face-down card' and not hidden.get('knownFaceName') and not hidden.get('previewManaCost'),'Private exile face leaked to opponent'
    if stage==1 and v['turn']>=3 and v['phase']=='MAIN1' and r['seat']==1 and not active:
     known=next(c for c in v['playableExile'] if c['id']==ex['id']);pick='card:'+str(ex['id']);assert pick in ids;active=True;stage=2;(out/'available.json').write_text(json.dumps(v))
     assert not any(c.get('tapped') for c in own['battlefield'] if c.get('land')),'Preview spent a land'
    elif stage==0 and v['phase']=='MAIN1' and r['seat']==1 and not active:
     card=next(c for c in own['hand'] if c['name']==name);pick='card:'+str(card['id']);active=True
    if stage==2 and cast and any(c['name']==name for c in own['battlefield']) and not v['stackItems']:
     spent=len(re.findall(r'\{[^}]+\}',cast['payment']['spentMana']));expected=(2 if a.case=='foretell' else 1 if a.case=='external-foretell' else 0)+a.tax;assert spent==expected,(spent,cast)
     assert cast['source']['id']==known['id'] and any(c['id']==known['id'] for c in own['battlefield']),'Exile cast changed card identity'
     assert known.get('previewManaCost')==(('{'+str(expected-1)+'}' if expected>1 else '')+'{G}' if a.case!='plot' else '{'+str(expected)+'}'),('incorrect native exile preview',known)
     result=dict(status='PASS',case=a.case,tax=a.tax,actualManaSpent=spent,preview=known.get('previewManaCost'),faceDown=known.get('faceDown'),runtimeHash=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))['runtimeHash'],driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),fixtureSha256=hashlib.sha256((R/'Tools/ForgeBridge/tests/ExileAlternativeFixture.java').read_bytes()).hexdigest());(out/'result.json').write_text(json.dumps(result,indent=2));(out/'stack.json').write_text(json.dumps(cast));print(json.dumps(result));break
   elif kind in ['SelectAbility','ChooseOptions','dialog']:
    target=next((o for o in opts if (('Plot' if a.case=='plot' else 'Foretell') in o['label'])),None)
    if target:pick=target['id']
    elif kind=='SelectAbility':pick=ids[0]
    else:raise AssertionError((stage,kind,[(o['id'],o['label']) for o in opts]))
   elif 'Mulligan' in kind:pass
   elif kind=='InputAttack':pass
   elif 'PayMana' in kind:
    pick='ok' if 'ok' in ids else next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for o in opts if o['label'].startswith('Forest [') or o['label'].startswith('Mountain ['))
   elif kind.startswith('InputSelect'):
    pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
   else:raise AssertionError((kind,[(o['id'],o['label']) for o in opts]))
   assert pick in ids,(kind,stage,ids,pick)
   proc.stdin.write(json.dumps(dict(type='respond',session='known',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
  else:raise AssertionError('No resolution')
 finally:proc.kill();proc.wait()

"""Actual casts/trigger + public projection boundaries for display provenance."""
import argparse, hashlib, json, os, pathlib, queue, shutil, subprocess, tempfile, threading, time

ROOT=pathlib.Path(__file__).resolve().parents[2]
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--output',type=pathlib.Path);ap.add_argument('--cases',nargs='+',default=['Pyroclasm','White Plume Adventurer','Brainstorm','Murder','order','projection']);args=ap.parse_args()
 out=(args.output or ROOT/'.tools/battle-text-fix-1016'/('native-'+str(time.time_ns()))).resolve();out.mkdir(parents=True,exist_ok=False)
 rt=ROOT/'ForgeRuntime';cfg=json.loads((rt/'runtime.json').read_text('utf-8-sig'));classes=out/'classes';classes.mkdir()
 javac=shutil.which('javac') or r'C:/Program Files/Java/jdk-17/bin/javac.exe'
 sources=[ROOT/'Tools/ForgeBridge'/s for s in ['java/mtgb/bridge/BridgeAbilityText.java','java/mtgb/bridge/ForgeBridge.java','tests/StateBranchFixture.java','tests/AbilityTextFixture.java']]
 subprocess.run([javac,'-encoding','UTF-8','-cp',str(rt/'classes')+';'+str(rt/'lib/*'),'-d',str(classes),*map(str,sources)],check=True)
 sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
 identity={'baseRuntimeHash':cfg['runtimeHash'],'sources':{str(p.relative_to(ROOT)):sha(p) for p in sources},'overlay':{str(p.relative_to(classes)):sha(p) for p in sorted(classes.rglob('*.class'))}}
 (out/'identity.json').write_text(json.dumps(identity,indent=2),'utf8')
 results=[];samples=[]
 for name in args.cases:
  folder=out/name.lower().replace(' ','-');folder.mkdir();(folder/'audit').mkdir()
  deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
  spell='White Plume Adventurer'if name=='order'else name
  spec=dict(session='text-audit',actor=1,own={'Hand':[] if name=='projection' else [spell],'Battlefield':['Soul Warden']if name=='order'else[]},other={'Battlefield':['Grizzly Bears']},auditDir=str(folder/'audit'),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck]);(folder/'start.json').write_text(json.dumps(spec),'utf8')
  seen=False;stage=0;target_seen=False;captured=[];result={'case':name,'status':'FAIL'}
  with tempfile.TemporaryDirectory(dir=folder) as tmp,(folder/'stderr.log').open('w',encoding='utf8') as err:
   process=subprocess.Popen([str(rt/cfg['java']),'-Xmx384m','-XX:ActiveProcessorCount=2','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,rt/'classes',rt/'lib/*'])),'mtgb.bridge.AbilityTextFixture' if name=='projection' else 'mtgb.bridge.StateBranchFixture',str(rt/cfg['assets']),str(folder/'start.json')],cwd=rt/cfg['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   messages=queue.Queue()
   def read(proc=process,mail=messages):
    for line in proc.stdout:mail.put(json.loads(line))
    mail.put(None)
   thread=threading.Thread(target=read,daemon=True);thread.start()
   try:
    for step in range(120):
     wire=messages.get(timeout=45)
     if wire is None:
      assert name=='projection' and process.wait(timeout=5)==0
      proof=json.loads((folder/'audit/projection.json').read_text('utf8'));assert proof['status']=='PASS';result.update(status='PASS',projection=proof);break
     assert wire['type']not in ['fatal','rejected','ended'],wire
     if wire['type']!='request':continue
     (folder/f'{step}.json').write_text(json.dumps(wire,ensure_ascii=False),'utf8')
     req=wire['request'];view=wire['views'][0];kind=req['kind'];options=[o for o in req['options']if not o.get('disabled')];ids=[o['id']for o in options]
     stack=[s for s in view['stackItems']if s['source']['name']==name and (name!='White Plume Adventurer' or s['kind']=='trigger')]
     if stack and not seen:
      seen=True;captured=stack;(folder/'view.json').write_text(json.dumps(view,ensure_ascii=False),'utf8')
      assert stack[0].get('rulesText') and stack[0].get('rulesSource'),stack[0]
      samples.extend({'name':name,'item':s}for s in stack)
     if kind=='InputPassPriority':
      if not stage and req['seat']==1 and view['phase']=='MAIN1' and name!='projection':
       pick='card:'+str(next(c['id']for c in view['players'][0]['hand']if c['name']==spell));stage=1
      else:pick='ok'
      if seen and not view['stackCount']:
       own=view['players'][0];other=view['players'][1]
       if name in ['Murder','Pyroclasm']:assert not any(c['name']=='Grizzly Bears'for c in other['battlefield']) and any(c['name']=='Grizzly Bears'for c in other['graveyardCards'])
       if name=='Murder':assert target_seen
       if name=='Brainstorm':assert own['handCount']==1 and own['libraryCount']==59,(own['handCount'],own['libraryCount'])
       if name=='White Plume Adventurer':assert any(c.get('helperArtName')=='The Initiative' or c['name']=='The Initiative'for c in own.get('effectMarkers',[])),own.get('effectMarkers')
       if name=='order':assert own['life']==31 and any(c.get('helperArtName')=='The Initiative'for c in own.get('effectMarkers',[]))
       (folder/'resolved.json').write_text(json.dumps(view,ensure_ascii=False),'utf8');result.update(status='PASS',stack=captured,targetRequest=target_seen);break
     elif 'Mulligan'in kind:pick='keep'if'keep'in ids else'ok'
     elif 'PayMana'in kind:pick='ok'if'ok'in ids else next(i for i in ids if i.startswith('card:'))
     elif kind=='dialog':pick=next(i for i in ids if i!='cancel')
     elif kind=='OrderAbilities':
      order=[o['ability']['id']for o in options if o.get('ability')]
      assert len(order)==2 and'submit-order'in ids
      for option in options:
       if not option.get('ability'):continue
       ability=option['ability'];assert ability.get('rulesText') and ability.get('rulesSource'),ability
       samples.append({'name':'order-'+ability['source']['name'],'item':ability})
      seen=True;pick='submit-order';target_seen=True
     elif kind=='InputSelectTargets':
      assert req.get('rulesText') and req.get('rulesSource');target_seen=True
      pick='ok'if req.get('selectedCount',0)>0 and'ok'in ids else next(o['id']for o in options if o.get('card',{}).get('name')=='Grizzly Bears')
     elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','InputSelectCard','OrderCards']:
      pick='confirm-cards'if'confirm-cards'in ids else'ok'if'ok'in ids else next(o['id']for o in options if o['id'].startswith(('select:','card:')) and not o.get('selected'))
     else:raise AssertionError((kind,req['message'],ids))
     assert pick in ids,(pick,ids)
     response=dict(type='respond',session=spec['session'],seat=req['seat'],requestId=req['requestId'],option=pick)
     if pick=='submit-order':response['order']=order
     process.stdin.write(json.dumps(response)+'\n');process.stdin.flush()
    else:raise AssertionError('Step limit')
   except Exception as error:result['error']=repr(error)
   finally:
    if process.poll()is None:process.kill();process.wait()
    thread.join(timeout=3)
  (folder/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf8');results.append(result);print(name,result['status'],result.get('error',''),flush=True)
  if result['status']!='PASS':break
 projection=next((r['projection']for r in results if 'projection'in r),None)
 if projection:
  for row in projection['samples']:
   if 'source'not in row or not row['projection'].get('rulesSource'):continue
   projected=row['projection'];source=row['source'];description=projected['rulesText'].replace('CARDNAME',source['name'])
   samples.append({'name':'adapter-'+row['case'],'item':dict(source=source,description=description,kind='trigger',**projected)})
 (out/'result.json').write_text(json.dumps({'identity':identity,'results':results,'samples':samples},ensure_ascii=False,indent=2),'utf8')
 (out.parent/'native-latest.txt').write_text(str(out),'utf8')
 assert len(results)==len(args.cases) and all(r['status']=='PASS'for r in results)
 print(out)
if __name__=='__main__':main()

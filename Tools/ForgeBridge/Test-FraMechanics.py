"""Bounded FRA mechanic regression using real Forge actions, not simulated resolution."""
import importlib.util,json,os,queue,subprocess,tempfile,threading,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'))
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'-d',str(b.classes),str(b.root/'Tools/ForgeBridge/tests/RealityFractureMechanicsFixture.java')],check=True)
out=b.root/'Tools/Validation/Reports/FraMechanics';out.mkdir(parents=True,exist_ok=True)
cases={'heartwood':'Hungering Puppetbeast','prepare':'Heartwood Crafter','surveil':'Semester Foreseer','threshold':'Theorix Metamage','flashback':'Recursive Recruitment','cycling':'Vinelasher Adept','modal':'Craftwork Crusher','grave-empower':'Campus Crier','counter':'Graft Surgeon'}
cases.update({c:"Theorist's Sanctum" for c in ['behold-none','behold-hand','behold-board']})
if 'comparison' in sys.argv:cases['comparison']='Overlord of the Hauntwoods'
def run(case):
 name=cases[case]
 with tempfile.TemporaryDirectory(prefix='fra-mechanics-') as temp,(out/(case+'.log')).open('w',encoding='utf8') as log,(out/(case+'.jsonl')).open('w',encoding='utf8') as trace:
  cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session=case,fixture=case,testCard=name,seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
  p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-cp',str(b.classes)+';'+str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'mtgb.bridge.RealityFractureMechanicsFixture',str(b.runtime/b.cfg['assets']),str(cfg)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def snap(m,suffix):
   v=dict(m['views'][0]);v.update(request=m['request'],awaitingInput=True);(out/(case+'-'+suffix+'.json')).write_text(json.dumps(v),encoding='utf8')
  started=False;second=False;surveilled=False;selected=[];hand=0;sourceid=None
  try:
   for step in range(180):
    m=q.get(timeout=45);trace.write(json.dumps(m)+'\n');trace.flush()
    assert m['type'] not in ('fatal','ended','rejected'),m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];one=v['players'][0];board=one['battlefield'];source=next((c for c in board if c['name']==name),None);pick='ok'
    print(case,step,kind,[(o['id'],o['label'][:70]) for o in opts if not o['label'].endswith('[Battlefield]') or not kind.startswith('InputPayMana')],flush=True)
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if started:
      done=False
      if case=='heartwood':done=source is not None and any(c.get('token') and 'Heartwood' in c['name'] for c in board)
      elif case=='counter':done=source is not None and source['power']==3 and source['toughness']==3
      elif case=='threshold':done=source is not None and source['power']==3 and source['toughness']==3 and 'FLYING' in source.get('keywords',[])
      elif case=='surveil':done=source is not None and surveilled and bool(source.get('preparedSpell'))
      elif case=='prepare' and source:
       if not second:
        assert source.get('preparedSpell');snap(m,'prepared');pick='card:'+str(source['preparedSpell']['id']);second=True
       else:done=not source.get('preparedSpell') and any(c.get('token') and 'Heartwood' in c['name'] for c in board)
      elif case=='flashback':
       tokens=[c for c in board if c.get('token') and c['name']=='Cadet'];done=len(tokens)==2 and all(c['power']==4 and c['toughness']==4 for c in tokens) and name not in one['graveyard']
      elif case=='cycling':done=name in one['graveyard'] and one['handCount']==hand
      elif case=='modal':done=source is not None and any(c.get('token') and c['name']=='Cadet' for c in board) and one['handCount']==hand
      elif case=='grave-empower':done=any(c.get('token') and c['name']=='Jace Token' and c['loyalty']==2 for c in board) and name not in one['graveyard']
      elif case.startswith('behold-'):done=source is not None and source['tapped']==(case=='behold-none')
      elif case=='comparison':
       god=next(c for c in board if c['name']=='Xenagos, God of Revels')
       done=source is not None and not source['creature'] and any(c['kind']=='TIME' and c['count']==4 for c in source['counters']) and not next(c for c in board if c['name']=='Thassa, God of the Sea')['creature']
       if done:assert god['devotion']==7 and god['devotionRequired']==7 and god['creature'],god
      if done:snap(m,'resolved');return dict(case=case,card=name,status='PASS',steps=step)
      if pick=='ok':raise AssertionError(('resolved unexpectedly',source,one))
     else:
      if case=='comparison':
       god=next(c for c in board if c['name']=='Xenagos, God of Revels');assert god['devotion']==5 and god['devotionRequired']==7 and not god['creature'],god
       sea=next(c for c in board if c['name']=='Thassa, God of the Sea');assert sea['devotion']==1 and sea['devotionRequired']==5 and not sea['creature'],sea
       snap(m,'before-devotion')
      option=next((o for o in opts if o.get('card',{}).get('name')==name or o['label'].startswith(name+' [')),None)
      if option:pick=option['id'];started=True;hand=one['handCount']
    elif kind.startswith('InputPayMana'):
     pick='ok' if 'ok' in ids else next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for land in ['Forest','Island','Swamp','Mountain','Plains'] for o in opts if o['label'].startswith(land+' ['))
    elif kind=='dialog':
     if case=='comparison':
      snap(m,'dialog');pick=next(o['id'] for o in opts if 'impending' in o['label'].lower())
     else:
      snap(m,'dialog');pick=next((o['id'] for o in opts if (case=='cycling' and 'cycling' in o['label'].lower()) or (case=='modal' and ('Create a' in o['label'] or 'Draw a card' in o['label']))),None) or next(i for i in ids if i.startswith('choice:'))
    elif kind=='RevealCards':
     snap(m,'reveal');pick='confirm-cards'
    elif kind in ('Surveil','Scry'):
     surveilled=True;snap(m,'surveil');pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-bottom:'))
    elif case=='modal' and kind=='ChooseModes':
     snap(m,'modes');pick=next((o['id'] for o in opts if o['id'].startswith('mode:') and not o.get('selected') and not o.get('disabled') and ('Create a' in o['label'] or 'Draw a card' in o['label'])),None) or 'confirm-modes'
    elif case=='modal' and kind not in ('InputPassPriority',):
     snap(m,'modes');pick=next((o['id'] for o in opts if ('Create a' in o['label'] or 'Draw a card' in o['label']) and o['id'] not in selected),None) or ('ok' if 'ok' in ids else ids[0]);selected.append(pick)
    elif kind=='SelectCards':
     snap(m,'search');pick='confirm-cards' if r.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')=='Mountain')
    elif kind.startswith('InputSelect'):
     snap(m,'select');pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
    elif kind not in ('InputPassPriority','InputConfirmMulligan','InputConfirm'):
     snap(m,'unknown');raise AssertionError(('Unhandled request',r))
    assert pick in ids,(kind,pick,opts)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('step limit')
  finally:
   if p.poll() is None:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or cases:
  try:result=run(case)
  except Exception as e:result=dict(case=case,card=cases[case],status='FAIL',error=str(e));print(str(e)[:2000],flush=True)
  results.append(result);print(result,flush=True)
 (out/('comparison-result.json' if 'comparison' in sys.argv else 'results.json')).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
 sys.exit(any(r['status']!='PASS' for r in results))

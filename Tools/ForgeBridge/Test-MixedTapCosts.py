"""Native mixed-color tap-cost differences; fixture setup, real cast/pay/resolve."""
import hashlib,json,os,queue,re,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mixed-tap-costs-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
ARTIFACTS=['Tidehollow Strix','Esper Stormblade','Sanctum Gargoyle','Reaper King','Ornithopter']
CASES={
 'convoke-color-switch':dict(source='March of the Multitudes',hand=['March of the Multitudes'],field=['Selesnya Guildmage','Soul Warden','Llanowar Elves','Grizzly Bears'],enemy=[],grave=[],purpose='convoke',x=1,wanted=['Selesnya Guildmage','Soul Warden','Llanowar Elves','Grizzly Bears']),
 'convoke-wrong-color':dict(source='March of the Multitudes',hand=['March of the Multitudes'],field=['Soul Warden']*5,enemy=[],grave=[],unpayable=True,purpose='convoke',x=1,wanted=['Soul Warden']*3),
 'improvise-multicolor':dict(source='Herald of Anguish',hand=['Herald of Anguish'],field=ARTIFACTS+['Swamp']*2,enemy=[],grave=[],purpose='improvise',wanted=ARTIFACTS),
 'improvise-colored-short':dict(source='Herald of Anguish',hand=['Herald of Anguish'],field=['Tidehollow Strix']*6+['Swamp'],enemy=[],grave=[],unpayable=True,purpose='improvise',wanted=['Tidehollow Strix']*5),
 'waterbend-overlap':dict(source='Ruthless Waterbender',hand=[],field=['Ruthless Waterbender','Tidehollow Strix',"Tormod's Crypt"],enemy=['Tidehollow Strix'],grave=[],tapped="Tormod's Crypt",purpose='waterbend',activated=True,wanted=['Tidehollow Strix','Ruthless Waterbender'])}

def run(name):
 spec=CASES[name];folder=OUT/name/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=name,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtureSha256=SHA(FIXTURE),spec=spec)
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,why):
  result=dict(meta,status=status,result=status+' '+name+': '+why)
  for p in [folder/'result.json',OUT/(name+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=name,spec=spec,seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK],auditDir=str(audit))),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  before=None;source=None;stackSeen=False;chosen=[];switchStage=0;answered=set();colors=[];cancelled=False
  def snap(m,label):
   v=dict(m['views'][0],request=m['request'],awaitingInput=True);(folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
  try:
   for step in range(130):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];own=v['players'][0];other=v['players'][1];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok'
    if q['requestId'] in answered:continue
    assert not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'];snap(m,'step-'+str(step))
    if kind=='InputPassPriority' and any(e['source']['id']==source for e in v['stackItems']):
     stackSeen=True
     assert all(next(c for c in own['battlefield'] if c['id']==i)['tapped'] for i in chosen)
     snap(m,'stack')
    if kind=='InputPassPriority' and q['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if before is None:
      before=json.loads(json.dumps(own));source=next(c['id'] for c in own['battlefield' if spec.get('activated') else 'hand'] if c['name']==spec['source']);snap(m,'before')
      if spec.get('unpayable') and 'card:'+str(source) not in ids:
       assert not any(c['tapped'] for c in own['battlefield'])
       return finish('PASS','Native cast option absent with sufficient generic tap resources but missing required colored shard; hand/board/life unchanged and both private priority views valid. No cast/effect claim for this availability-negative.')
      pick='card:'+str(source);assert pick in ids,('Expected cast absent',q)
     else:
      if spec.get('unpayable'):
       assert cancelled and not stackSeen
       for z in ['hand','battlefield','graveyardCards','exileCards']:
        # Native grouping hint changes after mana activation; it is not a resource.
        clean=lambda cards:[{k:v for k,v in c.items() if k!='stackablePermanent'} for c in cards]
        assert clean(own[z])==clean(before[z]),('Unpayable cancel changed',z)
       assert own['life']==before['life'] and own['manaPool']==before['manaPool'] and own['libraryCount']==before['libraryCount'];snap(m,'unpayable-cancelled')
       return finish('PASS','Actual cast attempt and draft tap resources leave required colored shard unpaid. Cancel from native mana prompt restores exact hand/board/zone/resources and private main priority; no spell resolves. A listed action is not a claim of affordability.')
      assert stackSeen;assert len(chosen)==len(spec['wanted']);assert own['life']==before['life']+(1 if spec['purpose']=='convoke' else 0) and own['libraryCount']==before['libraryCount']
      assert all(next(c for c in own['battlefield'] if c['id']==i)['tapped'] for i in chosen)
      if spec['purpose']=='convoke':
       tokens=[c for c in own['battlefield'] if c.get('token')];assert len(tokens)==1 and (tokens[0]['power'],tokens[0]['toughness'])==(1,1) and 'LIFELINK' in tokens[0]['keywords'];assert colors==(['Colorless'] if spec.get('generic') else ['Green','White'])
      elif spec['purpose']=='improvise':assert next(c for c in own['battlefield'] if c['id']==source)['power']==5 and sum(c['tapped'] for c in own['battlefield'] if c['land'])==2
      else:assert (next(c for c in own['battlefield'] if c['id']==source)['power'],next(c for c in own['battlefield'] if c['id']==source)['toughness'])==(2,4)
      snap(m,'resolved');return finish('PASS','Actual native mixed tap selection and payment; exact source/resource IDs, draft counts/remaining cost, committed taps and declared final effect; both private views and main priority recover. Convoke color switch or generic-only artifact payment/overlap measured in recorded requests.')
    elif kind=='InputPassPriority':pick='ok'
    elif kind=='ChooseNumber':pick='number-plus' if q['selectedValue']<spec.get('x',0) else 'confirm-number'
    elif kind=='InputSelectCardsForConvokeOrImprovise':
     assert q['selectionPurpose']==spec['purpose'] and q['sourceCard']['id']==source
     cards=[o for o in opts if o.get('card')];assert all(o['card']['ownerSeat']==1 and not o['card']['tapped'] for o in cards)
     if spec['purpose']=='waterbend':assert {o['card']['name'] for o in cards}==set(spec['wanted'])
     if spec.get('unpayable') and q['selectedCount']==len(spec['wanted']):
      assert q['remainingManaCost']==('{G}' if spec['purpose']=='convoke' else '{B}{B}');chosen=[o['card']['id'] for o in cards if o.get('selected')];snap(m,'unpayable-tap-selected');pick='ok'
     elif spec.get('unpayable'):
      wanted=spec['wanted'][q['selectedCount']];pick=next(o['id'] for o in cards if o['card']['name']==wanted and not o.get('selected'))
     elif name=='convoke-color-switch' and switchStage<3:
      guild=next(o for o in cards if o['card']['name']=='Selesnya Guildmage')
      if switchStage==1:assert q['selectedCount']==1 and q['remainingManaCost']=='{1}{W}{W}';snap(m,'green-chosen');switchStage=2
      elif switchStage==2:assert q['selectedCount']==0 and sorted(re.findall(r'\{([^}]+)\}',q['remainingManaCost']))==['1','G','W','W'];switchStage=3
      else:switchStage=1
      pick=guild['id']
     else:
      if spec.get('generic') and q['selectedCount']==1:
       assert sorted(re.findall(r'\{([^}]+)\}',q['remainingManaCost']))==['G','W','W'];snap(m,'generic-chosen')
      if name=='convoke-color-switch' and q['selectedCount']==1:assert sorted(re.findall(r'\{([^}]+)\}',q['remainingManaCost']))==['1','G','W'];snap(m,'white-chosen')
      if spec['purpose']=='waterbend' and q['selectedCount']==1:assert q['remainingManaCost']=='{1}'
      if q['selectedCount']==len(spec['wanted']):
       assert q['remainingManaCost']==('{B}{B}' if spec['purpose']=='improvise' else '0');chosen=[o['card']['id'] for o in cards if o.get('selected')];assert len(set(chosen))==len(spec['wanted']);snap(m,'tap-selected');pick='ok'
      else:
       selected={o['card']['name'] for o in cards if o.get('selected')};wanted=next(n for n in spec['wanted'] if n not in selected);pick=next(o['id'] for o in cards if o['card']['name']==wanted)
    elif kind=='dialog':
     if q['message']=='getAbilityToPlay':pick=next(o['id'] for o in opts if o['label'].startswith(spec['source']+' -'))
     elif 'Convoke' in q['message']:
      color='Colorless' if spec.get('generic') else 'Green' if switchStage==1 else 'White';pick=next(o['id'] for o in opts if o['label'].upper()==color.upper());colors.append(color);snap(m,'color-'+color)
     else:raise AssertionError(('Unknown dialog',q))
    elif 'PayMana' in kind:
     if spec.get('unpayable'):
      lands=[c for c in own['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids]
      if lands:pick='card:'+str(lands[0]['id'])
      else:assert 'cancel' in ids and not v['stackItems'];snap(m,'unpayable-payment');pick='cancel';cancelled=True
     else:pick='ok'
    elif kind in ['InputConfirmMulligan','InputConfirm']:pick='ok'
    elif kind=='InputMulligan':pick='keep'
    else:raise AssertionError(('Unknown request',q))
    assert pick in ids,(kind,pick,q);response=dict(type='respond',session=name,seat=q['seat'],requestId=q['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();answered.add(q['requestId']);print(name,step,kind,pick,flush=True);proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except BaseException as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()

if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES);assert all(k in CASES for k in selected);summary=OUT/'run-summary.json';results=[];summary.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for k in selected:results.append(run(k));print(results[-1]['result'],flush=True)
 except BaseException:summary.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 summary.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')

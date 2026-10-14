import json,os,queue,subprocess,tempfile,threading,time,traceback,shutil,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];B=R/'.tools/horizontal-pool-1010';RT=R/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text());F=B/'native'/str(time.time_ns());F.mkdir(parents=True);C=B/'classes';C.mkdir(exist_ok=True);U=B/'native-ui';U.mkdir(exist_ok=True)
fixture=R/'Tools/ForgeBridge/tests/RestrictedManaFixture.java'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(C),str(fixture)],check=True)
shutil.copy2(Path(__file__),F/'driver.py');shutil.copy2(fixture,F/'fixture.java')
checks=[]
def ck(ok,label):
 assert ok,label
 checks.append(label)
def wr(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def snap(m,label):
 wr(F/(label+'.json'),m)
 v=dict(m['views'][0],request=m['request'],awaitingInput=True);wr(U/(label+'.json'),v)
def pools(v):return v['players'][0]['manaPoolEntries']
def restricted(v):return [e for e in pools(v)if e['restricted']]
wr(F/'start.json',dict(session='mixed-restricted',fixture='mixed-restricted',seed=4,firstPlayerSeat=1,startingLife=20,hand=['Cavern of Souls','Shock','Manic Vandal'],lands=['Mountain']*3,counter=False,decks=[dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])]*2))
with tempfile.TemporaryDirectory(prefix='mtgb-pool-')as temp,(F/'engine.log').open('w',encoding='utf8')as log,(F/'trace.jsonl').open('w',encoding='utf8')as trace:
 p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[C,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(F/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');qout=queue.Queue()
 def read():
  try:
   for line in p.stdout:qout.put(json.loads(line))
  finally:qout.put(None)
 threading.Thread(target=read,daemon=True).start();stage=0;seen=set();ordinary=False;bad=False;ids={};paid=[]
 try:
  for step in range(160):
   m=qout.get(timeout=35);ck(isinstance(m,dict),'Native alive');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type']not in ['fatal','ended','rejected'],'No terminal/rejection')
   if m['type']!='request':continue
   q=m['request'];rid=q['requestId']
   if rid in seen:continue
   seen.add(rid);v=m['views'][0];one=v['players'][0];opts=[o for o in q['options']if not o.get('disabled')];available=[o['id']for o in opts];kind=q['kind'];pick=None
   if 'Mulligan'in kind:pick='keep'if'keep'in available else'ok'
   elif kind=='InputPassPriority':
    if q['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if stage==0:
      ids={c['name']:c['id']for c in one['hand']};pick='card:'+str(ids['Cavern of Souls']);stage=1
     elif stage==1:
      pick='card:'+str(next(c['id']for c in one['battlefield']if c['name']=='Mountain'and not c['tapped']));stage=2
     elif stage==2:pick='card:'+str(ids['Cavern of Souls']);stage=3
     elif stage==3:
      ck(len(pools(v))==2 and sorted(e['count']for e in pools(v))==[1,1],'Separate ordinary/restricted red 1+1');ck(one['manaPool'][3]==2,'Legacy total2');ck(restricted(v)[0]['sourceName']=='Cavern of Souls','Restricted public source');ck(m['views'][1]['players'][0]['manaPoolEntries']==pools(v),'Both viewers same public buckets');snap(m,'mixed');pick='card:'+str(ids['Shock']);stage=4
     elif stage==6:
      ck(v['players'][1]['life']==18 and 'Shock'in one['graveyard'],'Actual Shock resolves2');ck(len(pools(v))==1 and restricted(v)[0]['count']==1,'Ordinary only consumed');snap(m,'shock-resolved');pick='card:'+str(ids['Manic Vandal']);stage=7
     elif stage==8:
      ck(any(c['id']==ids['Manic Vandal']for c in one['hand']),'Cancel restores exact original hand');ck(len(restricted(v))==1 and restricted(v)[0]['count']==1 and sum(one['manaPool'])==1,'Partial cancellation restores restricted bucket');ck(sum(c['name']=='Mountain'and c['tapped']for c in one['battlefield'])==1,'Cancel did not tap more sources');snap(m,'refunded');pick='card:'+str(ids['Manic Vandal']);stage=9
     elif stage==10:
      ck(any(c['id']==ids['Manic Vandal']for c in one['battlefield']),'Actual Human permanent resolved original ID');ck(sum(one['manaPool'])==0 and not pools(v),'Exact total0 after payment');ck(sum(c['land']and c['tapped']for c in one['battlefield'])==4,'All four exact land productions committed');ck(one['life']==20 and v['players'][1]['life']==18,'Life exact');snap(m,'resolved');wr(F/'result.json',dict(status='PASS',checks=len(checks),assertions=checks,runtimeHash=CFG['runtimeHash'],operations='Actual Cavern land entry/Human choice; ordinary Mountain red plus Cavern restricted red; forbidden restricted Shock attempt unchanged, ordinary Shock2; Human partial restricted payment cancelled/refunded then recast with restricted+two ordinary mana; actual priority restored',paymentChoices=paid));wr(B/'native-result.json',dict(status='PASS',folder=F.relative_to(R).as_posix(),runtimeHash=CFG['runtimeHash'],checks=len(checks)));print('PASS',F,len(checks),flush=True);break
     else:pick='ok'
    else:pick='ok'
   elif kind in ['dialog','ChooseType','Option']:
    preferred=['Human','RED'];pick=next((o['id']for word in preferred for o in opts if o['label']==word),None)
    if pick is None:pick=next(o['id']for o in opts if 'one mana of any color'in o['label'].lower())
   elif kind=='InputSelectTargets':pick='ok'if q.get('remainingCount')==0 and'ok'in available else'player:'+str(v['players'][1]['targetId'])
   elif 'PayMana'in kind:
    if stage==4:
     ck('mana:R'in available and any(':restricted:'in i for i in available),'Both exact payment options');before_cost=q.get('remainingManaCost');snap(m,'shock-payment');pick=next(i for i in available if':restricted:'in i);stage=5
    elif stage==5:
     ck(len(pools(v))==2 and sum(one['manaPool'])==2,'Forbidden restricted payment changed no mana');ck(q.get('remainingManaCost')==before_cost,'Forbidden payment changed no cost');snap(m,'restricted-rejected');pick='mana:R';stage=6
    elif stage in [7,9]:
     snap(m,'human-payment-'+str(stage));pick=next(i for i in available if':restricted:'in i);stage=8 if stage==7 else 10
    elif stage==8:
     ck(sum(one['manaPool'])==0 and q.get('remainingManaCost')=='{2}','Restricted pays exactly red shard leaving generic2');snap(m,'partial');pick='cancel'
    elif stage==10:pick=next(i for i in available if i.startswith('card:')and any(c['id']==int(i[5:])and c['name']=='Mountain'and not c['tapped']for c in one['battlefield']))
    else:raise AssertionError(('Unhandled pay stage',stage,q))
    paid.append(dict(stage=stage,option=pick,requestId=rid))
   else:raise AssertionError(('Unhandled input',stage,q))
   ck(pick in available,'Exact legal option '+str(pick));print(step,stage,kind,pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session='mixed-restricted',seat=q['seat'],requestId=rid,option=pick))+'\n');p.stdin.flush()
  else:raise AssertionError('Step limit')
 except Exception as error:
  wr(F/'result.json',dict(status='FAIL',stage=stage,error=repr(error),checks=len(checks),runtimeHash=CFG['runtimeHash']));print('FAIL',F,flush=True);raise
 finally:
  if p.poll()is None:p.kill();p.wait()

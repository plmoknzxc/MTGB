"""Scoped real native operations: informative reveals must not require replies."""
import json,os,queue,subprocess,tempfile,threading,time,traceback,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';OUT=ROOT/'.tools/card-notices-1014'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={'forestcycling':('Generous Ent',['Forest']*8+['Mountain']*3),
       'mill':('Tome Scour',['Island']*3), 'exile':('Swords to Plowshares',['Plains']*3),
       'private-draw':('Divination',['Island']*5),'mana-source':('Lotus Petal',['Lotus Petal'])}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n','utf8')
def run(case,baseline=False):
 spell,lands=CASES[case]; folder=OUT/('baseline' if baseline else 'native')/case/str(time.time_ns());folder.mkdir(parents=True)
 audit=folder/'payment-audit';audit.mkdir();p=None;started=False;seen=False;steps=0
 try:
  with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   spec=dict(spell=spell,hand=[]if case=='mana-source'else[spell],field=lands,enemy=['Grizzly Bears'],library=['Forest']*8)
   start=folder/'start.json';write(start,dict(session=case,firstPlayerSeat=1,startingLife=20,seed=4,spec=spec,auditDir=str(audit),decks=[DECK,DECK]))
   cp=[OUT/'classes',RT/'classes',RT/'lib/*'] if not baseline else [RT/'classes',OUT/'classes',RT/'lib/*']
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,cp)),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   for step in range(160):
    m=q.get(timeout=45);assert m is not None,'native process ended';trace.write(json.dumps(m)+'\n');trace.flush()
    assert m['type'] not in {'fatal','rejected','ended'},m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];own=v['players'][0];other=v['players'][1];opts=[o for o in r['options']if not o.get('disabled')];ids=[o['id']for o in opts];kind=r['kind'];steps+=1
    write(folder/('step-'+str(step)+'.json'),m)
    print(case,step,kind,r.get('message','').split('\n')[-1][:80],flush=True)
    if kind=='RevealCards':
     write(folder/'blocked-reveal.json',m)
     if baseline:return dict(status='REPRODUCED',case=case,folder=str(folder.relative_to(ROOT)),blockingSeat=r['seat'])
     raise AssertionError('Informational reveal still requires a player reply')
    if started and v['stackCount']>0:seen=True
    if kind=='InputPassPriority' and started and(seen or case=='mana-source')and not v['stackCount'] and v['phase']=='MAIN1':
     notes=m['views'][1].get('notices',[]);moves=[n for n in notes if n['kind']=='card-move']
     assert all(n.get('fromZone')!='Stack'for n in moves),'Cast spells must not get redundant move windows'
     if case!='mana-source':assert not any(n.get('reason')=='cost'and any(c['id']==n.get('sourceCard',{}).get('id')for c in n['cards'])for n in moves),'Stack cost source itself must not get a window'
     if case=='forestcycling':
      relevant=[n for n in moves if n.get('fromZone')=='Library'and n.get('toZone')=='Hand']
      assert relevant and all(c['name']=='Forest'for n in relevant for c in n['cards']),relevant
      assert all(n.get('sourceCard',{}).get('name')==spell for n in relevant),relevant
      assert any(c['name']=='Forest'for c in own['hand']) and spell in own['graveyard']
     elif case=='mill':
      relevant=[n for n in moves if n.get('fromZone')=='Library'and n.get('toZone')=='Graveyard'and n.get('sourceCard',{}).get('name')==spell]
      assert sum(len(n['cards'])for n in relevant)==5,relevant
      assert len(other['graveyardCards'])==5,other['graveyardCards']
     elif case=='exile':
      relevant=[n for n in moves if n.get('fromZone')=='Battlefield'and n.get('toZone')=='Exile']
      assert any(c['name']=='Grizzly Bears'for n in relevant for c in n['cards']),relevant
      assert any(c['name']=='Grizzly Bears'for c in other['exileCards'])and other['life']==22
     elif case=='mana-source':
      relevant=[n for n in moves if n.get('fromZone')=='Battlefield'and n.get('toZone')=='Graveyard']
      assert any(c['name']=='Lotus Petal'for n in relevant for c in n['cards']),relevant
      assert not seen and sum(own['manaPool'])==1 and 'Lotus Petal'in own['graveyard']
     else:
      relevant=[n for n in notes if n['kind']in {'card-reveal','card-move'}and(n.get('toZone')=='Hand'or n['kind']=='card-reveal')]
      assert not relevant,relevant
      assert own['handCount']==beforeHand+1
     for i,view in enumerate(m['views'],1):write(folder/('result-seat'+str(i)+'.json'),dict(view,request=r,awaitingInput=True))
     write(folder/'completed.json',m)
     return dict(status='PASS',case=case,folder=str(folder.relative_to(ROOT)),requestCount=steps,informationalReplies=0,noticeIds=[n['id']for n in moves])
    pick='ok'
    if kind=='InputMulligan':pick='keep'
    elif kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1'and not started:
     card=next(c for c in own['battlefield'if case=='mana-source'else'hand']if c['name']==spell);pick='card:'+str(card['id']);beforeHand=own['handCount'];started=True
    elif kind in {'InputSelectTargets','SelectStackTarget'}:
     if 'ok'in ids:pick='ok'
     elif case=='mill':pick=next(o['id']for o in opts if o['id']=='player:1')
     else:pick=next(o['id']for o in opts if o.get('card',{}).get('name')=='Grizzly Bears')
    elif any('Forestcycling'in o.get('label','')for o in opts):pick=next(o['id']for o in opts if 'Forestcycling'in o.get('label',''))
    elif kind in {'InputSelectCardsFromList','InputSelectEntitiesFromList','InputSelectCard'}:
     if 'ok'in ids:pick='ok'
     else:pick=next(o['id']for o in opts if o.get('card',{}).get('name')=='Forest')
    elif kind in {'dialog','ChooseAbility','InputChooseAbility'}:
     matching=[o for o in opts if 'Forestcycling'in o.get('label','')];pick=(matching[0]if matching else opts[0])['id']
    if pick not in ids:raise AssertionError(('Unhandled exact native input',r))
    response=dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick)
    trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   raise AssertionError('No completed native outcome')
 except Exception as e:
  return dict(status='FAIL',case=case,folder=str(folder.relative_to(ROOT)),error=str(e),traceback=traceback.format_exc())
 finally:
  if p is not None:p.kill();p.wait()
if __name__=='__main__':
 baseline='--baseline'in sys.argv;cases=[c for c in sys.argv[1:]if c in CASES]or list(CASES)
 results=[run(case,baseline)for case in cases];path=OUT/('baseline-result.json'if baseline else 'native-result-'+str(time.time_ns())+'.json');write(path,results);print(json.dumps(results,ensure_ascii=False,indent=2));sys.exit(any(r['status']=='FAIL'for r in results))

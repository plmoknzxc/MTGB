"""Real Forge stack regression: three spells, stack targets and simultaneous triggers."""
import json,os,queue,subprocess,tempfile,threading
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/StackInteractions';out.mkdir(parents=True,exist_ok=True)
classes=root/'.tools/stack-interactions/classes'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/StackInteractionFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case,reverse=False):
 name=case+('-reverse' if reverse else '')
 with tempfile.TemporaryDirectory(prefix='mtgb-stack-') as temp,(out/(name+'.log')).open('w',encoding='utf-8') as log,(out/(name+'.jsonl')).open('w',encoding='utf-8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=name,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
  p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(classes)+';'+str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.StackInteractionFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def read():
   m=q.get(timeout=45);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type']!='fatal',m;return m
  def req():
   while True:
    m=read()
    if m['type']=='request':return m
    assert m['type'] not in ('rejected','ended'),m
  def send(m,id):
   r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session=name,seat=r['seat'],requestId=r['requestId'],option=id))+'\n');p.stdin.flush()
  def snap(m,label):
   v=dict(m['views'][m['request']['seat']-1]);v.update(request=m['request'],awaitingInput=True)
   (out/(name+'-'+label+'.json')).write_text(json.dumps(v),encoding='utf-8')
  try:
   m=req();casts=0;pay=[];maxstack=0;ordered=[];seenorder=False;targeted=False;reset=False
   for _ in range(160):
    r=m['request'];v=m['views'][r['seat']-1];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];stack=v['stackItems'];maxstack=max(maxstack,len(stack));pick='ok'
    print(name,kind,r['seat'],[e['source']['name'] for e in stack],flush=True)
    if kind=='InputPassPriority':
     spell=None
     if casts==0 and r['seat']==1 and v['phase']=='MAIN1':spell='Lightning Bolt' if case=='spells' else 'Goblin Piker'
     if case=='spells' and casts==1 and len(stack)==1 and r['seat']==2:spell='Counterspell'
     if case=='spells' and casts==2 and len(stack)==2 and r['seat']==1:spell='Negate'
     if case=='triggers' and casts==1 and len(stack)==2 and seenorder:
      assert [e['source']['name'] for e in stack]==ordered,([e['source']['name'] for e in stack],ordered)
      snap(m,'ordered');spell='Stifle'
     if spell:
      card=next(c for c in v['players'][r['seat']-1]['hand'] if c['name']==spell)
      pick='card:'+str(card['id']);assert pick in ids,(spell,opts)
      casts+=1;pay={'Lightning Bolt':['R'],'Counterspell':['U','U'],'Negate':['U','U'],'Goblin Piker':['R','R'],'Stifle':['U']}[spell]
     elif case=='spells' and casts==3 and maxstack==3 and len(stack)==0:
      assert v['players'][1]['life']==17,v; snap(m,'resolved');break
     elif case=='triggers' and casts==2 and maxstack==3 and len(stack)==0:
      assert v['players'][0]['life']==21,v;snap(m,'resolved');break
    elif kind.startswith('InputPayMana'):
     color=pay.pop(0) if pay else 'U'
     pick='mana:'+color
     if pick not in ids:
      land=next(c for c in v['players'][r['seat']-1]['battlefield'] if c['name']==('Mountain' if color=='R' else 'Island') and not c['tapped'] and 'card:'+str(c['id']) in ids)
      pick='card:'+str(land['id'])
    elif kind=='InputSelectTargets':pick='ok' if r.get('selectedCount')==1 and 'ok' in ids else 'player:'+str(v['players'][1]['targetId'])
    elif kind=='SelectStackTarget':
     snap(m,'target-'+str(casts));targeted=True
     if r.get('selectedCount')==1:
      assert any(o.get('selected') for o in opts) and 'reset-targets' in ids
      send(m,'ok');m=req();continue
     target=stack[0];pick='stack:'+str(target['id']);assert pick in ids,(pick,opts)
     send(m,'stack:999999');assert read()['error']=='invalid-option'
     previous=m;send(m,pick);m=req();send(previous,pick);assert read()['error']=='stale-request';echo=read();assert echo['views']==m['views'];continue
    elif kind=='OrderAbilities':
     seenorder=True;snap(m,'order')
     if not r.get('orderedAbilities'):snap(m,'order-start')
     if 'reset-order' in ids and not reset:
      pick='reset-order';ordered=[];reset=True
     elif 'confirm-order' in ids:pick='confirm-order'
     else:
      choices=[o for o in opts if o['id'].startswith('order:')];option=choices[-1 if reverse else 0];pick=option['id']
      ordered.append("Soul's Attendant" if "Attendant" in option['label'] else 'Soul Warden')
    elif kind=='dialog':pick=ids[0]
    assert pick in ids,(kind,pick,opts)
    if len(stack)==3:snap(m,'three')
    send(m,pick);m=req()
   else:raise AssertionError('No resolution in 160 requests')
   assert maxstack==3 and targeted
   if case=='triggers':assert seenorder and reset
   return 'PASS '+name+': max stack 3; genuine target, payment, resolution; illegal/stale rejected'+('; explicit trigger order and reset' if seenorder else '')
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
   (out/(name+'-process.txt')).write_text(f'pid={p.pid} exited={p.returncode}',encoding='utf-8')
if __name__=='__main__':
 try:
  results=[run('spells'),run('triggers'),run('triggers',True)]
  text='\n'.join(results);(out/'engine-result.txt').write_text(text,encoding='utf-8');print(text)
 except Exception as e:
  (out/'engine-result.txt').write_text('FAIL '+repr(e),encoding='utf-8');raise

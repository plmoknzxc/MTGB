"""Bounded native action permissions, targetability, cycling and restriction removal.

Requires the current locally built bridge. Every run retains its own wire evidence.
"""
import json,os,queue,subprocess,tempfile,threading,time,pathlib,traceback,sys,hashlib
ROOT=pathlib.Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';OUT=ROOT/'.tools/action-legality';OUT.mkdir(parents=True,exist_ok=True);CL=OUT/'classes';CL.mkdir(exist_ok=True)
cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CL),str(ROOT/'Tools/ForgeBridge/tests/ActionLegalityFixture.java')],check=True)
failures=0
for case in sys.argv[1:] or ['voice','totem','teferi','control','law','cage']:
 folder=OUT/(case+'-'+str(time.time_ns()));folder.mkdir();checks=[];result={'runtimeHash':cfg['runtimeHash'],'bridgeSha256':hashlib.sha256((RT/'classes/mtgb/bridge/ForgeBridge.class').read_bytes()).hexdigest()}
 def check(v,label):
  checks.append(dict(pass_=bool(v),label=label))
 with tempfile.TemporaryDirectory(dir=OUT) as temp,(folder/'engine.log').open('w',encoding='utf8') as log:
  start=pathlib.Path(temp)/'start.json';deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
  start.write_text(json.dumps(dict(session='legality',fixture=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[deck,deck])),encoding='utf8')
  p=subprocess.Popen([str(RT/cfg['java']),'-Xmx512m','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CL,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ActionLegalityFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  q=queue.Queue()
  def reader(proc,output):
   try:
    for line in proc.stdout:output.put(json.loads(line))
   finally:output.put(None)
  threading.Thread(target=reader,args=(p,q),daemon=True).start()
  def request():
   while True:
    m=q.get(timeout=40);assert m is not None,'worker exited'
    with (folder/'wire.jsonl').open('a',encoding='utf8')as f:f.write(json.dumps(m,ensure_ascii=False)+'\n')
    assert m['type'] not in ('fatal','rejected','ended'),m
    if m['type']=='request':return m
  def respond(m,id):
   r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session='legality',seat=r['seat'],requestId=r['requestId'],option=id))+'\n');p.stdin.flush();return request()
  try:
   m=request()
   for _ in range(40):
    if m['request']['kind']=='InputPassPriority' and m['request']['seat']==1 and m['views'][0]['phase']=='MAIN1':break
    opts=m['request']['options'];m=respond(m,next((x['id']for x in opts if x['id']=='ok'),opts[0]['id']))
   one=m['views'][0]['players'][0];opt=next(c for c in one['hand'] if c['name']=='Opt')
   if case=='cage':
    ids={o['id']for o in m['request']['options']}
    grave=next(c for c in one['graveyardCards']if c['name']=='Think Twice')
    check('card:'+str(grave['id'])not in ids,'Origin restriction blocks payable flashback')
    check(not any(o.get('label','').startswith('Opt [Library]')for o in m['request']['options']),'Origin restriction blocks permitted library spell')
    check('card:'+str(opt['id'])in ids,'Origin restriction keeps hand spell legal')
   m=respond(m,'card:'+str(opt['id']))
   for _ in range(35):
    if m['request']['kind']=='InputPassPriority' and m['request']['seat']==2:break
    if case=='law' and m['request']['kind']=='InputPassPriority'and m['request']['seat']==1:
     ids={o['id']for o in m['request']['options']};hand=m['views'][0]['players'][0]['hand'];brain=next(c for c in hand if c['name']=='Brainstorm');cycle=next(c for c in hand if c['name']=='Street Wraith')
     check('card:'+str(brain['id'])not in ids,'Per-turn limit removes second spell after actual cast')
     check('card:'+str(cycle['id'])in ids,'Per-turn spell limit keeps cycling legal')
    opts=m['request']['options'];m=respond(m,next((x['id']for x in opts if x['id']=='ok'),opts[0]['id']))
   assert m['request']['seat']==2,m['request']
   (folder/'opponent-priority.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
   offered={x['id']:x for x in m['request']['options']};two=m['views'][1]['players'][1]
   def offered_name(name,zone='hand'):
    c=next(c for c in two[zone]if c['name']==name);return offered.get('card:'+str(c['id']))
   for name in ['Requiting Hex','Get Lost']:
    check(bool(offered_name(name))==(case not in ('voice','teferi')),name+' spell permission')
   for name in ['Street Wraith','Boseiju, Who Endures']:
    check(bool(offered_name(name)),name+' legal hand activation remains')
   for name in ['Mogg Fanatic','Llanowar Elves']:
    check(bool(offered_name(name,'battlefield'))==(case!='totem'),name+' activation permission')
   check(bool(offered_name('Island','battlefield')),'Land mana remains usable')
   check(not offered_name('Annul'),'No legal stack target means no action')
   if case=='voice':
    wraith=next(c for c in two['hand']if c['name']=='Street Wraith')
    m=respond(m,'card:'+str(wraith['id']))
    for _ in range(40):
     if m['request']['kind']=='InputPassPriority'and m['request']['seat']==2 and m['views'][1]['players'][1]['life']==18:break
     opts=m['request']['options'];m=respond(m,next((x['id']for x in opts if x['id']=='ok'),opts[0]['id']))
    check(m['views'][1]['players'][1]['life']==18,'Cycling actually pays life under spell prohibition')
    check(any(c['name']=='Street Wraith'for c in m['views'][1]['players'][1].get('graveyardCards',[])),'Cycling puts card in graveyard')
    check(any(c['name']=='Voice of Victory'for c in m['views'][1]['players'][0]['battlefield']),'Voice remains while cycling resolves')
    (folder/'cycled.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
    otawara=next(c for c in m['views'][1]['players'][1]['hand']if c['name']=='Otawara, Soaring City')
    m=respond(m,'card:'+str(otawara['id']))
    for _ in range(70):
     field=m['views'][1]['players'][0]['battlefield'];voice=next((c for c in field if c['name']=='Voice of Victory'),None)
     if m['request']['kind']=='InputPassPriority'and m['request']['seat']==2 and voice is None:break
     opts=m['request']['options'];ids=[x['id']for x in opts]
     pick='card:'+str(voice['id'])if voice and 'Targets'in m['request']['kind']and'card:'+str(voice['id'])in ids and not m['request'].get('selectedCount') else next((x['id']for x in opts if x['id']=='ok'),opts[0]['id'])
     m=respond(m,pick)
    check(not any(c['name']=='Voice of Victory'for c in m['views'][1]['players'][0]['battlefield']),'Channel actually removes prohibition source')
    check(any(c['name']=='Voice of Victory'for c in m['views'][0]['players'][0]['hand']),'Voice returned to its owner hand')
    offered={x['id']:x for x in m['request']['options']};two=m['views'][1]['players'][1]
    check(bool(offered_name('Requiting Hex'))and bool(offered_name('Get Lost')),'Spells become available again after restriction leaves')
    (folder/'restored.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
   result['status']='PASS'if all(c['pass_']for c in checks)else'FAIL'
  except Exception:result.update(status='FAIL',error=traceback.format_exc())
  finally:
   if p.poll()is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
   result['checks']=checks;(folder/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print(folder);print(json.dumps(result,ensure_ascii=False))

 failures+=result['status']!='PASS'
sys.exit(bool(failures))

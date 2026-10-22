"""Real shuffle/draw flows plus audit integrity, determinism, failures and lifecycle."""
import argparse,gzip,hashlib,importlib.util,json,os,pathlib,queue,subprocess,tempfile,threading,time,uuid
ROOT=pathlib.Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';OUT=pathlib.Path(os.environ.get('MTGB_SHUFFLE_AUDIT_OUT',str(ROOT/'.tools/shuffle-audit-1022/current')));OUT.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('inspect_audit',ROOT/'Tools/ForgeBridge/Inspect-ShuffleAudit.py');inspector=importlib.util.module_from_spec(spec);spec.loader.exec_module(inspector)
parser=argparse.ArgumentParser();parser.add_argument('--skip-flows',action='store_true');args=parser.parse_args()
if not args.skip_flows:
 subprocess.run(['python','-X','utf8',str(ROOT/'Tools/ForgeBridge/Test-NoShuffle.py')],cwd=ROOT,env=dict(os.environ,MTGB_SHUFFLE_AUDIT='1',MTGB_NO_SHUFFLE_OUT=str(OUT/'native')),check=True)
checks=[]
def check(ok,message):
 checks.append(dict(pass_=bool(ok),assertion=message));assert ok,message
flows=json.loads((OUT/'native/result.json').read_text());reports=[]
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
check(flows['runtimeHash']==runtime['runtimeHash'],'Native flow results identify current runtime')
for case in flows['cases']:
 folder=OUT/'native'/case['name'];config=json.loads((folder/'start.json').read_text());path=pathlib.Path(config['shuffleAuditFile']);result=inspector.inspect(path);reports.append(result)
 check(result['complete'],'Complete digest/sequence/pairing/multiset audit '+case['name'])
 rows=[json.loads(line)for line in gzip.open(path,'rt',encoding='utf8')]
 check(result['shuffleAlgorithm']==('saved-order-v1'if config.get('noShuffle')else'physical-pile10-mash3-cut-v2'),'Audit identifies actual saved-order / physical shuffle algorithm '+case['name'])
 check(result['shufflePasses']==(0 if config.get('noShuffle')else 3),'Audit identifies finite mash count '+case['name'])
 check((result['shufflePiles'],result['shuffleMaxPacket'],result['shuffleCuts'])==((0,0,0)if config.get('noShuffle')else(10,3,1)),'Audit identifies pile, packet and cut schedule '+case['name'])
 check(rows[0]['decks']==config['decks']and rows[0]['seed']==13 and rows[0]['gameNumber']==2,'Original deck, seed and game identity '+case['name'])
 check(sum(p['draws']for p in result['players'].values())>=14,'Both opening hands observed '+case['name'])
 after=[r for r in rows if r['type']=='shuffle_after']
 for number,row in enumerate(after,1):
  fixture=json.loads((folder/'audit'/('shuffle-'+str(number)+'.json')).read_text())
  actual=[c['id']for c in fixture['cards']if c['seat']==row['seat']and c['zone']=='Library']
  check(actual==row['library'],'Audit equals independent actual GameEventShuffle snapshot '+case['name']+' #'+str(number))
 wire=(folder/'trace.json').read_text()
 check('shuffleAuditFile'not in wire and 'recordsSHA256'not in wire and 'shuffle_before'not in wire,'Audit is absent from bridge projections '+case['name'])
 if case['name']=='search':check(any(r.get('sourceName')=='Evolving Wilds'for r in after),'Actual search shuffle records source card')
 if case['name']=='mulligan':check(len(after)>2,'Actual mulligan produces extra shuffle audit')
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True);CP=os.pathsep.join(map(str,[CLASSES,RT/'classes',RT/'lib/*']))
subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',CP,'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/ShuffleAuditFixture.java')],check=True)
base=json.loads((OUT/'native/enabled/start.json').read_text())
advanced=[]
for mode in ['fixture','ended','crash']:
 folder=OUT/(mode+'-'+uuid.uuid4().hex[:8]);folder.mkdir();config=dict(base,session='audit-'+mode,noShuffle=False,testOutput=str(folder),shuffleAuditFile=str(folder/(uuid.uuid4().hex+'.jsonl.gz')))
 path=pathlib.Path(config['shuffleAuditFile']);start=folder/'start.json';start.write_text(json.dumps(config),encoding='utf8');mail=queue.Queue();wire=[]
 with tempfile.TemporaryDirectory(dir=folder)as tmp,(folder/'stderr.log').open('w',encoding='utf8')as err:
  proc=subprocess.Popen([str(RT/'jre/bin/java.exe'),'-Xmx384m','-XX:ActiveProcessorCount=2','-Djava.awt.headless=true','-cp',CP,'mtgb.bridge.ShuffleAuditFixture'if mode=='fixture'else'mtgb.bridge.ForgeBridge',str(RT/'assets'),str(start)],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
  def read(stdout=proc.stdout,inbox=mail):
   for line in stdout:
    try:inbox.put(json.loads(line))
    except ValueError:pass
   inbox.put(None)
  thread=threading.Thread(target=read,daemon=True);thread.start()
  try:
   for step in range(100):
    message=mail.get(timeout=45);check(message is not None and message['type']not in ['fatal','rejected'],'Advanced native worker healthy '+mode);wire.append(message)
    if message['type']=='fixture_done'or message['type']=='ended':break
    if message['type']!='request':continue
    if mode=='crash':time.sleep(.35);proc.kill();break
    request=message['request'];ids=[o['id']for o in request['options']if not o.get('disabled')]
    if mode=='ended'and request['kind']=='InputPassPriority':response=dict(type='concede',session=config['session'],seat=1)
    else:response=dict(type='respond',session=config['session'],seat=request['seat'],requestId=request['requestId'],option='keep'if'keep'in ids else'ok')
    proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush()
   if proc.poll()is None:proc.stdin.write(json.dumps(dict(type='shutdown',session=config['session']))+'\n');proc.stdin.flush()
   proc.wait(timeout=6)
  finally:
   if proc.poll()is None:proc.kill();proc.wait(timeout=5)
   (folder/'wire.json').write_text(json.dumps(wire),encoding='utf8')
 if mode=='crash':
  check(not path.exists()and pathlib.Path(str(path)+'.partial').exists(),'Killed process leaves identifiable partial audit')
  result=inspector.inspect(pathlib.Path(str(path)+'.partial'));check(not result['complete'],'Crash audit never reports complete')
 else:
  result=inspector.inspect(path);check(result['complete'],'Finalized advanced audit '+mode)
  if mode=='ended':check(result['gameFinished']and result['reason']=='game_ended','Actual concession produces completed-game footer')
  else:
   stats=json.loads((folder/'fixture-result.json').read_text());check(stats['seededPermutations']==300 and stats['status']=='PASS','300 actual seeded permutations unchanged by auditing')
   capacity=inspector.inspect(folder/'00000000000000000000000000000002.jsonl.gz')
   check(not capacity['complete']and capacity['writerError']=='capacity_limit','Capacity overflow explicitly marks incomplete and retains root cause with pending draw')
   check('[shuffle-audit] incomplete:'in(folder/'stderr.log').read_text(),'Filesystem/queue failures visible to host diagnostics')
   result['performance']=stats
 advanced.append(result)
 (folder/'inspection.json').write_text(json.dumps(result,indent=2),encoding='utf8')
report=dict(status='PASS',runtimeHash=runtime['runtimeHash'],checks=checks,flows=reports,advanced=advanced)
(OUT/'result.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('PASS',len(checks),'audit checks;',len(reports),'native flows; determinism/failure/normal-end/crash checks',flush=True)

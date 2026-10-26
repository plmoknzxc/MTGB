"""Native editor-only Commander eligibility/construction regression. No game is started."""
import json, pathlib, subprocess, os, argparse
parser=argparse.ArgumentParser()
parser.add_argument('--output',default='.tools/commander-editor-1015')
args=parser.parse_args()
root=pathlib.Path(__file__).resolve().parents[2]
out=(root/args.output).resolve();out.mkdir(parents=True,exist_ok=True)
names={"Atraxa, Praetors' Voice",'Sol Ring','Forest','Island','Teferi, Temporal Archmage','Jace, the Mind Sculptor','Thrasios, Triton Hero','Tymna the Weaver',"Abdel Adrian, Gorion's Ward",'Candlekeep Sage','Grist, the Hunger Tide','Shorikai, Genesis Engine'}
found={}
for line in (root/'ForgeRuntime/catalog.jsonl').open(encoding='utf8'):
 r=json.loads(line)
 if r['name'] in names and not r['ambiguous'] and r['name'] not in found:found[r['name']]=r['key']
def entry(name,n=1):
 s,c=found[name].split('/',1);return dict(setCode=s,collectorNumber=c,count=n)
checks=[]
def check(ok,label):checks.append(dict(pass_=bool(ok),label=label));print(('PASS ' if ok else 'FAIL ')+label,flush=True)
def run(name,cmds,main,fmt='commander',side=[]):
 config=dict(format=fmt,cards=main,sideboard=side,commanders=[entry(n) for n in cmds],candidates=[entry(n) for n in found]); p=out/(name+'.json');p.write_text(json.dumps(config),encoding='utf8')
 env=os.environ.copy();env['APPDATA']=str(out/'profile');env['LOCALAPPDATA']=str(out/'profile')
 r=subprocess.run([str(root/('ForgeRuntime/jre/bin/java.exe' if os.name=='nt' else 'ForgeRuntime/jre/bin/java')),'-Xmx384m','-Djava.awt.headless=true','-Dfile.encoding=UTF-8','-Duser.home='+str(out/'profile'),'-cp',str(root/'ForgeRuntime/classes')+os.pathsep+str(root/'ForgeRuntime/lib/*'),'mtgb.bridge.CommanderDeckInspector',str(root/'ForgeRuntime/assets'),str(p)],cwd=root/'ForgeRuntime/assets',env=env,capture_output=True,text=True,encoding='utf8',timeout=35)
 (out/(name+'.stdout')).write_text(r.stdout,encoding='utf8');(out/(name+'.stderr')).write_text(r.stderr,encoding='utf8');assert r.returncode==0,r.stderr;return json.loads(r.stdout)
a=run('native-eligibility',["Atraxa, Praetors' Voice"],[entry('Forest',99)])
for n in ["Atraxa, Praetors' Voice",'Grist, the Hunger Tide','Shorikai, Genesis Engine','Teferi, Temporal Archmage']:check(found[n] in a['legal'],'commander eligibility '+n)
for n in ['Sol Ring','Forest','Jace, the Mind Sculptor']:check(found[n] not in a['legal'],'reject commander '+n)
check(a['problem']=='','100 cards including commander accepted')
a=run('native-partner-options',['Thrasios, Triton Hero'],[entry('Forest',99)]);check(found['Tymna the Weaver'] in a['partners'],'native partner candidate');check(found['Sol Ring'] not in a['partners'],'reject nonpartner')
a=run('native-two-partners',['Thrasios, Triton Hero','Tymna the Weaver'],[entry('Forest',98)]);check(a['problem']=='','98+2 partner deck accepted')
a=run('native-background',["Abdel Adrian, Gorion's Ward"],[entry('Forest',99)]);check(found['Candlekeep Sage'] in a['partners'],'native background pair eligible')
a=run('native-brawl',['Jace, the Mind Sculptor'],[entry('Island',99)],'brawl');check(found['Jace, the Mind Sculptor'] in a['legal'] and a['problem']=='','Arena Brawl planeswalker 99+1 accepted')
a=run('native-identity',['Thrasios, Triton Hero'],[entry('Forest',98),entry('Tymna the Weaver')]);check(a['problem']=='identity','native off-identity rejected')
a=run('native-copies',["Atraxa, Praetors' Voice"],[entry('Forest',97),entry('Sol Ring',2)]);check(a['problem']=='copies','native singleton violation')
a=run('native-size',["Atraxa, Praetors' Voice"],[entry('Forest',60)]);check(a['problem']=='size','native incorrect deck size')
(out/'native-audit.json').write_text(json.dumps(checks,indent=2),encoding='utf8');assert all(x['pass_'] for x in checks)

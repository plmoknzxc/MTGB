"""Pinned native companion restriction and designated-payment integration."""
import json, os, pathlib, subprocess, concurrent.futures, hashlib

ROOT=pathlib.Path(__file__).resolve().parents[2]
RT=ROOT/'ForgeRuntime';OUT=ROOT/'.tools/companion-editor-1015/native';OUT.mkdir(parents=True,exist_ok=True)
NAMES=['Jegantha, the Wellspring','Gyruda, Doom of Depths','Kaheera, the Orphanguard','Keruga, the Macrosage','Lurrus of the Dream-Den','Lutri, the Spellchaser','Obosh, the Preypiercer','Umori, the Collector','Yorion, Sky Nomad','Zirda, the Dawnwaker']
WANTED=set(NAMES+['Mountain','Shock','Counterspell','Grizzly Bears','Air Elemental'])
CARDS={}
for line in (RT/'catalog.jsonl').open(encoding='utf-8-sig'):
 c=json.loads(line)
 if c['name'] in WANTED and (c['name'] not in CARDS or c['key'].startswith('10E/')):CARDS[c['name']]=c
def entry(name,count=1):
 code,number=CARDS[name]['key'].split('/');return dict(setCode=code,collectorNumber=number,count=count)
def inspect(name,valid):
 main=[entry('Mountain',80 if name.startswith('Yorion') and valid else 79 if name.startswith('Yorion') else 60)]
 additions={
  'Jegantha, the Wellspring':('Shock','Counterspell'),'Gyruda, Doom of Depths':('Grizzly Bears','Shock'),
  'Kaheera, the Orphanguard':('Air Elemental','Grizzly Bears'),'Keruga, the Macrosage':('Air Elemental','Shock'),
  'Lurrus of the Dream-Den':('Grizzly Bears','Air Elemental'),'Lutri, the Spellchaser':('Grizzly Bears','Grizzly Bears'),
  'Obosh, the Preypiercer':('Shock','Grizzly Bears'),'Umori, the Collector':('Grizzly Bears','Shock'),
  'Zirda, the Dawnwaker':('Mountain','Grizzly Bears')}
 if name in additions:
  main.append(entry(additions[name][0 if valid else 1],2 if name.startswith('Lutri') and not valid else 1))
  if name.startswith('Umori') and not valid:main.append(entry('Grizzly Bears'))
 deck=dict(cards=main,sideboard=[entry(name)],companion=entry(name))
 folder=OUT/(name.split(',')[0].lower()+('-legal' if valid else '-illegal'));folder.mkdir(exist_ok=True)
 config=dict(session='companion-check',inspectCompanions=True,seed=1,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])
 (folder/'start.json').write_text(json.dumps(config),encoding='utf8')
 args=[str(RT/'jre/bin/java.exe'),'-Xmx384m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Dmtgb.bridge.lazyCatalog=true','-Duser.home='+str(folder),'-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'mtgb.bridge.ForgeBridge',str(RT/'assets'),str(folder/'start.json')]
 result=subprocess.run(args,cwd=RT/'assets',env=dict(os.environ,APPDATA=str(folder),LOCALAPPDATA=str(folder)),capture_output=True,text=True,encoding='utf8',timeout=35)
 (folder/'stdout.txt').write_text(result.stdout,encoding='utf8');(folder/'stderr.txt').write_text(result.stderr,encoding='utf8')
 output=next((json.loads(l) for l in result.stdout.splitlines() if l.startswith('{"legal"')),None)
 passed=result.returncode==0 and output is not None and (CARDS[name]['key'] in output['legal'])==valid
 return dict(name=name,expectedLegal=valid,passed=passed,output=output,exitCode=result.returncode)
def main():
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda args:inspect(*args),[(n,v) for n in NAMES for v in [True,False]]))
 report=dict(results=results,bridgeClassSha256=hashlib.sha256((RT/'classes/mtgb/bridge/ForgeBridge.class').read_bytes()).hexdigest(),driverSha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),scope='actual Player.assignCompanion eligibility; no new payment from inspector')
 (OUT/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 for r in results:print(('PASS' if r['passed'] else 'FAIL'),r['name'],r['expectedLegal'],flush=True)
 if not all(r['passed'] for r in results):raise SystemExit(1)
if __name__=='__main__':main()

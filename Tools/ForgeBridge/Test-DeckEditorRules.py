"""Editor facet boundaries and native Commander companion starting-deck checks."""
from pathlib import Path
import json,os,subprocess,hashlib
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';OUT=ROOT/'.tools/deck-constraints-1015/native';OUT.mkdir(parents=True,exist_ok=True)
data=json.loads((ROOT/'Assets/StreamingAssets/DeckEditorRules.json').read_text(encoding='utf8'));rows={r['name']:r for r in data['cards']};checks=[]
def check(ok,label):
 checks.append({'pass':bool(ok),'label':label});print(('PASS ' if ok else 'FAIL ')+label,flush=True)
def permits(companion,card):return bool(rows[card]['companions']&(1<<data['companions'].index(companion)))
examples={
 'Gyruda, Doom of Depths':('Grizzly Bears','Shock'),
 'Jegantha, the Wellspring':('Shock','Counterspell'),
 'Kaheera, the Orphanguard':('Air Elemental','Grizzly Bears'),
 'Keruga, the Macrosage':('Air Elemental','Shock'),
 'Lurrus of the Dream-Den':('Grizzly Bears','Air Elemental'),
 'Obosh, the Preypiercer':('Shock','Grizzly Bears'),
 'Zirda, the Dawnwaker':('Mountain','Grizzly Bears')}
for companion,(yes,no) in examples.items():check(permits(companion,yes) and not permits(companion,no),companion+' per-card boundary')
check(rows['Thrasios, Triton Hero']['identity']==18,'native green blue identity')
check('Tymna the Weaver' in rows['Thrasios, Triton Hero']['partners'],'native partners')
check('Candlekeep Sage' in rows["Abdel Adrian, Gorion's Ward"]['partners'],'native background pair')
check(not rows['Jace, the Mind Sculptor']['commander'] and rows['Jace, the Mind Sculptor']['brawl'],'Brawl eligibility differs')
check(rows['Grist, the Hunger Tide']['commander'] and rows['Shorikai, Genesis Engine']['commander'],'nonstandard commander eligibility')
check(rows['Transguild Courier']['identity']==31,'all-color color indicator identity')
check(rows['Archangel Avacyn']['identity']==9,'both faces contribute identity')
check('Counterspell' in rows['Daxiver, Izzet Electromancer']['exemptions'],'native identity exception allows instants')
check(data['catalogHash']==hashlib.sha256((RT/'catalog.jsonl').read_bytes()).hexdigest(),'facets pinned to exact catalog')
wanted=set(data['companions'])|{'Thrasios, Triton Hero','Muldrotha, the Gravetide','Air Elemental','Mountain','Grizzly Bears','Shock','Counterspell'};printings={}
for line in (RT/'catalog.jsonl').open(encoding='utf8'):
 r=json.loads(line)
 if r['name'] in wanted and not r['ambiguous']:printings.setdefault(r['name'],r['key'])
def e(name,count=1):
 code,number=printings[name].split('/');return {'setCode':code,'collectorNumber':number,'count':count}
def native(label,companion,main,commanders,expected):
 # Unity's inspector makes the same starting-deck projection, with its fixed-size flag.
 deck={'cards':[e(n,c) for n,c in main+commanders],'sideboard':[e(companion)]}
 folder=OUT/label;folder.mkdir(exist_ok=True)
 config={'session':'editor-native','inspectCompanions':True,'commanderCompanionCheck':bool(commanders),'seed':1,'startingLife':20,'firstPlayerSeat':1,'decks':[deck,deck]}
 path=folder/'start.json';path.write_text(json.dumps(config),encoding='utf8')
 args=[str(RT/'jre/bin/java.exe'),'-Xmx384m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Dmtgb.bridge.lazyCatalog=true','-Dfile.encoding=UTF-8','-Duser.home='+str(folder),'-cp',str(RT/'classes')+os.pathsep+str(RT/'lib/*'),'mtgb.bridge.ForgeBridge',str(RT/'assets'),str(path)]
 result=subprocess.run(args,cwd=RT/'assets',env=dict(os.environ,APPDATA=str(folder),LOCALAPPDATA=str(folder)),capture_output=True,text=True,encoding='utf8',timeout=35)
 (folder/'stdout').write_text(result.stdout,encoding='utf8');(folder/'stderr').write_text(result.stderr,encoding='utf8')
 answer=next((json.loads(l) for l in result.stdout.splitlines() if l.startswith('{"legal"')),None)
 check(result.returncode==0 and answer is not None and (printings[companion] in answer['legal'])==expected,label)
native('gyruda-includes-even-commander','Gyruda, Doom of Depths',[('Mountain',98)],[('Thrasios, Triton Hero',1)],True)
native('lurrus-rejects-expensive-commander','Lurrus of the Dream-Den',[('Mountain',99)],[('Muldrotha, the Gravetide',1)],False)
native('lurrus-accepts-cheap-commander','Lurrus of the Dream-Den',[('Mountain',99)],[('Thrasios, Triton Hero',1)],True)
native('yorion-impossible-fixed-commander-size','Yorion, Sky Nomad',[('Mountain',99)],[('Thrasios, Triton Hero',1)],False)
native('yorion-ordinary-eighty','Yorion, Sky Nomad',[('Mountain',80)],[],True)
native('lutri-counts-commander-name','Lutri, the Spellchaser',[('Mountain',98),('Thrasios, Triton Hero',1)],[('Thrasios, Triton Hero',1)],False)
native('umori-counts-commander-type','Umori, the Collector',[('Mountain',98),('Shock',1)],[('Thrasios, Triton Hero',1)],False)
(OUT/'checks.json').write_text(json.dumps(checks,indent=2),encoding='utf8');assert all(c['pass'] for c in checks)

"""Collect the dock/known-zone delta without promoting historical full jobs."""
import hashlib, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'.tools/resolution-dock-1005'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
cases=[json.loads((OUT/'native'/n).read_text(encoding='utf-8')) for n in ['hand-memory-result.json','brain-two-result.json','explore-keep-result.json','growth-many-yes-result.json']]
assert all(c['PASS'] for c in cases)
ui=(OUT/'ui-result.txt').read_text(encoding='utf-8');hand=(OUT/'hand-browser-result.txt').read_text(encoding='utf-8')
assert ui.startswith('PASS') and hand.startswith('PASS')
rt=ROOT/'ForgeRuntime';cfg=json.loads((rt/'runtime.json').read_text(encoding='utf-8-sig'));manifest=json.loads((rt/'files.json').read_text(encoding='utf-8'))
assert cfg['runtimeHash']==sha(rt/'files.json')
classes=[x for x in manifest['files'] if x['path'].startswith('classes/mtgb/bridge/')]
for x in classes:assert sha(rt/x['path'])==x['sha256'],x['path']
for n in ['ForgeBridge.java','BridgeLibraryKnowledge.java']:assert sha(ROOT/'Tools/ForgeBridge/java/mtgb/bridge'/n)==sha(rt/'source/bridge/mtgb/bridge'/n)
files=sorted(p for p in OUT.rglob('*') if p.is_file() and p.suffix in ('.json','.jsonl','.txt','.png') and 'attempts' not in p.parts and not p.name.startswith('ui-attempt') and p.name!='proof.json')
source=[ROOT/'Assets/Scripts/UI'/('ForgeBattlefieldController.'+n+'.cs') for n in ['Integrated','HandActions','CardSelection','Arena','LandChoices','BattlefieldView','Zones','OtherHand']]
source += [ROOT/'Assets/Scripts/UI/ForgeBattlefieldController.cs',ROOT/'Assets/Scripts/Editor/ResolutionDockPresentationValidation.cs',ROOT/'Tools/ForgeBridge/HiddenZoneMemoryFixture.java',ROOT/'Tools/ForgeBridge/HiddenZoneMemoryNative.py',ROOT/'Tools/ForgeBridge/ResolutionReviewFixture.java',ROOT/'Tools/ForgeBridge/ResolutionReviewNative.py']
record=dict(status='PASS',nativeRuleFlows=len(cases),cases=cases,ui=ui,handBrowser=hand,runtimeHash=cfg['runtimeHash'],bridgeClasses=classes,
 limits=['Only these native flows and three-language archived-request Unity replay passed; no live-network, Commander or full-pool claim.','Main dock UI pass plus narrower hand-browser delta; unrelated native cases were not rerun.','Runtime verification checks bridge classes, source copies and manifest hash, not all asset hashes.','Private known-hand information is not public spectator permission; multiple library positions and all hidden-zone operations are outside this proof.'],
 evidence=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in files],sources=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in source])
(OUT/'proof.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:record[k] for k in ['status','nativeRuleFlows','runtimeHash']}))

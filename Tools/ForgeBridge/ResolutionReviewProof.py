"""Collect the focused evidence without promoting it to a whole-project PASS."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.tools/resolution-ui-1005'
CASES=['growth-yes','growth-no','growth-cancel','genesis-yes','genesis-no','genesis-spell','brain-two','wrath-two','wrath-zero','explore-keep','explore-grave','explore-draw','explore-shuffle']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
results=[json.loads((OUT/'native'/(case+'-result.json')).read_text(encoding='utf-8')) for case in CASES]
assert all(r['PASS'] for r in results)
ui=(OUT/'ui-result.txt').read_text(encoding='utf-8');payment=(OUT/'payment/ui-result.txt').read_text(encoding='utf-8')
delta=(OUT/'x-symbol-delta.txt').read_text(encoding='utf-8')
explore=(OUT/'explore-prompt-delta.txt').read_text(encoding='utf-8')
assert ui.startswith('PASS') and payment.startswith('PASS') and delta.startswith('PASS') and explore.startswith('PASS')
runtime=ROOT/'ForgeRuntime';config=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'));manifest=json.loads((runtime/'files.json').read_text(encoding='utf-8'))
assert config['runtimeHash']==sha(runtime/'files.json')
for entry in manifest['files']:
    if entry['path'].startswith('classes/mtgb/bridge/'):
        assert sha(runtime/entry['path'])==entry['sha256'],entry['path']
for name in ['ForgeBridge.java','BridgeLibraryKnowledge.java']:
    assert sha(ROOT/'Tools/ForgeBridge/java/mtgb/bridge'/name)==sha(runtime/'source/bridge/mtgb/bridge'/name)
files=sorted(p for p in OUT.rglob('*') if p.is_file() and p.suffix in ('.json','.jsonl','.txt','.png') and 'attempts' not in p.parts and p.name!='proof.json')
record={'status':'PASS','nativeRuleCases':12,'adapterChecks':1,'cases':results,'ui':ui,'payment':payment,'xSymbolDelta':delta,'explorePromptDelta':explore,'runtimeHash':config['runtimeHash'],
 'limits':['Focused native rules and archived-request Unity raycasts, not a live networking/Commander/full-pool PASS.','Same-ID shuffle check invokes the production event adapter; separate from actual card-effect scenarios.','Spectators retain only continuously public native tops; private looks and remembered public reveals are not promoted to spectator permission.','Native legacy case wrath-two chooses X=3 and pays 3 energy.','Only bridge class hashes and manifest hash checked; other runtime entries retained, no full-runtime rehash.'],
 'evidence':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in files]}
(OUT/'proof.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:record[k] for k in ('status','nativeRuleCases','adapterChecks','runtimeHash')},ensure_ascii=False))

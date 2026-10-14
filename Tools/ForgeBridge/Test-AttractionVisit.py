"""Bounded retained Attraction deltas; published rules, initial fixtures and entropy only."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
R=Path(__file__).resolve().parents[2];B=R/'.tools/attraction-visit-1012'
p=argparse.ArgumentParser();p.add_argument('case',choices=['visit','modifier-prefix','flag-tail','phase']);p.add_argument('--describe',action='store_true');a=p.parse_args()
drivers={'visit':('native.py',[]),'modifier-prefix':('modifier.py',['--prefix']),'flag-tail':('flag-tail.py',[]),'phase':('phase.py',[])}
if a.describe:
 print(json.dumps(dict(case=a.case,driver=drivers[a.case][0],productionRulesOnly=True,completedPrefixSeparateFromIndependentTail=True)));sys.exit(0)
files=json.loads((R/'ForgeRuntime/files.json').read_text('utf-8-sig'))['files']
prefixes=['classes/mtgb/bridge/','classes/forge/game/ability/effects/RollDiceEffect','classes/forge/game/ability/effects/OpenAttractionEffect','classes/forge/game/trigger/TriggerRolledDie','classes/forge/game/player/Player','classes/forge/game/phase/PhaseHandler']
actual=[x for x in files if any(x['path'].startswith(prefix)for prefix in prefixes)]
assert actual,'Published class manifest required'
for x in actual:assert hashlib.sha256((R/'ForgeRuntime'/x['path']).read_bytes()).hexdigest()==x['sha256'],x['path']
before=set((B/'native').iterdir());driver,args=drivers[a.case]
subprocess.run([sys.executable,'-X','utf8',str(B/driver),*args],cwd=R,check=True)
created=set((B/'native').iterdir())-before;assert len(created)==1,'Exactly one actual bounded native driver'
result=json.loads((created.pop()/'result.json').read_text('utf8'));assert result['status'].startswith('PASS')
print('PASS_BOUNDED '+a.case)

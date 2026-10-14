"""Current ordinary chosen-color non-mana land-origin increment, real native actions only."""
from pathlib import Path
import argparse,hashlib,json,os,subprocess,sys
R=Path(__file__).resolve().parents[2];B=R/'.tools/choice-nonmana-1012'
p=argparse.ArgumentParser();p.add_argument('case',choices=['sun-nonmana']);p.add_argument('--describe',action='store_true');a=p.parse_args()
if a.describe:
 print(json.dumps({'case':a.case,'nativeOperations':4,'manaPaidOperations':2,'tapActivations':2,'mana':7,'scenarios':1,'results':'Blue chosen vs red unchosen; ordinary non-mana trigger stack; precise targets/source and natural clear','productionRulesOnly':True}));sys.exit(0)
assert not os.environ.get('MTGB_NAME_OVERRIDE'),'No engine class override allowed'
assert not os.environ.get('MTGB_CHOICE_ASSETS'),'Published assets only'
files=json.loads((R/'ForgeRuntime/files.json').read_text('utf-8-sig'))['files']
classes=[x for x in files if x['path'].startswith(('classes/mtgb/bridge/','classes/forge/game/card/Card','classes/forge/game/ability/effects/ManaEffect','classes/forge/game/trigger/TriggerManaAdded'))]
assert classes,'Published identity manifest required'
for x in classes:assert hashlib.sha256((R/'ForgeRuntime'/x['path']).read_bytes()).hexdigest()==x['sha256'],x['path']
folder=B/'native'/a.case;before=set(folder.iterdir()) if folder.exists() else set()
subprocess.run([sys.executable,'-X','utf8',str(B/'native.py'),a.case],cwd=R,check=True)
created=set(folder.iterdir())-before;assert len(created)==1,'Exactly one native delta'
r=json.loads((created.pop()/'result.json').read_text('utf-8'));assert r['status']=='PASS_BOUNDED' and not r['violations']
print('PASS_BOUNDED '+a.case)

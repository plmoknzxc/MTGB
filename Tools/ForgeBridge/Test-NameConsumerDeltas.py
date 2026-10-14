"""Actual downstream Council/Garth effects; old source proofs remain independently scoped."""
from pathlib import Path
import argparse,hashlib,json,os,subprocess,sys
R=Path(__file__).resolve().parents[2];B=R/'.tools/name-consumer-delta-1012'
p=argparse.ArgumentParser();p.add_argument('case',choices=['council-garth-downstream']);p.add_argument('--describe',action='store_true');a=p.parse_args()
if a.describe:
 print(json.dumps(dict(case=a.case,endpoints=5,committedOperations=6,totalMana=17,scope='Council actual name prohibition/own discount/removal/opponent normal cast, Garth accepted copied Shivan Dragon paid4RR. Initial positions only.')));sys.exit(0)
assert not os.environ.get('MTGB_NAME_OVERRIDE')and not os.environ.get('MTGB_CHOICE_ASSETS'),'Published rules and assets only'
for row in json.loads((R/'ForgeRuntime/files.json').read_text('utf8'))['files']:
 if row['path'].startswith('classes/mtgb/bridge/'):
  assert hashlib.sha256((R/'ForgeRuntime'/row['path']).read_bytes()).hexdigest()==row['sha256'],row['path']
folder=B/'native'/a.case;before=set(folder.iterdir())if folder.exists()else set()
subprocess.run([sys.executable,'-X','utf8',str(B/'native.py'),a.case],cwd=R,check=True)
created=set(folder.iterdir())-before;assert len(created)==1,'Exactly one new actual host'
result=json.loads((created.pop()/'result.json').read_text('utf8'))
assert result['status']=='PASS_BOUNDED'and not result['violations']
print('PASS_BOUNDED '+a.case)

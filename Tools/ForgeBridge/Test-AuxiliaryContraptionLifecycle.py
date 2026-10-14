"""Retained delta drivers, production runtime only; no staged rule overrides."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
R=Path(__file__).resolve().parents[2];B=R/'.tools/auxiliary-lifecycle-1012'
parser=argparse.ArgumentParser();parser.add_argument('case',choices=['prefix','lifecycle','advance','copy-rigger']);parser.add_argument('--describe',action='store_true');args=parser.parse_args()
drivers={'prefix':('native.py',['--prefix']),'lifecycle':('native.py',[]),'advance':('extra.py',[]),'copy-rigger':('copy-rigger.py',[])}
if args.describe:
 print(json.dumps({'case':args.case,'driver':drivers[args.case][0],'productionRuntimeOnly':True,'initialPositionsAreNotEffects':True}));sys.exit(0)
cfg=json.loads((R/'ForgeRuntime/runtime.json').read_text('utf-8-sig'));files=json.loads((R/'ForgeRuntime/files.json').read_text('utf-8-sig'))['files']
relevant=[x for x in files if x['path'].startswith('classes/mtgb/bridge/')]
assert relevant,'Published class manifest required'
for x in relevant:assert hashlib.sha256((R/'ForgeRuntime'/x['path']).read_bytes()).hexdigest()==x['sha256'],x['path']
if args.case in ['advance','copy-rigger']:
 subprocess.run([sys.executable,'-X','utf8',str(B/('make-extra.py'if args.case=='advance'else'make-copy.py'))],cwd=R,check=True)
before=set((B/'native').iterdir());script,extra=drivers[args.case]
subprocess.run([sys.executable,'-X','utf8',str(B/script),*extra],cwd=R,check=True)
created=set((B/'native').iterdir())-before;assert len(created)==1,'One actual native flow per case'
result=json.loads((created.pop()/'result.json').read_text('utf-8'));assert result['status'].startswith('PASS')
print('PASS_BOUNDED '+args.case)

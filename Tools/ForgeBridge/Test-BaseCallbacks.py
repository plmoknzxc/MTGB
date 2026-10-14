"""Runtime proxy / inherited-method contract checks. No card effect claims."""
import json,subprocess,sys
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';OUT=R/'Tools/Validation/Reports/BaseCallbacks';CLASSES=R/'.tools/base-callbacks/classes'
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
cfg=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(R/'Tools/ForgeBridge/tests/BaseCallbackFixture.java')],check=True)
results=[]
for case in sys.argv[1:] or ['base-defaults','base-unsupported']:
    p=subprocess.run([str(RT/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.BaseCallbackFixture',case,str(OUT/(case+'-resolution.json'))],capture_output=True,text=True,encoding='utf8',timeout=30)
    (OUT/(case+'.log')).write_text(p.stdout+p.stderr,encoding='utf8')
    text=p.stdout.strip() if p.returncode==0 else 'FAIL '+case+': '+p.stderr[-2000:]
    print(text,flush=True);results.append(text)
(OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(not x.startswith('PASS') for x in results))

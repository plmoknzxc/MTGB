"""Native multiple optional color reductions. Expected resources are explicit."""
import importlib.util,json,sys,hashlib,traceback,time
from pathlib import Path
spec=importlib.util.spec_from_file_location('costs',Path(__file__).with_name('Test-CostInteractions.py'))
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
CASES=[]
for label,mods,mana in [('tax',['Sphere of Resistance'],3),('reduce',['Helm of Awakening'],1),('floor',['Trinisphere'],3)]:
    name='optional-two-colors-'+label
    base.add(name,'Doom Foretold',mana,['Island']*4,mods,['Defiler of Faith','Defiler of Flesh'],life=4,effect='creature',choice='life',choiceCount=2)
    CASES.append(name)
    name='optional-same-color-'+label
    base.add(name,'Angel of the Ruins',5+(1 if label=='tax' else -1 if label=='reduce' else 0),['Island']*6,mods,['Defiler of Faith']*2,life=4,effect='creature',choice='life',choiceCount=2)
    CASES.append(name)
def run(name):
    result=base.run(name)
    result.update(result='PASS '+name,runtimeHash=base.CFG['runtimeHash'],sourceHash=base.DRIVER_HASH,fixtureHash=base.FIXTURE_HASH,wrapperHash=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (base.OUT/(name+'-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');return result
if __name__=='__main__':
    results=[]
    for name in sys.argv[1:] or CASES:
        try:result=run(name);print('PASS '+name,flush=True)
        except Exception as error:
            traceback.print_exc();print('FAIL '+name,flush=True)
            result=dict(case=name,status='FAIL',result='FAIL '+name,error=repr(error),runtimeHash=base.CFG['runtimeHash'],sourceHash=base.DRIVER_HASH,fixtureHash=base.FIXTURE_HASH,wrapperHash=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
            (base.OUT/(name+'-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
        results.append(result)
    (base.OUT/('optional-run-'+str(time.time_ns())+'.json')).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
    sys.exit(any(r['status']!='PASS' for r in results))

"""Verify current cost protocol, bounded native outcomes and separate UI replay."""
import hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.tools/x-stack-insert-1005';RT=ROOT/'ForgeRuntime'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    cfg=read(RT/'runtime.json');assert cfg['runtimeHash']==sha(RT/'files.json')
    for row in read(RT/'files.json')['files']:
        if row['path'].startswith('classes/'):assert sha(RT/row['path'])==row['sha256'],row['path']
    cases=[]
    for name in ('normal','reduce','increase','xx-reduce','xx-increase','xx-zero','counter-tax'):
        base=OUT/'native';r=read(base/(name+'-result.json'))
        assert r['status']=='PASS' and r['runtimeHash']==cfg['runtimeHash']
        assert r['traceSha256']==sha(base/(name+'.jsonl'))
        stack=read(base/(name+'-stack.json'));e=next(e for e in stack['stackItems'] if e['source']['name'] in ('Song of Totentanz','Walking Ballista'))
        assert e['hasXValue'] and e['xValue']==r['x'] and e['source']['castingManaCost']==r['castingManaCost']
        resolved=read(base/(name+'-resolved.json'));assert sum(c['tapped'] for c in resolved['players'][0]['battlefield'] if c['land'])==r['manaSpent']
        if name=='counter-tax':
            tax=read(base/(name+'-resolution-tax.json'));e=next(e for e in tax['stackItems'] if e['source']['name']=='Song of Totentanz')
            assert e['source']['castingManaCost']=='{3}{R}' and e['xValue']==3 and r['manaSpent']==7
        cases.append(dict(result=r,stackSha256=sha(base/(name+'-stack.json')),resolvedSha256=sha(base/(name+'-resolved.json'))))
    ui=OUT/'ui-final';result=(ui/'result.txt').read_text();assert result.startswith('PASS:'),result
    screenshots={}
    for p in ui.glob('*.png'):
        data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';w,h=struct.unpack('>II',data[16:24]);assert w>1000 and h>500
        screenshots[p.name]=dict(sha256=sha(p),width=w,height=h)
    assert len(screenshots)>=18,'Missing relevant language/cost screenshots'
    sources=['Assets/Scripts/UI/ForgeBattlefieldController.ChosenSpell.cs','Assets/Scripts/UI/ForgeBattlefieldController.Prepared.cs','Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs','Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs','Assets/Scripts/UI/ForgeBattlefieldController.StackList.cs','Assets/Scripts/Networking/ForgeWire.cs','Assets/Scripts/Editor/XSpellPresentationValidation.cs','Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java','Tools/ForgeBridge/NativePatches.json','Tools/ForgeBridge/Test-XCastingCost.py','Tools/ForgeBridge/tests/XCastingCostFixture.java']
    proof=dict(status='PASS',nativeCases=cases,ui=result,screenshots=screenshots,currentSources={p:sha(ROOT/p) for p in sources},runtimeHash=cfg['runtimeHash'],limitations=['Ordinary modern printed card frame samples only; other frame geometry not exhaustively sampled.','Omen/Wrath/Stomp/Soul Tether inputs are historical native snapshots; new cost cases use current native snapshots.','Triple copies in browser are layout adapters.','Not all cost modifiers, alternative payment types, copied-X overrides or live network paths.'])
    (OUT/'proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf8');print('PASS: seven native cost cases; separate bounded UI evidence; runtime classes match manifest')
if __name__=='__main__':main()

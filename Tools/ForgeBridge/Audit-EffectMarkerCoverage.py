"""Fail bridge rebuilds when pinned Forge adds unreviewed delayed-trigger storage."""
import json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2];game=root/'.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
expected={
 'trigger/TriggerHandler.java':({'activeTriggers','delayedTriggers','thisTurnDelayedTriggers','playerDefinedDelayedTriggers','waitingTriggers'},r'(?:private|protected)[^;\n]*<(?:Player, )?Trigger(?:Waiting)?>\s+(\w+)\s*='),
 'phase/Phase.java':({'at','until','untilMap','untilEndMap','registerMap'},r'(?:private|protected)[^;\n]*<(?:Player, )?GameCommand>\s+(\w+)\s*='),
 'phase/PhaseHandler.java':({'extraTurns','extraPhases'},r'private[^;\n]*Extra(?:Turn|Phase)[^;\n]*>\s+(\w+)\s*=')}
report={'scope':'public Command effect objects and delayed-trigger storage; not all Magic card rules','schemas':{},'registrationCallers':[],'unreviewed':[]}
for path,(known,pattern) in expected.items():
 actual=set(re.findall(pattern,(game/path).read_text(encoding='utf8')))
 report['schemas'][path]=sorted(actual)
 if actual!=known:report['unreviewed'].append(dict(path=path,added=sorted(actual-known),removed=sorted(known-actual)))
for path in sorted(game.rglob('*.java')):
 for number,line in enumerate(path.read_text(encoding='utf8').splitlines(),1):
  calls=re.findall(r'\b(register\w*DelayedTrigger)\s*\(',line)
  for call in calls:
   report['registrationCallers'].append(dict(path=str(path.relative_to(game)),line=number,method=call))
   if call not in {'registerDelayedTrigger','registerThisTurnDelayedTrigger','registerPlayerDefinedDelayedTrigger'}:report['unreviewed'].append(dict(path=str(path),method=call))
piece=(root/'.tools/forge-proof/upstream/forge-core/src/main/java/forge/card/GamePieceType.java').read_text(encoding='utf8')
piece=re.sub(r'/\*.*?\*/','',piece,flags=re.S)
actual=set(re.findall(r'^\s*([A-Z_]+)\s*[,}]',piece,re.M))
known={'CARD','TOKEN','EFFECT','COPIED_SPELL','ATTRACTION','CONTRAPTION','PLANAR','SCHEME','AVATAR','DUNGEON'}
report['gamePieceTypes']=sorted(actual)
if actual!=known:report['unreviewed'].append(dict(path='GamePieceType.java',added=sorted(actual-known),removed=sorted(known-actual)))
report['status']='FAIL' if report['unreviewed'] else 'PASS'
out=root/'Tools/Validation/Reports/MarkerCoverage';out.mkdir(parents=True,exist_ok=True)
(out/'source-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(report['status']+': effect marker source audit; '+str(len(report['registrationCallers']))+' registration sites')
if report['unreviewed']:print(json.dumps(report['unreviewed'],ensure_ascii=False));raise SystemExit(1)

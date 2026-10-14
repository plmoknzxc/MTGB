"""Bounded source classification of mana restrictions and spending modifiers.

This inventory is not effect evidence. Preserve exact predicates and referenced
SVars: two identically named TrigSpent variables need not do the same thing.
"""
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RT = ROOT / 'ForgeRuntime'
FIELDS = ('RestrictValid', 'AddsNoCounter', 'AddsKeywords', 'AddsKeywordsValid',
          'AddsKeywordsUntil', 'AddsCounters', 'TriggersWhenSpent',
          'PersistentMana', 'CombatMana')
SPECIAL_RESTRICTIONS = {'nonSpell': 'restriction-nonspell',
    'CumulativeUpkeep': 'restriction-cumulative-upkeep',
    'CostContainsX': 'restriction-cost-X', 'CostContainsC': 'restriction-cost-C',
    'CantPayGenericCosts': 'restriction-mana-shard',
    'CantCastNonArtifactSpells': 'restriction-nonartifact-spell'}

def branches(fields):
    result = set()
    for predicate in fields.get('RestrictValid', '').split(','):
        if not predicate:
            continue
        result.add(SPECIAL_RESTRICTIONS.get(predicate,
            'restriction-cast-zone' if predicate.startswith('CantCastSpellFrom')
            else 'restriction-native-validity'))
    for field, family in [('AddsNoCounter', 'spending-no-counter'),
        ('AddsKeywords', 'spending-keywords'), ('AddsCounters', 'spending-etb-counters'),
        ('TriggersWhenSpent', 'spending-delayed-trigger'),
        ('PersistentMana', 'pool-persistent'), ('CombatMana', 'pool-combat')]:
        if field in fields:
            result.add(family)
    return sorted(result)

def classify():
    rows = []
    counts = Counter()
    for scope in ('cardsfolder', 'tokenscripts', 'effects'):
        for path in sorted((RT/'assets/res'/scope).rglob('*.txt')):
            counts[scope] += 1
            lines = path.read_text(encoding='utf-8-sig').splitlines()
            face = 0
            face_by_line = []
            definitions = []
            for number, line in enumerate(lines, 1):
                if line == 'ALTERNATE':
                    face += 1
                face_by_line.append(face)
                if (m := re.match(r'^SVar:([^:]+):(.*)$', line)):
                    definitions.append(dict(name=m[1], value=m[2], line=number, face=face))
            for number, line in enumerate(lines, 1):
                if not re.search(r'(?:AB|SP|DB)\$ Mana(?:Reflected)?(?:\s*\||\s*$)', line):
                    continue
                params = {m[1].strip(): m[2].strip() for part in line.split('|')
                          if (m := re.search(r'([^:$]+)\$\s*(.*)', part))}
                modifiers = {key: params[key] for key in FIELDS if key in params}
                if not modifiers:
                    continue
                rows.append(dict(path=path.relative_to(ROOT).as_posix(),
                    sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                    line=number, declaration=line, params=params, modifiers=modifiers,
                    nativeBranches=branches(modifiers), cardFaceIndex=face_by_line[number-1],
                    # Preserve local definitions, including repeated names on different cards.
                    sourceSVars={d['name']: d['value'] for d in definitions
                                 if d['face'] == face_by_line[number-1]},
                    sourceSVarDeclarations=definitions,
                    owner='R09', status='SOURCE_CLASSIFIED_NOT_EFFECT_PASS'))
    families = defaultdict(list)
    for row in rows:
        for family in row['nativeBranches']:
            families[family].append({'path': row['path'], 'line': row['line']})
    # Local completeness checks against known counterexamples to the earlier scan.
    assert any(r['path'].endswith('pyromancers_goggles.txt') and
               'TriggersWhenSpent' in r['modifiers'] for r in rows)
    assert any(r['path'].endswith('savage_ventmaw.txt') and
               'PersistentMana' in r['modifiers'] for r in rows)
    assert any(r['path'].endswith('avatar_roku_firebender.txt') and
               'CombatMana' in r['modifiers'] for r in rows)
    assert any(r['path'].endswith('rousing_refrain.txt') and
               r['params'].get('SP') == 'Mana' for r in rows)
    assert len({(r['path'], r['line']) for r in rows}) == len(rows)
    # The two faces reuse TrigMana; neither definition may silently overwrite the other.
    kessig = [r for r in rows if r['path'].endswith('kessig_naturalist_lord_of_the_ulvenwald.txt')]
    assert {r['cardFaceIndex'] for r in kessig} == {0, 1}
    for row in rows:
        assert row['sourceSVars'] == {d['name']: d['value'] for d in row['sourceSVarDeclarations']
                                     if d['face'] == row['cardFaceIndex']}
    return dict(status='SOURCE_CLASSIFIED_NOT_EFFECT_PASS',
        scope='Current runtime txt AB/SP/DB Mana/ManaReflected declarations containing the nine AbilityManaPart restriction/spending/pool fields. Does not classify all produced-value expressions, mana replacement/static effects, costs, reachability, or referenced effect execution. General validity predicates and local SVars retain exact source; grouped branch names are not equivalent-rule or UI PASS.',
        runtimeHash=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))['runtimeHash'],
        driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        filesRead={scope: counts[scope] for scope in ('cardsfolder','tokenscripts','effects')},
        fields=list(FIELDS), rows=rows, families=dict(sorted(families.items())))

if __name__ == '__main__':
    output=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'.tools/mana-consumer-classification-1006/classified.json'
    data=classify();output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(output=str(output),rows=len(data['rows']),branches={k:len(v) for k,v in data['families'].items()},status=data['status'])))

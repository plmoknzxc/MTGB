"""Join every original candidate to its current evidence and reviewed disposition."""
import collections
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'Tools/ForgeBridge/Reports'
def read(name):
    return json.loads((REPORTS / name).read_text(encoding='utf-8'))
def identity(g):
    return tuple(g[k] for k in ('key', 'name', 'language', 'field', 'detail'))
contexts = json.loads((ROOT / '.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))
original = {identity(x['gap']): x for x in contexts}
sources = {identity(x): x for x in read('LocalizedMissingFieldReview.json')['entries']}
gatherer_path = REPORTS / 'GathererJapaneseSupplements.json'
gatherer = {(x['key'],x['name']):x for x in read(gatherer_path.name)['entries']} if gatherer_path.exists() else {}
prints_path = REPORTS / 'GathererJapaneseAllPrints.json'
prints = {(x['key'],x['name']):x for x in read(prints_path.name)['entries']} if prints_path.exists() else {}
whisper_path = REPORTS / 'WhisperJapaneseRemaining.json'
whisper = {(x['key'],x['name']):x for x in read(whisper_path.name)['entries']} if whisper_path.exists() else {}
remaining_path = REPORTS / 'RemainingJapaneseReviewedRules.json'
remaining = {(x['key'],x['name']):x for x in read(remaining_path.name)['entries']} if remaining_path.exists() else {}
rows = []
for evidence in read('ReviewedPresentationCurrentEvidence.json')['entries']:
    gap = evidence['original']
    row = dict(**gap)
    field = gap['field']
    if field in ('name', 'type', 'rules'):
        source = sources[identity(gap)]
        row.update(status=source['status'], sourceReview=source)
        column = {'name': 1, 'type': 2, 'rules': 3}[field]
        row['currentValue'] = (evidence.get('currentFields') or ['', '', '', ''])[column]
        official = gatherer.get((gap['key'], gap['name']))
        if official:
            row['gathererReview'] = official
            if source['status'] == 'translation-unavailable-in-checked-sources' and official['status'] == 'published-Japanese-rules-imported' and re.search(r'[\u3040-\u30ff\u3400-\u9fff]',row['currentValue']):
                row['status'] = 'official-Gatherer-field-imported'
        if gap['name'] == 'P-No Way Out':
            row['identityEvidence'] = 'Forge cardsfolder/n/p_no_way_out.txt: Plane — Duskmourn, with dread counters and manifest dread; separate from the No Way Out sorcery.'
        print_review = prints.get((gap['key'],gap['name'])) if gap['language']=='ja-JP' else None
        if print_review:
            row['gathererAllPrintsReview'] = print_review
            if row['status']=='translation-unavailable-in-checked-sources':
                if print_review['status'] in ('published-Japanese-rules-imported-from-print-index','published-Japanese-rules-transcribed-from-official-image') and re.search(r'[\u3040-\u30ff\u3400-\u9fff]',row['currentValue']):
                    row['status']='official-Gatherer-field-imported'
                elif field=='rules' and print_review['status']=='intrinsic-basic-land-mana-symbol-display' and row['currentValue']==print_review['displayRules']:
                    row['status']='intrinsic-basic-land-mana-symbol-display'
        bilingual = whisper.get((gap['key'],gap['name'])) if gap['language']=='ja-JP' else None
        if bilingual:
            row['bilingualDatabaseReview']=bilingual
            if row['status']=='translation-unavailable-in-checked-sources' and bilingual['status']=='bilingual-published-Japanese-rules-verified' and re.search(r'[\u3040-\u30ff\u3400-\u9fff]',row['currentValue']):
                row['status']='published-bilingual-database-field-imported'
        repaired=remaining.get((gap['key'],gap['name'])) if gap['language']=='ja-JP' else None
        if repaired:
            row['remainingReviewedRuleEvidence']=repaired
            if row['status']=='translation-unavailable-in-checked-sources' and re.search(r'[\u3040-\u30ff\u3400-\u9fff]',row['currentValue']):
                row['status']='official-Japanese-release-notes-field-imported' if repaired['status']=='official-Japanese-release-notes-rules-imported' else 'reviewed-published-image-or-face-field-imported'
        if 'verificationMode' in evidence:
            row['verificationMode']=evidence['verificationMode']
            row['runtimeRecheckPending']=evidence.get('runtimeRecheckPending',False)
    elif field == 'keyword':
        row.update(englishRules=original[identity(gap)]['englishRules'], currentRules=evidence['rules'],
                   currentEnglishMechanics=evidence['expected'], currentLocalizedMechanics=evidence['actual'])
        if not evidence['expectedStillPresent']:
            row.update(status='false-expectation-excluded', reason='Reviewed card/self-reference, named card, descriptive heading, spellbook/booster label or counter name; contextual matching no longer treats it as this mechanic.')
        elif evidence['actualPresent']:
            row.update(status='localized-mechanic-recognized', reason='Current localized rule matches the corresponding English mechanic family after alias, parser or exact rule repair.')
        else:
            assert gap['language'] == 'ja-JP'
            name = gap['name']
            if gap['detail'].startswith('Affinity'):
                reason = 'Japanese printing spells out the per-permanent cost reduction; Forge English normalizes it to Affinity. Cost-reduction prose is preserved, not relabeled as a printed keyword.'
            elif name == 'Arena':
                reason = 'Japanese printing spells out reciprocal power-based damage instead of the Fight keyword. Missing tap-target instruction separately corrected as a presentation erratum.'
            elif name == "Urza's Science Fair Project":
                reason = 'Japanese explicitly says attacking does not tap it, rather than using Vigilance.'
            elif name == 'Form of the Squirrel':
                reason = 'Japanese explicitly says the player cannot be targeted by spells or abilities, rather than using Shroud.'
            else:
                raise AssertionError('Unreviewed keyword: ' + repr(gap))
            row.update(status='equivalent-published-prose', reason=reason)
    else:
        assert field == 'token'
        assert evidence['mechanicRouteVerified'] and evidence['localImages'] == evidence['relatedImages'] > 0, gap
        row.update(status='explicit-token-link-verified' if evidence['actualPresent'] else 'implicit-mechanic-token-route-verified',
                   currentRules=evidence['rules'], localizedMechanics=evidence['actual'],
                   verifiedLocalTokenKeys=evidence['tokenKeys'], localImages=evidence['localImages'],
                   runtimeEvidence='.tools/presentation-audit/reviewed-' + ('japanese' if gap['language']=='ja-JP' else 'chinese') + '-token-runtime.txt')
    rows.append(row)
assert len(rows) == len(contexts) == 5186
counts = dict(collections.Counter(x['status'] for x in rows))
missing_rules = collections.Counter(x['language'] for x in rows if x['field'] == 'rules' and x['status'] == 'translation-unavailable-in-checked-sources')
output = dict(complete=True, originalCandidates=len(rows), dispositions=counts,
              unavailableRuleFields=dict(missing_rules), entries=rows,
              limitation='Every original flagged item reviewed against current identity, matching and available cached translation sources. Source absence is not universal nonexistence. This is not a sentence-by-sentence audit of unflagged translations or a game-resolution validation of all mechanics; all printing variants and ambiguous identities are outside the original scan.')
(REPORTS / 'LocalizedPresentationReviewed.json').write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(counts=counts, unavailableRuleFields=dict(missing_rules)), ensure_ascii=False))

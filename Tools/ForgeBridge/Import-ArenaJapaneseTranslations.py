"""Import missing Japanese fields from the official Arena card database.

Fetch only the version manifest and card database, not an Arena installation.
Identity matching uses the database's exact English face name. Rebalanced faces
retain their A- prefix; alternate text variants and pronunciation-only rows are
never imported. Existing published translations retain priority at runtime.
"""
import base64
import gzip
import hashlib
import html
import json
import re
import sqlite3
from pathlib import Path

import requests

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / '.tools/arena-localization'
DEST = ROOT / 'Assets/StreamingAssets/CardTranslations/ja-JP.arena.txt'
REPORT = ROOT / 'Tools/ForgeBridge/Reports/ArenaJapaneseTranslations.json'
JA = re.compile(r'[\u3040-\u30ff\u3400-\u9fff]')


def download_database():
    WORK.mkdir(parents=True, exist_ok=True)
    session = requests.Session()
    session.headers['User-Agent'] = 'MTGB-local-published-translation-import/1.0'
    def get(url):
        response = session.get(url, timeout=(15, 120))
        response.raise_for_status()
        return response
    version = get('https://mtgarena.downloads.wizards.com/Live/Windows64/version').json()
    # Version components are numeric, unlike the human-readable release date.
    current = max(version['Versions'], key=lambda value: tuple(map(int, value.split('.'))))
    manifest_version = '_'.join(current.split('.')[2:])
    identifiers = get('https://assets.mtgarena.wizards.com/External_' + manifest_version + '.mtga').text.strip().splitlines()
    for identifier in identifiers:
        blob = get('https://assets.mtgarena.wizards.com/Manifest_' + identifier + '.mtga').content
        manifest = json.loads(gzip.decompress(blob) if blob.startswith(b'\x1f\x8b') else blob)
        for asset in manifest['Assets']:
            if not asset['Name'].startswith('Raw_CardDatabase_'):
                continue
            url = 'https://assets.mtgarena.wizards.com/' + asset['Name'] + ('.' + asset['wrapper'] if asset.get('wrapper') else '')
            path = WORK / 'CardDatabase.mtga'
            expected = base64.b64decode(asset['sha256']).hex()
            if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                content = get(url).content
                if asset.get('wrapper') == 'gz':
                    content = gzip.decompress(content)
                if hashlib.sha256(content).hexdigest() != expected:
                    raise ValueError('Official database checksum mismatch')
                temporary = path.with_suffix('.part')
                temporary.write_bytes(content)
                temporary.replace(path)
            source = dict(version=current, url=url, sha256=expected)
            (WORK / 'source.json').write_text(json.dumps(source, indent=2), encoding='utf-8')
            return path, source
    raise ValueError('No official card database in manifest')


def clean(text):
    text = (text or '').replace('<sprite="SpriteSheet_MiscIcons" name="arena_a">', 'A-')
    text = re.sub(r'<br\s*/?>', '\n', text)
    text = re.sub(r'<[^>]+>', '', text)
    # Remove phonetic readings, preserving ability reminder text.
    text = re.sub(r'(?<=[\u3400-\u9fff])（[\u3040-\u309f]+）', '', text)
    def mana(match):
        value = match[1]
        symbols = re.findall(r'o(?:\(([^)]+)\)|([0-9]+|[WUBRGCSTXYZ]))', value, re.I)
        if not symbols:
            return match[0]
        return ''.join('{' + (hybrid or ordinary).upper() + '}' for hybrid, ordinary in symbols)
    text = re.sub(r'\{([^}]+)\}', mana, text)
    return html.unescape(text).strip()


def field(text):
    return clean(text).replace('|', '｜').replace('\r', '').replace('\n', '\\n')


def main():
    path, source = download_database()
    db = sqlite3.connect('file:' + path.as_posix() + '?mode=ro', uri=True)
    db.row_factory = sqlite3.Row
    # 0 = unformatted, 1 = full display text. 2/4 are pronunciation/search text.
    def localizations(language):
        result = {}
        for row in db.execute('SELECT LocId, Loc FROM Localizations_' + language + ' WHERE Formatted IN (0,1) ORDER BY Formatted DESC'):
            result[row['LocId']] = clean(row['Loc'])
        return result
    english, japanese = localizations('enUS'), localizations('jaJP')
    abilities = {row['Id']: row for row in db.execute('SELECT * FROM Abilities')}
    local = {}
    for line in (ROOT / '.tools/keyword-glossary/catalog-rules.tsv').read_text(encoding='utf-8').splitlines():
        name, rules = [base64.b64decode(value).decode('utf-8') for value in line.split('\t')]
        local[name] = rules
    rows, evidence, conflicts = {}, {}, []
    for card in db.execute('SELECT * FROM Cards ORDER BY IsPrimaryCard DESC, GrpId DESC'):
        name = english.get(card['TitleId'], '')
        if name not in local or name in rows:
            continue
        title = japanese.get(card['TitleId'], '')
        type_text = japanese.get(card['TypeTextId'], '')
        subtype = japanese.get(card['SubtypeTextId'], '')
        if subtype:
            type_text += ' — ' + subtype
        pairs = [(int(value.split(':')[0]), int(value.split(':')[1])) for value in card['AbilityIds'].split(',') if value]
        ability_ids = [identifier for _, identifier in pairs]
        # The per-card TextId is authoritative, not the generic base ability TextId.
        texts = []
        for ability_id, identifier in pairs:
            text = japanese.get(identifier, '')
            if text == '#ClassLevelArtifact':
                # Arena draws the level gate separately. The preceding level
                # activation and following gated abilities retain their order.
                continue
            ability = abilities.get(ability_id)
            # Station level is drawn as a frame component in Arena, not prose.
            # Preserve its threshold between the ordinary and gated abilities.
            if text == '#StationLevelInstrinsic_NoTranslation' and ability and ability['LevelRequirement']:
                text = japanese.get(60000, '') + ' ' + ability['LevelRequirement']
            if ability and ability['LoyaltyCost'] and not re.match(r'^[+＋\-－−‐–]?[0-9０-９XＸ]+[：:]', text):
                text = ability['LoyaltyCost'] + '：' + text
            texts.append(text)
        if not JA.search(title) or not JA.search(type_text):
            continue
        if local[name].strip() and (not texts or any(not JA.search(text) for text in texts)):
            conflicts.append(dict(name=name, grpId=card['GrpId'], reason='incomplete Japanese ability text', textIDs=ability_ids))
            continue
        text = '\n'.join(dict.fromkeys(texts))
        rows[name] = [name, field(title), field(type_text), field(text)]
        evidence[name] = dict(name=name, grpId=card['GrpId'], titleID=card['TitleId'], textIDs=ability_ids,
                              englishRules='\n'.join(english.get(identifier, '') for identifier in ability_ids),
                              rebalanced=bool(card['IsRebalanced']), textSha256=hashlib.sha256(text.encode()).hexdigest())
    # Combined identities are composed only when each face exists in this source.
    for name in local:
        if name in rows or ' // ' not in name:
            continue
        parts = [rows.get(face) for face in name.split(' // ')]
        if all(parts):
            rows[name] = [name, ' // '.join(part[1] for part in parts), ' // '.join(part[2] for part in parts),
                          '\\n\\n'.join(part[1] + '\\n' + part[3] for part in parts)]
            evidence[name] = dict(name=name, faces=name.split(' // '))
    DEST.write_text('\n'.join('|'.join(rows[name]) for name in sorted(rows)) + '\n', encoding='utf-8')
    merged = {}
    for translation in ('ja-JP.txt', 'ja-JP.supplement.txt', 'ja-JP.arena.txt'):
        for line in (DEST.parent / translation).read_text(encoding='utf-8-sig').splitlines():
            parts = line.split('|', 3)
            if len(parts) != 4:
                continue
            previous = merged.setdefault(parts[0], parts)
            for column in range(1, 4):
                partial = column == 3 and re.search(r"[A-Za-z][A-Za-z'’-]*(?: +[A-Za-z][A-Za-z'’-]*){3,}", previous[column])
                if (not JA.search(previous[column]) or partial) and parts[column]:
                    previous[column] = parts[column]
    alchemy = []
    for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
        card = json.loads(line)
        if re.fullmatch(r'Y[A-Z]{3}', card['key'].split('/')[0]):
            alchemy.append(card)
    missing = [dict(key=card['key'], name=card['name'], column=column)
               for card in alchemy for column in (1, 2, 3)
               if not JA.search(merged.get(card['name'], ['', '', '', ''])[column])]
    coverage = dict(cards=len(alchemy), missingFields=missing)
    report = dict(source=source, rows=len(rows), entries=[evidence[name] for name in sorted(rows)],
                  rejected=conflicts, alchemyCoverage=coverage)
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Imported {len(rows)} official Japanese faces; {len(conflicts)} incomplete candidates rejected', flush=True)
    print(f'Alchemy coverage: {len(alchemy)} cards; {len(missing)} missing Japanese fields', flush=True)


if __name__ == '__main__':
    main()

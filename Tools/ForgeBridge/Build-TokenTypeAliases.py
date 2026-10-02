"""Derive display-only token subtype names from local card type translations; no network.

Use the dominant alignment only: rare inconsistent subtype ordering must not turn
Goblin into Samurai. Proper token names remain exact per-card references.
"""
import argparse
import collections
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Assets/Resources/TokenTypeAliases.json'

# Token-only subtypes have few or no ordinary-card type lines to vote on.
# These names are present in the local Japanese rules corpus, including historic spellings.
JAPANESE_TOKEN_NAMES = {
    'Snake': ['蛇', 'ヘビ'], 'Servo': ['霊気装置'], 'Blood': ['血'],
    'Powerstone': ['パワーストーン'], 'Map': ['地図'], 'Inkling': ['墨獣'],
    'Germ': ['細菌'], 'Scion': ['末裔'], 'Army': ['軍団'], 'Junk': ['ジャンク'],
    'Mutagen': ['ミュータンジェン'], 'Cadet': ['実習生'], 'Villain': ['悪人'],
    'Hero': ['英雄'], 'Young Hero': ['若き英雄'], 'Royal': ['王族'],
    'Monster': ['怪物'], 'Sorcerer': ['魔術師'], 'Wicked': ['ひねくれ者'],
    'Cursed': ['呪われし者'], 'Virtuous': ['聖者'], 'Role': ['役割'],
    'Sand': ['砂漠の民'], 'Heartwood': ['魂木'], 'Gold': ['金'],
    'Copy': ['コピー'],
    'Incubator': ['培養器'], 'Lizard': ['トカゲ'], 'Tentacle': ['触手'],
    'Graveborn': ['墓生まれ'], 'Vibranium': ['ヴィブラニウム'],
    'Lander': ['着陸船'], 'Moogle': ['モーグリ'], 'Shard': ['破片'],
    'Octopus': ['タコ'], 'Robot': ['ロボット'],
    'Serf': ['農奴'], 'Pentavite': ['ペンタバイト'], 'Triskelavite': ['トリスケラバイト'],
    'Sculpture': ['彫像'], 'Gingerbrute Token': ['ジンジャーブルート'],
    'Merfolk Wizard': ['マーフォーク'], 'Rat Rogue': ['ネズミ'],
    'Rukh': ['鳥'],
    'The Void': ['ヴォイド'],
}


def build():
    japanese = {}
    japanese_rules = {}
    for filename in ('ja-JP.txt', 'ja-JP.supplement.txt'):
        path = ROOT / 'Assets/StreamingAssets/CardTranslations' / filename
        if not path.exists():
            continue
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            fields = line.split('|')
            if len(fields) >= 3 and fields[2]:
                japanese[fields[0]] = fields[2]
            if len(fields) >= 4 and fields[3]:
                japanese_rules[fields[0]] = fields[3]
    translations = {}
    for line in (ROOT / 'Assets/StreamingAssets/CardTranslations/zh-CN.txt').read_text(encoding='utf-8-sig').splitlines():
        fields = line.split('|')
        if len(fields) >= 3:
            translations[fields[0]] = fields[2]
    votes = collections.defaultdict(collections.Counter)
    japanese_votes = collections.defaultdict(collections.Counter)
    seen = set()
    faces = []
    for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf8').splitlines():
        for face in json.loads(line)['faces']:
            if face['name'] in seen:
                continue
            seen.add(face['name'])
            faces.append(face)
            english = face.get('subtypes', [])
            japanese_parts = re.split('[～—]', japanese.get(face['name'], ''), maxsplit=1)
            if english and len(japanese_parts) == 2:
                subtypes = [s.strip() for s in re.split('[・／/]', japanese_parts[1])]
                if len(english) == len(subtypes):
                    for en, ja in zip(english, subtypes):
                        if ja:
                            japanese_votes[en][ja] += 1
            parts = re.split('[～—]', translations.get(face['name'], ''), maxsplit=1)
            if not english or len(parts) != 2:
                continue
            chinese = [s.strip() for s in re.split('[／/]', parts[1])]
            if len(english) == len(chinese):
                for en, zh in zip(english, chinese):
                    if zh:
                        votes[en][zh] += 1
    # Exact named tokens can be localized from their parent rules even when no
    # standalone token translation exists. Accept only one unambiguous pairing.
    related = json.loads((ROOT / 'Assets/Resources/RelatedTokenArt.json').read_text(encoding='utf8'))['entries']
    references = {}
    for parent in related:
        references.setdefault(parent['name'], {t['name'] for t in parent['tokens']})
    named = collections.defaultdict(set)
    for face in faces:
        source = face.get('text', '')
        candidates = [n for n in references.get(face['name'], ())
                      if n not in votes and n not in japanese_votes and n not in JAPANESE_TOKEN_NAMES
                      and re.search(r'\b(?:create\s+(?:\w+\s+){0,3}|named\s+|name is\s+)' + re.escape(n) + r'(?=[,.;\\\n]|\s|$)', source, re.I)]
        local_names = set(re.findall(r'[「『《]([^」』》]+)[」』》](?:という|と言う)名前', japanese_rules.get(face['name'], '')))
        if len(candidates) == len(local_names) == 1:
            value = next(iter(local_names)).split('/')[0]
            named[candidates[0]].add(value)
    entries = []
    for en in sorted(votes.keys() | japanese_votes.keys() | JAPANESE_TOKEN_NAMES.keys() | named.keys()):
        entry = dict(en=en)
        for language, catalog in (('zh', votes), ('ja', japanese_votes)):
            choices = catalog.get(en)
            if choices:
                value, count = choices.most_common(1)[0]
                if count / sum(choices.values()) >= .8:
                    entry[language] = value
        aliases = set(JAPANESE_TOKEN_NAMES.get(en, [])) | named.get(en, set())
        if entry.get('ja'):
            aliases.add(entry['ja'])
        if aliases:
            entry['jaAliases'] = sorted(aliases)
            entry['ja'] = entry.get('ja') or entry['jaAliases'][0]
        if len(entry) > 1:
            entries.append(entry)
    return json.dumps(dict(entries=entries), ensure_ascii=False, indent=2) + '\n'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build()
    if args.check:
        assert DEST.read_text(encoding='utf8') == result, 'Token type aliases are stale'
    else:
        DEST.write_text(result, encoding='utf8')
    print('PASS:', len(json.loads(result)['entries']), 'local token subtype translations')

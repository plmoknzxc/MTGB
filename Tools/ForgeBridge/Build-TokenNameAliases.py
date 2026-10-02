"""Build local Chinese named-token aliases from identity-checked source metadata."""
import json
from pathlib import Path
from LocalizedArtIdentity import matches

ROOT = Path(__file__).resolve().parents[2]
tokens = {}
for entry in json.loads((ROOT / 'Assets/Resources/RelatedTokenArt.json').read_text(encoding='utf-8'))['entries']:
    for token in entry['tokens']:
        tokens.setdefault(token['key'], token)
entries = []
for code in sorted({key.split('/')[0] for key in tokens}):
    path = ROOT / '.tools/chinese-card-images' / ('set-' + code + '.json')
    if not path.exists():
        continue
    for item in json.loads(path.read_text(encoding='utf-8'))['items']:
        key = item['set'].upper() + '/' + item['collector_number']
        token = tokens.get(key)
        if not token:
            continue
        faces = [item] + (item.get('other_faces') or [])
        if not matches(token, [face['display_name'] for face in faces], item.get('oracle_id')):
            continue
        aliases = sorted({face['display_name_zh'] for face in faces if face.get('display_name_zh')})
        # Some new helper tokens have a translated subtype but retain their
        # English name in the source (e.g. Mutagen and Heartwood).
        for face in faces:
            subtype = (face.get('display_type_line') or '').split('～')
            if len(subtype) == 2:
                aliases.extend((subtype[1], subtype[1].replace('／', '')))
        if token['name'] == 'Sculpture':
            aliases.append('雕像')  # Older published Doomed Artisan wording.
        if token['name'] == 'Contract':
            aliases.append('合约')  # Published Scriv, the Obligator wording.
        aliases = sorted(set(aliases))
        if aliases:
            entries.append(dict(key=key, aliases=aliases))
destination = ROOT / 'Assets/Resources/TokenNameAliases.json'
destination.write_text(json.dumps(dict(entries=entries), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Identity-checked Chinese token names:', len(entries))

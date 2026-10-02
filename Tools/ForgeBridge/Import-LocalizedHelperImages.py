"""Import original official Chinese helper faces; no runtime downloads or edits."""
import hashlib
import io
import json
from pathlib import Path
import requests
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
INDEX = ROOT / 'Assets/Resources/SpecialEffectArt.json'
JOBS = [
    ('Lost Mine of Phandelver', 'TAFR/21', 0, 'https://media.wizards.com/2021/afr/cs_4MqeT8nWd9.png', 'https://magic.wizards.com/zh-Hans/news/feature/adventures-forgotten-realms-mechanics-2021-07-02'),
    ('Tomb of Annihilation', 'TAFR/22', 0, 'https://media.wizards.com/2021/afr/cs_fZkwUeSE1i.png', 'https://magic.wizards.com/zh-Hans/news/feature/adventures-forgotten-realms-mechanics-2021-07-02'),
    ('Dungeon of the Mad Mage', 'TAFR/20', 0, 'https://media.wizards.com/2021/afr/cs_yu0OoWrZHr.png', 'https://magic.wizards.com/zh-Hans/news/feature/adventures-forgotten-realms-mechanics-2021-07-02'),
    ('Undercity', 'TCLB/20', 0, 'https://media.wizards.com/2022/clb/cs_6swTVEGnrF.png', 'https://magic.wizards.com/zh-Hans/news/feature/commander-legends-battle-for-baldurs-gate-mechanics'),
    ('The Ring', 'TLTR/H13', 0, 'https://media.wizards.com/2023/ltr/cs_fdaec7ef3c.png', 'https://magic.wizards.com/zh-Hans/news/feature/the-lord-of-the-rings-tales-of-middle-earth-mechanics'),
    ('The Ring Tempts You', 'TLTR/H13', 1, 'https://media.wizards.com/2023/ltr/cs_e308b5dc9a.png', 'https://magic.wizards.com/zh-Hans/news/feature/the-lord-of-the-rings-tales-of-middle-earth-mechanics'),
]

def main():
    data = json.loads(INDEX.read_text(encoding='utf8'))
    report = []
    for name, key, face, url, source in JOBS:
        set_code, collector = key.split('/')
        path = ROOT / 'CardImages/zh-CN' / set_code / (collector + ('_2' if face else '') + '.png')
        if path.exists():
            content = path.read_bytes()
        else:
            response = requests.get(url, timeout=30)
            response.raise_for_status()
            content = response.content
        with Image.open(io.BytesIO(content)) as image:
            assert image.format == 'PNG' and min(image.size) >= 256, name
            size = image.size
            image.verify()
        if not path.exists():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        entry = next(e for e in data['entries'] if e['name'] == name)
        localized = entry.setdefault('localized', [])
        localized[:] = [e for e in localized if e['language'] != 'zh-CN']
        localized.append(dict(language='zh-CN', key=key, face=face, source=source, imageSource=url))
        report.append(dict(name=name, path=str(path.relative_to(ROOT)), size=size, sha256=hashlib.sha256(content).hexdigest(), source=source, imageSource=url))
    INDEX.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    out = ROOT / 'Tools/Validation/Reports/SpecialEffectAssets/localized.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('PASS: 6 original official Chinese helper faces provisioned locally.')

if __name__ == '__main__':
    main()

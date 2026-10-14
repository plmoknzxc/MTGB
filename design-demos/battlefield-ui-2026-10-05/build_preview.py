from pathlib import Path
from PIL import Image
import base64, io, json

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
def art(path, size=(360, 520)):
    image = Image.open(ROOT / path).convert('RGB')
    image.thumbnail(size)
    data = io.BytesIO()
    image.save(data, format='JPEG', quality=88)
    return 'data:image/jpeg;base64,' + base64.b64encode(data.getvalue()).decode()
def card(key):
    for lang in ['zh-CN/', '']:
        for ext in ['.jpg', '.png']:
            p = ROOT / ('CardImages/' + lang + key + ext)
            if p.exists(): return art(p.relative_to(ROOT))
    raise FileNotFoundError(key)
assets = {
    'background': art('Assets/Art/Battlefield/ObsidianSanctum.png', (1920,1080)),
    'back': art('Assets/Resources/UI/CardBack.png'),
    'bear': card('10E/268'), 'mountain': card('10E/376'),
    'island': art('design-demos/mtgb-battleground/assets/card-island.jpg'),
    'charm': card('FDN/721'), 'bolt': card('SLZ/63'),
    'counterspell': card('PF26/5'), 'negate': card('TMT/47'),
    'warden': card('SPG/65'), 'wildguide': card('BLB/196'),
    'garruk': card('TELD/19'), 'ring': card('TLTR/H13'),
    'dungeon': card('TAFR/21'), 'blessing': card('TBLC/40'),
    'monarch': card('TAFC/13'), 'speed': card('TDFT/14'),
    'day': card('TMID/19'),
}
for name, key in json.loads((OUT / 'full-board-assets.json').read_text('utf-8')).items():
    assets[name] = card(key)
for c in 'WUBRGC':
    p = ROOT / f'Assets/Resources/UI/ManaSymbols/Mana_{c}.png'
    assets[c] = 'data:image/png;base64,' + base64.b64encode(p.read_bytes()).decode()
template = (OUT / 'template.html').read_text('utf-8')
(OUT / 'index.html').write_text(template.replace('__ASSETS__', json.dumps(assets, ensure_ascii=False)), 'utf-8')
print('Created index.html:', (OUT / 'index.html').stat().st_size, 'bytes')

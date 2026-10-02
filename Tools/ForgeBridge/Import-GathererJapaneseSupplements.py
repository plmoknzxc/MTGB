"""Review missing Japanese rules against official Gatherer printed-text records.

Offline only. Retains provenance and refuses language/printing/name mismatches.
Missing text is not inferred from English. English version differences are kept
for review instead of overwriting an existing translation.
"""
import concurrent.futures
import hashlib
import html
import json
import re
import sys
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / '.tools/gatherer-ja'
REPORTS = ROOT / 'Tools/ForgeBridge/Reports'
JA = re.compile(r'[\u3040-\u30ff\u3400-\u9fff]')

def extract_cards(html):
    chunks = []
    for match in re.finditer(r'self\.__next_f\.push\((.*?)\)</script>', html):
        try:
            data = json.loads(match[1])
            if len(data) > 1 and isinstance(data[1], str):
                chunks.append(data[1])
        except (ValueError, TypeError):
            continue
    flight = ''.join(chunks)
    cards = {}
    for match in re.finditer(r'"card":(?=\{)', flight):
        try:
            card, _ = json.JSONDecoder().raw_decode(flight[match.end():])
            if card.get('kind') == 'CardData':
                cards[card['id']] = card
        except (ValueError, KeyError):
            continue
    return list(cards.values())

def url_for(key, name):
    code, number = key.split('/', 1)
    ascii_name = unicodedata.normalize('NFKD', name).encode('ascii', 'ignore').decode().lower()
    slug = re.sub('[^a-z0-9]+', '-', ascii_name).strip('-')
    return f'https://gatherer.wizards.com/{code}/ja-jp/{urllib.parse.quote(number, safe="")}/{slug}'

def fetch(candidate):
    key, name = candidate['key'], candidate['name']
    url = url_for(key, name)
    path = WORK / (hashlib.sha256(url.encode()).hexdigest()[:24] + '.json')
    if path.exists():
        return json.loads(path.read_text(encoding='utf-8'))
    result = dict(key=key, name=name, url=url)
    try:
        request = urllib.request.Request(url, headers={'User-Agent': 'MTGB-Localization-Audit/1.0'})
        with urllib.request.urlopen(request, timeout=15) as response:
            raw = response.read()
            result.update(finalUrl=response.url, sourceSha256=hashlib.sha256(raw).hexdigest(),
                          cards=extract_cards(raw.decode('utf-8')), httpStatus=response.status)
    except urllib.error.HTTPError as error:
        result.update(httpStatus=error.code, error=str(error))
    except Exception as error:
        result.update(httpStatus=0, error=str(error))
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    time.sleep(.12)
    return result

def comparable(text, name):
    text = clean(text)
    text = re.sub(r'\([^()]*\)', '', text or '')
    text = text.replace(name, 'this permanent')
    text = re.sub(r'\bthis (?:creature|artifact|enchantment|land|planeswalker|Equipment|Aura|card)\b', 'this permanent', text, flags=re.I)
    text = re.sub(r'\benters the battlefield\b', 'enters', text, flags=re.I)
    text = re.sub(r'(?m)^\s*[*•・]\s*', '', text)
    text = re.sub(r'\s+-\s+', ' ', text)
    return re.sub(r'\s+|[—―～~.,:;"“”]', '', text).lower()

def clean(text):
    text = html.unescape(text or '')
    text = re.sub(r'</?(?:i|b|strong|em)>', '', text, flags=re.I).replace('\r', '').strip()
    def mana(match):
        body = match[1]
        tokens = re.findall(r'[WUBRGC]/[WUBRGC]|[WUBRGC]/P|2/[WUBRGC]|\d+|[WUBRGCSTXYZ]', body)
        return ''.join('{'+token+'}' for token in tokens) if ''.join(tokens) == body else match[0]
    return re.sub(r'\{([^{}]+)\}', mana, text)

def reviewed_english(text, name):
    # Exact, reviewed English-source differences; no Japanese rule is rewritten.
    if name == 'Edea, Possessed Sorceress':
        text = text.replace("it's owner's", "its owner's")
    if name == 'Fated Clash':
        text = text.replace('destoy all creatures', 'destroy all creatures')
    if name == 'Brilliant Wings' and 'Enchant creature you control' in text:
        text = text.replace('Enchanted permanent has', 'Enchanted creature has')
    return text

def separated_face(card, key, name):
    rules, oracle = clean(card.get('instanceText')), clean(card.get('oracleText'))
    title, typ = card.get('instanceName') or '', card.get('instanceTypeLine') or ''
    if '//ADV//' not in rules:
        return title, typ, rules, oracle
    # The official page stores both Adventure areas in instanceText, even on
    # its Adventure URL. Require the paired English face header and mana row.
    assert key == 'LCC/52' and name in ('Promise of Aclazotz', 'Foul Rebirth')
    front, back = rules.split('//ADV//', 1)
    if name == 'Promise of Aclazotz':
        return title, typ, front.strip(), oracle.split('//ADV//', 1)[0].strip()
    parent = fetch(dict(key=key, name='Promise of Aclazotz'))
    primary = next(c for c in parent['cards'] if c.get('oracleName') == 'Promise of Aclazotz')
    en_lines = [l.strip() for l in clean(primary['oracleText']).split('//ADV//', 1)[1].splitlines() if l.strip()]
    ja_lines = [l.strip() for l in back.splitlines() if l.strip()]
    assert en_lines[0] == name and en_lines[1] == ja_lines[1] == '{2}{B}'
    assert en_lines[2] == 'Sorcery - Adventure' and '出来事' in ja_lines[2]
    title = re.sub(r'（[ぁ-ゖァ-ヺー]+）', '', ja_lines[0])
    return title, ja_lines[2], '\n'.join(ja_lines[3:]), '\n'.join(en_lines[3:])

def main():
    WORK.mkdir(parents=True, exist_ok=True)
    reviewed = json.loads((REPORTS / 'LocalizedPresentationReviewed.json').read_text(encoding='utf-8'))
    contexts = json.loads((ROOT / '.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))
    english = {(x['gap']['key'], x['gap']['name']): x['englishRules'] for x in contexts}
    candidates = [x for x in reviewed['entries'] if x['language'] == 'ja-JP' and x['field'] == 'rules'
                  and x['status'] in ('translation-unavailable-in-checked-sources', 'official-Gatherer-field-imported')]
    entries, rows = [], {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for index, record in enumerate(pool.map(fetch, candidates), 1):
            key, name = record['key'], record['name']
            result = dict(key=key, name=name, source=record['url'], httpStatus=record['httpStatus'])
            code, number = key.split('/', 1)
            matching = [c for c in record.get('cards', []) if c.get('oracleName') == name
                        and c.get('setCode', '').upper() == code.upper()
                        and str(c.get('cardNumberVariant') or c.get('cardNumber')) == number
                        and c.get('languageCode') == 'ja']
            if not matching:
                result['status'] = 'no-exact-Japanese-record-at-checked-URL'
            else:
                card = matching[0]
                title, typ, rules, oracle = separated_face(card, key, name)
                result.update(printedName=title, printedRules=rules,
                              englishRules=oracle, sourceSha256=record['sourceSha256'])
                if not JA.search(rules):
                    result['status'] = 'official-Japanese-record-without-Japanese-rules'
                elif comparable(reviewed_english(english[(key, name)], name), name) != comparable(oracle, name):
                    result['status'] = 'Japanese-rules-found-English-version-needs-review'
                else:
                    result['status'] = 'published-Japanese-rules-imported'
                    rows[name] = [name, title if JA.search(title) else '', typ if JA.search(typ) else '', rules]
            entries.append(result)
            if index % 100 == 0 or index == len(candidates):
                print(f'{index}/{len(candidates)}; imported={len(rows)}', flush=True)
    dest = ROOT / 'Assets/StreamingAssets/CardTranslations/ja-JP.gatherer.txt'
    dest.write_text('\n'.join('|'.join(v.replace('|', '｜').replace('\r', '').replace('\n', '\\n') for v in row)
                              for _, row in sorted(rows.items())) + '\n', encoding='utf-8')
    report = dict(complete=True, reviewed=len(entries), imported=len(rows), entries=entries,
                  limitation='Checked exact representative printing URLs; URL absence does not prove that another printing or official release note has no Japanese text. Only identity-checked Japanese printed text with aligned English rules imported. Runtime remains local-only.')
    (REPORTS / 'GathererJapaneseSupplements.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Gatherer review complete', flush=True)

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main()

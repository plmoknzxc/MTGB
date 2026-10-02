"""Report candidate translation gaps from paired local Oracle text; never auto-edit aliases."""
import base64, collections, json, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
glossary = json.loads((ROOT/'Assets/Resources/KeywordGlossary.json').read_text(encoding='utf-8'))['entries']
translations = {}
for line in (ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.txt').read_text(encoding='utf-8').splitlines():
    parts = line.split('|', 3)
    if len(parts) == 4:
        translations[parts[0]] = parts[3].replace('\\n', '\n')
supplement=ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.supplement.txt'
if supplement.exists():
    for line in supplement.read_text(encoding='utf-8').splitlines():
        parts=line.split('|',3)
        if len(parts)==4 and not re.search(r'[\u3400-\u9fff]',translations.get(parts[0],'')):
            translations[parts[0]]=parts[3].replace('\\n','\n')
evidence = collections.defaultdict(lambda: collections.defaultdict(list))
patterns = [(entry,re.compile(re.escape(entry['en'])+r'(?=$|[^A-Za-z])',re.I)) for entry in glossary if entry['autoDisplay']]
headers = [(entry,re.compile('^'+re.escape(entry['en'])+r'(?=\s*(?:$|\(|[0-9{—～]))',re.I))
           for entry in sorted(glossary,key=lambda e:-len(e['en'])) if entry['autoDisplay']]
header_rows = []
for line in (ROOT/'.tools/keyword-glossary/catalog-rules.tsv').read_text(encoding='utf-8').splitlines():
    name, rules = (base64.b64decode(x).decode('utf-8') for x in line.split('\t'))
    if name not in translations:
        continue
    english, chinese = rules.splitlines(), translations[name].splitlines()
    if len(english) != len(chinese):
        continue  # Line positions are evidence only when both faces retain the same layout.
    for en, zh in zip(english, chinese):
        for entry,pattern in headers:
            if pattern.match(en):
                header_rows.append([entry['en'],name,en,translations[name]])
                break
        for entry,pattern in patterns:
            if not pattern.match(en):
                continue
            term = re.match(r'[\u3400-\u9fff！]+', zh)
            if not term:
                continue
            term = term[0].rstrip('！')
            if term not in [entry['zh'], *entry.get('aliases', [])]:
                evidence[entry['en']][term].append(dict(card=name, en=en, zh=zh))
report = {key: {term: dict(count=len(samples), samples=samples[:3])
                for term, samples in sorted(values.items(), key=lambda x:-len(x[1]))}
          for key, values in sorted(evidence.items())}
out=ROOT/'.tools/keyword-glossary/translation-candidates.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
header_path=ROOT/'.tools/keyword-glossary/catalog-translated-headers.tsv'
header_path.write_text('\n'.join('\t'.join(base64.b64encode(s.encode('utf-8')).decode() for s in row)
                                 for row in header_rows),encoding='utf-8')
print(f'Translated headers: {len(header_rows)}; distinct mechanics: {len({r[0] for r in header_rows})}')
for key, terms in report.items():
    print(key+': '+', '.join(term+' ('+str(value['count'])+')' for term,value in terms.items()))

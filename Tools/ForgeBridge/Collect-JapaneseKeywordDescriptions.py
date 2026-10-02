"""Refresh local Japanese CR definitions; normal glossary builds stay offline."""
import json
import re
from pathlib import Path
import requests
from bs4 import BeautifulSoup

HERE = Path(__file__).resolve().parent
URL = 'https://mtg-jp.com/gameplay/rules/docs/0006836/'

def collect(text):
    headings = list(re.finditer(r'(?m)^(70[12]\.\d+)\.\s*([^\n]+／[^\n]+)', text))
    entries = {}
    for i, heading in enumerate(headings):
        ja, en = heading[2].rsplit('／', 1)
        block = text[heading.end():headings[i+1].start() if i+1<len(headings) else len(text)]
        paragraphs = re.findall(r'(?m)^'+re.escape(heading[1])+r'[a-z]+\s+([^\n]+)', block)
        if not paragraphs:
            continue
        help_text = paragraphs[0]
        # Some first paragraphs merely identify an ability's category.
        if len(help_text)<80 and len(paragraphs)>1:
            help_text += '\n'+paragraphs[1]
        entries[en.casefold().rstrip('!')] = dict(ja=ja, jaHelp=help_text, rule=heading[1])
    assert entries['equip']['ja']=='装備'
    assert 'コスト' in entries['affinity']['jaHelp']
    return dict(source=URL, version='20260807.0', entries=entries)

if __name__=='__main__':
    response=requests.get(URL, timeout=30)
    response.raise_for_status()
    response.encoding='utf8'
    data=collect(BeautifulSoup(response.text, 'html.parser').get_text('', strip=False))
    (HERE/'JapaneseKeywordRules.json').write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('Collected',len(data['entries']),'Japanese CR definitions')

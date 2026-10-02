"""Build/check display-only keyword help. Does not change card scripts or game rules.

Inputs: pinned Forge Keyword.java, reviewed Chinese help, CR heading baseline,
and reviewed translated aliases (never fixed-number reminder templates).
"""
import argparse
import collections
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
DEST = ROOT / 'Assets/Resources/KeywordGlossary.json'
FORGE = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/keyword/Keyword.java'
BASELINE = HERE / 'KeywordRulesBaseline.json'
INTERNAL = {'UNDEFINED', 'MAYFLASHCOST', 'MAYFLASHSAC'}
QUIET = {'activate','attach','cast','counter','create','destroy','discard','double','triple','exchange','exile','play','reveal','sacrifice','search','shuffle','tap and untap','enchant'}

def rows(path):
    for line in path.read_text(encoding='utf8').splitlines():
        if line and not line.startswith('#'):
            yield line.split('|')

def build():
    entries = {r['en'].casefold(): r for r in json.loads((HERE / 'KeywordCore.json').read_text(encoding='utf8'))}
    forge = []
    for line in FORGE.read_text(encoding='utf8').splitlines():
        match = re.match(r'\s*(\w+)\(("(?:[^"\\]|\\.)*"),.*?, (?:true|false), ("(?:[^"\\]|\\.)*")\)', line)
        if not match or match[1] in INTERNAL:
            continue
        name, reminder = json.loads(match[2]), json.loads(match[3])
        forge.append(name)
        # Forge's printf/token-count templates are not suitable as literal UI text.
        reminder = re.sub(r'\{%(?:\d+\$)?[ds]:([^}]+)\}', r'N \1(s)', reminder)
        reminder = re.sub(r'%(?:\d+\$)?d', 'N', reminder)
        reminder = re.sub(r'%(?:\d+\$)?s', 'the stated cost or quality', reminder).replace('CARDNAME', 'this card')
        entries.setdefault(name.casefold(), dict(en=name, enHelp=reminder, zh='',zhHelp='',ja='',jaHelp='',aliases=[]))
    for row in rows(HERE / 'KeywordDescriptions.zh-CN.tsv'):
        name, aliases, help_text = row
        entry = entries[name.casefold()]
        entry.update(zh=aliases.split(',')[0], zhHelp=help_text)
        entry.setdefault('aliases', []).extend(aliases.split(','))
    for row in rows(HERE / 'KeywordActions.tsv'):
        name, aliases, help_text, english = row
        entries[name.casefold()] = dict(en=name, zh=aliases.split(',')[0], ja='', enHelp=english,
            zhHelp=help_text, jaHelp='', aliases=aliases.split(','))

    japanese_rules=json.loads((HERE / 'JapaneseKeywordRules.json').read_text(encoding='utf8'))['entries']
    for key, entry in entries.items():
        definition=japanese_rules.get(key.rstrip('!'))
        if definition:
            if not entry['ja']: entry['ja']=definition['ja']
            if not entry['jaHelp']: entry['jaHelp']=definition['jaHelp']
            entry.setdefault('aliases', []).append(definition['ja'])

    for name, japanese, help_text in rows(HERE / 'KeywordDescriptions.ja-JP.tsv'):
        entry = entries[name.casefold()]
        entry.update(ja=japanese, jaHelp=help_text)
        entry.setdefault('aliases', []).append(japanese)

    for name, aliases in json.loads((HERE / 'KeywordAliases.json').read_text(encoding='utf8')).items():
        entry=entries[name.casefold()]
        entry['aliases'].extend(aliases)
        if not entry['ja']:
            entry['ja']=next((term for term in aliases if re.search(r'[ぁ-ヿ]',term)), '')

    def extra(key, values): entries[key]['aliases'].extend(values)
    extra('typecycling', ['Basic landcycling','Plainscycling','Islandcycling','Swampcycling','Mountaincycling','Forestcycling','Landcycling','Slivercycling','Wizardcycling','Halflingcycling','类型循环','地循环','基本地循环','平原循环','海岛循环','沼泽循环','山脉循环','树林循环','裂片妖循环','法术师循环','半身人循环'])
    extra('landwalk',['Plainswalk','Islandwalk','Swampwalk','Mountainwalk','Forestwalk','Desertwalk','Nonbasic landwalk','Snow landwalk','Snow-covered landwalk','平原行者','海岛行者','沼泽行者','山脉行者','树林行者','沙漠行者','非基本地行者','雪境地行者'])
    extra('umbra armor',['Totem armor','图腾甲'])
    extra('amass',['amasses','amassed','動員'])
    extra('connive',['connives','connived','謀議'])
    extra('explore',['explores','explored','探検'])
    extra('surveil',['surveils','surveilled'])
    extra('scry',['scries','scried'])
    extra('ninjutsu',['Commander ninjutsu','指挥官忍术','上忍術'])
    extra('for mirrodin',['For Mirrodin!'])
    extra('start your engines',['Start your engines!'])
    extra('manifest dread',['戦慄予示'])
    extra('collect evidence',['証拠収集'])
    extra('infinity',['∞'])
    extra('prepared',['备法','準備済'])
    extra('adventure',['冒険'])
    extra('omen',['前兆'])
    extra('forage',['觅粮'])
    reviewed = {
        'adapt':['順応'], 'discover':['発見'], 'monstrosity':['怪物化'],
        'investigate':['調査'], 'proliferate':['増殖'], 'fateseal':['消術','论命'],
        'clash':['激突'], 'fight':['格闘'], 'level up':['Ｌｖアップ','Lvアップ'],
        'behold':['请援','後見を受け'], 'blight':['枯朽'],
        'double team':['二体掛り'], 'specialize':['専門化'],
        'starting intensity':['強度'], 'power-up':['パワーアップ'],
        'teamwork':['チームワーク'], 'firebending':['火の技'],
        'time travel':['タイムトラベル'], 'lifelink':['魂絆'], 'ward':['語法'],
        'double agenda':['両策'], 'cloak':['匿伏'], 'absorb':['抵受'],
        'planeswalk':['时空换入','时空换出','时空换离','时空换境'], 'abandon':['撤废'],
        'open an attraction':['打开一个景点','打开景点'], 'harness':['掌控'],
        'recruit':['募兵'], 'poisonous':['剧毒'], 'suspect':['怀疑','容疑をかけ'],
        'indestructible':['不会毁坏'],
        'roll to visit your attractions':['掷骰造访景点'],
        'set in motion':['发动此邪计','发动邪计'],
        'face a villainous choice':['面临邪恶抉择'], 'station':['驻舱'],
        'explore':['勘查']
    }
    for key, aliases in reviewed.items():
        extra(key, aliases)
    extra('landwalk',['砂漠渡り','選ばれたタイプの渡り'])
    extra('sneak',['隠密'])
    entries['starting intensity']['enHelp']='Track the printed starting intensity. Other abilities explain how it changes and what it affects.'
    for name,help_text in json.loads((HERE / 'KeywordEnglishOverrides.json').read_text(encoding='utf8')).items():
        entries[name.casefold()]['enHelp']=help_text
    # Reviewed against actual paired English/Chinese card text. Keep earlier names
    # as aliases, so existing user data remains readable after display-name corrections.
    for name,zh in json.loads((HERE / 'KeywordLocalNames.json').read_text(encoding='utf8')).items():
        entry=entries[name.casefold()]
        old=entry['zh']
        if old!=zh:
            entry['aliases'].append(old)
            if old and not re.search(r'[A-Za-z]',old):
                entry['zhHelp']=entry['zhHelp'].replace(old,zh.rstrip('！'))
        entry['zh']=zh
        entry['aliases'].append(zh.rstrip('！'))
    for e in entries.values():
        # Rules use inflected verbs such as 督励しても / 督励された.
        if e['ja'].endswith('する') and len(e['ja'])>2:
            e['aliases'].append(e['ja'][:-2])
        e['enHelp']=e['enHelp'].replace('the stated cost or quality','the printed cost').replace('an additional the printed cost','the additional printed cost')
        e['aliases']=sorted(set(e.get('aliases',[])),key=lambda s:(-len(s),s))
        e['autoDisplay']=e['en'].casefold() not in QUIET
        if not e['zhHelp'] or not e['enHelp']: raise ValueError('Missing help: '+e['en'])
        if not e['jaHelp']: raise ValueError('Missing Japanese help: '+e['en'])
        if '%' in e['enHelp'] or '{%' in e['enHelp']: raise ValueError('Unexpanded template: '+e['en'])
    baseline=json.loads(BASELINE.read_text(encoding='utf8'))
    grouped={'daybound and nightbound':['daybound','nightbound'],'∞ (infinity)':['infinity'], 'for mirrodin!':['for mirrodin'],'start your engines!':['start your engines']}
    missing=[]
    for rule in baseline['headings']:
        for key in grouped.get(rule['name'].casefold(),[rule['name'].casefold()]):
            if key not in entries: missing.append(rule['name'])
    if missing: raise ValueError('Uncovered CR headings: '+', '.join(missing))
    output=dict(source=baseline['source'], rulesDate=baseline['date'], entries=sorted(entries.values(),key=lambda e:e['en'].casefold()))
    return output, len(forge), len(baseline['headings'])

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    output, forge, rules=build()
    encoded=json.dumps(output,ensure_ascii=False,indent=2)+'\n'
    if args.check:
        assert DEST.read_text(encoding='utf8')==encoded,'Keyword glossary is stale; rebuild it.'
    else: DEST.write_text(encoded,encoding='utf8')
    print(f'PASS: {len(output["entries"])} entries; {forge} Forge keywords; {rules} CR headings; EN/ZH/JA help complete. '+('Up to date.' if args.check else 'Built.'))

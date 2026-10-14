"""Contextual declaration classification. Never grants rule or UI PASS.

Keyword resolution mirrors the pinned Keyword.getKeywordDetails parser. Exact
parameters and unresolved list components remain in the occurrence table.
"""
import json
import re


def keyword_catalog(source):
    return {json.loads(m[2]).casefold(): json.loads(m[2]) for m in re.finditer(
        r'^\s*(\w+)\(("(?:[^"\\]|\\.)*"),', source, re.M)
        if m[1] != 'UNDEFINED'}


def keyword_name(value, catalog):
    # Keyword.java: colon first; otherwise exact multi-word name, then first word.
    if ':' in value:
        return catalog.get(value.split(':', 1)[0].casefold())
    exact = catalog.get(value.casefold())
    return exact or catalog.get(value.split(' ', 1)[0].casefold())


def declarations(text):
    face = 0
    for number, line in enumerate(text.splitlines(), 1):
        if line == 'ALTERNATE':
            face += 1
        if line.startswith(('K:', 'AlternateMode:')):
            field, value = line.split(':', 1)
            value = value.strip()
            yield dict(line=number, face=face, field=field, value=value,
                       legacyValue=value.split(':', 1)[0])
        elif line.startswith(('A:', 'T:', 'R:', 'S:', 'SVar:')):
            for segment in line.split('|'):
                m = re.match(r'^(?:[\w-]+:)*\s*(\w+)\$\s*(.*)$', segment.strip())
                if m and (m[1] in ('AddKeyword', 'Condition', 'Attributes')
                          or m[1].endswith('Condition')):
                    yield dict(line=number, face=face, field=m[1], value=m[2].strip(),
                               legacyValue=m[2].strip())


def resolve(item, catalog, entry_names, normalize, split_modes):
    """Return a source classification and known references, never an effect verdict."""
    field, value = item['field'], item['value']
    names = []
    parts = value.split(' & ') if field == 'AddKeyword' else [value]
    if field in ('K', 'AddKeyword'):
        for part in parts:
            name = keyword_name(part, catalog)
            names.append(name if name and normalize(name) in entry_names else None)
        if all(names):
            return 'keyword-variant', names, []
        return 'unclassified', [n for n in names if n], [p for p, n in zip(parts, names) if not n]
    if field == 'AlternateMode' and value in split_modes:
        return 'card-face-metadata', [], []
    if field not in ('Condition', 'Attributes', 'AlternateMode'):
        # LifeCondition, PresentCondition, etc. already have Parameter:<field>
        # inventory entries. They are not an independent Condition keyword.
        return 'exact-field-parameter', [], []
    if normalize(value) in entry_names:
        return 'existing-entry-reference', [entry_names[normalize(value)]], []
    return 'unclassified', [], [value]

"""Conservative face-name matching shared by the offline art importers."""
import json
from pathlib import Path


def normalized(name):
    return (name or '').replace('’', "'").replace('꞉', ':').replace('—', '-').replace('–', '-').casefold().strip()


def face_names(name):
    return {normalized(name)} | {normalized(face) for face in (name or '').split(' // ')}


def matches(local, source_names, oracle_id=None):
    names = face_names(local['name'])
    for face in local.get('faces', []):
        names.update(face_names(face['name']))
    sources = set()
    for name in source_names:
        sources.update(face_names(name))
    names.discard('')
    sources.discard('')
    if names.intersection(sources):
        return True
    # These renamed printings were checked against English rules and card stats.
    # Require the source's Oracle identity too; a collector number alone is unsafe.
    for alias in ALIASES:
        if (normalized(alias['localName']) in names
                and normalized(alias['sourceName']) in sources
                and alias['oracleID'] == oracle_id):
            return True
    return False


ALIASES = json.loads(Path(__file__).with_name('LocalizedArtAliases.json').read_text(encoding='utf-8'))

"""Offline regression for emblem links and the emblems-only image filter."""
import gzip
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

TOOLS = Path(__file__).resolve().parents[1]


def module(name):
    spec = importlib.util.spec_from_file_location(name, TOOLS / (name + '.py'))
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


class EmblemIndexRegression(unittest.TestCase):
    def test_link_and_download_filters_include_subtype_without_other_combo_pieces(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            streaming = root / 'Assets/StreamingAssets'
            streaming.mkdir(parents=True)
            (root / 'Assets/Resources').mkdir()
            cache = root / '.tools/card-images'
            cache.mkdir(parents=True)
            (streaming / 'CardArtSources.jsonl').write_text(
                json.dumps({'key': 'SRC/1', 'name': 'Source'}), encoding='utf8')
            tokens = [
                {'id': 'plain', 'set': 'test', 'collector_number': '1', 'name': 'Plain', 'type_line': 'Emblem'},
                {'id': 'subtype', 'set': 'test', 'collector_number': '2', 'name': 'Subtyped', 'type_line': 'Emblem — Gideon'},
                {'id': 'creature', 'set': 'test', 'collector_number': '3', 'name': 'Ordinary', 'type_line': 'Token Creature — Emblem'},
                {'id': 'combo', 'set': 'test', 'collector_number': '4', 'name': 'Other', 'type_line': 'Legendary Planeswalker — Gideon'},
            ]
            for token in tokens:
                token['oracle_text'] = token['name'] + ' rule.'
                token['image_uris'] = {'normal': 'https://cards.scryfall.io/' + token['id'] + '.jpg'}
            source = {'id': 'source', 'set': 'src', 'collector_number': '1', 'name': 'Source',
                      'all_parts': [dict(id=t['id'], name=t['name'], type_line=t['type_line'],
                                         component='token' if t['id'] == 'creature' else 'combo_piece')
                                    for t in tokens]}
            with gzip.open(cache / 'default-cards.jsonl.gz', 'wt', encoding='utf8') as file:
                for row in [source] + tokens:
                    file.write(json.dumps(row) + '\n')
            builder = module('Build-RelatedTokenIndex')
            builder.ROOT = root
            builder.build()
            index = json.loads((root / 'Assets/Resources/RelatedTokenArt.json').read_text())
            self.assertEqual([t['key'] for t in index['entries'][0]['tokens']], ['TEST/1', 'TEST/2', 'TEST/3'])
            importer = module('Import-RelatedTokenImages')
            importer.ROOT, importer.CACHE = root, cache
            jobs, missing = importer.plan(emblems_only=True)
            self.assertEqual([j['key'] for j in jobs], ['TEST/1', 'TEST/2'])
            self.assertEqual(missing, [])
            jobs, missing = importer.plan()
            self.assertEqual([j['key'] for j in jobs], ['TEST/1', 'TEST/2', 'TEST/3'])
            self.assertEqual(missing, [])


if __name__ == '__main__':
    unittest.main()

"""Reject missing/ambiguous upstream patch context before compilation."""
import importlib.util
from pathlib import Path
import unittest
import hashlib
import tempfile

R=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('native_patch',R/'Tools/ForgeBridge/Apply-NativePatches.py')
patch=importlib.util.module_from_spec(spec);spec.loader.exec_module(patch)

class NativePatchTests(unittest.TestCase):
    def test_pinned_java_checkout_accepts_lf_and_crlf(self):
        original=b'class Example {\r\n}\r\n'
        fix={'id':'example','sourceSha256':hashlib.sha256(original).hexdigest()}
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'Example.java'
            for content in (original,original.replace(b'\r\n',b'\n')):
                path.write_bytes(content)
                self.assertEqual(patch.reviewed_java_source(path,fix),'class Example {\n}\n')

    def test_pinned_java_checkout_rejects_content_changes(self):
        original=b'class Example {\r\n}\r\n'
        fix={'id':'example','sourceSha256':hashlib.sha256(original).hexdigest()}
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'Example.java'
            path.write_bytes(b'class Changed {\n}\n')
            with self.assertRaises(ValueError):patch.reviewed_java_source(path,fix)

    def test_missing_context_fails_instead_of_skipping_fix(self):
        fix={'id':'cancel','changes':[{'before':'sort(cards);','after':'check(cards); sort(cards);'}]}
        with self.assertRaises(ValueError):patch.patched_source('sort(other);',fix)

    def test_ambiguous_context_fails_instead_of_patching_other_path(self):
        fix={'id':'cancel','changes':[{'before':'sort(cards);','after':'check(cards); sort(cards);'}]}
        with self.assertRaises(ValueError):patch.patched_source('sort(cards); sort(cards);',fix)

if __name__=='__main__':unittest.main()

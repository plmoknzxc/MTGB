"""Catch upstream callback drift instead of quietly ignoring a new decision."""
import importlib.util
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('contract',ROOT/'Tools/ForgeBridge/Audit-InteractionContract.py')
contract=importlib.util.module_from_spec(spec);spec.loader.exec_module(contract)


class ContractParsingTests(unittest.TestCase):
    def test_forwarding_call_is_not_a_declaration(self):
        rows=contract.declarations('''public interface Boundary {
 default <T> List<T> choose(String message,List<T> choices) {
   return choose(message,choices,null);
 }
 <T> List<T> choose(String message,List<T> choices,Function<T,String> labels);
}''')
        self.assertEqual([r['name'] for r in rows],['choose','choose'])
        self.assertEqual([r['default'] for r in rows],[True,False])

    def test_new_void_default_is_visible(self):
        rows=contract.declarations('''public interface Boundary {
 default void requestPlayerDecision(List<Card> choices) {}
}''')
        self.assertEqual(rows[0]['name'],'requestPlayerDecision')
        self.assertEqual(rows[0]['returnType'],'void')

    def test_overload_change_changes_contract(self):
        old=contract.declarations('public interface Boundary {\n Integer count(String title,int min,int max);\n}')
        new=contract.declarations('public interface Boundary {\n Integer count(String title,int min,int max,boolean optional);\n}')
        self.assertNotEqual(old[0]['signature'],new[0]['signature'])

    def test_multiline_calls_keep_source_lines(self):
        source='// getGui().fake();\ngetGui()\n .choose(\n items);\n/* getGui().alsoFake(); */\ngetGui().confirm();'
        self.assertEqual(contract.direct_calls(source),[{'method':'choose','line':2},{'method':'confirm','line':6}])

    def test_default_body_change_requires_hash_review(self):
        old='public interface Boundary {\n default boolean yes() {return true;}\n}'
        new=old.replace('true','false')
        self.assertEqual(contract.declarations(old),contract.declarations(new))
        self.assertNotEqual(contract.hashlib.sha256(old.encode()).hexdigest(),contract.hashlib.sha256(new.encode()).hexdigest())

    def test_throws_declaration_is_not_lost(self):
        rows=contract.declarations('public interface Boundary {\n void browse(String url) throws IOException, URISyntaxException;\n}')
        self.assertEqual(len(rows),1)
        self.assertIn('throws IOException, URISyntaxException',rows[0]['signature'])

    def test_string_braces_comments_do_not_hide_next_declaration(self):
        rows=contract.declarations('public interface Boundary {\n default String url() {return "https://example/{";}\n void choose();\n}')
        self.assertEqual([r['name'] for r in rows],['url','choose'])

    def test_static_helper_bypass_is_visible(self):
        source='String s="SGuiChoose.fake()";\nSGuiChoose\n .oneOrNone(items);\nGuiBase.getInterface()\n .getChoices(items);\n// SOptionPane.fake()\n'
        self.assertEqual(contract.base_calls(source),[
            {'helper':'SGuiChoose','method':'oneOrNone','line':2},
            {'helper':'GuiBase.getInterface()','method':'getChoices','line':4}])


if __name__=='__main__':unittest.main()

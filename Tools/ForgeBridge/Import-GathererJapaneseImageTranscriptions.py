"""Import five manually reviewed Japanese printed rules from official images.

No machine translation: each clause was read from the recorded image and
checked against the canonical English effect. Source hashes bind that review.
"""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
sources=json.loads((ROOT/'.tools/gatherer-ja/printed-rule-images/sources.json').read_text(encoding='utf-8'))
transcriptions={
 'Owlbear Cub':('アウルベアの仔','クリーチャー — 鳥・熊','母親来たる — アウルベアの仔が８つ以上の土地をコントロールしているプレイヤー１人を攻撃するたび、あなたのライブラリーの一番上にあるカード８枚を見る。あなたはその中からクリーチャー・カード１枚をタップ状態かつそのプレイヤーを攻撃している状態で戦場に出してもよい。残りをあなたのライブラリーの一番下に無作為の順番で置く。'),
 'Planetary Annihilation':('惑星殲滅','ソーサリー','各プレイヤーはそれぞれ、自分がコントロールしている土地６つを選び、その後、残りを生け贄に捧げる。惑星殲滅は各クリーチャーにそれぞれ６点のダメージを与える。'),
 'Scouring Swarm':('洗い流す大群','クリーチャー — 昆虫','飛行\nあなたが土地１つを生け贄に捧げるたび、あなたの墓地に７枚以上の土地・カードがあるなら、このクリーチャーのコピーであるトークン１体をタップ状態で生成する。そうでないなら、飛行を持つ黒の１/１の昆虫・クリーチャー・トークン１体をタップ状態で生成する。'),
 'Moxite Refinery':('モーキサイト精製所','アーティファクト','{2}, {T}, あなたがコントロールしていてアーティファクトやクリーチャーである１つの上からカウンターＸ個を取り除く：以下から１つを選ぶ。起動はソーサリーとしてのみ行う。\n・アーティファクト１つを対象とする。その上に蓄積カウンターＸ個を置く。\n・クリーチャー１体を対象とする。その上に＋１/＋１カウンターＸ個を置く。'),
 'Eumidian Hatchery':('ユーミディアンの孵化場','土地','{T}, １点のライフを支払う：{B}を加える。この土地の上に孵化カウンター１個を置く。\nこの土地が戦場から墓地に置かれたとき、この上にあった孵化カウンター１個につき１体の、飛行を持つ黒の１/１の昆虫・クリーチャー・トークンを生成する。')
}
for item in sources:
    assert hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest()==item['sha256']
    item['printedName'],item['printedType'],item['printedRules']=transcriptions[item['name']]
    item['review']='Read official Japanese image; independently compared costs, conditions, quantities, targets, timing, token characteristics and destinations with recorded canonical English rules. Mana icons represented by canonical brace notation; physical line wrapping removed.'
    # Never overwrite an existing localized image.
    dest=ROOT/'CardImages/ja-JP'/(item['key']+'.png')
    if not dest.exists():
        dest.parent.mkdir(parents=True,exist_ok=True)
        dest.write_bytes((ROOT/item['path']).read_bytes())
    item['localImagePath']=str(dest.relative_to(ROOT))
(ROOT/'Tools/ForgeBridge/Reports/GathererJapaneseImageTranscriptions.json').write_text(json.dumps(dict(complete=True,entries=sources),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Five official printed-image transcriptions validated and recorded.')

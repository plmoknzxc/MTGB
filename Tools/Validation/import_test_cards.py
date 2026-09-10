"""向 CardDatabases/core.mtgbdb 导入一组覆盖全部卡牌类型的测试卡（TST 系列）。

用法：python Tools/Validation/import_test_cards.py
- 自动备份原库为 core.mtgbdb.bak（覆盖旧备份）
- 幂等：先删除同 set_code 的旧数据再插入
"""
import os
import shutil
import sqlite3
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DB = os.path.join(ROOT, "CardDatabases", "core.mtgbdb")
SET_CODE = "TST"

# type_flags: Artifact=1 Battle=2 Creature=4 Enchantment=8 Instant=16 Kindred=32 Land=64 Planeswalker=128 Sorcery=256

CARDS = [
    {
        "key": "TST/1", "id": 2101, "name": "罗堰妖精", "types": 4, "cost": "{G}",
        "rules": "生物 — 妖精 德鲁伊\n{T}：加{G}。",
        "power": 1, "toughness": 1, "subtypes": "妖精 德鲁伊", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "CardScripts/TST/m1.lua",
    },
    {
        "key": "TST/2", "id": 2102, "name": "巨爬兽", "types": 4, "cost": "{3}{G}{G}",
        "rules": "生物 — 亚龙\n巨爬兽没有异能，但它很大。",
        "power": 6, "toughness": 4, "subtypes": "亚龙", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "",
    },
    {
        "key": "TST/3", "id": 2103, "name": "变巨", "types": 16, "cost": "{G}",
        "rules": "瞬间\n目标生物获得+3/+3直到回合结束。",
        "power": 0, "toughness": 0, "subtypes": "", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "CardScripts/TST/m3.lua",
    },
    {
        "key": "TST/4", "id": 2104, "name": "预视", "types": 256, "cost": "{2}{U}",
        "rules": "法术\n抓两张牌。",
        "power": 0, "toughness": 0, "subtypes": "", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "CardScripts/TST/m4.lua",
    },
    {
        "key": "TST/5", "id": 2105, "name": "太阳戒", "types": 1, "cost": "{1}",
        "rules": "神器\n{T}：加{C}{C}。",
        "power": 0, "toughness": 0, "subtypes": "装置", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "CardScripts/TST/m5.lua",
    },
    {
        "key": "TST/6", "id": 2106, "name": "地狱梦魇", "types": 8, "cost": "{1}{B}{B}",
        "rules": "结界\n每当一位对手抓一张牌时，该玩家失去1点生命。",
        "power": 0, "toughness": 0, "subtypes": "", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "CardScripts/TST/m6.lua",
    },
    {
        "key": "TST/7", "id": 2107, "name": "杰斯，机巧心智法师", "types": 128, "cost": "{1}{U}{U}",
        "rules": "鹏洛客 — 杰斯\n+1：抓一张牌。\n-3：对对手造成3点伤害。\n初始忠诚 4。",
        "power": 0, "toughness": 0, "subtypes": "杰斯", "supertypes": 0,
        "loyalty": 4, "defense": 0, "script": "CardScripts/TST/m7.lua",
    },
    {
        "key": "TST/8", "id": 2108, "name": "卡德海姆之战", "types": 2, "cost": "{2}{U}{U}",
        "rules": "战役 — 攻城\n（此战役进入对手的战场。对手可以攻击它；当它失去最后一个防御指示物时，\n将它放逐并可以施放背面。）\n背面：世界树薪火\n地\n{T}：加{U}。",
        "power": 0, "toughness": 0, "subtypes": "攻城", "supertypes": 0,
        "loyalty": 0, "defense": 5, "script": "CardScripts/TST/m8.lua",
        "back_name": "世界树薪火", "back_types": 64, "back_cost": "",
        "back_rules": "地\n{T}：加{U}。", "back_subtypes": "",
    },
    {
        "key": "TST/9", "id": 2109, "name": "换体变形", "types": 48, "cost": "{2}{W}",
        "rules": "瞬间 — 变形师\n放逐目标生物。",
        "power": 0, "toughness": 0, "subtypes": "变形师", "supertypes": 0,
        "loyalty": 0, "defense": 0, "script": "CardScripts/TST/m9.lua",
    },
]


def main():
    if not os.path.exists(DB):
        print(f"ERROR: database not found: {DB}", file=sys.stderr)
        return 1

    backup = DB + ".bak"
    shutil.copyfile(DB, backup)
    print(f"backup -> {backup}")

    conn = sqlite3.connect(DB)
    cur = conn.cursor()
    cur.execute("PRAGMA foreign_keys = ON")

    keys = [card["key"] for card in CARDS]
    placeholders = ",".join("?" for _ in keys)
    cur.execute(
        "DELETE FROM cards WHERE card_key IN (%s) OR set_code = ?" % placeholders,
        [*keys, SET_CODE],
    )

    for card in CARDS:
        cur.execute(
            """INSERT INTO cards (
                card_key, card_id, oracle_id, set_code, collector_number, name,
                type_flags, mana_cost, rules_text, power, toughness,
                script_path, enabled, supertype_flags, subtypes
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?)""",
            (
                card["key"], card["id"], f"test-{card['id']}", SET_CODE,
                card["key"].split("/")[1], card["name"], card["types"],
                card["cost"], card["rules"], card["power"], card["toughness"],
                card["script"], card["supertypes"], card["subtypes"],
            ),
        )
        # 正面主特征（施放流程所需）
        cur.execute(
            """INSERT INTO card_features (
                card_key, face_index, feature_index, feature_role, role_value,
                name, type_flags, mana_cost, rules_text, power, toughness,
                loyalty, defense, supertype_flags, subtypes,
                power_expression, toughness_expression
            ) VALUES (?, 0, 0, 0, '', ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, '', '')""",
            (
                card["key"], card["name"], card["types"], card["cost"],
                card["rules"], card["power"], card["toughness"],
                card["loyalty"], card["defense"], card["supertypes"],
                card["subtypes"],
            ),
        )
        cur.execute(
            "INSERT OR REPLACE INTO card_characteristics (card_key, loyalty, defense) VALUES (?, ?, ?)",
            (card["key"], card["loyalty"], card["defense"]),
        )
        if "back_name" in card:
            cur.execute(
                """INSERT INTO card_features (
                    card_key, face_index, feature_index, feature_role, role_value,
                    name, type_flags, mana_cost, rules_text, power, toughness,
                    loyalty, defense, supertype_flags, subtypes,
                    power_expression, toughness_expression
                ) VALUES (?, 1, 0, 0, '', ?, ?, ?, ?, 0, 0, 0, 0, ?, ?, '', '')""",
                (
                    card["key"], card["back_name"], card["back_types"],
                    card["back_cost"], card["back_rules"],
                    card["back_supertypes"] if "back_supertypes" in card else 0,
                    card["back_subtypes"],
                ),
            )

    conn.commit()

    count = cur.execute(
        "SELECT COUNT(*) FROM cards WHERE set_code = ?", (SET_CODE,)
    ).fetchone()[0]
    total = cur.execute("SELECT COUNT(*) FROM cards").fetchone()[0]
    conn.close()
    print(f"TST cards in database: {count}; total cards now: {total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

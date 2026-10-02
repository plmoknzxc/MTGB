"""Separate the opposite-face reminder footer from modal DFC rules text."""
import re


def face_rules(card, face):
    text = face.get('printed_text', '')
    if card.get('layout') != 'modal_dfc':
        return text
    for other in card.get('card_faces', []):
        if other is face:
            continue
        marker = '\n' + other.get('printed_type_line', '') + '\n'
        if marker == '\n\n' or marker not in text:
            continue
        body, footer = text.rsplit(marker, 1)
        # A footer contains the opposite mana cost, or its short tap-for-mana
        # summary. Do not strip type names occurring in ordinary ability prose.
        cost = other.get('mana_cost', '')
        mana = other.get('type_line') == 'Land' and '{T}: Add ' in other.get('oracle_text', '')
        if body and ((cost and footer.strip() == cost) or
                     (mana and re.fullmatch(r'\{T\}[：:]\s*\{[^}]+\}(?:を加える。|を加える)', footer.strip()))):
            text = body
    return text

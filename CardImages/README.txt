Put complete card images in a set-code folder:

CardImages/<SET CODE>/<COLLECTOR NUMBER>.png

Examples:
CardImages/WOE/45.png
CardImages/MKM/223.jpg

Supported extensions: .png, .jpg, .jpeg

Set the same set_code and collector_number in the card database. If no matching
image exists, the game uses its generated card template as a fallback.

Lua scripts use the same set-code and collector-number convention under
CardScripts/<SET CODE>/m<COLLECTOR NUMBER>.lua.

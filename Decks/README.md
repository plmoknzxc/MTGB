Place player deck files here.

Deck files use the `.mdk` extension. They are plain text files with one card
entry per line, similar to YGO deck lists, but MTGB does not use an Extra Deck
section.
MTGA export text is not a deck file format here; import/export support should
be handled by dedicated UI commands.

Example:

```text
#name Starter
#commander
1 SOS/65
#main
40 MTGB/1
4 SOS/65
!side
2 MTGB/2
```

Card lines can use an optional quantity prefix such as `4 SOS/65`, then
`SET/COLLECTOR_NUMBER` such as `SOS/65`, or a numeric internal card ID for
compatibility. Old repeated one-card-per-line decks are still accepted. Blank
lines and `//` comments are ignored.

Supported sections are `#commander`, `#main`, and `!side`/`#side`.

The runtime enforces a four-copy limit across commander, main deck, and
sideboard, except for Basic lands.

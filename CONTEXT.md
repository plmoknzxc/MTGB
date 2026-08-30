# MTGB

MTGB is a configurable digital tabletop for Magic: The Gathering-style matches. It combines faithful support for official Magic card content and rules with player-authored game content and match variations.

## Language

**Official Magic Content**:
Cards, rules, and formats reproduced from Magic: The Gathering source material for use in MTGB.
_Avoid_: Built-in cards, default card pool

**MTGB Client Distribution**:
The MTGB client release, distributed with the official card data and card images selected for its supported content.
_Avoid_: DIY package, data editor

**Data Editor**:
A separately distributed application for authoring and maintaining MTGB card data.
_Avoid_: Content package, in-game editor

**DIY Content Package**:
A separately downloadable package of player-authored MTGB card content, distributed externally and installed at the same version by the authoritative match server and participating players.
_Avoid_: Client distribution, data editor

**Custom Card Effect**:
Player-authored behavior attached to a card, including behavior that can alter rules or victory conditions.
_Avoid_: DIY effect, mod

**Authoritative Match Server**:
The server process that owns match state and executes the card data and effects for the selected content version. Clients submit actions and render only the server's resulting player view.
_Avoid_: Relay server, client authority

**Match Variant**:
A configurable match structure that changes the normal rules, player count, or victory conditions.
_Avoid_: Game mode, ruleset

**Verified Rules Coverage**:
The versioned set of Magic rules, mechanics, and cards whose behavior MTGB has tested and guarantees. Other official content may still be imported only with player-authored effects.
_Avoid_: Full official support, complete rules compatibility

**Committed Online Action**:
An action that has been accepted by an online match and cannot be undone.
_Avoid_: Reversible action, takeback

**In-Match History**:
The compact player-facing projection of significant actions and outcomes in the current match, separate from the complete replay record.
_Avoid_: Full engine trace, replay

**Match Replay**:
The complete recording made available after a match has ended, independent of the compact in-match history. It runs against the currently installed content and is visible in full to match participants.
_Avoid_: In-match history, engine log

**Replay Video**:
A rendered video export of a match replay that preserves its visual playback at export time.
_Avoid_: Interactive replay, in-match history

**Commander Match**:
A future multiplayer Match Variant based on Magic's Commander format, intended to support four players.
_Avoid_: EDH mode, four-player mode

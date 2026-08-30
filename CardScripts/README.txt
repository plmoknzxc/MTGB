Put card Lua scripts in a set-code folder:

CardScripts/<SET CODE>/m<COLLECTOR NUMBER>.lua

Examples:
CardScripts/WOE/m45.lua
CardScripts/MKM/m223.lua

Scripts use Lua-CSharp and must return a table. Declare ApiVersion = 1.
The complete Chinese SDK reference is:

Docs/LuaSDK.zh-CN.md

New cards should register triggered, activated, replacement, continuous, and
rule effects from OnInit instead of adding card-specific C# code.

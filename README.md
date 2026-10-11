# DyeingAPI
This mod adds a new Dyeing system.  
This mod needs to be on both the Client and the Server to work properly, but mods using it only need to be on the Server. (This mod also adds new PhysicalMaterials, so it needs to be on the Client as well, but DyeRecipes themselves are Server-only)  
Requires https://github.com/Circl-NoE/ATTMateriaLib.  

To dye an item, just dunk it in a liquid.

A recipe matches liquids two ways. A preset liquid (Water, the potions) matches by its LiquidDefinition hash, listed in `liquids`. A custom liquid, such as a stew or a liquid made by another mod, has no LiquidDefinition, so it matches by color instead: use the constructor that takes `liquidColors` (0 to 1 RGB) and the hue, saturation, and value tolerances. Colors are compared in HSV, and hue wraps around, so 0 and 360 are the same red. Hue is ignored for grays and very dark colors, and saturation for very dark colors. If several recipes match, the closest one wins.

The base mod adds three recipes and materials by default, Colorless Leather, Canvas, and Rope. These are intended to be used as the base for other dyeing recipes.  

This is an unofficial, fan-made mod for A Township Tale. This mod is not affiliated with or endorsed by the owners or developers of A Township Tale.  

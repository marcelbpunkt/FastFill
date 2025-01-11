# FastFill

Author: marcelbpunkt (GitHub) aka madmarty28 (Nexus/Vortex)

## Introduction

This tiny Valheim mod serves to significantly speed up item bulk 
insertion, i.e. inserting wood into any kind of fire or charcoal kilns, coal or
ore into smelters or blast furnaces, barley into windmills, or flax into 
spinning wheels, while holding "Use".

## Dependencies

Only [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

## How to use this mod

Just install the mod, start Valheim, go to e.g. a charcoal kiln, press and
hold "Use" to fill it up with wood and it will insert wood as it would (no pun
intended) normally do, just significantly faster. Same goes for all the
aforementioned items. You can configure the fill-up speed in the config file
or via config managers like the Enhanced BepInEx Configuration Manager. The
only value you can change  is the "holdRepeatInverval" which is the time (in
seconds) between one insertion and the next. In vanilla Valheim this is 0.2
seconds which is also the max value since it would  break some code otherwise.
The minimum is 0 which entirely disables the repeated insertion, i.e. only
a single item will be inserted.

## Source code

Available at [GitHub](https://github.com/marcelbpunkt/FastFill)

## License

GNU GPL v3.0 (see also LICENSE.txt)
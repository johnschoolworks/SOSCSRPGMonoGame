# SoscMonoGame — Phase 2: Items, Weapons & Inventory

Based on the working Phase 1 project; MonoGame DesktopGL + Myra, `net10.0`.

## Implemented

- Typed `Item` hierarchy: `Weapon`, `HealingPotion`, `Loot`.
- Stackable inventory, quantity checks, removal and item lookup.
- Player begins with Rusty Sword and **three** Minor Healing Potions.
- Attack damage comes from the equipped weapon (Rusty Sword: 2–5).
- Each defeated spider drops one Spider Silk; inventory updates on screen.
- Potion heals up to 5 HP, consumes one item only if HP is not full, and cannot be used at 0 HP.
- Equip Rusty/Iron buttons verify weapon ownership. Iron Sword is deliberately not available yet; a trader or quest can award it in a later phase.
- All Phase 1 locations, movement, rest and combat are retained.

## Run (Ubuntu 26.04)

```sh
cd SoscMonoGame
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run
```

## Manual acceptance test

1. Inventory initially shows Rusty Sword x1 and Minor Healing Potion x3.
2. At full HP, Drink Potion does not reduce quantity.
3. Travel Home → Town Square → Forest; attack spider until defeated.
4. Confirm Spider Silk x1 and XP/Gold increase.
5. After taking damage, Drink Potion reduces quantity and restores at most 5 HP.
6. Equip Iron refuses without the weapon; Equip Rusty succeeds.
7. Return home to heal fully, without consuming potions.

## Scope

No quest, merchant, save/load, equipment selection dropdown or item purchase yet. The inventory is a functional first pass, not a full Scott Lilly lesson-by-lesson port.

**Build status:** Generated from the provided Phase 1 source. The authoring environment does not have the .NET SDK, so compilation and runtime behavior must be validated on your Linux host.

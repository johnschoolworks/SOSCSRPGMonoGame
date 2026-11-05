# SoscMonoGame — Phase 4: Trader / Buy / Sell

Based on Phase 3; keeps MonoGame DesktopGL, Myra, net10.0, world, inventory, combat and the spider-silk quest.

## Trading

Travel north from Home to **Town Square** to meet **Mara the Merchant**. The trading panel lists stock and buy prices, and items you own with sell prices. Each button trades one unit and refreshes the UI.

- Rusty Sword: buy 12 G / sell 6 G; initial merchant stock 2.
- Iron Sword: buy 45 G / sell 22 G; initial merchant stock 1.
- Minor Healing Potion: buy 8 G / sell 4 G; initial merchant stock 8.
- Spider Silk: sell 1 G (merchant does not initially stock silk).
- Initial player gold: 10 G.
- Trading is allowed only in Town Square, with HP above zero.
- Insufficient funds, out-of-stock purchases and selling missing items are rejected without changing balances.
- The last copy of an equipped weapon cannot be sold. Equip another owned weapon first.
- Spider Silk can be sold, but doing so reduces quest progress. Keep 3 silk for the quest.
- Merchant stock persists as you move between locations during the session.

## Run on Ubuntu 26.04

```bash
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run
```

## Manual acceptance checks

1. Start at Home; trading panel says to visit Town Square.
2. Move North to Town Square; buy one potion for 8 G (10 -> 2), inventory potion count increases.
3. Try buying another potion with 2 G; reject without changing gold or stock.
4. Sell a potion; receive 4 G, inventory decreases and merchant stock increases.
5. Try selling your only equipped Rusty Sword; reject.
6. Return to Forest; trading is unavailable. Collect 3 Spider Silk and return to Town Square to complete Phase 3 quest.

## Limitations

No save/load, dialogue, multiple merchants, dynamic pricing, or scrolling trade UI yet. This is a small, fixed-price Phase 4 implementation, not the entire Scott Lilly trading lesson.

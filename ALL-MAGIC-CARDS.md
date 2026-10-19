# Import Magic cards

Finish your match and close Arena, then double-click **Import-All-Magic-Cards.bat**. The importer downloads the full Scryfall catalogue, finds cards supported by the local rules engine, and imports supported missing cards with artwork. Leave the window open until it finishes. Restart your local server afterward, then reopen Arena.

The importer stops safely if Arena is open. It does not close the game or restart services. Downloaded metadata and artwork are cached, so you can run it again after an interruption or to pick up new cards.

Temporary artwork connection failures are retried automatically. Any images still unavailable are listed in the report, and the other cards can install. Run the importer again later to pick up skipped cards.

If the bulk catalogue chooses an online-only reprint of a paper card, the importer now searches for a paper printing by its exact Oracle identity. These lookups are grouped and cached. Special/timeshifted rarity is supported with Arena's rare display badge; original rarity stays in the source data. This recovers cards such as Horned Sliver, Mnemonic Sliver, and Essence Sliver.

## Read the results

Open **`data/magic-import/report.txt`** for the readable summary. Detailed results are in **`data/magic-import/report.json`**.

The full catalogue includes cards that cannot yet be imported. Existing native Arena cards keep their native printings. New imports initially support normal card layouts; cards missing Forge rules or requiring unsupported layouts are skipped and reported. A card appearing in the catalogue does not mean it is playable, and rules availability alone does not prove every interaction or animation works.

New planeswalkers, double-faced cards, and other special layouts still need dedicated display support. Existing Arena versions remain available. The importer uses [Scryfall's bulk data](https://scryfall.com/docs/api/bulk-data), following its recommendation to use bulk downloads for large card collections. [Scryfall API guidance](https://scryfall.com/docs/faqs/i-m-having-trouble-accessing-the-scryfall-api-or-i-m-blocked-17)

## Refresh while keeping your match open

From this project folder, run:

```powershell
python -u tools/import_magic_cards.py --catalog-only
```

This refreshes the catalogue and checks installed-card and rules availability. It does not download artwork, change the active card pack, or install anything into Arena.

## Other import options

Run these from the project folder:

| Action | Command |
| --- | --- |
| Prepare metadata and artwork for later installation | `python -u tools/import_magic_cards.py` |
| Install prepared/supported imports with Arena closed | `python -u tools/import_magic_cards.py --install` |
| Force a check for refreshed bulk card data | `python -u tools/import_magic_cards.py --catalog-only --refresh` |
| Import only names from a deck list or plain text file | `python -u tools/import_magic_cards.py --install --names "my-cards.txt"` |
| Use the cached bulk catalogue | `python -u tools/import_magic_cards.py --install --offline` |

`--offline` uses an existing bulk-data cache. It is not a promise that missing artwork is available locally. Re-running the import reuses downloaded files.

Installing while the server is running does not reload its cached card metadata. Restart it after the import; the card importer leaves that timing to you.

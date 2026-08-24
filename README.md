# ue5-pak-translator

![License](https://img.shields.io/badge/License-MIT-yellow.svg)

A pipeline for translating Unreal Engine 5 games by patching `.uasset` / `.umap` files inside `.pak` archives.

Built for **Pseudoregalia** (PT-BR), but designed to work with any UE5 game that stores text in DataTables, UI Widgets, or Maps.

---

## Pipeline overview

```
game .pak
   │
   ▼ repak extract
extracted assets/
   │
   ▼ TextTool extract
texts.json          ← all translatable strings
   │
   ▼ translator.py (LM Studio)
texts_translated.json
   │
   ▼ make_ascii_translations.py  (if needed — see ASCII section)
texts_translated_ascii.json
   │
   ▼ TextTool import
ModFiles/           ← modified assets
   │
   ▼ repak pack
game_PTBR_P.pak     ← drop into game Paks/ folder
```

---

## Prerequisites

### Tools (place in `tools/`)
- **repak.exe** — https://github.com/trumank/repak/releases
- **UAssetAPI.dll** — https://github.com/atenfyr/UAssetAPI/releases
- **oo2core_9_win64.dll** — copy from the game's `Binaries/Win64/` folder or download from [this link](https://pt.dll-files.com/oo2core_8_win64.dll.html)

See `tools/README.md` for details.

### Software
- [.NET 9 SDK](https://dotnet.microsoft.com/download) — to build TextTool
- Python 3.11+ — for translator.py and helper scripts
- [LM Studio](https://lmstudio.ai/) — local LLM inference server (or any OpenAI-compatible API)

---

## Step-by-step usage

### 1. Extract the game pak

```powershell
tools\repak.exe extract game_content.pak --output Extracted\
```

### 2. Build TextTool

```powershell
cd TextTool
dotnet build -c Release
```

### 3. Extract translatable strings

```powershell
dotnet run -c Release -- extract ..\Extracted output\texts.json examples\pseudoregalia\assets.txt
```

`assets.txt` lists which files to scan. Edit or create your own for a different game.

### 4. Translate

```powershell
python translator.py examples\pseudoregalia\translator_config.json
```

Edit `translator_config.json` to point to your LM Studio instance and adjust the system prompt for your game and target language.

### 5. (Optional) Strip accents

If the game uses a bitmap/custom font without accented glyphs, run:

```powershell
python make_ascii_translations.py output\texts_translated.json output\texts_translated_ascii.json
```

Set `$TRANSLATED` in your config to the `_ascii.json` version.

### 6. Build and install the mod pak

```powershell
.\repack_mod.ps1 -ConfigFile examples\pseudoregalia\config.ps1
```

This imports translations into the assets and packs them into a `_P.pak` file that UE5 loads on top of the original.

---

## Adapting to another game

1. **Find the pak** — usually in `<game>/Content/Paks/`.
2. **Extract it** with repak. Verify it works: `repak list game.pak`.
3. **Find text assets** — look for `DT_*.uasset` (DataTables), `UI_*.uasset` / `WBP_*.uasset` (widgets), `.umap` files with NPC dialogue.
4. **Create `assets.txt`** — list the files you found (pak-relative paths, forward slashes).
5. **Run `TextTool extract`** and inspect `texts.json`. Prune false positives by adjusting `IsTranslatable()` in `Program.cs` or your skip regexes in the config.
6. **Find pak settings** — you need the pak format version and path hash seed:
   - Open the original `.pak` in a hex editor. Bytes `0x20–0x23` at the end of the file contain the format version. Common UE5 values: `V11` (UE5.0–5.1), `V11` with FnV64BugFix (UE5.2+).
   - The path hash seed is a 32-bit value — try `0` first; if repak warns about hash mismatches, inspect the pak footer.
7. **Set the engine version** in `config.ps1` (`$ENGINE_VERSION`). Match it to the game's UE version. Common values: `VER_UE5_0`, `VER_UE5_1`, `VER_UE5_2`, `VER_UE5_3`.
8. **Write a `config.ps1`** following `examples/pseudoregalia/config.ps1` as a template.

---

## ASCII limitation

### Why accented characters may not work

Many UE5 indie games use **custom bitmap fonts** that only contain ASCII glyphs (0x20–0x7E). When the game tries to render a character outside that range, it either:
- Shows an empty box / rectangle
- Crashes during asset deserialization at startup

**Pseudoregalia** exhibits both behaviors:
- Assets loaded at startup (e.g. `DT_UpgradeData`) crash the game immediately.
- Assets loaded lazily (e.g. `DT_NoteData`) survive but show boxes.

The root cause is that the translator produced **UTF-8 bytes** for accented characters (e.g., `ã` = `0xC3 0xA3`), which UE reads as two Latin-1 characters (`Ã` + `£`). UAssetAPI then writes these back faithfully, and the game crashes on the unexpected byte sequence.

### The fix

Use `make_ascii_translations.py` to strip diacritics via Unicode NFD decomposition before importing:

```
ã → a   ç → c   é → e   ó → o   ú → u   etc.
```

This produces readable translations without crashing. It is the **recommended default** for any game you haven't verified supports extended characters.

### How to test if your game supports accents

1. Translate only a **lazy-loaded** asset (one that is only read when the player actively triggers it, not on boot).
2. Launch the game and navigate to that content without crashing.
3. If it renders correctly — the font supports that character range and you can try accents for other files.
4. If it crashes on startup, the offending asset is eager-loaded. Use ASCII-only translations.

---

## Project structure

```
ue5-pak-translator/
├── README.md
├── .gitignore
├── repack_mod.ps1            generic build script
├── translator.py             LM Studio translation client
├── make_ascii_translations.py  strip diacritics
├── TextTool/
│   ├── TextTool.csproj
│   └── Program.cs            extract / import / roundtrip
├── tools/
│   └── README.md             where to get repak, UAssetAPI, Oodle
├── examples/
│   └── pseudoregalia/
│       ├── assets.txt              assets to translate
│       ├── config.ps1              repak + path settings
│       └── translator_config.json  system prompt + manual dict
└── output/                   gitignored — texts.json, cache, etc.
```

---

## TextTool reference

```
TextTool [--engine VER_UE5_1] extract   <pak_root> <output.json>         [assets.txt]
TextTool [--engine VER_UE5_1] import    <pak_root> <translated.json> <mod_root> [assets.txt]
TextTool [--engine VER_UE5_1] roundtrip <pak_root> <out_dir>         [assets.txt]
```

- `--engine` sets the UAssetAPI `EngineVersion` enum value. Can also be set via `TEXTTOOL_ENGINE` env var.
- `roundtrip` reads every asset and writes it back unchanged — useful to verify UAssetAPI can parse your game's assets before translating.
- `import` processes only files that have at least one non-empty `Translated` field in the JSON, so you can safely run it with a partially translated file.

---

## License

MIT — see [LICENSE](LICENSE). Feel free to fork and adapt this for other UE5 games or languages; credit is appreciated but not required.

Third-party tools used (not redistributed here, see [tools/README.md](tools/README.md)): [repak](https://github.com/trumank/repak) (MIT/Apache-2.0), [UAssetAPI](https://github.com/atenfyr/UAssetAPI) (MIT).

---


# Pseudoregalia — game-specific configuration for repack_mod.ps1 (Polish / PL)
# Dot-sourced by repack_mod.ps1 when passed as -ConfigFile

$REPO_ROOT    = "$PSScriptRoot\..\.."          # ue5-pak-translator root

# ── Paths ────────────────────────────────────────────────────────────────────

# Directory containing the extracted game .pak (already extracted once — see README)
$PAK_ROOT     = "$REPO_ROOT\Extracted"

# Translation JSON to use — point this at your translated texts.json
# (fill in the "Translated" field for each entry, keep everything else unchanged)
$TRANSLATED   = "$REPO_ROOT\output\texts_translated_pl.json"

# Staging directory for modified assets (wiped and rebuilt each run)
$MOD_FILES    = "$REPO_ROOT\ModFiles_PL"

# Output .pak path — must end with _P.pak so UE loads it as an override
# Point this at YOUR OWN Pseudoregalia install folder, e.g.:
# "C:\Program Files (x86)\Steam\steamapps\common\Pseudoregalia\pseudoregalia\Content\Paks\pseudoregalia-PL_P.pak"
$OUT_PAK      = "$REPO_ROOT\output\pseudoregalia-PL_P.pak"

# ── Tools ────────────────────────────────────────────────────────────────────

$REPAK         = "$REPO_ROOT\tools\repak.exe"
$TEXTTOOL_DIR  = "$REPO_ROOT\TextTool"
$ASSETS_TXT    = "$PSScriptRoot\assets.txt"

# ── Pak settings (same values as PT-BR — game-specific, not language-specific) ─

# repak pak format version — Pseudoregalia uses V11 (UE5.1)
$PAK_VERSION   = "V11"

# Path hash seed — game-specific value, same for every language mod of this game
# Pseudoregalia: 0xC5F0BCC9 = 3320888521 decimal
$PATH_HASH_SEED = 3320888521

# UAssetAPI engine version
$ENGINE_VERSION = "VER_UE5_1"

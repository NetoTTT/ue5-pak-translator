# Pseudoregalia — game-specific configuration for repack_mod.ps1
# Dot-sourced by repack_mod.ps1 when passed as -ConfigFile

$REPO_ROOT    = "$PSScriptRoot\..\.."          # ue5-pak-translator root

# ── Paths ────────────────────────────────────────────────────────────────────

# Directory containing the extracted game .pak (run repak once to get this)
$PAK_ROOT     = "C:\PseudoMod\Extracted"

# Translation JSON to use. Options:
#   texts_translated.json       — full translations (may include accents)
#   texts_translated_ascii.json — accent-stripped safe version (recommended)
$TRANSLATED   = "C:\PseudoMod\output\texts_translated_ascii.json"

# Staging directory for modified assets (wiped and rebuilt each run)
$MOD_FILES    = "C:\PseudoMod\ModFiles"

# Output .pak path — must end with _P.pak so UE loads it as an override
$OUT_PAK      = "C:\Program Files (x86)\Steam\steamapps\common\Pseudoregalia\pseudoregalia\Content\Paks\pseudoregalia-PTBR_P.pak"

# ── Tools ────────────────────────────────────────────────────────────────────

$REPAK         = "$REPO_ROOT\tools\repak.exe"
$TEXTTOOL_DIR  = "$REPO_ROOT\TextTool"
$ASSETS_TXT    = "$PSScriptRoot\assets.txt"

# ── Pak settings ─────────────────────────────────────────────────────────────

# repak pak format version — Pseudoregalia uses V11 (UE5.1)
$PAK_VERSION   = "V11"

# Path hash seed — game-specific value found via hex editor in original .pak
# Pseudoregalia: 0xC5F0BCC9 = 3320888521 decimal
$PATH_HASH_SEED = 3320888521

# UAssetAPI engine version
$ENGINE_VERSION = "VER_UE5_1"

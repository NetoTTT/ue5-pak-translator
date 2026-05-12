# repack_mod.ps1 — Build a UE5 translation mod .pak file
#
# Usage:
#   .\repack_mod.ps1 -ConfigFile examples\pseudoregalia\config.ps1
#
# Or set variables directly and dot-source this script from your own config.

param(
    [string]$ConfigFile = ""
)

$ErrorActionPreference = "Stop"

# ── Load game-specific config ────────────────────────────────────────────────
if ($ConfigFile -ne "" -and (Test-Path $ConfigFile)) {
    . (Resolve-Path $ConfigFile)
} elseif (Test-Path "config.ps1") {
    . (Resolve-Path "config.ps1")
} else {
    Write-Host "No config file found. Pass -ConfigFile <path> or create config.ps1 here." -ForegroundColor Red
    Write-Host "See examples\pseudoregalia\config.ps1 for reference." -ForegroundColor Yellow
    exit 1
}

# Required variables (must be set in config):
#   $PAK_ROOT        — directory containing extracted game assets (pak_root)
#   $TRANSLATED      — path to texts_translated.json (or _ascii.json)
#   $MOD_FILES       — staging directory for modified assets
#   $OUT_PAK         — full path of the output .pak file (must end with _P.pak)
#   $REPAK           — path to repak.exe
#   $TEXTTOOL_DIR    — directory containing TextTool.csproj
#   $ASSETS_TXT      — path to assets.txt listing which files to modify
#   $PAK_VERSION     — repak pak version string (e.g. "V11")
#   $PATH_HASH_SEED  — repak path hash seed (decimal integer)
#
# Optional:
#   $ENGINE_VERSION  — UAssetAPI engine version (default: VER_UE5_1)

if (-not $ENGINE_VERSION) { $ENGINE_VERSION = "VER_UE5_1" }

foreach ($var in @("PAK_ROOT","TRANSLATED","MOD_FILES","OUT_PAK","REPAK","TEXTTOOL_DIR","ASSETS_TXT","PAK_VERSION","PATH_HASH_SEED")) {
    if (-not (Get-Variable -Name $var -ErrorAction SilentlyContinue).Value) {
        Write-Host "Config is missing: `$$var" -ForegroundColor Red
        exit 1
    }
}

# ── Step 1: clear staging directory ─────────────────────────────────────────
Write-Host "=== Step 1: Clearing mod staging directory ===" -ForegroundColor Cyan
if (Test-Path $MOD_FILES) {
    Get-ChildItem $MOD_FILES | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $MOD_FILES | Out-Null

# ── Step 2: import translations ──────────────────────────────────────────────
Write-Host "`n=== Step 2: Importing translations into assets ===" -ForegroundColor Cyan
Set-Location $TEXTTOOL_DIR
$env:TEXTTOOL_ENGINE = $ENGINE_VERSION
dotnet run -c Release -- import $PAK_ROOT $TRANSLATED $MOD_FILES $ASSETS_TXT
if ($LASTEXITCODE -ne 0) { Write-Host "TextTool import failed!" -ForegroundColor Red; exit 1 }

# ── Step 3: pack into .pak ──────────────────────────────────────────────────
Write-Host "`n=== Step 3: Packing mod .pak ===" -ForegroundColor Cyan
Set-Location $MOD_FILES
& $REPAK pack --version $PAK_VERSION --path-hash-seed $PATH_HASH_SEED . $OUT_PAK
if ($LASTEXITCODE -ne 0) { Write-Host "repak failed!" -ForegroundColor Red; exit 1 }

Write-Host "`n=== Done! ===" -ForegroundColor Green
Write-Host "Mod installed at: $OUT_PAK"
Write-Host "To uninstall: delete $OUT_PAK"

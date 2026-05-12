# tools/ — Required binaries (not included)

Place the following files in this directory before building:

---

## repak.exe

**Purpose:** Extract and pack Unreal Engine `.pak` archives.

**Download:** https://github.com/trumank/repak/releases  
Get the latest `repak-x86_64-pc-windows-msvc.zip`, extract `repak.exe` here.

**Usage in this project:** called by `repack_mod.ps1` to create the final `_P.pak` override file.

---

## UAssetAPI.dll

**Purpose:** Read and write Unreal Engine `.uasset` / `.umap` / `.uexp` files.

**Download:** https://github.com/atenfyr/UAssetAPI/releases  
Get the latest release `.zip`, extract `UAssetAPI.dll` here.

The `TextTool.csproj` references this file via `<HintPath>..\tools\UAssetAPI.dll</HintPath>`.

---

## oo2core_9_win64.dll  (Oodle — optional)

**Purpose:** Oodle decompression, required only if the game pak uses Oodle compression (most UE5 titles do).

**How to obtain:**  
Copy `oo2core_9_win64.dll` from the game installation folder (usually in `<game>\Binaries\Win64\`) or download it from [pt.dll-files.com](https://pt.dll-files.com/oo2core_8_win64.dll.html).

repak will warn you if Oodle is needed and the DLL is missing.

---

## Summary

| File | Source |
|------|--------|
| `repak.exe` | github.com/trumank/repak |
| `UAssetAPI.dll` | github.com/atenfyr/UAssetAPI |
| `oo2core_9_win64.dll` | Game installation or [pt.dll-files.com](https://pt.dll-files.com/oo2core_8_win64.dll.html) |

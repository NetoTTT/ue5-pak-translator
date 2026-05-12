using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

// Usage:
//   TextTool [--engine VER_UE5_1] extract   <pak_root> <output.json>  [assets.txt]
//   TextTool [--engine VER_UE5_1] import    <pak_root> <translated.json> <mod_root> [assets.txt]
//   TextTool [--engine VER_UE5_1] roundtrip <pak_root> <out_dir>      [assets.txt]
//
// assets.txt: one pak-relative path per line; lines starting with # are ignored.
// Engine version can also be set via TEXTTOOL_ENGINE env var (default: VER_UE5_1).

class TextEntry
{
    public string File { get; set; } = "";
    public string RowOrExport { get; set; } = "";
    public string PropertyPath { get; set; } = "";
    public string Original { get; set; } = "";
    public string Translated { get; set; } = "";
}

class Program
{
    static EngineVersion EV = EngineVersion.VER_UE5_1;
    static string[] TargetAssets = Array.Empty<string>();

    static void Main(string[] rawArgs)
    {
        var args = rawArgs.ToList();

        // --engine flag
        int evIdx = args.IndexOf("--engine");
        if (evIdx >= 0 && evIdx + 1 < args.Count)
        {
            if (Enum.TryParse<EngineVersion>(args[evIdx + 1], true, out var ev))
                EV = ev;
            else
            { Console.Error.WriteLine($"Unknown engine version: {args[evIdx + 1]}"); Environment.Exit(1); }
            args.RemoveRange(evIdx, 2);
        }
        else
        {
            var envEV = Environment.GetEnvironmentVariable("TEXTTOOL_ENGINE");
            if (!string.IsNullOrEmpty(envEV) && Enum.TryParse<EngineVersion>(envEV, true, out var ev))
                EV = ev;
        }

        if (args.Count < 3)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  TextTool [--engine VER_UE5_1] extract   <pak_root> <output.json>  [assets.txt]");
            Console.WriteLine("  TextTool [--engine VER_UE5_1] import    <pak_root> <translated.json> <mod_root> [assets.txt]");
            Console.WriteLine("  TextTool [--engine VER_UE5_1] roundtrip <pak_root> <out_dir>      [assets.txt]");
            Console.WriteLine();
            Console.WriteLine("assets.txt  one pak-relative path per line (# = comment)");
            Console.WriteLine("TEXTTOOL_ENGINE env var sets default engine version (default: VER_UE5_1)");
            return;
        }

        string mode    = args[0].ToLower();
        string pakRoot = args[1];

        if (mode == "extract")
        {
            LoadAssets(args.Count >= 4 ? args[3] : null);
            var entries = ExtractAll(pakRoot);
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(args[2], JsonSerializer.Serialize(entries, opts));
            Console.WriteLine($"\nExtracted {entries.Count} text entries → {args[2]}");
        }
        else if (mode == "import")
        {
            if (args.Count < 4) { Console.WriteLine("import needs <mod_root>"); return; }
            LoadAssets(args.Count >= 5 ? args[4] : null);
            var entries = JsonSerializer.Deserialize<List<TextEntry>>(File.ReadAllText(args[2]))!;
            ImportAll(pakRoot, entries, args[3]);
        }
        else if (mode == "roundtrip")
        {
            LoadAssets(args.Count >= 4 ? args[3] : null);
            Roundtrip(pakRoot, args[2]);
        }
        else
        {
            Console.WriteLine($"Unknown mode: {mode}");
        }
    }

    // ─── ASSETS LIST ────────────────────────────────────────────────────────

    static void LoadAssets(string? assetFile)
    {
        if (assetFile == null || !File.Exists(assetFile))
        {
            if (assetFile != null)
                Console.Error.WriteLine($"Warning: assets file not found: {assetFile}");
            TargetAssets = Array.Empty<string>();
            return;
        }
        TargetAssets = File.ReadAllLines(assetFile)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();
        Console.WriteLine($"Loaded {TargetAssets.Length} assets from {assetFile}");
    }

    // ─── EXTRACT ────────────────────────────────────────────────────────────

    static List<TextEntry> ExtractAll(string pakRoot)
    {
        if (TargetAssets.Length == 0)
        {
            Console.Error.WriteLine("No assets loaded. Provide an assets.txt file as the last argument.");
            return new List<TextEntry>();
        }

        var all = new List<TextEntry>();
        foreach (var rel in TargetAssets)
        {
            var path = Path.Combine(pakRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { Console.WriteLine($"  SKIP (missing): {rel}"); continue; }

            Console.Write($"  {rel} ... ");
            try
            {
                var asset = new UAsset(path, EV);
                var entries = ExtractFromAsset(asset, rel);
                all.AddRange(entries);
                Console.WriteLine($"{entries.Count} strings");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
        return all;
    }

    static List<TextEntry> ExtractFromAsset(UAsset asset, string relPath)
    {
        var results = new List<TextEntry>();
        foreach (var export in asset.Exports)
        {
            string exportName = export.ObjectName.ToString();
            if (export is DataTableExport dte)
            {
                foreach (var row in dte.Table.Data)
                    WalkProps(row.Value, relPath, $"[{row.Name}]", results);
            }
            else if (export is NormalExport ne)
            {
                WalkProps(ne.Data, relPath, exportName, results);
            }
        }
        return results;
    }

    static void WalkProps(IEnumerable<PropertyData> props, string file, string prefix, List<TextEntry> results)
    {
        foreach (var prop in props)
        {
            string path = $"{prefix}.{prop.Name}";
            if (prop is TextPropertyData txt)
            {
                string? val = txt.CultureInvariantString?.Value;
                if (IsTranslatable(val))
                    results.Add(new TextEntry { File = file, RowOrExport = prefix, PropertyPath = path, Original = val! });
            }
            else if (prop is StrPropertyData str)
            {
                string? val = str.Value?.Value;
                if (IsTranslatable(val))
                    results.Add(new TextEntry { File = file, RowOrExport = prefix, PropertyPath = path, Original = val! });
            }
            else if (prop is ArrayPropertyData arr && arr.Value != null)
            {
                for (int i = 0; i < arr.Value.Length; i++)
                {
                    string ePath = $"{path}[{i}]";
                    if (arr.Value[i] is TextPropertyData etxt)
                    {
                        string? val = etxt.CultureInvariantString?.Value;
                        if (IsTranslatable(val))
                            results.Add(new TextEntry { File = file, RowOrExport = prefix, PropertyPath = ePath, Original = val! });
                    }
                    else if (arr.Value[i] is StrPropertyData estr)
                    {
                        string? val = estr.Value?.Value;
                        if (IsTranslatable(val))
                            results.Add(new TextEntry { File = file, RowOrExport = prefix, PropertyPath = ePath, Original = val! });
                    }
                    else if (arr.Value[i] is StructPropertyData sd)
                        WalkProps(sd.Value, file, ePath, results);
                }
            }
            else if (prop is StructPropertyData spd)
                WalkProps(spd.Value, file, path, results);
        }
    }

    // ─── IMPORT ─────────────────────────────────────────────────────────────

    static void ImportAll(string pakRoot, List<TextEntry> entries, string modRoot)
    {
        var byFile = new Dictionary<string, List<TextEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.Translated)) continue;
            if (!byFile.ContainsKey(e.File)) byFile[e.File] = new List<TextEntry>();
            byFile[e.File].Add(e);
        }

        foreach (var (rel, fileEntries) in byFile)
        {
            var path = Path.Combine(pakRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { Console.WriteLine($"  SKIP (missing): {rel}"); continue; }

            Console.Write($"  {rel} ... ");
            try
            {
                var asset = new UAsset(path, EV);
                int count = ApplyAll(asset, fileEntries);

                var outPath = Path.Combine(modRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                asset.Write(outPath);
                int encodingDelta = FixLatin1Encoding(outPath, asset, fileEntries);
                string fixNote = encodingDelta != 0 ? $" [Latin-1 fix: {encodingDelta:+0;-0}B]" : "";
                Console.WriteLine($"{count} replacements → {outPath}{fixNote}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
    }

    static int ApplyAll(UAsset asset, List<TextEntry> entries)
    {
        // Last translation wins on duplicate PropertyPath (can happen with maps)
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in entries.Where(e => !string.IsNullOrWhiteSpace(e.Translated)))
            map[e.PropertyPath] = e.Translated;

        int count = 0;
        foreach (var export in asset.Exports)
        {
            if (export is DataTableExport dte)
            {
                foreach (var row in dte.Table.Data)
                    count += PatchProps(row.Value, $"[{row.Name}]", map);
            }
            else if (export is NormalExport ne)
            {
                count += PatchProps(ne.Data, export.ObjectName.ToString(), map);
            }
        }
        return count;
    }

    static int PatchProps(IEnumerable<PropertyData> props, string prefix, Dictionary<string, string> map)
    {
        int count = 0;
        foreach (var prop in props)
        {
            string path = $"{prefix}.{prop.Name}";
            if (prop is TextPropertyData txt)
            {
                if (map.TryGetValue(path, out var t) && txt.CultureInvariantString != null)
                { txt.CultureInvariantString.Value = t; count++; }
            }
            else if (prop is StrPropertyData str)
            {
                if (map.TryGetValue(path, out var t) && str.Value != null)
                { str.Value.Value = t; count++; }
            }
            else if (prop is ArrayPropertyData arr && arr.Value != null)
            {
                for (int i = 0; i < arr.Value.Length; i++)
                {
                    string ePath = $"{path}[{i}]";
                    if (arr.Value[i] is TextPropertyData etxt && map.TryGetValue(ePath, out var et))
                    {
                        if (etxt.CultureInvariantString != null) { etxt.CultureInvariantString.Value = et; count++; }
                    }
                    else if (arr.Value[i] is StrPropertyData estr && map.TryGetValue(ePath, out var es))
                    {
                        if (estr.Value != null) { estr.Value.Value = es; count++; }
                    }
                    else if (arr.Value[i] is StructPropertyData sd)
                        count += PatchProps(sd.Value, ePath, map);
                }
            }
            else if (prop is StructPropertyData spd)
                count += PatchProps(spd.Value, path, map);
        }
        return count;
    }

    // ─── ROUNDTRIP ──────────────────────────────────────────────────────────

    static void Roundtrip(string pakRoot, string outDir)
    {
        if (TargetAssets.Length == 0)
        {
            Console.Error.WriteLine("No assets loaded. Provide an assets.txt file as the last argument.");
            return;
        }
        foreach (var rel in TargetAssets)
        {
            var path = Path.Combine(pakRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { Console.WriteLine($"  SKIP: {rel}"); continue; }
            var outPath = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            var asset = new UAsset(path, EV);
            asset.Write(outPath);
            var origBytes = File.ReadAllBytes(path);
            var modBytes  = File.ReadAllBytes(outPath);
            if (origBytes.Length != modBytes.Length)
                Console.WriteLine($"  SIZE DIFF: {rel}  orig={origBytes.Length} out={modBytes.Length}");
            else
            {
                int diffs = 0;
                for (int i = 0; i < origBytes.Length; i++) if (origBytes[i] != modBytes[i]) diffs++;
                Console.WriteLine(diffs > 0 ? $"  BYTE DIFF ({diffs} bytes): {rel}" : $"  OK: {rel}");
            }
            var origExp = path.Replace(".uasset", ".uexp").Replace(".umap", ".uexp");
            var modExp  = outPath.Replace(".uasset", ".uexp").Replace(".umap", ".uexp");
            if (File.Exists(origExp) && File.Exists(modExp))
            {
                var ob = File.ReadAllBytes(origExp); var mb = File.ReadAllBytes(modExp);
                if (ob.Length != mb.Length) Console.WriteLine($"    .uexp SIZE DIFF: orig={ob.Length} out={mb.Length}");
                else { int d = 0; for (int i = 0; i < ob.Length; i++) if (ob[i] != mb[i]) d++; if (d > 0) Console.WriteLine($"    .uexp BYTE DIFF ({d} bytes)"); }
            }
        }
    }

    // ─── LATIN-1 ENCODING FIX ───────────────────────────────────────────────
    // UAssetAPI writes UTF-16LE (negative length prefix) for strings with chars > 127,
    // but the game engine expects Latin-1 (positive length prefix) for these fields.
    // This post-processes the .uexp to convert UTF-16LE → Latin-1 and patches
    // SerialSize / SerialOffset in the .uasset header.
    //
    // NOTE: Pseudoregalia crashes on non-ASCII chars even after this fix because
    // the game uses a custom font with ASCII-only glyphs. Strip accents before
    // importing (use make_ascii_translations.py). This fix is kept for games that
    // do support extended Latin-1 chars.

    static int FixLatin1Encoding(string uassetPath, UAsset asset, List<TextEntry> entries)
    {
        bool isMap = uassetPath.EndsWith(".umap", StringComparison.OrdinalIgnoreCase);
        string uexpPath = isMap
            ? Path.ChangeExtension(uassetPath, ".uexp")
            : uassetPath[..^7] + ".uexp";

        if (!File.Exists(uexpPath)) return 0;

        var pairs = new List<(byte[] search, byte[] replace)>();
        var seen = new HashSet<string>();
        foreach (var e in entries.Where(e => !string.IsNullOrEmpty(e.Translated)))
        {
            string t = e.Translated;
            if (!t.Any(c => c > 127)) continue;
            if (t.Any(c => c > 255)) continue;
            if (!seen.Add(t)) continue;

            int numChars = t.Length + 1;
            var utf16  = Encoding.Unicode.GetBytes(t + "\0");
            var search = new byte[4 + utf16.Length];
            BitConverter.GetBytes(-numChars).CopyTo(search, 0);
            utf16.CopyTo(search, 4);

            var latin1  = Encoding.Latin1.GetBytes(t + "\0");
            var replace = new byte[4 + latin1.Length];
            BitConverter.GetBytes(latin1.Length).CopyTo(replace, 0);
            latin1.CopyTo(replace, 4);

            pairs.Add((search, replace));
        }

        if (pairs.Count == 0) return 0;

        long uassetFileSize = new FileInfo(uassetPath).Length;
        var ranges = asset.Exports.Select(exp => (
            start: exp.SerialOffset - uassetFileSize,
            end:   exp.SerialOffset - uassetFileSize + exp.SerialSize
        )).ToArray();
        var exportDeltas = new long[asset.Exports.Count];

        var uexpList  = new List<byte>(File.ReadAllBytes(uexpPath));
        int totalDelta = 0;

        foreach (var (search, replace) in pairs)
        {
            int pos = 0;
            while (true)
            {
                int idx = FindBytes(uexpList, search, pos);
                if (idx < 0) break;
                int delta = replace.Length - search.Length;
                for (int k = 0; k < ranges.Length; k++)
                {
                    if (idx < ranges[k].start)
                        ranges[k] = (ranges[k].start + delta, ranges[k].end + delta);
                    else if (idx < ranges[k].end)
                    { ranges[k] = (ranges[k].start, ranges[k].end + delta); exportDeltas[k] += delta; }
                }
                uexpList.RemoveRange(idx, search.Length);
                uexpList.InsertRange(idx, replace);
                totalDelta += delta;
                pos = idx + replace.Length;
            }
        }

        if (totalDelta == 0) return 0;

        File.WriteAllBytes(uexpPath, uexpList.ToArray());

        var uassetBytes = File.ReadAllBytes(uassetPath);
        long cumulative = 0;
        for (int i = 0; i < asset.Exports.Count; i++)
        {
            var exp       = asset.Exports[i];
            long oldSize  = exp.SerialSize;
            long newSize  = oldSize + exportDeltas[i];
            long oldOffset = exp.SerialOffset;
            long newOffset = oldOffset + cumulative;

            if (newSize == oldSize && newOffset == oldOffset)
            { cumulative += exportDeltas[i]; continue; }

            var pattern = new byte[20];
            BitConverter.GetBytes((uint)exp.ObjectFlags).CopyTo(pattern, 0);
            BitConverter.GetBytes(oldSize).CopyTo(pattern, 4);
            BitConverter.GetBytes(oldOffset).CopyTo(pattern, 12);

            int patchIdx = FindBytes(uassetBytes, pattern, 64);
            if (patchIdx >= 0)
            {
                BitConverter.GetBytes(newSize).CopyTo(uassetBytes, patchIdx + 4);
                BitConverter.GetBytes(newOffset).CopyTo(uassetBytes, patchIdx + 12);
            }
            cumulative += exportDeltas[i];
        }

        File.WriteAllBytes(uassetPath, uassetBytes);
        return totalDelta;
    }

    static int FindBytes(List<byte> source, byte[] pattern, int start)
    {
        int srcLen = source.Count, patLen = pattern.Length;
        for (int i = start; i <= srcLen - patLen; i++)
        {
            bool match = true;
            for (int j = 0; j < patLen; j++) if (source[i + j] != pattern[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    static int FindBytes(byte[] source, byte[] pattern, int start)
    {
        int srcLen = source.Length, patLen = pattern.Length;
        for (int i = start; i <= srcLen - patLen; i++)
        {
            bool match = true;
            for (int j = 0; j < patLen; j++) if (source[i + j] != pattern[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    static bool IsTranslatable(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 3) return false;
        if (s.StartsWith("/Game/") || s.StartsWith("/Script/") || s.StartsWith("/Engine/")) return false;
        if (s.StartsWith("BP_") || s.StartsWith("WBP_") || s.StartsWith("T_") || s.StartsWith("M_")) return false;
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^[0-9A-F]{16,}$")) return false;
        return s.Count(char.IsLetter) >= 3;
    }
}

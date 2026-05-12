"""
UE5 Pak Translator — generic LM Studio translation script.
Loads game-specific config from a JSON file.

Usage:
    python translator.py <config.json>

The config file drives all game-specific settings (see examples/pseudoregalia/).
"""

import json
import re
import sys
import time
import urllib.request
import urllib.error
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

# ── Load config ─────────────────────────────────────────────────────────────

def load_config(path: str) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)

# ── Parsing for "thinking" model responses ───────────────────────────────────

def extract_answer(text: str) -> str:
    """Strip reasoning blocks, return only the final answer."""
    # Gemma 4: "...thinking...<channel|>ANSWER"
    if "<channel|>" in text:
        part = text.split("<channel|>")[-1].strip()
        if part:
            return part
    # Gemma 4 alternate: "<|channel>thought\n...\n<|channel>response\nANSWER"
    if "<|channel>" in text:
        parts = re.split(r"<\|channel\>[a-zA-Z_]+\s*\n?", text)
        for part in reversed(parts):
            clean = part.strip()
            if clean:
                return clean
    # Qwen3: <think>...</think> ANSWER
    m = re.search(r"</think>\s*(.*)", text, re.DOTALL)
    if m:
        return m.group(1).strip()
    return text.strip()

# ── API call ─────────────────────────────────────────────────────────────────

def translate(text: str, cfg: dict, retries: int = 3) -> str | None:
    est_tokens = min(int(len(text) * 2) + 800, 2000)
    payload = {
        "model":       cfg["model"],
        "messages":    [
            {"role": "system", "content": cfg["system_prompt"]},
            {"role": "user",   "content": text},
        ],
        "temperature": cfg.get("temperature", 0.2),
        "max_tokens":  est_tokens,
        "stream":      False,
    }
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    req  = urllib.request.Request(
        cfg["lm_studio_url"],
        data=body,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )
    for attempt in range(retries):
        try:
            with urllib.request.urlopen(req, timeout=180) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                raw  = data["choices"][0]["message"]["content"]
                return extract_answer(raw)
        except urllib.error.HTTPError as e:
            body_err = e.read().decode("utf-8", errors="replace")
            print(f"    [HTTP {e.code} attempt {attempt+1}]: {body_err[:200]}")
        except Exception as e:
            print(f"    [Error attempt {attempt+1}]: {e}")
        if attempt < retries - 1:
            time.sleep(3)
    return None

# ── Helpers ───────────────────────────────────────────────────────────────────

def load_json(p: Path):
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else {}

def save_json(p: Path, data):
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

# ── Main ─────────────────────────────────────────────────────────────────────

def main():
    if len(sys.argv) < 2:
        print("Usage: python translator.py <config.json>")
        print("       See examples/pseudoregalia/translator_config.json")
        sys.exit(1)

    cfg = load_config(sys.argv[1])

    # Resolve paths relative to config file location
    config_dir  = Path(sys.argv[1]).parent
    input_json  = Path(cfg["input_json"])  if Path(cfg["input_json"]).is_absolute()  else config_dir / cfg["input_json"]
    output_json = Path(cfg["output_json"]) if Path(cfg["output_json"]).is_absolute() else config_dir / cfg["output_json"]
    cache_file  = Path(cfg["cache_file"])  if Path(cfg["cache_file"]).is_absolute()  else config_dir / cfg["cache_file"]

    manual      = cfg.get("manual", {})
    skip_re     = re.compile("|".join(cfg.get("skip_regex", []))) if cfg.get("skip_regex") else None

    entries: list = json.loads(input_json.read_text(encoding="utf-8"))
    cache:   dict = load_json(cache_file)

    total   = len(entries)
    done    = sum(1 for e in entries if e.get("Translated"))
    skipped = 0
    errors  = 0

    game = cfg.get("game_name", "UE5 Game")
    print(f"{game} Translator")
    print(f"Total: {total} | Already translated: {done} | Cache: {len(cache)}\n")

    for i, entry in enumerate(entries):
        original = entry.get("Original", "")
        if not original:
            entry["Translated"] = ""
            continue

        if entry.get("Translated"):
            continue

        if original in manual:
            entry["Translated"] = manual[original]
            cache[original] = manual[original]
            done += 1
            continue

        if skip_re and skip_re.search(original):
            entry["Translated"] = original
            skipped += 1
            continue

        if len(original) < 4 and not (original and original[0].isupper()):
            entry["Translated"] = original
            skipped += 1
            continue

        if original in cache:
            entry["Translated"] = cache[original]
            done += 1
            continue

        preview = original[:70].replace('\n', '|')
        print(f"[{done+1}/{total}] {preview!r}")

        result = translate(original, cfg)
        if result:
            if len(result) < 2 and len(original) > 3:
                print(f"  Warning: response too short, keeping original")
                result = original
            elif len(result) > len(original) * 6 + 300:
                print(f"  Warning: response too long ({len(result)} chars), keeping original")
                result = original
            entry["Translated"] = result
            cache[original]     = result
            done += 1
            print(f"  >> {result[:70].replace(chr(10), '|')!r}")
        else:
            entry["Translated"] = original
            errors += 1
            print(f"  [FAIL] keeping original")

        if (i + 1) % 10 == 0:
            save_json(output_json, entries)
            save_json(cache_file, cache)
            print(f"  -- saved [{done}/{total}] --\n")

    save_json(output_json, entries)
    save_json(cache_file, cache)

    print(f"\n{'='*50}")
    print(f"Done!  Translated:{done}  Skipped:{skipped}  Errors:{errors}")
    print(f"Output: {output_json}")

if __name__ == "__main__":
    main()

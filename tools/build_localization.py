"""ACT の英語の書き出しと訳の対応表から、プラグインに埋め込む言語ごとの XML を作る。

使い方:
    python tools/build_localization.py

ACT の作者のリポジトリ（EQAditu/AdvancedCombatTracker の Localization/en-US）から、ACT が -exportcontroltext で
書き出した英語の文字列を取得し、tools/translations/<言語>.json（英語 → 訳）で置き換えたものを
src/ACTLocalizationPlus/Localization/<言語>.<画面>.xml に書き出す。訳の無い文字列は書き出さない（英語のまま表示される）。
"""
from __future__ import annotations

import json
import sys
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
UPSTREAM = ROOT / "tools" / "upstream"
TRANSLATIONS = ROOT / "tools" / "translations"
OUT = ROOT / "src" / "ACTLocalizationPlus" / "Localization"
API = "https://api.github.com/repos/EQAditu/AdvancedCombatTracker/contents/Localization/en-US"
UA = {"User-Agent": "ACT-Localization-Plus builder"}
LANGUAGES = ("ja", "ko", "zh")
# 解析データの名前（攻撃の種類・ダメージの種類・既定のエンカウント名など）は、FFXIV_ACT_Plugin や OverlayPlugin が
# 名前で探すことがあるので訳さない。ACT の作者も「解析用の文字列は解析プラグインが変えるもの」としている
EXCLUDED_KEY_PREFIXES = ("data-", "encounterData-")


def is_data_key(key: str) -> bool:
    """解析データの名前か。attackTypeTerm-all・mergedEncounterTerm-all など「〜Term-」の項目もこれにあたる。"""
    head = key.split("-", 1)[0]
    return key.startswith(EXCLUDED_KEY_PREFIXES) or head.endswith("Term")


def fetch_upstream() -> list[Path]:
    """英語の書き出しを取得する。取得済みならそれを使う（作者の版が変わったときは tools/upstream を消してやり直す）。"""
    UPSTREAM.mkdir(parents=True, exist_ok=True)
    existing = sorted(UPSTREAM.glob("*.xml"))
    if existing:
        return existing
    with urllib.request.urlopen(urllib.request.Request(API, headers=UA), timeout=60) as res:
        entries = json.load(res)
    for e in entries:
        if e["type"] == "file" and e["name"].endswith(".xml"):
            url = e["download_url"]
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as res:
                (UPSTREAM / e["name"]).write_bytes(res.read())
    return sorted(UPSTREAM.glob("*.xml"))


def build(language: str, sources: list[Path]) -> tuple[int, int]:
    table = json.loads((TRANSLATIONS / f"{language}.json").read_text(encoding="utf-8"))
    translated = total = 0
    for src in sources:
        root = ET.parse(src).getroot()
        if root.tag == "InternalStrings":
            out = ET.Element("InternalStrings")
            for s in root.iter("string"):
                if is_data_key(s.get("key", "")):
                    continue
                total += 1
                value = table.get(s.get("value"))
                if value and value != s.get("value"):
                    ET.SubElement(out, "string", key=s.get("key"), value=value)
                    translated += 1
            name = "InternalStrings"
        else:
            out = ET.Element("ControlText", Form=root.get("Form"))
            for c in root.iter("Control"):
                total += 1
                value = table.get(c.get("Text"))
                if value and value != c.get("Text"):
                    ET.SubElement(out, "Control", ControlPath=c.get("ControlPath"), UniqueName=c.get("UniqueName"), Text=value)
                    translated += 1
            name = root.get("Form")
        ET.indent(out, space="  ")
        ET.ElementTree(out).write(OUT / f"{language}.{name}.xml", encoding="utf-8", xml_declaration=True)
    return translated, total


def main() -> int:
    sources = fetch_upstream()
    if not sources:
        print("ACT の英語の書き出しを取得できませんでした。", file=sys.stderr)
        return 1
    OUT.mkdir(parents=True, exist_ok=True)
    for old in OUT.glob("*.xml"):
        old.unlink()
    for language in LANGUAGES:
        translated, total = build(language, sources)
        print(f"{language}: {translated}/{total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

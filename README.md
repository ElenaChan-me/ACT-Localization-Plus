# ACT Localization Plus

ACT（Advanced Combat Tracker）の画面を、日本語・韓国語・中国語（簡体字）で表示する ACT プラグインです。

An ACT (Advanced Combat Tracker) plugin that shows ACT's screens in Japanese, Korean or Simplified Chinese.

## 特徴 / Features

- ACT 自身のローカライズの仕組み（`ImportControlTextXML` と `ActGlobals.ActLocalization.LocalizationStrings`）を使うので、ACT の動作には手を入れません。
  Uses ACT's own localization mechanism, so ACT's behavior is not modified.
- メニュー・タブ・ボタン・設定画面・メッセージなど、ACT が翻訳用に書き出す約1,570項目のうち約1,500項目を訳しています。訳の無いものは英語のまま表示されます。
  About 1,500 of the ~1,570 strings ACT exports for localization are translated; the rest stay in English.
- 攻撃の種類やエンカウント名など、解析データの名前（`〜Term-*`・`data-*`・`encounterData-*`）は訳しません。FFXIV_ACT_Plugin や OverlayPlugin が名前で探すことがあるためです。
  Parsing/data terms are not translated, because other plugins may look them up by name.
- 表示言語は、プラグインと同じフォルダの `xivtoolhub-settings.json`（`"language": "ja" | "ko" | "zh" | "en"`）で決めます。ファイルが無いときは日本語です。
  The language is read from `xivtoolhub-settings.json` next to the DLL (Japanese when absent).

## インストール / Install

[XIVToolHub](https://github.com/ElenaChan-me/XIVToolHub) で ACT をインストールすると一緒に選ばれ、ACT に登録されます。表示言語もハブで選んだものになります。

When ACT is installed with XIVToolHub, this plugin is selected together and registered in ACT.

手で入れる場合は、リリースの `ACTLocalizationPlus.dll` を ACT の「プラグイン」タブで追加してください。画面の文字は ACT の起動中にしか置き換えられないため、追加・言語の変更のあとは ACT を再起動してください。

To install manually, add `ACTLocalizationPlus.dll` in ACT's Plugins tab and restart ACT.

## ビルド / Build

```powershell
# ACT 本体を lib に置く（配布物なのでリポジトリには入れていません）
Copy-Item "<ACT のフォルダ>\Advanced Combat Tracker.exe" lib\
# 訳の XML を作る（ACT の作者のリポジトリから英語の書き出しを取得します）
python tools/build_localization.py
dotnet test
dotnet build -c Release
```

- 訳は `tools/translations/<言語>.json`（英語 → 訳）にあります。直したら `build_localization.py` を実行し直してください。
  Translations live in `tools/translations/<lang>.json`; rerun the build script after editing.
- ACT が更新されて文字列が増えたときは、`tools/upstream` を消して `build_localization.py` を実行すると最新の書き出しを取得します。
  Delete `tools/upstream` to refetch ACT's latest exports after an ACT update.

## 権利について / Credits

ACT は EQAditu 氏の作品です。このプラグインは ACT を同梱せず、ACT のプラグイン API と、作者が公開している翻訳用の書き出しの形式を使っています。

ACT is made by EQAditu. This plugin does not include ACT; it uses ACT's plugin API and the localization export format published by its author.

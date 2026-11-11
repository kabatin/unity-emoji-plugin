# Changelog

このプロジェクトの変更履歴。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] - 2026-11-11

初回リリース。

### Added
- 実行時に `TMP_SpriteAsset` を作り、OS が描いた絵文字を TMP 自身に描かせる仕組み。
  導入するだけで全 TMP テキストに効き、最初のフレームから表示される
- 合成シーケンス（ZWJ・肌色修飾子・国旗・キーキャップ・タグ）を 1 文字として表示
- 前処理を持たないテキストへの安全網（前処理の自動付与、欠落文字の登録と作り直し）
- 独自の `ITextPreprocessor` から呼べる公開 API `NativeEmojiTextProcessor.Process`
- ネイティブ描画: iOS（UIKit）/ Android（Canvas）/ macOS Editor（AppKit）
- `Tools > Native Emoji > Regenerate Codepoint Tables`（Unicode の emoji-data.txt から判定テーブルを生成）
- `Tools > Native Emoji > Export .unitypackage`

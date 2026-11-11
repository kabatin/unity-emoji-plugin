# Native Emoji for TextMeshPro - 開発ガイド

**最終更新:** 2026-11-11
**Unity:** 6000.3.6f1（TextMeshPro は com.unity.ugui 2.0.0 に同梱）

---

## プロジェクト概要

Unity の TextMeshPro で絵文字を **OS のネイティブ API で実行時レンダリング** するプラグイン。

スプライトアセットに絵文字画像を埋め込まないのが設計の前提。Apple の絵文字は再配布が規約で
禁じられているため、画像を同梱せず、端末の OS に描かせたものをその場でテクスチャ化する。

**仕組み（実行時スプライト方式）:**

```
起動時        画像を持たない空の TMP_SpriteAsset を作り
              TMP_Settings.defaultSpriteAsset に差し替える

テキスト描画  TMP の前処理（ITextPreprocessor）で絵文字を検出
            → OS のネイティブ API で描画（PNG）
            → 実行時アトラスのマスへ GPU コピー
            → 合成シーケンスは <sprite index=N> に置き換え
            → TMP 自身がメッシュを作って描画
```

前処理は TMP がメッシュを作る直前（`OnPreRenderCanvas` → `ParseInputText` → `PreprocessText`）に
**同期で**走る。同じ Canvas 更新の中で登録が済むので、最初のフレームから絵文字が出る。

コンポーネントの追加は不要で、導入するだけで全 TMP テキストに効く。

---

## 現在の状態

### 動作確認済み

| 項目 | 状態 | 確認方法 |
|------|------|----------|
| 絵文字検出（複合シーケンス含む） | 動作 | EditMode 63 件 |
| ネイティブ描画（macOS エディタ） | 動作 | EditMode 5 件（契約テスト） |
| スプライト登録・前処理 | 動作 | EditMode 9 件 |
| 最初のフレームからの表示・入力欄 | 動作 | PlayMode 9 件 |
| iOS プレイヤースクリプトのコンパイル | 通過 | `PlayerBuildInterface.CompilePlayerScripts` |
| iOS 実機での表示 | 確認済み | 実機での目視 |
| Android | **未検証** | － |

### テストの実行方法

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity"

"$UNITY" -batchmode -projectPath UnityProject \
  -runTests -testPlatform EditMode -testResults /tmp/em.xml -logFile /tmp/em.log

"$UNITY" -batchmode -projectPath UnityProject \
  -runTests -testPlatform PlayMode -testResults /tmp/pm.xml -logFile /tmp/pm.log
```

PlayMode は `-nographics` を付けない（ネイティブ描画が AppKit を使う）。
Unity Editor でプロジェクトを開いていると起動できない。
スプライト関連のテストは TMP Essential Resources のインポートが要る（無いとスキップされる）。
ネイティブ描画を使うテストは macOS の Editor が前提。

### プレイヤービルドのコンパイル確認

**EditMode / PlayMode テストだけでは、プレイヤービルド専用の失敗を検出できない。**
`#if UNITY_EDITOR` の括り間違いはエディタ上では通ってしまい、iOS / Android のビルドだけが落ちる。

ランタイムのコードを触ったら、`PlayerBuildInterface.CompilePlayerScripts`（target iOS）を
`-executeMethod` で呼ぶ一時スクリプトで確認する。ビルドターゲットを切り替える必要はない。
`NativeEmoji.Runtime.dll` が作られて `error CS` が無ければ合格。

---

## ファイル構成

`UnityProject/Assets/NativeEmoji/` が **UPM パッケージのルート**（`com.kabatin.native-emoji`）。
開発プロジェクトの Assets 配下に置いたまま、git URL の `?path=` で直接インストールできる。

```
UnityProject/Assets/NativeEmoji/
  package.json / CHANGELOG.md / LICENSE.md
  Runtime/ … Editor/ … Plugins/ … Tests/
  Samples~/Demo/Sample.unity   ← Unity からは見えない（UPM のサンプル）
```

`Samples~` は末尾の `~` により Unity が無視する。目視確認でシーンを開くときは一時的に出す。

```bash
cp -r "UnityProject/Assets/NativeEmoji/Samples~/Demo" UnityProject/Assets/_Demo
# 確認が済んだら削除する（コミットしない）
```

配布物の作り方は [README.md](../README.md) の「開発」を参照。

### Runtime (`UnityProject/Assets/NativeEmoji/Runtime/`)

| ファイル | 役割 |
|---------|------|
| `EmojiParser.cs` | Unicode 絵文字パーサー。貪欲前方スキャンで最長一致。**Unity 非依存** |
| `EmojiCodepoints.cs` | コードポイント判定（テーブルへの委譲） |
| `EmojiCodepointTables.cs` | **生成ファイル**。emoji-data.txt から生成。手で編集しない |
| `NativeBridge.cs` | プラットフォーム別のネイティブ呼び出しと、対応環境の判定（`IsSupported`） |
| `Sprite/NativeEmojiSprite.cs` | 公開の入口（Install / Uninstall / IsInstalled、自動有効化） |
| `Sprite/NativeEmojiSpriteRegistry.cs` | 実行時 TMP_SpriteAsset とアトラス（internal） |
| `Sprite/NativeEmojiTextProcessor.cs` | 前処理の本体（公開。独自の前処理から呼べる） |
| `Sprite/NativeEmojiTextPreprocessor.cs` | 前処理を持たないテキストに付ける ITextPreprocessor（internal） |
| `Sprite/NativeEmojiTextWatcher.cs` | 安全網（前処理の付与・欠落文字の登録と作り直し）（internal） |

絵文字判定テーブルの再生成:
Unity メニューの `Tools > Native Emoji > Regenerate Codepoint Tables`
（`Editor/EmojiTableGenerator.cs`。unicode.org から emoji-data.txt を取得する）

### ネイティブプラグイン (`Plugins/`)

| ファイル | プラットフォーム |
|---------|----------------|
| `iOS/EmojiRenderer.mm` | iOS（UIKit） |
| `Android/EmojiRenderer.java` | Android（Canvas + Paint） |
| `macOS/EmojiRenderer.m` | macOS エディタ用のソース。**取り込み設定は全プラットフォーム無効** |
| `macOS/EmojiRenderer.bundle` | 上記のビルド済みユニバーサルバイナリ（Editor で使う） |

`EmojiRenderer.m` の `.meta` を全プラットフォーム無効にしてあるのは、既定のままだと
iOS 向けに有効になり、AppKit を使うソースが Xcode プロジェクトに入ってビルドが落ちるため。
**この設定を戻さないこと。** Editor 用はビルド済みの `.bundle` が担う。

macOS バンドルの再ビルド: `cd Assets/NativeEmoji/Plugins/macOS && ./build.sh`

**`build.sh` の `-fobjc-arc` は外さないこと。** 無いと MRR でビルドされ、
呼び出しごとにビットマップと PNG バッファがリークする（コードは ARC 前提で書かれている）。

**3 プラットフォームとも、要求した width × height の実寸ピクセルを返す契約。**

---

## 設計上の判断（変更する前に読む）

1. **登録は前処理の中で同期に行う。** TMP がメッシュを作る前に登録が済むので初回フレームに間に合う。
   非同期・次フレームにすると豆腐か空白が見える
2. **グリフ寸法は 64 / 64 / BearingX 0 / BearingY 48 / Advance 64、グリフ scale 1.28、
   faceInfo.pointSize 0。** pointSize 0 だと TMP は本文フォントの ascentLine を基準に大きさを決める。
   絵文字は「ascentLine × 1.28」の正方形になり、マスの解像度に左右されない。
   広く使われている 64px の絵文字スプライトシートと同じ寸法なので、シートからの差し替えでも見た目が変わらない
3. **112px で描いて中央 96px を切り出す。** OS はマスの約 79% に描く（文字サイズ = 高さ × 0.85）。
   一般的な絵文字シートの絵はマスの約 94%。7/6 倍で描くと顔の絵が約 92% になり、横長の 🤝 もはみ出さない
4. **単独のコードポイントは置き換えず unicode で登録する。**
   タグに置き換えるとフォントが持つ記号（⭐ など）まで絵文字に変わる
5. **合成シーケンスのスプライトの unicode は 0xFFFE。** TMP の unicode 検索から外れる値
6. **既定テキスト表示の記号（❤ ☀ など）を単独で登録するときは FE0F を付けて描く。**
   そのまま渡すと OS が単色の文字で描く（暗い背景で見えない）。
   この登録はフォントにグリフが無いときだけ起きる
7. **リッチテキストが無効のテキストでは合成シーケンスを構成要素に分ける。** タグが使えないため。
   ZWJ・異体字セレクタ・キーキャップ記号・タグ文字は TMP が豆腐にするので落とす
8. **描画に失敗したら登録しない。** `NativeBridge` は失敗時に不透明な灰色の四角を返すので、
   角のピクセルが透明でない画像は失敗として扱う。失敗した絵文字は覚えておき、描き直しに行かない
   （前処理はメッシュ更新のたびに走るため）
9. **アトラスへの反映は GPU 上のマス単位のコピー（`Graphics.CopyTexture`）。**
   アトラス全体（2048² / 16MB）を送り直さない。コピーは同じ形式のテクスチャ間に限られ、
   PNG の読み込み結果は ARGB32 なのでアトラスも ARGB32。CPU 側の写しは手放す（約 16MB 節約）。
   GPU コピーができない端末は CPU で書いて全体を反映する
10. **アトラスの UV はメッシュに焼かれるので、登録済みのマスは動かさない。**
    満杯（2048² を 100px 刻み = 400 個）になったら以降は登録しない（警告を 1 回出す）
11. **空の実行時アセットは、登録の前に一度 `UpdateLookupTables()` を通す。**
    版が空だと版上げ処理が走り、後から足したテーブルを消す。
    `spriteInfoList` は空のリストで用意する（null のままだと版上げ処理が落ちる）。
    `fallbackSpriteAssets` には元の既定アセットを入れ、導入先の `<sprite name=...>` を引けるままにする
12. **前処理を持たないテキストは、絵文字を含むと分かった時点で前処理を付けて
    同じ更新の中で作り直す。** 欠落文字の通知だけを頼ると、構成要素がすべて登録済みの
    合成シーケンスが分解表示のまま残る。入れ子の作り直しはフラグで防ぐ
13. **Editor では Play 終了時に `Uninstall` して TMP Settings を元に戻す。**
    TMP Settings はアセットそのもので、戻さないと破棄済みの実行時アセットを指したまま Edit Mode に残る
14. **描いた画像はアトラス（メモリ）にだけ持つ。** ディスクへのキャッシュ・送信・
    アセットへの書き出しはしない（Apple 絵文字の再配布にあたらないようにするための線。
    正式採用の前に法務の確認が要る）
15. **`TMP_InputField` の本文には前処理を付けない。** 入力欄は `characterInfo.index` を元の文字列上の
    位置として使うので、タグに置き換えるとキャレットと選択範囲がずれる
16. **OS に描かせられない環境では有効にしない（`NativeBridge.IsSupported`）。**
    既定スプライトアセットを差し替えると、描けないのに導入先の表示だけ変えてしまう
17. **RawImage を上に重ねる方式は採らない。** 大きさが本文と揃わない・最初のフレームに間に合わず
    豆腐が見える・合成文字の後ろに余白が残る・前処理を持つテキストで位置がずれる

### 絵文字判定は Unicode の原典データから生成する

`©` `❤` `☀` `🌡` のような Emoji_Presentation=No の記号は、U+FE0F が無ければ絵文字にしない。
判定は `emoji-data.txt` から生成したテーブルで行う。ブロック単位の手書き範囲に戻さないこと。
絵文字でない文字（錬金術記号・囲み英数字など）を巻き込み、
かつ Emoji_Presentation=No の絵文字を取りこぼす。

---

## 既知の制限

- 数値を差し込む `SetText(書式, float…)` と StringBuilder・char[] 版は前処理を通らず、
  未登録の絵文字は豆腐のまま
- リッチテキストが無効のテキスト、および `TMP_InputField` の入力欄では合成シーケンスが分解表示になる
- 1 セッションで 400 個まで（2048² のアトラス、96px のマス）
- 新しい絵文字の初回描画はメッシュ生成の中で同期に行うので、新しい絵文字が多い画面を
  初めて開くと一瞬重くなりうる（Editor で 20 個 約 30ms。大半は OS の初回のグリフ描画）
- Edit Mode（Prefab 編集画面など）では絵文字は出ない（実行時だけの仕組み）
- OS が描けない絵文字は OS の豆腐がそのまま登録される（描画可否の判定は未実装）
- アトラスは GPU 上に約 16MB
- Android は未検証（描画倍率も macOS でしか測っていない）
- iOS / Android の実機と macOS の Editor 以外は非対応（スタンドアロン・WebGL・Windows / Linux の Editor）

---

## 残っている作業

- [ ] Android 実機での動作確認（描画倍率の計測を含む）
- [ ] Editor の Play で Sample シーン（`Samples~/Demo`）を開いての目視確認
      （自動テストは通っているが、余白の有無や色味は人の目で見る必要がある）
- [ ] ネイティブ側から PNG ではなく RGBA を直接返す（PNG の変換と復号が消える。
      見積もりで新しい絵文字 20 個の初回が 32ms → 24ms 程度）
- [ ] 画面のデータを受け取ってから表示するまでの間に本文の絵文字を先に登録しておく API
- [ ] OS が描けない絵文字の判定（Android `Paint.hasGlyph`、iOS は CoreText）
- [ ] Asset Store 向けのパッケージ整備（デモシーン、サンプル）

---

## 参考資料

- [README.md](../README.md) - 導入手順と導入側で必要な対応
- [TextMeshPro Documentation](https://docs.unity3d.com/Packages/com.unity.textmeshpro@latest)
- [Unicode Emoji (UTS #51)](https://www.unicode.org/reports/tr51/)

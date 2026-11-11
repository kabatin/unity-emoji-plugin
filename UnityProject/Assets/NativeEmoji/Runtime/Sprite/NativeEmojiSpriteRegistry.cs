using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;

namespace NativeEmoji
{
    /// <summary>
    /// 実行時に作る TMP_SpriteAsset と、そのアトラス。
    /// OS が描いた絵文字をアトラスのマスへ書き込み、TMP のスプライトとして登録する。
    /// 絵文字の画像はビルドに含めず、描いた画像はこのアトラス（メモリ上）にだけ持つ。ディスクやサーバーへは書き出さない。
    /// </summary>
    internal static class NativeEmojiSpriteRegistry
    {
        // 合成シーケンス用のスプライトに入れる unicode。TMP の unicode 検索から外れる値（TMP_SpriteAsset.cs:184）
        public const uint SequenceUnicode = 0xFFFE;

        // グリフ寸法は、広く使われている 64px の絵文字スプライトシートと同じ値にしている。
        // faceInfo.pointSize を 0 のままにすると、TMP は本文フォントの ascentLine を基準に大きさを決める（TextMeshProUGUI.cs:2916-2921）。
        // その結果、絵文字は「本文フォントの ascentLine × 1.28」の正方形になり、マスの解像度には左右されない
        private static readonly GlyphMetrics LegacyMetrics = new(64, 64, 0, 48, 64);
        private const float LegacyGlyphScale = 1.28f;

        private const int AtlasSize = 2048;
        private const int CellPadding = 2;
        private const int CellSize = 96;
        private const int CellStride = CellSize + CellPadding * 2;
        private const int CellsPerRow = AtlasSize / CellStride;
        private const int MaxCells = CellsPerRow * CellsPerRow;

        // OS はマスの約 79% の大きさで絵文字を描く（文字サイズ = 高さ × 0.85）。一般的な絵文字シートの絵はマスの約 94%。
        // 大きめに描いて中央のマス分を切り出す。横長の絵（🤝 など）がマスからはみ出さない大きさに合わせている
        private const int RenderSize = 112;

        private static TMP_SpriteAsset s_Asset;
        private static Texture2D s_Atlas;
        private static Material s_Material;
        private static TMP_SpriteAsset s_PreviousDefaultSpriteAsset;
        private static readonly Dictionary<string, int> s_IndexByEmoji = new();
        // 描けなかった絵文字。前処理はメッシュ更新のたびに走るので、覚えておかないと毎回描き直しに行く
        private static readonly HashSet<string> s_FailedEmoji = new();
        private static bool s_AtlasDirty;
        private static bool s_UseGpuCopy;
        private static bool s_HasWarnedFull;
        private static bool s_HasWarnedFormat;

        public static bool IsInstalled => s_Asset != null;

        public static bool Install()
        {
            if (s_Asset != null)
            {
                return true;
            }

            // OS に描かせられない環境（スタンドアロン・WebGL・macOS 以外の Editor）では、既定スプライトアセットに手を付けない
            if (!NativeBridge.IsSupported)
            {
                return false;
            }

            // TMP Settings は Essential Resources に含まれるアセット。未インポートだと存在しない
            if (TMP_Settings.instance == null)
            {
                Debug.LogWarning("[NativeEmoji] TMP Settings が無いため有効にできない（TMP Essential Resources をインポートする）");
                return false;
            }

            var shader = FindSpriteShader();
            if (shader == null)
            {
                Debug.LogError("[NativeEmoji] TextMeshPro/Sprite シェーダが見つからないため、絵文字を表示できない");
                return false;
            }

            // 登録のたびにアトラス全体（16MB）を GPU へ送り直さないよう、描いた絵文字は GPU 上でマスへコピーする。
            // コピーは形式が同じテクスチャ間に限られ、PNG の読み込み結果は ARGB32 になるので、アトラスも ARGB32 にする。
            // GPU 上のコピーができない端末では、CPU 側で書き込んでアトラス全体を反映する
            s_UseGpuCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            s_Atlas = new Texture2D(AtlasSize, AtlasSize, s_UseGpuCopy ? TextureFormat.ARGB32 : TextureFormat.RGBA32, false)
            {
                name = "NativeEmojiAtlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            // マスの余白が透明でないと、隣のマスや未初期化の色がにじむ
            using (var clear = new NativeArray<byte>(AtlasSize * AtlasSize * 4, Allocator.Temp, NativeArrayOptions.ClearMemory))
            {
                s_Atlas.SetPixelData(clear, 0);
            }

            // GPU 上でコピーするなら CPU 側の写しは使わないので、反映と同時に手放す（約 16MB）
            s_Atlas.Apply(false, s_UseGpuCopy);

            s_Material = new Material(shader) { name = "NativeEmojiSprite", hideFlags = HideFlags.DontSave };
            s_Material.SetTexture(ShaderUtilities.ID_MainTex, s_Atlas);

            s_Asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            s_Asset.name = "NativeEmojiSprite";
            s_Asset.hideFlags = HideFlags.DontSave;
            s_Asset.spriteSheet = s_Atlas;
            s_Asset.material = s_Material;
            // UpdateLookupTables の版上げ処理が null のまま読むため、空で用意する
            s_Asset.spriteInfoList = new List<TMP_Sprite>();
            // 元の既定アセットに入っていた独自スプライト（<sprite name=...>）を引き続き引けるよう、フォールバックに残す
            s_PreviousDefaultSpriteAsset = TMP_Settings.defaultSpriteAsset;
            s_Asset.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
            if (s_PreviousDefaultSpriteAsset != null)
            {
                s_Asset.fallbackSpriteAssets.Add(s_PreviousDefaultSpriteAsset);
            }

            // 版が空だと版上げ処理が走り、以後に足したテーブルを消してしまうので、登録前に一度通して 1.1.0 にしておく
            s_Asset.UpdateLookupTables();

            TMP_Settings.defaultSpriteAsset = s_Asset;
            return true;
        }

        public static void Uninstall()
        {
            if (s_Asset == null)
            {
                return;
            }

            TMP_Settings.defaultSpriteAsset = s_PreviousDefaultSpriteAsset;
            DestroyObject(s_Asset);
            DestroyObject(s_Material);
            DestroyObject(s_Atlas);
            s_Asset = null;
            s_Material = null;
            s_Atlas = null;
            s_PreviousDefaultSpriteAsset = null;
            s_IndexByEmoji.Clear();
            s_FailedEmoji.Clear();
            s_AtlasDirty = false;
            s_UseGpuCopy = false;
            s_HasWarnedFull = false;
            s_HasWarnedFormat = false;
        }

        /// <summary>
        /// 絵文字を登録し、spriteCharacterTable 上の番号を返す（&lt;sprite index=N&gt; の N）。登録済みなら描き直さない。
        /// </summary>
        /// <param name="unicode">単独のコードポイントならその値（TMP の unicode 検索で引かれる）。合成シーケンスは <see cref="SequenceUnicode"/></param>
        public static bool TryGetOrRegister(string emoji, uint unicode, out int spriteIndex)
        {
            if (s_IndexByEmoji.TryGetValue(emoji, out spriteIndex))
            {
                return true;
            }

            spriteIndex = -1;
            if (s_Asset == null || s_FailedEmoji.Contains(emoji))
            {
                return false;
            }

            var cell = s_IndexByEmoji.Count;
            if (cell >= MaxCells)
            {
                if (!s_HasWarnedFull)
                {
                    s_HasWarnedFull = true;
                    Debug.LogWarning($"[NativeEmoji] アトラスが満杯（{MaxCells} 個）のため、以降の絵文字は登録しない");
                }

                return false;
            }

            var x = cell % CellsPerRow * CellStride + CellPadding;
            var y = cell / CellsPerRow * CellStride + CellPadding;
            // ❤ のような既定テキスト表示の記号を単独で登録するのは、フォントにグリフが無いときだけ。
            // そのまま OS に渡すと単色の文字で描かれる（暗い背景で見えない）ので、絵文字表示を指定して描く
            var renderText = unicode != SequenceUnicode && !EmojiCodepoints.IsDefaultPresentation((int)unicode) ? emoji + "\uFE0F" : emoji;
            if (!TryRenderInto(renderText, x, y))
            {
                s_FailedEmoji.Add(emoji);
                return false;
            }

            var glyph = new TMP_SpriteGlyph((uint)s_Asset.spriteGlyphTable.Count, LegacyMetrics, new GlyphRect(x, y, CellSize, CellSize), LegacyGlyphScale, 0);
            s_Asset.spriteGlyphTable.Add(glyph);
            var character = new TMP_SpriteCharacter(unicode, s_Asset, glyph) { name = ToSpriteName(emoji) };
            s_Asset.spriteCharacterTable.Add(character);
            // 追加したグリフとアセットの結び付け（textAsset）と unicode・名前の検索表はここで作られる
            s_Asset.UpdateLookupTables();

            spriteIndex = s_Asset.spriteCharacterTable.Count - 1;
            s_IndexByEmoji.Add(emoji, spriteIndex);
            return true;
        }

        /// <summary>
        /// CPU 側で書き込んだマスを GPU へ反映する（GPU 上でコピーできない端末だけ）。
        /// テクスチャ全体を送り直すので、登録のたびではなく 1 テキスト分の登録が済んでから呼ぶ。
        /// </summary>
        public static void ApplyIfDirty()
        {
            if (!s_AtlasDirty || s_Atlas == null)
            {
                return;
            }

            s_AtlasDirty = false;
            s_Atlas.Apply(false, false);
        }

        private static bool TryRenderInto(string emoji, int x, int y)
        {
            var png = NativeBridge.RenderEmoji(emoji, RenderSize, RenderSize);
            if (png == null || png.Length == 0)
            {
                return false;
            }

            var rendered = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!rendered.LoadImage(png) || rendered.width != RenderSize || rendered.height != RenderSize)
                {
                    return false;
                }

                // 描画に失敗するとブリッジは不透明な灰色の四角を返す（NativeBridge.CreateFallbackTexture）。
                // 正常な絵文字は中央に描かれて角が透明なので、角で見分けて登録しない
                if (rendered.GetPixel(0, 0).a > 0f)
                {
                    return false;
                }

                const int offset = (RenderSize - CellSize) / 2;
                if (s_UseGpuCopy)
                {
                    if (rendered.format != s_Atlas.format)
                    {
                        // アトラスは CPU 側の写しを手放しているので、CPU での書き込みにも切り替えられない
                        if (!s_HasWarnedFormat)
                        {
                            s_HasWarnedFormat = true;
                            Debug.LogWarning($"[NativeEmoji] 描いた絵文字の形式（{rendered.format}）がアトラス（{s_Atlas.format}）と違うため登録できない");
                        }

                        return false;
                    }

                    Graphics.CopyTexture(rendered, 0, 0, offset, offset, CellSize, CellSize, s_Atlas, 0, 0, x, y);
                    return true;
                }

                s_Atlas.SetPixels(x, y, CellSize, CellSize, rendered.GetPixels(offset, offset, CellSize, CellSize));
                s_AtlasDirty = true;
                return true;
            }
            finally
            {
                DestroyObject(rendered);
            }
        }

        // ビルドに含まれるシェーダしか見つからない。TMP Essentials の EmojiOne など、既存の既定スプライトアセットが
        // 使っているシェーダなら確実に含まれているので、そちらを優先する
        private static Shader FindSpriteShader()
        {
            var current = TMP_Settings.defaultSpriteAsset;
            if (current != null && current.material != null && current.material.shader != null)
            {
                return current.material.shader;
            }

            return Shader.Find("TextMeshPro/Sprite");
        }

        // Edit Mode（EditMode テストなど）では Destroy を呼べない
        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        // 絵文字シートの慣例に合わせて、コードポイントの 16 進を '-' でつないだ名前にする（例: 1f91d-1f3fe）
        private static string ToSpriteName(string emoji)
        {
            var codepoints = new List<string>(4);
            for (var i = 0; i < emoji.Length; i++)
            {
                var codepoint = char.ConvertToUtf32(emoji, i);
                if (codepoint > 0xFFFF)
                {
                    i++;
                }

                codepoints.Add(codepoint.ToString("x4"));
            }

            return string.Join("-", codepoints);
        }
    }
}

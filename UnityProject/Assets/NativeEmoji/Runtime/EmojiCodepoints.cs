namespace NativeEmoji
{
    /// <summary>
    /// 絵文字コードポイントの判定。
    ///
    /// EmojiParser から参照される純粋なデータ層。Unity 依存なし。
    /// 判定は Unicode Emoji 仕様（UTS #51）の 2 つの性質に対応する:
    ///
    /// - <b>Emoji</b>（絵文字になり得るか）… <see cref="IsEmojiCapable"/>
    /// - <b>Emoji_Presentation</b>（修飾子なしで既定が絵文字表示か）… <see cref="IsDefaultPresentation"/>
    ///
    /// 「© のような既定テキスト表示の記号を絵文字として差し替えてしまう」事故を防ぐため、
    /// この 2 つを分けて持つ。U+FE0F が付いて初めて絵文字になる文字を区別できる。
    ///
    /// 判定テーブルは <see cref="EmojiCodepointTables"/>（emoji-data.txt から生成）。
    /// ブロック単位の手書き範囲では、絵文字でない文字（錬金術記号や囲み英数字など）を
    /// 巻き込んだり、Emoji_Presentation=No の絵文字を取りこぼしたりするため、
    /// 原典データから生成している。
    /// </summary>
    public static class EmojiCodepoints
    {
        // ──────────────────────────────────────────────
        // 特殊コードポイント
        // ──────────────────────────────────────────────

        /// <summary>U+FE0F VARIATION SELECTOR-16（絵文字表示を指定）</summary>
        public const int VariationSelector16 = 0xFE0F;

        /// <summary>U+FE0E VARIATION SELECTOR-15（テキスト表示を指定）</summary>
        public const int VariationSelector15 = 0xFE0E;

        /// <summary>U+200D ZERO WIDTH JOINER</summary>
        public const int ZeroWidthJoiner = 0x200D;

        /// <summary>U+20E3 COMBINING ENCLOSING KEYCAP</summary>
        public const int CombiningEnclosingKeycap = 0x20E3;

        /// <summary>U+1F3F4 WAVING BLACK FLAG（タグシーケンスの基底）</summary>
        public const int WavingBlackFlag = 0x1F3F4;

        /// <summary>U+E007F CANCEL TAG（タグシーケンス終端）</summary>
        public const int TagCancel = 0xE007F;

        // ──────────────────────────────────────────────
        // Unicode プロパティ
        // ──────────────────────────────────────────────

        /// <summary>
        /// 絵文字として表示され得るコードポイントか（Unicode の Emoji プロパティ）。
        /// ここで true でも、既定がテキスト表示の文字は U+FE0F が必要
        /// （<see cref="IsDefaultPresentation"/> を併せて見る）。
        /// </summary>
        public static bool IsEmojiCapable(int cp)
        {
            return EmojiCodepointTables.HasEmojiProperty(cp);
        }

        /// <summary>
        /// 修飾子なしで既定が絵文字表示になるコードポイントか
        /// （Unicode の Emoji_Presentation プロパティ）。
        ///
        /// false の文字（© ® ❤ ☀ ✂ ↔ 🌡 🗺 等）は既定がテキスト表示であり、
        /// U+FE0F を伴わない限り絵文字として差し替えるべきではない。
        /// </summary>
        public static bool IsDefaultPresentation(int cp)
        {
            return EmojiCodepointTables.HasEmojiPresentationProperty(cp);
        }

        // ──────────────────────────────────────────────
        // シーケンス構成要素
        // ──────────────────────────────────────────────

        /// <summary>
        /// Regional Indicator（国旗を構成する記号）か判定する。U+1F1E6〜U+1F1FF。
        /// </summary>
        public static bool IsRegionalIndicator(int cp)
        {
            return cp >= 0x1F1E6 && cp <= 0x1F1FF;
        }

        /// <summary>
        /// 肌色修飾子か判定する。U+1F3FB〜U+1F3FF。
        /// </summary>
        public static bool IsSkinToneModifier(int cp)
        {
            return cp >= 0x1F3FB && cp <= 0x1F3FF;
        }

        /// <summary>
        /// キーキャップシーケンスの基底文字か判定する。0-9、#、*。
        /// </summary>
        public static bool IsKeycapBase(int cp)
        {
            return (cp >= 0x0030 && cp <= 0x0039) || cp == 0x0023 || cp == 0x002A;
        }

        /// <summary>
        /// タグシーケンスを構成するタグ文字か判定する。U+E0020〜U+E007E。
        /// </summary>
        public static bool IsTagCharacter(int cp)
        {
            return cp >= 0xE0020 && cp <= 0xE007E;
        }
    }
}

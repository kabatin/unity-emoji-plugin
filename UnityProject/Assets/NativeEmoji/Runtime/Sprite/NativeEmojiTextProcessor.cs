using System.Text;

namespace NativeEmoji
{
    /// <summary>
    /// TMP の前処理で、テキスト中の絵文字を実行時スプライトとして登録する。
    /// TMP がメッシュを作る前（同じ Canvas 更新の中）に登録するので、最初のフレームから絵文字が出る。
    /// 独自の ITextPreprocessor を使っているテキストは、その前処理の中からこのメソッドを呼ぶ（README 参照）。
    /// </summary>
    public static class NativeEmojiTextProcessor
    {
        private const int ZeroWidthJoiner = 0x200D;
        private const int CombiningEnclosingKeycap = 0x20E3;

        private static readonly StringBuilder s_Builder = new();

        /// <param name="richText">false のときは &lt;sprite&gt; タグを使えないので、合成シーケンスを構成要素に分けて表示する</param>
        public static string Process(string text, bool richText)
        {
            if (string.IsNullOrEmpty(text) || !NativeEmojiSpriteRegistry.IsInstalled || !MayContainEmoji(text))
            {
                return text;
            }

            var results = EmojiParser.ParseAll(text);
            if (results.Count == 0)
            {
                return text;
            }

            s_Builder.Clear();
            var cursor = 0;
            foreach (var result in results)
            {
                s_Builder.Append(text, cursor, result.startIndex - cursor);
                AppendEmoji(result.emojiString, richText);
                cursor = result.EndIndex;
            }

            s_Builder.Append(text, cursor, text.Length - cursor);
            NativeEmojiSpriteRegistry.ApplyIfDirty();
            return s_Builder.ToString();
        }

        private static void AppendEmoji(string emoji, bool richText)
        {
            var first = char.ConvertToUtf32(emoji, 0);
            if (emoji.Length == (first > 0xFFFF ? 2 : 1))
            {
                // 単独のコードポイントは置き換えない。フォントにグリフがあればフォント、無ければ TMP が既定の
                // スプライトアセットを unicode で引く（TMP 標準の優先順。⭐ のようにフォントが持つ記号を絵文字に変えない）
                NativeEmojiSpriteRegistry.TryGetOrRegister(emoji, (uint)first, out _);
                s_Builder.Append(emoji);
                return;
            }

            if (richText && NativeEmojiSpriteRegistry.TryGetOrRegister(emoji, NativeEmojiSpriteRegistry.SequenceUnicode, out var spriteIndex))
            {
                s_Builder.Append("<sprite index=").Append(spriteIndex).Append('>');
                return;
            }

            AppendDecomposed(emoji);
        }

        // リッチテキストが無効だと <sprite> タグを使えない。構成要素を単独の絵文字として並べ、
        // TMP が豆腐にする結合用の文字（ZWJ・異体字セレクタ・キーキャップ記号・タグ文字）は落とす
        private static void AppendDecomposed(string emoji)
        {
            for (var i = 0; i < emoji.Length; i++)
            {
                var codepoint = char.ConvertToUtf32(emoji, i);
                var length = codepoint > 0xFFFF ? 2 : 1;
                if (!IsJoiningCharacter(codepoint))
                {
                    var piece = emoji.Substring(i, length);
                    // 異体字セレクタを落とすと ❤ のような既定テキスト表示の記号も残るが、フォントにグリフが無い場合だけスプライトが使われる
                    if (EmojiCodepoints.IsEmojiCapable(codepoint) && !EmojiCodepoints.IsKeycapBase(codepoint))
                    {
                        NativeEmojiSpriteRegistry.TryGetOrRegister(piece, (uint)codepoint, out _);
                    }

                    s_Builder.Append(piece);
                }

                i += length - 1;
            }
        }

        private static bool IsJoiningCharacter(int codepoint)
        {
            return codepoint == ZeroWidthJoiner
                   || codepoint == CombiningEnclosingKeycap
                   || codepoint is >= 0xFE00 and <= 0xFE0F
                   || EmojiCodepoints.IsTagCharacter(codepoint);
        }

        // ほとんどのテキストは絵文字を含まないので、解析の前に候補の文字があるかだけを見る。
        // 前処理は ForceMeshUpdate のたびに呼ばれる。… → ※ ★ ♪ ① のような普通の記号で解析を走らせないよう、
        // Unicode の Emoji プロパティを持つ文字（キーキャップの土台になる数字・# * を除く）と結合用の文字だけを候補にする
        internal static bool MayContainEmoji(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var c in text)
            {
                if (c < '\u00A9')
                {
                    continue;
                }

                if (char.IsSurrogate(c)
                    || c is >= '\uFE00' and <= '\uFE0F'
                    || c == '\u20E3'
                    || EmojiCodepoints.IsEmojiCapable(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

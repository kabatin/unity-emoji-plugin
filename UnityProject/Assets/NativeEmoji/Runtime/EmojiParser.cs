using System.Collections.Generic;

namespace NativeEmoji
{
    /// <summary>
    /// Unicode絵文字パーサー
    ///
    /// すべての絵文字シーケンスタイプを正しく解析する：
    /// - 単一コードポイント絵文字（U+1F600 等）
    /// - 異体字セレクタシーケンス（❤️ = U+2764 + U+FE0F）
    /// - 肌色修飾子（👍🏽 = U+1F44D + U+1F3FD）
    /// - ZWJシーケンス（👨‍👩‍👧‍👦 = 複数絵文字 + U+200D で結合）
    /// - 国旗シーケンス（🇯🇵 = Regional Indicator ペア）
    /// - キーキャップシーケンス（1️⃣ = 数字 + U+FE0F + U+20E3）
    /// - タグシーケンス（🏴󠁧󠁢󠁳󠁣󠁴󠁿 = U+1F3F4 + タグ文字列）
    ///
    /// 貪欲前方スキャンアルゴリズムにより、最長一致でシーケンスを検出する。
    /// 判定テーブルは <see cref="EmojiCodepoints"/>。Unity依存なし（純粋なC#文字列処理）。
    ///
    /// <para>
    /// 既定では Unicode の Emoji_Presentation に従い、既定がテキスト表示の記号
    /// （© ® ☀ ✂ ↔ 等）は U+FE0F を伴う場合のみ絵文字として扱う。
    /// 「© 2026」の © がネイティブ絵文字に差し替わるのを防ぐための既定値。
    /// 素の ❤ や ☀ も絵文字として扱いたい場合は includeTextPresentationSymbols を true にする。
    /// U+FE0E（テキスト表示指定）が続く場合は、常に絵文字として扱わない。
    /// </para>
    /// </summary>
    public static class EmojiParser
    {
        // ──────────────────────────────────────────────
        // 解析結果
        // ──────────────────────────────────────────────

        /// <summary>
        /// パース結果。元テキスト中での絵文字位置と長さを保持する。
        /// </summary>
        public struct ParseResult
        {
            /// <summary>絵文字文字列（サロゲートペア・ZWJ等を含む完全な文字列）</summary>
            public string emojiString;

            /// <summary>元テキスト中の開始インデックス（UTF-16単位）</summary>
            public int startIndex;

            /// <summary>絵文字シーケンスのUTF-16長</summary>
            public int utf16Length;

            /// <summary>元テキスト中の終端インデックス（この位置は含まない）</summary>
            public int EndIndex => startIndex + utf16Length;
        }

        // ──────────────────────────────────────────────
        // 公開API
        // ──────────────────────────────────────────────

        /// <summary>
        /// テキスト全体から絵文字を全件検出する。
        /// </summary>
        /// <param name="text">解析対象テキスト</param>
        /// <param name="includeTextPresentationSymbols">
        /// true の場合、U+FE0F を伴わない既定テキスト表示の記号（© ❤ ☀ 等）も絵文字として扱う
        /// </param>
        /// <returns>検出した絵文字のリスト（出現順）</returns>
        public static List<ParseResult> ParseAll(string text, bool includeTextPresentationSymbols = false)
        {
            var results = new List<ParseResult>();
            if (string.IsNullOrEmpty(text))
                return results;

            int index = 0;
            while (index < text.Length)
            {
                ParseResult? result = ParseNext(text, index, includeTextPresentationSymbols);
                if (result.HasValue)
                {
                    results.Add(result.Value);
                    index = result.Value.EndIndex;
                }
                else
                {
                    // 絵文字でなければ次のコードポイントへ進む（サロゲートペアを考慮）
                    ReadCodepoint(text, index, out int skipLen);
                    index += skipLen > 0 ? skipLen : 1;
                }
            }

            return results;
        }

        /// <summary>
        /// 指定位置から次の絵文字シーケンスを解析する。
        /// 貪欲前方スキャンにより最長一致を返す。
        /// </summary>
        /// <param name="text">解析対象テキスト</param>
        /// <param name="startIndex">走査開始位置（UTF-16単位）</param>
        /// <param name="includeTextPresentationSymbols">
        /// true の場合、U+FE0F を伴わない既定テキスト表示の記号も絵文字として扱う
        /// </param>
        /// <returns>絵文字が見つかればParseResult、なければnull</returns>
        public static ParseResult? ParseNext(string text, int startIndex,
                                             bool includeTextPresentationSymbols = false)
        {
            if (string.IsNullOrEmpty(text) || startIndex < 0 || startIndex >= text.Length)
                return null;

            int cp = ReadCodepoint(text, startIndex, out int cpLen);
            int afterBase = startIndex + cpLen;

            // ── キーキャップシーケンス（1️⃣ 等）──
            // 基底文字は数字・#・* なので、キーキャップとして成立する場合のみ絵文字
            if (EmojiCodepoints.IsKeycapBase(cp))
            {
                int keycapLen = TryParseKeycap(text, afterBase, cpLen);
                return keycapLen > 0 ? MakeResult(text, startIndex, keycapLen) : (ParseResult?)null;
            }

            // ── 国旗（Regional Indicator ペア）──
            if (EmojiCodepoints.IsRegionalIndicator(cp))
            {
                if (afterBase < text.Length)
                {
                    int cp2 = ReadCodepoint(text, afterBase, out int cp2Len);
                    if (EmojiCodepoints.IsRegionalIndicator(cp2))
                        return MakeResult(text, startIndex, cpLen + cp2Len);
                }

                // 単独のRegional Indicatorは絵文字としない
                return null;
            }

            // ── タグシーケンス（🏴󠁳󠁣󠁴󠁿 等）──
            if (cp == EmojiCodepoints.WavingBlackFlag)
            {
                int tagLen = TryParseTagSequence(text, afterBase, cpLen);
                if (tagLen > 0)
                    return MakeResult(text, startIndex, tagLen);

                // タグが続かない場合は通常の絵文字（🏴）として処理を続ける
            }

            // ── 絵文字になり得るコードポイントか ──
            if (!EmojiCodepoints.IsEmojiCapable(cp))
                return null;

            // ── 表示形式の判定 ──
            int following = afterBase < text.Length
                ? ReadCodepoint(text, afterBase, out _)
                : -1;

            // U+FE0E が続く場合はテキスト表示の明示指定なので絵文字としない
            if (following == EmojiCodepoints.VariationSelector15)
                return null;

            // 既定がテキスト表示の記号は U+FE0F を伴う場合のみ絵文字として扱う
            if (!EmojiCodepoints.IsDefaultPresentation(cp) &&
                following != EmojiCodepoints.VariationSelector16 &&
                !includeTextPresentationSymbols)
            {
                return null;
            }

            // ── 貪欲前方スキャン ──
            // 基底絵文字が確定したので、後続の修飾子とZWJシーケンスを消費する
            int totalLen = ConsumeModifiers(text, startIndex, cpLen);
            totalLen = ConsumeZWJSequence(text, startIndex, totalLen);

            return MakeResult(text, startIndex, totalLen);
        }

        /// <summary>
        /// 指定位置のコードポイントを読み取る。サロゲートペアを正しく処理する。
        /// </summary>
        /// <param name="text">テキスト</param>
        /// <param name="index">読み取り位置</param>
        /// <param name="charLength">消費したchar数（1または2。範囲外なら0）</param>
        /// <returns>Unicodeコードポイント（範囲外なら-1）</returns>
        public static int ReadCodepoint(string text, int index, out int charLength)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
            {
                charLength = 0;
                return -1;
            }

            char c = text[index];
            if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                charLength = 2;
                return char.ConvertToUtf32(c, text[index + 1]);
            }

            charLength = 1;
            return c;
        }

        // ──────────────────────────────────────────────
        // シーケンス解析（内部）
        // ──────────────────────────────────────────────

        /// <summary>
        /// キーキャップシーケンスの解析を試みる。
        /// 基底文字の後に FE0F（任意）+ 20E3 が続くか確認する。
        /// </summary>
        /// <param name="text">テキスト</param>
        /// <param name="afterBase">基底文字の直後の位置</param>
        /// <param name="baseLen">基底文字のchar長</param>
        /// <returns>成功時はシーケンス全体のchar長、失敗時は0</returns>
        private static int TryParseKeycap(string text, int afterBase, int baseLen)
        {
            int pos = afterBase;
            int len = baseLen;

            // FE0F（任意）
            int cp = ReadCodepoint(text, pos, out int cpLen);
            if (cp == EmojiCodepoints.VariationSelector16)
            {
                pos += cpLen;
                len += cpLen;
            }

            // 20E3（必須）
            cp = ReadCodepoint(text, pos, out cpLen);
            if (cp == EmojiCodepoints.CombiningEnclosingKeycap)
                return len + cpLen;

            return 0;
        }

        /// <summary>
        /// タグシーケンスの解析を試みる。
        /// U+1F3F4 の後にタグ文字（U+E0020-U+E007E）が1つ以上続き、
        /// U+E007F（CANCEL TAG）で終端するか確認する。
        /// </summary>
        /// <param name="text">テキスト</param>
        /// <param name="afterBase">U+1F3F4の直後の位置</param>
        /// <param name="baseLen">U+1F3F4のchar長</param>
        /// <returns>成功時はシーケンス全体のchar長、失敗時は0</returns>
        private static int TryParseTagSequence(string text, int afterBase, int baseLen)
        {
            int pos = afterBase;
            int len = baseLen;
            int tagCount = 0;

            while (pos < text.Length)
            {
                int cp = ReadCodepoint(text, pos, out int cpLen);

                if (EmojiCodepoints.IsTagCharacter(cp))
                {
                    pos += cpLen;
                    len += cpLen;
                    tagCount++;
                    continue;
                }

                // 終端タグ：タグ文字が1つ以上あれば有効
                if (cp == EmojiCodepoints.TagCancel)
                    return tagCount > 0 ? len + cpLen : 0;

                // タグ文字でも終端でもない → タグシーケンスではない
                return 0;
            }

            // テキスト終端に達したが終端タグがない → 無効
            return 0;
        }

        /// <summary>
        /// 基底絵文字の後続修飾子を消費する。
        /// 異体字セレクタ（FE0F）と肌色修飾子を処理する。
        /// </summary>
        /// <param name="text">テキスト</param>
        /// <param name="seqStart">シーケンス開始位置</param>
        /// <param name="currentLen">現在のシーケンス長</param>
        /// <returns>修飾子消費後のシーケンス長</returns>
        private static int ConsumeModifiers(string text, int seqStart, int currentLen)
        {
            // 順序を問わず、異体字セレクタと肌色修飾子が続く限り取り込む。
            // 👍🏽︎ のように肌色の後ろに FE0F が来る並びもあるため、
            // 片方ずつ 1 回だけ見る実装だと utf16Length が実際の長さと食い違う。
            while (true)
            {
                int pos = seqStart + currentLen;
                int cp = ReadCodepoint(text, pos, out int cpLen);

                if (cp != EmojiCodepoints.VariationSelector16 &&
                    !EmojiCodepoints.IsSkinToneModifier(cp))
                {
                    break;
                }

                currentLen += cpLen;
            }

            return currentLen;
        }

        /// <summary>
        /// ZWJ（U+200D）で連結された後続の絵文字シーケンスを貪欲に消費する。
        /// ZWJ + 絵文字 + 修飾子 のパターンを繰り返し検出する。
        ///
        /// ZWJ の後続は結合要素なので、既定の表示形式（Emoji_Presentation）は問わない。
        /// 👨‍⚕ のように U+FE0F を伴わない結合も有効なシーケンスとして扱う。
        /// </summary>
        /// <param name="text">テキスト</param>
        /// <param name="seqStart">シーケンス開始位置</param>
        /// <param name="currentLen">現在のシーケンス長</param>
        /// <returns>ZWJ消費後のシーケンス長</returns>
        private static int ConsumeZWJSequence(string text, int seqStart, int currentLen)
        {
            while (true)
            {
                int pos = seqStart + currentLen;

                // ZWJ があるか
                int zwjCp = ReadCodepoint(text, pos, out int zwjLen);
                if (zwjCp != EmojiCodepoints.ZeroWidthJoiner)
                    break;

                // ZWJ の後に絵文字が続くか
                int afterZwj = pos + zwjLen;
                int nextCp = ReadCodepoint(text, afterZwj, out int nextLen);
                if (nextCp < 0)
                    break;

                if (!EmojiCodepoints.IsEmojiCapable(nextCp))
                    break;

                // ZWJ + 次の絵文字を消費し、その修飾子も続けて消費する
                currentLen += zwjLen + nextLen;
                currentLen = ConsumeModifiers(text, seqStart, currentLen);
            }

            return currentLen;
        }

        // ──────────────────────────────────────────────
        // ユーティリティ
        // ──────────────────────────────────────────────

        /// <summary>
        /// ParseResultを生成するヘルパー。
        /// </summary>
        private static ParseResult MakeResult(string text, int startIndex, int length)
        {
            return new ParseResult
            {
                emojiString = text.Substring(startIndex, length),
                startIndex = startIndex,
                utf16Length = length
            };
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace NativeEmoji.Tests.EditMode
{
    /// <summary>
    /// EmojiParser の EditMode ユニットテスト
    /// 全絵文字シーケンスタイプの解析精度を検証する
    /// </summary>
    [TestFixture]
    public class EmojiParserTests
    {
        // ──────────────────────────────────────────────
        // ParseAll: 基本テスト
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_EmptyString_ReturnsEmpty()
        {
            var results = EmojiParser.ParseAll("");
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseAll_NullString_ReturnsEmpty()
        {
            var results = EmojiParser.ParseAll(null);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseAll_PlainText_ReturnsEmpty()
        {
            var results = EmojiParser.ParseAll("Hello World");
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseAll_JapaneseText_ReturnsEmpty()
        {
            var results = EmojiParser.ParseAll("こんにちは世界");
            Assert.AreEqual(0, results.Count);
        }

        // ──────────────────────────────────────────────
        // 単一コードポイント絵文字
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_SingleEmoji_Grinning()
        {
            // 😀 = U+1F600 (サロゲートペア: 2 char)
            var results = EmojiParser.ParseAll("\U0001F600");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("\U0001F600", results[0].emojiString);
            Assert.AreEqual(0, results[0].startIndex);
            Assert.AreEqual(2, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_SingleEmoji_Heart_WithoutVS16_NotEmojiByDefault()
        {
            // ❤ = U+2764 は Emoji_Presentation=No。VS16 が無ければ既定ではテキスト表示
            var results = EmojiParser.ParseAll("\u2764");
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseAll_SingleEmoji_Heart_WithoutVS16_OptIn()
        {
            // includeTextPresentationSymbols=true なら素の ❤ も絵文字として扱う
            var results = EmojiParser.ParseAll("\u2764", includeTextPresentationSymbols: true);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("\u2764", results[0].emojiString);
            Assert.AreEqual(0, results[0].startIndex);
            Assert.AreEqual(1, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_SingleEmoji_Rocket()
        {
            // 🚀 = U+1F680
            var results = EmojiParser.ParseAll("\U0001F680");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("\U0001F680", results[0].emojiString);
            Assert.AreEqual(2, results[0].utf16Length);
        }

        // ──────────────────────────────────────────────
        // バリエーションセレクタ付き (VS16 = U+FE0F)
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_VariationSelector_HeartWithVS16()
        {
            // ❤️ = U+2764 + U+FE0F
            var results = EmojiParser.ParseAll("\u2764\uFE0F");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("\u2764\uFE0F", results[0].emojiString);
            Assert.AreEqual(0, results[0].startIndex);
            Assert.AreEqual(2, results[0].utf16Length); // 1 (heart) + 1 (VS16)
        }

        [Test]
        public void ParseAll_VariationSelector_StarWithVS16()
        {
            // ⭐ = U+2B50 + U+FE0F
            var results = EmojiParser.ParseAll("\u2B50\uFE0F");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(2, results[0].utf16Length);
        }

        // ──────────────────────────────────────────────
        // 肌色修飾子 (U+1F3FB - U+1F3FF)
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_SkinTone_ThumbsUpMedium()
        {
            // 👍🏽 = U+1F44D + U+1F3FD
            string emoji = "\U0001F44D\U0001F3FD";
            var results = EmojiParser.ParseAll(emoji);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(emoji, results[0].emojiString);
            Assert.AreEqual(4, results[0].utf16Length); // 2 (thumbsup) + 2 (skin tone)
        }

        [Test]
        public void ParseAll_SkinTone_WavingHandLight()
        {
            // 👋🏻 = U+1F44B + U+1F3FB
            string emoji = "\U0001F44B\U0001F3FB";
            var results = EmojiParser.ParseAll(emoji);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(emoji, results[0].emojiString);
            Assert.AreEqual(4, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_SkinTone_WavingHandDark()
        {
            // 👋🏿 = U+1F44B + U+1F3FF
            string emoji = "\U0001F44B\U0001F3FF";
            var results = EmojiParser.ParseAll(emoji);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(emoji, results[0].emojiString);
            Assert.AreEqual(4, results[0].utf16Length);
        }

        // ──────────────────────────────────────────────
        // ZWJシーケンス (U+200D)
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_ZWJ_Family()
        {
            // 👨‍👩‍👧‍👦 = U+1F468 + ZWJ + U+1F469 + ZWJ + U+1F467 + ZWJ + U+1F466
            string family = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";
            var results = EmojiParser.ParseAll(family);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(family, results[0].emojiString);
            Assert.AreEqual(family.Length, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_ZWJ_Couple()
        {
            // 👨‍❤️‍👨 = U+1F468 + ZWJ + U+2764 + VS16 + ZWJ + U+1F468
            string couple = "\U0001F468\u200D\u2764\uFE0F\u200D\U0001F468";
            var results = EmojiParser.ParseAll(couple);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(couple, results[0].emojiString);
        }

        [Test]
        public void ParseAll_ZWJ_PilotWoman()
        {
            // 👩‍✈️ = U+1F469 + ZWJ + U+2708 + VS16
            string pilot = "\U0001F469\u200D\u2708\uFE0F";
            var results = EmojiParser.ParseAll(pilot);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(pilot, results[0].emojiString);
        }

        // ──────────────────────────────────────────────
        // 国旗シーケンス (Regional Indicator ペア)
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_Flag_Japan()
        {
            // 🇯🇵 = U+1F1EF + U+1F1F5
            string flag = "\U0001F1EF\U0001F1F5";
            var results = EmojiParser.ParseAll(flag);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(flag, results[0].emojiString);
            Assert.AreEqual(4, results[0].utf16Length); // 2 + 2 (サロゲートペア×2)
        }

        [Test]
        public void ParseAll_Flag_US()
        {
            // 🇺🇸 = U+1F1FA + U+1F1F8
            string flag = "\U0001F1FA\U0001F1F8";
            var results = EmojiParser.ParseAll(flag);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(flag, results[0].emojiString);
            Assert.AreEqual(4, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_Flag_SingleRegionalIndicator_NotEmoji()
        {
            // 単独のRegional Indicatorは絵文字として扱わない
            string single = "\U0001F1EF"; // J だけ
            var results = EmojiParser.ParseAll(single);
            Assert.AreEqual(0, results.Count);
        }

        // ──────────────────────────────────────────────
        // キーキャップシーケンス
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_Keycap_One()
        {
            // 1️⃣ = '1' + U+FE0F + U+20E3
            string keycap = "1\uFE0F\u20E3";
            var results = EmojiParser.ParseAll(keycap);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(keycap, results[0].emojiString);
            Assert.AreEqual(3, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_Keycap_Hash()
        {
            // #️⃣ = '#' + U+FE0F + U+20E3
            string keycap = "#\uFE0F\u20E3";
            var results = EmojiParser.ParseAll(keycap);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(keycap, results[0].emojiString);
            Assert.AreEqual(3, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_Keycap_Asterisk()
        {
            // *️⃣ = '*' + U+FE0F + U+20E3
            string keycap = "*\uFE0F\u20E3";
            var results = EmojiParser.ParseAll(keycap);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(keycap, results[0].emojiString);
            Assert.AreEqual(3, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_Keycap_WithoutVS16()
        {
            // 1⃣ = '1' + U+20E3 (VS16なし)
            string keycap = "1\u20E3";
            var results = EmojiParser.ParseAll(keycap);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(keycap, results[0].emojiString);
            Assert.AreEqual(2, results[0].utf16Length);
        }

        // ──────────────────────────────────────────────
        // タグシーケンス
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_TagSequence_Scotland()
        {
            // 🏴󠁧󠁢󠁳󠁣󠁴󠁿 = U+1F3F4 + タグ文字 (gbsct) + U+E007F
            string scotland = "\U0001F3F4\U000E0067\U000E0062\U000E0073\U000E0063\U000E0074\U000E007F";
            var results = EmojiParser.ParseAll(scotland);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(scotland, results[0].emojiString);
        }

        [Test]
        public void ParseAll_TagSequence_England()
        {
            // 🏴󠁧󠁢󠁥󠁮󠁧󠁿 = U+1F3F4 + タグ文字 (gbeng) + U+E007F
            string england = "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F";
            var results = EmojiParser.ParseAll(england);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(england, results[0].emojiString);
        }

        // ──────────────────────────────────────────────
        // 混合テキスト
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_MixedText_HelloWorld()
        {
            // "Hello😀World🇯🇵!"
            string text = "Hello\U0001F600World\U0001F1EF\U0001F1F5!";
            var results = EmojiParser.ParseAll(text);
            Assert.AreEqual(2, results.Count);

            // 😀
            Assert.AreEqual("\U0001F600", results[0].emojiString);
            Assert.AreEqual(5, results[0].startIndex); // "Hello" = 5 chars
            Assert.AreEqual(2, results[0].utf16Length);

            // 🇯🇵
            Assert.AreEqual("\U0001F1EF\U0001F1F5", results[1].emojiString);
            Assert.AreEqual(5 + 2 + 5, results[1].startIndex); // "Hello" + 😀(2) + "World"
            Assert.AreEqual(4, results[1].utf16Length);
        }

        [Test]
        public void ParseAll_MixedText_JapaneseWithEmoji()
        {
            string text = "今日は\U0001F600天気がいい\u2600\uFE0F";
            var results = EmojiParser.ParseAll(text);
            Assert.AreEqual(2, results.Count);

            Assert.AreEqual("\U0001F600", results[0].emojiString);
            Assert.AreEqual(3, results[0].startIndex); // "今日は" = 3 chars

            Assert.AreEqual("\u2600\uFE0F", results[1].emojiString);
        }

        // ──────────────────────────────────────────────
        // 連続絵文字
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_ConsecutiveEmojis_Three()
        {
            // 😀😀😀
            string text = "\U0001F600\U0001F600\U0001F600";
            var results = EmojiParser.ParseAll(text);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(0, results[0].startIndex);
            Assert.AreEqual(2, results[1].startIndex);
            Assert.AreEqual(4, results[2].startIndex);
        }

        [Test]
        public void ParseAll_ConsecutiveEmojis_DifferentTypes()
        {
            // 😀❤️🚀（❤ には VS16 を付ける）
            string text = "\U0001F600\u2764\uFE0F\U0001F680";
            var results = EmojiParser.ParseAll(text);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("\U0001F600", results[0].emojiString);
            Assert.AreEqual("\u2764\uFE0F", results[1].emojiString);
            Assert.AreEqual("\U0001F680", results[2].emojiString);
        }

        // ──────────────────────────────────────────────
        // エッジケース
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_CopyrightSign_WithoutVS16_NotEmoji()
        {
            // © = U+00A9 は既定テキスト表示。「© 2026」が絵文字に化けてはいけない
            var results = EmojiParser.ParseAll("\u00A9 2026 Example");
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseAll_CopyrightSign_WithVS16_IsEmoji()
        {
            // ©️ = U+00A9 + VS16 なら絵文字
            var results = EmojiParser.ParseAll("\u00A9\uFE0F");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("\u00A9\uFE0F", results[0].emojiString);
            Assert.AreEqual(2, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_RegisteredSign_RequiresVS16()
        {
            Assert.AreEqual(0, EmojiParser.ParseAll("\u00AE").Count);
            Assert.AreEqual(1, EmojiParser.ParseAll("\u00AE\uFE0F").Count);
        }

        [Test]
        public void ParseAll_DoubleExclamation_RequiresVS16()
        {
            Assert.AreEqual(0, EmojiParser.ParseAll("\u203C").Count);
            Assert.AreEqual(1, EmojiParser.ParseAll("\u203C\uFE0F").Count);
        }

        [Test]
        public void ParseAll_NumbersWithoutKeycap_NotEmoji()
        {
            // 普通の数字はキーキャップシーケンスではない
            var results = EmojiParser.ParseAll("12345");
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseAll_HashWithoutKeycap_NotEmoji()
        {
            // '#'だけはキーキャップではない
            var results = EmojiParser.ParseAll("#tag");
            Assert.AreEqual(0, results.Count);
        }

        // ──────────────────────────────────────────────
        // ReadCodepoint
        // ──────────────────────────────────────────────

        [Test]
        public void ReadCodepoint_BMP()
        {
            int cp = EmojiParser.ReadCodepoint("A", 0, out int len);
            Assert.AreEqual('A', cp);
            Assert.AreEqual(1, len);
        }

        [Test]
        public void ReadCodepoint_SurrogatePair()
        {
            // 😀 = U+1F600
            string emoji = "\U0001F600";
            int cp = EmojiParser.ReadCodepoint(emoji, 0, out int len);
            Assert.AreEqual(0x1F600, cp);
            Assert.AreEqual(2, len);
        }

        [Test]
        public void ReadCodepoint_OutOfBounds()
        {
            int cp = EmojiParser.ReadCodepoint("A", 5, out int len);
            Assert.AreEqual(-1, cp);
            Assert.AreEqual(0, len);
        }

        // ──────────────────────────────────────────────
        // ParseNext
        // ──────────────────────────────────────────────

        [Test]
        public void ParseNext_NullString_ReturnsNull()
        {
            var result = EmojiParser.ParseNext(null, 0);
            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void ParseNext_IndexOutOfBounds_ReturnsNull()
        {
            var result = EmojiParser.ParseNext("hello", 100);
            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void ParseNext_NonEmoji_ReturnsNull()
        {
            var result = EmojiParser.ParseNext("hello", 0);
            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void ParseNext_EmojiAtStart()
        {
            var result = EmojiParser.ParseNext("\U0001F600hello", 0);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("\U0001F600", result.Value.emojiString);
            Assert.AreEqual(0, result.Value.startIndex);
            Assert.AreEqual(2, result.Value.utf16Length);
        }

        [Test]
        public void ParseNext_EmojiAtOffset()
        {
            string text = "Hello\U0001F600";
            var result = EmojiParser.ParseNext(text, 5);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("\U0001F600", result.Value.emojiString);
            Assert.AreEqual(5, result.Value.startIndex);
        }

        // ──────────────────────────────────────────────
        // 複合テスト：実際の使用パターン
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_RealisticChatMessage()
        {
            // チャットメッセージの典型パターン
            string text = "お疲れ様です\U0001F600\U0001F44D\U0001F3FD 明日もよろしく\U0001F1EF\U0001F1F5";
            var results = EmojiParser.ParseAll(text);

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("\U0001F600", results[0].emojiString);             // 😀
            Assert.AreEqual("\U0001F44D\U0001F3FD", results[1].emojiString);   // 👍🏽
            Assert.AreEqual("\U0001F1EF\U0001F1F5", results[2].emojiString);   // 🇯🇵
        }

        [Test]
        public void ParseAll_AllSequenceTypes()
        {
            // すべてのシーケンスタイプを含むテキスト
            string text =
                "\U0001F600" +                              // 単一コードポイント 😀
                "\u2764\uFE0F" +                            // VS16付き ❤️
                "\U0001F44D\U0001F3FD" +                    // 肌色修飾子 👍🏽
                "1\uFE0F\u20E3" +                           // キーキャップ 1️⃣
                "\U0001F1EF\U0001F1F5";                     // 国旗 🇯🇵

            var results = EmojiParser.ParseAll(text);
            Assert.AreEqual(5, results.Count);

            Assert.AreEqual("\U0001F600", results[0].emojiString);
            Assert.AreEqual("\u2764\uFE0F", results[1].emojiString);
            Assert.AreEqual("\U0001F44D\U0001F3FD", results[2].emojiString);
            Assert.AreEqual("1\uFE0F\u20E3", results[3].emojiString);
            Assert.AreEqual("\U0001F1EF\U0001F1F5", results[4].emojiString);
        }

        [Test]
        public void ParseAll_StartAndEndIndices_Consecutive()
        {
            // 連続する絵文字のインデックスが正確か検証
            string text = "\U0001F600\u2764\uFE0F\U0001F680";
            var results = EmojiParser.ParseAll(text);

            Assert.AreEqual(3, results.Count);

            // 😀: startIndex=0, length=2
            Assert.AreEqual(0, results[0].startIndex);
            Assert.AreEqual(2, results[0].utf16Length);

            // ❤️: startIndex=2, length=2（本体 + VS16）
            Assert.AreEqual(2, results[1].startIndex);
            Assert.AreEqual(2, results[1].utf16Length);

            // 🚀: startIndex=4, length=2
            Assert.AreEqual(4, results[2].startIndex);
            Assert.AreEqual(2, results[2].utf16Length);

            // 合計がテキスト長と一致（連続なので）
            var last = results[2];
            Assert.AreEqual(text.Length, last.startIndex + last.utf16Length);
        }

        // ──────────────────────────────────────────────
        // 表示形式（Emoji_Presentation / 異体字セレクタ）
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_DefaultPresentationBMP_WithoutVS16_IsEmoji()
        {
            // ⌚ U+231A / ⭐ U+2B50 / ✅ U+2705 は Emoji_Presentation=Yes なので VS16 不要
            Assert.AreEqual(1, EmojiParser.ParseAll("\u231A").Count, "U+231A");
            Assert.AreEqual(1, EmojiParser.ParseAll("\u2B50").Count, "U+2B50");
            Assert.AreEqual(1, EmojiParser.ParseAll("\u2705").Count, "U+2705");
        }

        [Test]
        public void ParseAll_TextPresentationSymbols_WithoutVS16_NotEmoji()
        {
            // ☀ U+2600 / ✂ U+2702 / ↔ U+2194 / ⌂ U+2302 は VS16 が無ければテキスト表示
            Assert.AreEqual(0, EmojiParser.ParseAll("\u2600").Count, "U+2600");
            Assert.AreEqual(0, EmojiParser.ParseAll("\u2702").Count, "U+2702");
            Assert.AreEqual(0, EmojiParser.ParseAll("\u2194").Count, "U+2194");
            Assert.AreEqual(0, EmojiParser.ParseAll("\u2302").Count, "U+2302");
        }

        [Test]
        public void ParseAll_VS15_SuppressesEmoji()
        {
            // U+FE0E はテキスト表示の明示指定なので、絵文字として扱わない
            Assert.AreEqual(0, EmojiParser.ParseAll("\u2764\uFE0E").Count, "heart + VS15");
            Assert.AreEqual(0, EmojiParser.ParseAll("\u2B50\uFE0E").Count, "star + VS15");
        }

        [Test]
        public void ParseAll_ArrowsInUIText_NotEmoji()
        {
            // UIの装飾に使われる矢印や記号が絵文字に差し替わらないこと
            string text = "Next \u2192 Back \u2190 OK";
            Assert.AreEqual(0, EmojiParser.ParseAll(text).Count);
        }

        [Test]
        public void ParseAll_TextPresentation_OptIn_DetectsAll()
        {
            // オプトイン時は素の記号も検出する
            var results = EmojiParser.ParseAll("\u00A9\u2764\u2600", includeTextPresentationSymbols: true);
            Assert.AreEqual(3, results.Count);
        }

        [Test]
        public void ParseAll_ZWJ_WithoutVS16_InSequence()
        {
            // 👨‍⚕ = U+1F468 + ZWJ + U+2695（VS16なし）。
            // ZWJ の後続は結合要素なので表示形式を問わず1シーケンスとして扱う
            string doctor = "\U0001F468\u200D\u2695";
            var results = EmojiParser.ParseAll(doctor);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(doctor, results[0].emojiString);
        }

        [Test]
        public void ParseAll_SkinToneModifier_Standalone()
        {
            // 肌色修飾子が単独で現れた場合も色見本として描画対象にする
            var results = EmojiParser.ParseAll("\U0001F3FD");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(2, results[0].utf16Length);
        }

        [Test]
        public void ParseAll_EndIndex_MatchesStartPlusLength()
        {
            var results = EmojiParser.ParseAll("a\U0001F600b");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(1, results[0].startIndex);
            Assert.AreEqual(3, results[0].EndIndex);
        }

        [Test]
        public void ParseAll_UnterminatedTagSequence_FallsBackToFlag()
        {
            // 🏴 + タグ文字（終端タグなし）→ 🏴 単体として検出し、残りは読み飛ばす
            string text = "\U0001F3F4\U000E0067\U000E0062";
            var results = EmojiParser.ParseAll(text);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("\U0001F3F4", results[0].emojiString);
        }

        [Test]
        public void ParseAll_LoneSurrogate_DoesNotThrow()
        {
            // 壊れた文字列でも例外を出さない
            Assert.DoesNotThrow(() => EmojiParser.ParseAll("\uD83D"));
            Assert.DoesNotThrow(() => EmojiParser.ParseAll("\uDE00"));
            Assert.DoesNotThrow(() => EmojiParser.ParseAll("a\uD83D"));
        }

        [Test]
        public void ParseNext_NegativeIndex_ReturnsNull()
        {
            Assert.IsNull(EmojiParser.ParseNext("\U0001F600", -1));
        }

        // ──────────────────────────────────────────────
        // Unicode プロパティに基づく判定
        // （手書きのブロック範囲では非絵文字を巻き込んでいた）
        // ──────────────────────────────────────────────

        [Test]
        public void ParseAll_NonEmojiSmpBlocks_NotDetected()
        {
            // いずれも SMP の記号だが Unicode の Emoji プロパティを持たない
            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F71A").Count, "U+1F71A 錬金術記号");
            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F814").Count, "U+1F814 Supplemental Arrows-C");
            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F130").Count, "U+1F130 Squared Latin A");
            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F210").Count, "U+1F210 Squared CJK");
        }

        [Test]
        public void ParseAll_NonEmojiSmp_NotDetectedEvenWithVS16()
        {
            // Emoji プロパティ自体が無いので、FE0F を付けても絵文字にはならない
            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F71A\uFE0F").Count);
        }

        [Test]
        public void ParseAll_TextPresentationSmp_RequiresVS16()
        {
            // Emoji=Yes / Emoji_Presentation=No の SMP 絵文字。
            // BMP の © と同じ扱いになっていること（方針の非対称を作らない）
            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F321").Count, "🌡 温度計 VS16なし");
            Assert.AreEqual(1, EmojiParser.ParseAll("\U0001F321\uFE0F").Count, "🌡 温度計 VS16あり");

            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F5FA").Count, "🗺 地図 VS16なし");
            Assert.AreEqual(1, EmojiParser.ParseAll("\U0001F5FA\uFE0F").Count, "🗺 地図 VS16あり");

            Assert.AreEqual(0, EmojiParser.ParseAll("\U0001F441").Count, "👁 目 VS16なし");
            Assert.AreEqual(1, EmojiParser.ParseAll("\U0001F441\uFE0F").Count, "👁 目 VS16あり");
        }

        [Test]
        public void ParseAll_ColoredShapes_AreEmoji()
        {
            // U+1F7E0-U+1F7EB（色付き丸・四角）は絵文字ブロックではない範囲にあるが絵文字
            Assert.AreEqual(1, EmojiParser.ParseAll("\U0001F7E0").Count, "🟠");
            Assert.AreEqual(1, EmojiParser.ParseAll("\U0001F7EB").Count, "🟫");
        }

        [Test]
        public void ParseAll_SkinToneAfterVS16_ConsumesBoth()
        {
            // 👍 + 肌色 + FE0F のように修飾子の後ろに FE0F が来ても
            // シーケンス長が実際と食い違わないこと
            string text = "\U0001F44D\U0001F3FD\uFE0F";
            var results = EmojiParser.ParseAll(text);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(text.Length, results[0].utf16Length);
        }

        [Test]
        public void UnicodeVersion_IsRecorded()
        {
            // 生成元データのバージョンが追える状態であること
            Assert.IsNotEmpty(EmojiCodepointTables.UnicodeVersion);
            Assert.AreNotEqual("unknown", EmojiCodepointTables.UnicodeVersion);
        }
    }
}

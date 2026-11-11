using System.Text;
using NUnit.Framework;
using TMPro;

namespace NativeEmoji.Tests.EditMode
{
    /// <summary>
    /// 実行時スプライト方式の前処理と登録の契約テスト。
    /// ネイティブ描画を使うため、macOS の Editor で実行する（NativeBridgeTests と同じ前提）。
    /// </summary>
    public class NativeEmojiSpriteTests
    {
        private const int Zwj = 0x200D;
        private const int VariationSelector16 = 0xFE0F;

        private TMP_SpriteAsset _previousDefault;

        [SetUp]
        public void SetUp()
        {
            // TMP Settings はアセットそのもの。Essential Resources を入れていないと存在しない
            if (TMP_Settings.instance == null)
            {
                Assert.Ignore(
                    "TMP Essential Resources が未インポート。" +
                    "Window > TextMeshPro > Import TMP Essential Resources で入れる");
            }

            _previousDefault = TMP_Settings.defaultSpriteAsset;
            Assert.IsTrue(NativeEmojiSprite.Install(), "スプライト用シェーダが見つからず有効にできない");
        }

        [TearDown]
        public void TearDown()
        {
            NativeEmojiSprite.Uninstall();
        }

        [Test]
        public void Install_ReplacesDefaultSpriteAsset_AndUninstallRestoresIt()
        {
            Assert.AreNotSame(_previousDefault, TMP_Settings.defaultSpriteAsset);
            Assert.AreEqual("NativeEmojiSprite", TMP_Settings.defaultSpriteAsset.name);

            NativeEmojiSprite.Uninstall();

            Assert.AreSame(_previousDefault, TMP_Settings.defaultSpriteAsset);
            Assert.IsFalse(NativeEmojiSprite.IsInstalled);
        }

        [Test]
        public void Install_KeepsPreviousDefaultAsFallback()
        {
            // 既定アセットに入れていた独自スプライト（<sprite name=...>）を引けるままにする
            if (_previousDefault == null)
            {
                Assert.Ignore("既定スプライトアセットが未設定");
            }

            CollectionAssert.Contains(TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets, _previousDefault);
        }

        [Test]
        public void Process_TextWithoutEmoji_ReturnsSameInstance()
        {
            var text = "こんにちは…→※★♪ 2026";

            var result = NativeEmojiTextProcessor.Process(text, true);

            Assert.AreSame(text, result);
            Assert.AreEqual(0, TMP_Settings.defaultSpriteAsset.spriteCharacterTable.Count);
        }

        [Test]
        public void Process_SingleEmoji_KeepsTextAndRegistersByUnicode()
        {
            var text = "a" + E(0x1F60A) + "b";

            var result = NativeEmojiTextProcessor.Process(text, true);

            Assert.AreEqual(text, result);
            var character = TMP_Settings.defaultSpriteAsset.spriteCharacterTable[0];
            Assert.AreEqual(0x1F60Au, character.unicode);
            Assert.AreEqual("1f60a", character.name);
        }

        [Test]
        public void Process_Sequence_IsReplacedWithSpriteTag()
        {
            var family = E(0x1F468, Zwj, 0x1F469, Zwj, 0x1F467);

            var result = NativeEmojiTextProcessor.Process("a" + family + "b", true);

            Assert.AreEqual("a<sprite index=0>b", result);
            var character = TMP_Settings.defaultSpriteAsset.spriteCharacterTable[0];
            Assert.AreEqual(0xFFFEu, character.unicode);
            Assert.AreEqual("1f468-200d-1f469-200d-1f467", character.name);
        }

        [Test]
        public void Process_SameSequenceTwice_RegistersOnce()
        {
            var heart = E(0x2764, VariationSelector16);

            NativeEmojiTextProcessor.Process(heart, true);
            var result = NativeEmojiTextProcessor.Process(heart + heart, true);

            Assert.AreEqual("<sprite index=0><sprite index=0>", result);
            Assert.AreEqual(1, TMP_Settings.defaultSpriteAsset.spriteCharacterTable.Count);
        }

        [Test]
        public void Process_SequenceWithoutRichText_IsDecomposedWithoutJoiners()
        {
            var family = E(0x1F468, Zwj, 0x1F469, Zwj, 0x1F467);

            var result = NativeEmojiTextProcessor.Process(family, false);

            Assert.AreEqual(E(0x1F468, 0x1F469, 0x1F467), result);
            Assert.AreEqual(3, TMP_Settings.defaultSpriteAsset.spriteCharacterTable.Count);
        }

        [Test]
        public void RegisteredSprite_UsesFixedGlyphMetrics()
        {
            NativeEmojiTextProcessor.Process(E(0x1F600), true);

            var glyph = TMP_Settings.defaultSpriteAsset.spriteGlyphTable[0];
            Assert.AreEqual(64f, glyph.metrics.width);
            Assert.AreEqual(64f, glyph.metrics.height);
            Assert.AreEqual(0f, glyph.metrics.horizontalBearingX);
            Assert.AreEqual(48f, glyph.metrics.horizontalBearingY);
            Assert.AreEqual(64f, glyph.metrics.horizontalAdvance);
            Assert.AreEqual(1.28f, glyph.scale);
            Assert.AreEqual(0f, TMP_Settings.defaultSpriteAsset.faceInfo.pointSize);
        }

        [Test]
        public void Process_NotInstalled_ReturnsInput()
        {
            NativeEmojiSprite.Uninstall();
            var text = "a" + E(0x1F60A);

            Assert.AreSame(text, NativeEmojiTextProcessor.Process(text, true));
        }

        private static string E(params int[] codepoints)
        {
            var builder = new StringBuilder();
            foreach (var codepoint in codepoints)
            {
                builder.Append(char.ConvertFromUtf32(codepoint));
            }

            return builder.ToString();
        }
    }
}

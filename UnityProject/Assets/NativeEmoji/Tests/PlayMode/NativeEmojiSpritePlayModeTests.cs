using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NativeEmoji.Tests.PlayMode
{
    /// <summary>
    /// 実行時スプライト方式の PlayMode テスト。
    ///
    /// EditMode テスト（NativeEmojiSpriteTests）は前処理を直接呼んで確かめるが、
    /// ここでは実際の Canvas 更新を通して
    /// 「最初のフレームから TMP 自身が絵文字を描いているか」を確かめる。
    /// 前処理を持たないテキストに前処理を付ける安全網は、この経路でしか通らない。
    /// </summary>
    [TestFixture]
    public class NativeEmojiSpritePlayModeTests
    {
        private GameObject canvasGO;

        [SetUp]
        public void SetUp()
        {
            canvasGO = new GameObject("TestCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGO != null)
                Object.Destroy(canvasGO);
        }

        [Test]
        public void AutoInstall_RunsBeforeFirstScene()
        {
            // RuntimeInitializeOnLoadMethod(BeforeSceneLoad) で有効になっているはず
            Assert.IsTrue(NativeEmojiSprite.IsInstalled,
                "自動有効化が働いていない。NATIVE_EMOJI_DISABLE_AUTO_INSTALL が定義されていないか確認する");
            Assert.IsNotNull(TMP_Settings.defaultSpriteAsset);
            Assert.AreEqual("NativeEmojiSprite", TMP_Settings.defaultSpriteAsset.name);
        }

        [UnityTest]
        public IEnumerator CompositeSequence_RendersAsSpriteOnFirstFrame()
        {
            // 🤝🏽 = U+1F91D + 肌色修飾子。合成シーケンスなので <sprite> に置き換わる
            var text = CreateText("\U0001F91D\U0001F3FD");
            yield return null;

            Assert.IsTrue(HasSpriteElement(text), "最初のフレームでスプライトとして描かれていない");
            AssertNoMissingGlyph(text);
        }

        [UnityTest]
        public IEnumerator ZwjSequence_RendersAsSingleSprite()
        {
            // 👨‍👩‍👧‍👦 は 1 つのスプライトになり、構成要素の豆腐が並ばない
            var text = CreateText("\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466");
            yield return null;

            Assert.IsTrue(HasSpriteElement(text));

            int spriteCount = 0;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                if (text.textInfo.characterInfo[i].elementType == TMP_TextElementType.Sprite)
                    spriteCount++;
            }

            Assert.AreEqual(1, spriteCount, "合成シーケンスが 1 つにまとまっていない");
        }

        [UnityTest]
        public IEnumerator TextWithoutPreprocessor_GetsPreprocessorAttached()
        {
            // 安全網。前処理を持たないテキストでも絵文字が出るようにする
            var text = CreateText("\U0001F91D\U0001F3FD");
            Assert.IsNull(text.textPreprocessor, "生成直後は前処理を持たない");

            yield return null;

            Assert.IsNotNull(text.textPreprocessor, "前処理が付けられていない");
        }

        [UnityTest]
        public IEnumerator PlainText_GetsNoPreprocessor()
        {
            // 絵文字を含まないテキストには手を入れない
            var text = CreateText("Hello World");
            yield return null;

            Assert.IsNull(text.textPreprocessor);
        }

        [UnityTest]
        public IEnumerator SingleCodepoint_IsNotReplacedWithTag()
        {
            // 単独のコードポイントはタグに置き換えない
            var text = CreateText("\U0001F600");
            yield return null;

            Assert.IsFalse(text.GetParsedText().Contains("<sprite"),
                "単独の絵文字がタグに置き換えられている");
            AssertNoMissingGlyph(text);
        }

        [UnityTest]
        public IEnumerator InputFieldText_IsNotRewritten()
        {
            // TMP_InputField は characterInfo.index を元の文字列上の位置として使う。
            // 前処理でタグに置き換えると位置がずれるので、入力欄の本文には前処理を付けない
            var inputGO = new GameObject("Input", typeof(RectTransform));
            inputGO.transform.SetParent(canvasGO.transform, false);
            var body = CreateText(string.Empty);
            body.transform.SetParent(inputGO.transform, false);
            var input = inputGO.AddComponent<TMP_InputField>();
            input.textViewport = inputGO.GetComponent<RectTransform>();
            input.textComponent = body;

            // 🇯🇵 = Regional Indicator 2 つ（UTF-16 で 4）
            input.text = "\U0001F1EF\U0001F1F5abc";
            yield return null;

            Assert.IsNull(body.textPreprocessor, "入力欄の本文に前処理が付いている");

            var info = body.textInfo;
            int indexOfA = -1;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (info.characterInfo[i].character == 'a')
                {
                    indexOfA = info.characterInfo[i].index;
                    break;
                }
            }

            Assert.AreEqual(4, indexOfA, "文字の位置が元の文字列とずれている");
        }

        [UnityTest]
        public IEnumerator MissingCharacter_IsRegisteredAndRecovered()
        {
            // FE0F の無い ❤ は本文フォントに無い。OnMissingCharacter で登録して作り直す
            var text = CreateText("❤");
            yield return null;

            Assert.IsTrue(HasSpriteElement(text), "欠落文字がスプライトとして復帰していない");
            AssertNoMissingGlyph(text);
        }

        [UnityTest]
        public IEnumerator TextChange_KeepsRenderingEmoji()
        {
            var text = CreateText("plain");
            yield return null;

            text.text = "\U0001F91D\U0001F3FD";
            yield return null;

            Assert.IsTrue(HasSpriteElement(text));
            AssertNoMissingGlyph(text);
        }

        // ──────────────────────────────────────────────
        // ヘルパー
        // ──────────────────────────────────────────────

        private TMP_Text CreateText(string content)
        {
            var go = new GameObject("TestText");
            go.transform.SetParent(canvasGO.transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 32;
            tmp.text = content;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 100);

            return tmp;
        }

        private static bool HasSpriteElement(TMP_Text text)
        {
            TMP_TextInfo info = text.textInfo;

            for (int i = 0; i < info.characterCount; i++)
            {
                if (info.characterInfo[i].elementType == TMP_TextElementType.Sprite)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 豆腐（欠落グリフ）が残っていないこと。
        /// TMP は見つからない文字を代替グリフに置き換えるので、文字そのものが保持されているかで見る。
        /// </summary>
        private static void AssertNoMissingGlyph(TMP_Text text)
        {
            TMP_TextInfo info = text.textInfo;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ci = info.characterInfo[i];
                if (!ci.isVisible || ci.elementType == TMP_TextElementType.Sprite)
                    continue;

                Assert.AreNotEqual(0u, (uint)ci.textElement.unicode,
                    $"charInfo[{i}] が欠落グリフになっている");
            }
        }
    }
}

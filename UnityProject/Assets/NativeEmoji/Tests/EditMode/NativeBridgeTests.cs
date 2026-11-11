using NUnit.Framework;
using UnityEngine;

namespace NativeEmoji.Tests.EditMode
{
    /// <summary>
    /// NativeBridge の契約テスト
    ///
    /// ネイティブ側（macOS は EmojiRenderer.bundle）が実際に絵文字を描いて
    /// PNG を返しているかを確認する。ここが黙って失敗すると
    /// フォールバックのグレー四角が表示されるだけで、原因が分かりにくい。
    /// </summary>
    [TestFixture]
    public class NativeBridgeTests
    {
        private const int Size = 64;

        /// <summary>フォールバックテクスチャの色（NativeBridge と合わせる）</summary>
        private static readonly Color32 FallbackColor = new Color32(200, 200, 200, 255);

        [SetUp]
        public void SetUp()
        {
#if !UNITY_EDITOR_OSX && !UNITY_EDITOR_WIN
            Assert.Ignore("エディタ用ネイティブレンダラーが無いプラットフォーム");
#endif
        }

        [Test]
        public void RenderEmoji_ReturnsDecodablePng()
        {
            byte[] png = NativeBridge.RenderEmoji("\U0001F600", Size, Size);

            Assert.IsNotNull(png, "PNG が返っていない");
            Assert.Greater(png.Length, 0, "PNG が空");

            Texture2D texture = LoadTexture(png);
            try
            {
                Assert.AreEqual(Size, texture.width);
                Assert.AreEqual(Size, texture.height);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void RenderEmoji_DrawsSomethingVisible()
        {
            byte[] png = NativeBridge.RenderEmoji("\U0001F600", Size, Size);
            Texture2D texture = LoadTexture(png);

            try
            {
                Color32[] pixels = texture.GetPixels32();

                int opaquePixels = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.a > 0)
                        opaquePixels++;
                }

                Assert.Greater(opaquePixels, 0, "すべて透明。何も描かれていない");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void RenderEmoji_IsNotFallbackTexture()
        {
            byte[] png = NativeBridge.RenderEmoji("\U0001F600", Size, Size);
            Texture2D texture = LoadTexture(png);

            try
            {
                Color32[] pixels = texture.GetPixels32();

                bool allFallbackColor = true;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.r != FallbackColor.r || pixel.g != FallbackColor.g ||
                        pixel.b != FallbackColor.b || pixel.a != FallbackColor.a)
                    {
                        allFallbackColor = false;
                        break;
                    }
                }

                Assert.IsFalse(allFallbackColor,
                    "グレー一色のフォールバックが返っている。ネイティブ描画が失敗している");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void RenderEmoji_EmptyString_ReturnsNull()
        {
            Assert.IsNull(NativeBridge.RenderEmoji("", Size, Size));
            Assert.IsNull(NativeBridge.RenderEmoji(null, Size, Size));
        }

        [Test]
        public void RenderEmoji_ZWJSequence_Succeeds()
        {
            // 複合シーケンスもネイティブ側が1つの絵文字として描けること
            string family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";
            byte[] png = NativeBridge.RenderEmoji(family, Size, Size);

            Assert.IsNotNull(png);
            Assert.Greater(png.Length, 0);
        }

        // ──────────────────────────────────────────────
        // ヘルパー
        // ──────────────────────────────────────────────

        private static Texture2D LoadTexture(byte[] png)
        {
            Assert.IsNotNull(png, "PNG が null");

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Assert.IsTrue(texture.LoadImage(png), "PNG をデコードできない");

            return texture;
        }
    }
}

using TMPro;

namespace NativeEmoji
{
    /// <summary>
    /// 前処理（ITextPreprocessor）を持たないテキストに付ける前処理。
    /// 合成シーケンスを &lt;sprite&gt; タグに置き換えられるようにする。
    /// </summary>
    internal sealed class NativeEmojiTextPreprocessor : ITextPreprocessor
    {
        private readonly TMP_Text _text;

        public NativeEmojiTextPreprocessor(TMP_Text text)
        {
            _text = text;
        }

        public string PreprocessText(string text)
        {
            return NativeEmojiTextProcessor.Process(text, _text.richText);
        }
    }
}

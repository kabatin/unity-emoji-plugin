using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NativeEmoji
{
    /// <summary>
    /// 前処理を通らなかった絵文字の安全網。
    /// - 前処理（ITextPreprocessor）を持たないテキストに絵文字が含まれていたら、前処理を付けて同じ Canvas 更新の中で作り直す
    /// - TMP が文字を見つけられなかったとき（フォントに無い既定テキスト表示の ❤ など）は、その絵文字を登録して同じく作り直す
    /// 作り直しは画面に描かれる前に終わるので、豆腐のメッシュは表示されない。
    /// TMP_InputField の本文には前処理を付けない。入力欄は文字の位置（characterInfo.index）を元の文字列上の位置として
    /// 使うので、タグに置き換えるとキャレットがずれる。入力欄では合成シーケンスは構成要素に分かれて表示される。
    /// 数値を差し込む SetText(書式, float…) と StringBuilder・char[] 版は対象外。再解析で文字の配列を作り直さない
    /// （TMP_Text.cs:2027-2030）ため、最初の解析で豆腐に置き換わった文字が戻らない。
    /// </summary>
    internal static class NativeEmojiTextWatcher
    {
        private static readonly HashSet<TMP_Text> s_PendingTexts = new();
        private static bool s_IsReparsing;

        public static void Install()
        {
            TMP_Text.OnMissingCharacter += OnMissingCharacter;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        public static void Uninstall()
        {
            TMP_Text.OnMissingCharacter -= OnMissingCharacter;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            s_PendingTexts.Clear();
            s_IsReparsing = false;
        }

        private static void OnMissingCharacter(int unicode, int stringIndex, string text, TMP_FontAsset fontAsset, TMP_Text textComponent)
        {
            if (textComponent == null || !EmojiCodepoints.IsEmojiCapable(unicode) || EmojiCodepoints.IsKeycapBase(unicode))
            {
                return;
            }

            if (NativeEmojiSpriteRegistry.TryGetOrRegister(char.ConvertFromUtf32(unicode), (uint)unicode, out _))
            {
                s_PendingTexts.Add(textComponent);
            }
        }

        // TMP がメッシュを作り終えた直後（描画前）に呼ばれる
        private static void OnTextChanged(Object obj)
        {
            if (s_IsReparsing || obj is not TMP_Text text)
            {
                return;
            }

            // 前処理が無いと合成シーケンスを <sprite> タグにできない。構成要素がすべて登録済みだと文字の欠落も起きないので、
            // 欠落を待たずに、絵文字を含むと分かった時点で前処理を付ける
            var needsPreprocessor = text.textPreprocessor == null
                                    && NativeEmojiTextProcessor.MayContainEmoji(text.text)
                                    && !IsInputFieldText(text);
            if (!s_PendingTexts.Remove(text) && !needsPreprocessor)
            {
                return;
            }

            NativeEmojiSpriteRegistry.ApplyIfDirty();
            if (needsPreprocessor)
            {
                text.textPreprocessor = new NativeEmojiTextPreprocessor(text);
            }

            // 作り直しでもこのイベントが発火するので、入れ子で呼ばれないようにする
            s_IsReparsing = true;
            try
            {
                text.ForceMeshUpdate(false, true);
            }
            finally
            {
                s_IsReparsing = false;
            }
        }

        private static bool IsInputFieldText(TMP_Text text)
        {
            var inputField = text.GetComponentInParent<TMP_InputField>();
            return inputField != null && inputField.textComponent == text;
        }
    }
}

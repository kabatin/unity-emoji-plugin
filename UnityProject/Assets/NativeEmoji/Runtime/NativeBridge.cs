using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NativeEmoji
{
    /// <summary>
    /// ネイティブプラグインへのブリッジ
    /// </summary>
    public static class NativeBridge
    {
        /// <summary>
        /// この実行環境で OS に絵文字を描かせられるか。
        /// iOS / Android の実機と macOS の Editor だけ true。それ以外では <see cref="RenderEmoji"/> は null を返す。
        /// </summary>
        public static bool IsSupported
        {
            get
            {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
                return true;
#elif UNITY_EDITOR_OSX
                return true;
#else
                return false;
#endif
            }
        }

        #region iOS Native Plugin
#if UNITY_IOS && !UNITY_EDITOR
        // 絵文字は必ず非ASCII。既定のマーシャリング（ANSI）は実装依存なので
        // UTF-8 であることを明示する
        [DllImport("__Internal")]
        private static extern IntPtr _RenderEmoji(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string emoji,
            int width, int height, out int dataLength);
#endif
        #endregion

        #region Android Native Plugin
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass emojiRendererClass;
        private static AndroidJavaClass EmojiRendererClass
        {
            get
            {
                if (emojiRendererClass == null)
                {
                    emojiRendererClass = new AndroidJavaClass("com.nativeemoji.EmojiRenderer");
                }
                return emojiRendererClass;
            }
        }
#endif
        #endregion

        #region Editor Mac Plugin
#if UNITY_EDITOR_OSX
        // 絵文字は必ず非ASCII。既定のマーシャリング（ANSI）は実装依存なので
        // UTF-8 であることを明示する
        [DllImport("EmojiRenderer")]
        private static extern IntPtr RenderEmojiMac(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string emoji,
            int width, int height, out int dataLength);
#endif
        #endregion

        /// <summary>
        /// 絵文字をネイティブでレンダリング
        /// </summary>
        public static byte[] RenderEmoji(string emoji, int width, int height)
        {
            if (string.IsNullOrEmpty(emoji))
            {
                Debug.LogWarning("NativeBridge: emoji is null or empty");
                return null;
            }

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RenderEmoji_iOS(emoji, width, height);
#elif UNITY_ANDROID && !UNITY_EDITOR
                return RenderEmoji_Android(emoji, width, height);
#elif UNITY_EDITOR_OSX
                return RenderEmoji_EditorMac(emoji, width, height);
#else
                // 未対応の環境。呼ぶ側は IsSupported で先に見分ける
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"NativeBridge: RenderEmoji failed - {e.Message}");
                return CreateFallbackTexture(width, height);
            }
        }

        #region Platform Implementations

#if UNITY_IOS && !UNITY_EDITOR
        private static byte[] RenderEmoji_iOS(string emoji, int width, int height)
        {
            int dataLength;
            IntPtr dataPtr = _RenderEmoji(emoji, width, height, out dataLength);
            
            if (dataPtr == IntPtr.Zero || dataLength == 0)
            {
                Debug.LogWarning($"NativeBridge: iOS rendering failed for {emoji}");
                return CreateFallbackTexture(width, height);
            }

            byte[] data = new byte[dataLength];
            Marshal.Copy(dataPtr, data, 0, dataLength);
            
            return data;
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static byte[] RenderEmoji_Android(string emoji, int width, int height)
        {
            byte[] data = EmojiRendererClass.CallStatic<byte[]>("renderEmoji", emoji, width, height);
            
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning($"NativeBridge: Android rendering failed for {emoji}");
                return CreateFallbackTexture(width, height);
            }
            
            return data;
        }
#endif

#if UNITY_EDITOR_OSX
        private static byte[] RenderEmoji_EditorMac(string emoji, int width, int height)
        {
            try
            {
                int dataLength;
                IntPtr dataPtr = RenderEmojiMac(emoji, width, height, out dataLength);

                if (dataPtr == IntPtr.Zero || dataLength == 0)
                {
                    Debug.LogWarning($"NativeBridge: macOS rendering failed for {emoji}");
                    return CreateFallbackTexture(width, height);
                }

                byte[] data = new byte[dataLength];
                Marshal.Copy(dataPtr, data, 0, dataLength);

                return data;
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"NativeBridge: EmojiRenderer.bundle not found - {e.Message}");
                Debug.LogError(
                    "EmojiRenderer.bundle をビルドする: NativeEmoji/Plugins/macOS で ./build.sh");
                return CreateFallbackTexture(width, height);
            }
        }
#endif

        #endregion

        /// <summary>
        /// フォールバックテクスチャ生成（単色の四角）
        /// </summary>
        private static byte[] CreateFallbackTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            
            // グレーの四角
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(200, 200, 200, 255);
            }
            
            tex.SetPixels32(pixels);
            tex.Apply();
            
            byte[] data = tex.EncodeToPNG();
            
            // Edit Mode では DestroyImmediate を使用
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(tex);
            else
            #endif
                UnityEngine.Object.Destroy(tex);
            
            return data;
        }
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NativeEmoji
{
    /// <summary>
    /// OS が描いた絵文字を、実行時に作る TMP_SpriteAsset のスプライトとして TMP 自身に描かせる仕組みの入口。
    /// 既定では最初のシーンを読み込む前に自動で有効になり、すべての TMP テキストに効く（コンポーネントの追加は不要）。
    /// 自動で有効にしたくない場合はスクリプトシンボル NATIVE_EMOJI_DISABLE_AUTO_INSTALL を定義し、任意の時点で <see cref="Install"/> を呼ぶ。
    /// </summary>
    public static class NativeEmojiSprite
    {
        public static bool IsInstalled => NativeEmojiSpriteRegistry.IsInstalled;

        /// <summary>
        /// TMP の既定スプライトアセットを実行時アセットに差し替え、テキストの監視を始める。
        /// </summary>
        /// <returns>有効になったら true。未対応の環境（<see cref="NativeBridge.IsSupported"/> が false）、TMP Settings が無い、スプライト用シェーダが見つからない場合は false</returns>
        public static bool Install()
        {
            if (NativeEmojiSpriteRegistry.IsInstalled)
            {
                return true;
            }

            if (!NativeEmojiSpriteRegistry.Install())
            {
                return false;
            }

            NativeEmojiTextWatcher.Install();
            return true;
        }

        /// <summary>
        /// 監視をやめ、TMP の既定スプライトアセットを元に戻す。登録した絵文字とアトラスは破棄する。
        /// </summary>
        public static void Uninstall()
        {
            NativeEmojiTextWatcher.Uninstall();
            NativeEmojiSpriteRegistry.Uninstall();
        }

#if !NATIVE_EMOJI_DISABLE_AUTO_INSTALL
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInstall()
        {
            Install();
        }
#endif

#if UNITY_EDITOR
        // TMP Settings はアセットそのものなので、Play 終了で元に戻さないと破棄済みの実行時アセットを指したまま Edit Mode に残る
        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && IsInstalled)
            {
                Uninstall();
            }
        }
#endif
    }
}

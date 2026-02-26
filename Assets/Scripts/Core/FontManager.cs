// ============================================================
// ThemeParkGame - FontManager
// 日本語フォント一元管理（Noto Sans JP）
// WebGLビルドで日本語が正しく表示されるようにする
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// 日本語対応フォントを一元管理する静的クラス。
    /// Noto Sans JP を Resources/Fonts/ からロードし、全UIで共有する。
    /// フォールバックとして組み込みフォントを使用する。
    /// </summary>
    public static class FontManager
    {
        private static Font _regular;
        private static Font _bold;
        private static bool _initialized;

        /// <summary>
        /// 日本語対応レギュラーフォントを取得する。
        /// 全UIテキストでこのフォントを使用すること。
        /// </summary>
        public static Font Regular
        {
            get
            {
                if (!_initialized) Initialize();
                return _regular;
            }
        }

        /// <summary>
        /// 日本語対応ボールドフォントを取得する。
        /// タイトル・見出しで使用する。
        /// </summary>
        public static Font Bold
        {
            get
            {
                if (!_initialized) Initialize();
                return _bold;
            }
        }

        /// <summary>
        /// フォントを初期化する。最初のアクセス時に自動実行される。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;

            // Noto Sans JP をロード（Resources/Fonts/ に配置）
            _regular = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            _bold = Resources.Load<Font>("Fonts/NotoSansJP-Bold");

            // ロード失敗時のフォールバック
            if (_regular == null)
            {
                _regular = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_regular == null)
                    _regular = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Debug.LogWarning("[FontManager] Noto Sans JP Regular が見つかりません。フォールバックフォントを使用します。");
            }
            else
            {
                WebGLOptimizer.LogVerbose("[FontManager] Noto Sans JP Regular をロードしました");
            }

            if (_bold == null)
            {
                // Bold がなければ Regular を代用
                _bold = _regular;
                if (_regular != null && Resources.Load<Font>("Fonts/NotoSansJP-Bold") == null)
                {
                    WebGLOptimizer.LogVerbose("[FontManager] Noto Sans JP Bold が見つかりません。Regular で代用します");
                }
            }
            else
            {
                WebGLOptimizer.LogVerbose("[FontManager] Noto Sans JP Bold をロードしました");
            }

            _initialized = true;
        }

        /// <summary>
        /// フォントを取得する。boldがtrueの場合ボールドフォントを返す。
        /// 既存コードからの移行を簡単にするためのヘルパー。
        /// </summary>
        public static Font GetFont(bool bold = false)
        {
            return bold ? Bold : Regular;
        }
    }
}

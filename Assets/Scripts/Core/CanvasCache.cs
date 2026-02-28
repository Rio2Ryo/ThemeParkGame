// ============================================================
// ThemeParkGame - CanvasCache
// FindObjectOfType<Canvas>() の共有キャッシュ
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// FindObjectOfType&lt;Canvas&gt;() の呼び出しを一元管理する静的キャッシュ。
    /// 各UIシステムが個別に FindObjectOfType を呼ぶ代わりに、
    /// CanvasCache.Get() を使うことでキャッシュされた参照を共有する。
    /// Canvas が破棄された場合は次回呼び出し時に再取得する。
    /// </summary>
    public static class CanvasCache
    {
        private static Canvas _cached;

        /// <summary>
        /// キャッシュされた Canvas を返す。
        /// キャッシュが無効（null または破棄済み）の場合のみ FindObjectOfType を実行する。
        /// </summary>
        public static Canvas Get()
        {
            if (_cached == null)
            {
                _cached = Object.FindObjectOfType<Canvas>();
            }
            return _cached;
        }

        /// <summary>
        /// キャッシュを明示的にクリアする（シーン遷移時等）。
        /// </summary>
        public static void Clear()
        {
            _cached = null;
        }
    }
}

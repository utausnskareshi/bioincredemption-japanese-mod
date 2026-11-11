using System;
using System.Collections.Generic;
using TMPro;

namespace BioIncJp.Plugin.Fonts
{
    /// <summary>
    /// フォントアセットの「フォールバック」(その文字を持っていないときに代わりに探しに行く先)の一覧を操作する。
    /// </summary>
    internal static class FontFallbacks
    {
        /// <summary>
        /// 太字用の日本語フォントを割り当てるべきフォントかどうかを、アセット名から判定する。
        /// ゲームのフォントは "Exo2 Bold" "Exo2SemiBold" のように名前へ太さが入っている。
        /// </summary>
        public static bool UsesBoldWeight(string assetName)
        {
            return assetName != null && assetName.IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 一覧へ日本語フォントを登録する。
        /// </summary>
        /// <param name="fallbacks">対象の一覧(その場で書き換える)。</param>
        /// <param name="japanese">登録する日本語フォント。</param>
        /// <param name="prioritize">
        /// true なら先頭へ置く。ゲームには中国語フォントのフォールバックが元から入っており、
        /// それより前に置かないと、漢字が中国語の字形で表示されてしまう。
        /// false なら末尾へ置く(日本語表示が無効のときに、他言語の表示へ影響を与えないため)。
        /// </param>
        /// <returns>一覧を変更した場合は true。</returns>
        public static bool Ensure(List<TMP_FontAsset> fallbacks, TMP_FontAsset japanese, bool prioritize)
        {
            if (fallbacks is null)
            {
                throw new ArgumentNullException(nameof(fallbacks));
            }

            if (japanese is null)
            {
                throw new ArgumentNullException(nameof(japanese));
            }

            var index = fallbacks.IndexOf(japanese);
            var desiredIndex = prioritize ? 0 : (index >= 0 ? fallbacks.Count - 1 : fallbacks.Count);
            if (index == desiredIndex)
            {
                return false;
            }

            if (index >= 0)
            {
                fallbacks.RemoveAt(index);
            }

            fallbacks.Insert(prioritize ? 0 : fallbacks.Count, japanese);
            return true;
        }
    }
}

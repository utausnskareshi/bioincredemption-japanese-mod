# 第三者ソフトウェアのライセンス

配布用ZIPには、以下の第三者ソフトウェアとフォントを**無改変のまま**同梱しています。
それぞれのライセンス文は、ZIP 内の `licenses` フォルダ(リポジトリでは [licenses](licenses))に収録しています。

| 名称 | 版 | ライセンス | 同梱場所 | 入手元 |
|---|---|---|---|---|
| BepInEx | 5.4.23.6 | MIT | `payload/BepInEx/core/` ほか | https://github.com/BepInEx/BepInEx |
| HarmonyX (0Harmony) | BepInEx 同梱版 | MIT | `payload/BepInEx/core/` | https://github.com/BepInEx/HarmonyX |
| MonoMod (RuntimeDetour / Utils) | BepInEx 同梱版 | MIT | `payload/BepInEx/core/` | https://github.com/MonoMod/MonoMod |
| Mono.Cecil | BepInEx 同梱版 | MIT | `payload/BepInEx/core/` | https://github.com/jbevain/cecil |
| UnityDoorstop | 4.6.0 | LGPL-2.1 | `payload/winhttp.dll` ほか | https://github.com/NeighTools/UnityDoorstop |
| Noto Sans JP (Medium / Bold) | Sans 2.004 | SIL Open Font License 1.1 | `payload/BepInEx/plugins/BioIncRedemptionJP/fonts/` | https://github.com/notofonts/noto-cjk |
| .NET ランタイム / WPF | 10.0 | MIT | `BioIncRedemptionJP.exe` に内包 | https://github.com/dotnet/runtime , https://github.com/dotnet/wpf |

## 補足

- **BepInEx 一式**(`winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`changelog.txt`、`BepInEx/core/` 以下)は、
  BepInEx 公式の配布物 `BepInEx_win_x64_5.4.23.6.zip` の中身をそのまま収録したものです。
- **UnityDoorstop**(`winhttp.dll`)は LGPL-2.1 です。ソースコードは上記の入手元(タグ `v4.6.0`)で公開されています。
  本配布物では改変せず、そのままの形で再配布しています。
- **Noto Sans JP** は SIL Open Font License 1.1 に基づいて同梱しています。フォントファイルは改変していません。
  ライセンス文はフォントと同じフォルダにも `OFL.txt` として置いています。
- **.NET ランタイム**のライセンスと、ランタイムが含む第三者コンポーネントの表記は、配布ZIP内の
  `licenses/dotnet-LICENSE.txt` と `licenses/dotnet-ThirdPartyNotices.txt` を参照してください。
  この2つは、ビルドのときに .NET SDK から配布ZIPへ写すため、リポジトリの `licenses` には含めていません。

## 同梱していないもの

- Bio Inc. Redemption 本体のファイル(プログラム、画像、音声、原文のテキストなど)は、一切含んでいません。

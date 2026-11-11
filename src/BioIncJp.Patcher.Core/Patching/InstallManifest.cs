using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BioIncJp.Patcher.Core.Patching;

/// <summary>ファイル1つに対して、適用時に行った(行う)操作。</summary>
public enum FileAction
{
    /// <summary>ゲームフォルダに無かったので、新しく作った。元に戻すときは削除する。</summary>
    Create,

    /// <summary>内容の違うファイルが既にあったので、退避してから置き換えた。元に戻すときは退避したものを戻す。</summary>
    Replace,

    /// <summary>同じ内容のファイルが既にあったので、何もしなかった。このパッチの配布物と同一なので、元に戻すときは削除する。</summary>
    KeepIdentical,
}

/// <summary>適用の進み具合。</summary>
public enum InstallStatus
{
    /// <summary>適用の途中(途中で中断された場合もこの状態で残る)。</summary>
    Installing,

    /// <summary>適用が完了した。</summary>
    Installed,
}

/// <summary>導入記録に残す、ファイル1つ分の情報。</summary>
/// <param name="Path">ゲームフォルダからの相対パス('/' 区切り)。</param>
/// <param name="Action">行った操作。</param>
/// <param name="Sha256">配置したファイルの SHA-256。</param>
/// <param name="BackupPath">置き換えた場合の退避先(パッチ用フォルダからの相対パス)。置き換えていなければ null。</param>
public sealed record ManifestFile(string Path, FileAction Action, string Sha256, string? BackupPath);

/// <summary>
/// 導入記録。適用時に「何を置き、何を作ったか」を残し、元に戻すときの拠り所にする。
/// </summary>
/// <param name="SchemaVersion">この記録の形式の版。</param>
/// <param name="ToolVersion">適用したパッチツールの版。</param>
/// <param name="Status">適用の進み具合。</param>
/// <param name="InstalledAtUtc">適用した日時(UTC)。</param>
/// <param name="GameBuildId">適用時点のゲームのビルド番号(不明なら空文字)。</param>
/// <param name="BepInExPreexisting">適用前から BepInEx が導入されていたか。true の場合、BepInEx 本体には手を付けない。</param>
/// <param name="CreatedDirectories">適用時に新しく作ったフォルダ(ゲームフォルダからの相対パス)。</param>
/// <param name="Files">配置したファイル。</param>
public sealed record InstallManifest(
    int SchemaVersion,
    string ToolVersion,
    InstallStatus Status,
    DateTimeOffset InstalledAtUtc,
    string GameBuildId,
    bool BepInExPreexisting,
    IReadOnlyList<string> CreatedDirectories,
    IReadOnlyList<ManifestFile> Files)
{
    /// <summary>現在の記録形式の版。</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>JSON との相互変換に使う、ビルド時に生成される変換コード(実行時のリフレクションに頼らない)。</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InstallManifest))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext
{
}

/// <summary>導入記録の読み書き。</summary>
public static class ManifestStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>導入記録のファイルの絶対パス。</summary>
    public static string GetManifestPath(string gameRoot)
    {
        return Path.Combine(gameRoot, PatchLayout.PatchDataDirectory, PatchLayout.ManifestFileName);
    }

    /// <summary>導入記録を JSON 文字列へ変換する(副作用なし)。</summary>
    public static string Serialize(InstallManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.InstallManifest);
    }

    /// <summary>JSON 文字列から導入記録を復元する(副作用なし)。</summary>
    /// <exception cref="PatchException">JSON として読めない、または必要な項目が欠けている場合。</exception>
    public static InstallManifest Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        InstallManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.InstallManifest);
        }
        catch (JsonException ex)
        {
            throw new PatchException($"導入記録を読み取れません(内容が壊れています): {ex.Message}", ex);
        }

        // 記録は外部のファイルなので、型の上では null になり得ない項目も実際に確認する
        if (manifest is null || manifest.Files is null || manifest.CreatedDirectories is null)
        {
            throw new PatchException("導入記録を読み取れません(必要な項目がありません)。");
        }

        // 各要素も確認する。ここで弾いておかないと、後の処理が null を踏んで想定外の例外で止まってしまう。
        var hasBrokenEntry = manifest.ToolVersion is null
            || manifest.GameBuildId is null
            || manifest.CreatedDirectories.Any(string.IsNullOrWhiteSpace)
            || manifest.Files.Any(file => file is null || string.IsNullOrWhiteSpace(file.Path) || file.Sha256 is null);
        if (hasBrokenEntry)
        {
            throw new PatchException("導入記録を読み取れません(ファイルやフォルダの記録に、欠けている項目があります)。");
        }

        if (manifest.SchemaVersion > InstallManifest.CurrentSchemaVersion)
        {
            throw new PatchException(
                $"導入記録が、このツールより新しい版のツールで作られています(記録の版 {manifest.SchemaVersion})。新しい版のツールで操作してください。");
        }

        return manifest;
    }

    /// <summary>導入記録を読み込む。ファイルが無ければ null。</summary>
    /// <exception cref="PatchException">ファイルはあるが読めない・壊れている場合。</exception>
    public static InstallManifest? TryLoad(string gameRoot)
    {
        var path = GetManifestPath(gameRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PatchException($"導入記録を読み込めません: {path} ({ex.Message})", ex);
        }
    }

    /// <summary>
    /// 導入記録を保存する。書き込み途中で中断されても壊れた記録が残らないよう、別名で書いてから置き換える。
    /// </summary>
    public static void Save(string gameRoot, InstallManifest manifest)
    {
        var path = GetManifestPath(gameRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, Serialize(manifest), Utf8NoBom);
        File.Move(temporaryPath, path, overwrite: true);
    }
}

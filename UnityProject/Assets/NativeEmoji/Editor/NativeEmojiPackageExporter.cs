using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NativeEmoji.Editor
{
    /// <summary>
    /// 配布用の .unitypackage を書き出す。
    ///
    /// UPM（git URL）で入れる場合はこのツールは不要。
    /// Package Manager を使わないプロジェクト向けの配布手段。
    /// </summary>
    public static class NativeEmojiPackageExporter
    {
        // テストは配布物に含めない（利用側では実行されないため）
        private static readonly string[] ExportFolders = { "Runtime", "Editor", "Plugins" };
        private static readonly string[] ExportFiles = { "LICENSE.md", "CHANGELOG.md" };

        [MenuItem("Tools/Native Emoji/Export .unitypackage")]
        public static void Export()
        {
            string packageRoot = ResolvePackageRoot();
            if (packageRoot == null)
            {
                Debug.LogError("NativeEmojiPackageExporter: package.json が見つからない");
                return;
            }

            if (!packageRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"NativeEmojiPackageExporter: .unitypackage は Assets 配下からしか書き出せない（現在: {packageRoot}）。\n" +
                    "リポジトリを clone した作業用プロジェクトで実行する");
                return;
            }

            string version = ReadVersion(packageRoot);
            var assetPaths = CollectAssetPaths(packageRoot);

            if (assetPaths.Count == 0)
            {
                Debug.LogError("NativeEmojiPackageExporter: 書き出す対象が無い");
                return;
            }

            // Assets の 2 つ上がリポジトリのルート
            string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../dist"));
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir, $"native-emoji-{version}.unitypackage");
            AssetDatabase.ExportPackage(assetPaths.ToArray(), outputPath, ExportPackageOptions.Recurse);

            Debug.Log($"NativeEmojiPackageExporter: {outputPath} を書き出した（{assetPaths.Count} 項目 / v{version}）");
        }

        /// <summary>
        /// バッチモードから呼ぶ入口（-executeMethod 用）
        /// </summary>
        public static void ExportFromCommandLine()
        {
            Export();
            EditorApplication.Exit(0);
        }

        private static List<string> CollectAssetPaths(string packageRoot)
        {
            var paths = new List<string>();

            foreach (string folder in ExportFolders)
            {
                string path = $"{packageRoot}/{folder}";
                if (AssetDatabase.IsValidFolder(path))
                {
                    paths.Add(path);
                }
                else
                {
                    Debug.LogWarning($"NativeEmojiPackageExporter: {path} が無い");
                }
            }

            foreach (string file in ExportFiles)
            {
                string path = $"{packageRoot}/{file}";
                if (File.Exists(Path.GetFullPath(path)))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// package.json の位置からパッケージのルートを決める
        /// </summary>
        private static string ResolvePackageRoot()
        {
            foreach (string guid in AssetDatabase.FindAssets("package t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/NativeEmoji/package.json", StringComparison.Ordinal))
                {
                    return path.Substring(0, path.Length - "/package.json".Length);
                }
            }

            return null;
        }

        private static string ReadVersion(string packageRoot)
        {
            try
            {
                string json = File.ReadAllText(Path.GetFullPath($"{packageRoot}/package.json"));
                Match match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NativeEmojiPackageExporter: version を読めなかった - {e.Message}");
            }

            return "0.0.0";
        }
    }
}

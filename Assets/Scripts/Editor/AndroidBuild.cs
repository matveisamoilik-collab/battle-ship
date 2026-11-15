using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless signed Android App Bundle build.
///
///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod AndroidBuild.BuildAab
///             [-versionName 0.9.0] [-versionCode 10] -logFile Logs/batch_android.log
///
/// Keystore passwords are read from C:\project\keystore.txt (outside the repo):
///   line 1 = keystore password, line 2 = key (alias) password.
/// Output: aab/ShipWar-&lt;versionName&gt;.aab
/// </summary>
public static class AndroidBuild
{
    const string PasswordFile = @"C:\project\keystore.txt";

    [MenuItem("ShipButtlr/Build Android AAB")]
    public static void BuildAab()
    {
        string versionName = Arg("-versionName");
        string versionCode = Arg("-versionCode");
        if (!string.IsNullOrEmpty(versionName)) PlayerSettings.bundleVersion = versionName;
        if (!string.IsNullOrEmpty(versionCode)) PlayerSettings.Android.bundleVersionCode = int.Parse(versionCode);

        if (!File.Exists(PasswordFile))
            Fail("Password file not found: " + PasswordFile);
        string[] lines = File.ReadAllLines(PasswordFile).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        if (lines.Length < 1) Fail("Password file is empty: " + PasswordFile);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystorePass = lines[0];
        PlayerSettings.Android.keyaliasPass = lines.Length > 1 ? lines[1] : lines[0];
        if (!File.Exists(PlayerSettings.Android.keystoreName))
            Fail("Keystore not found: " + PlayerSettings.Android.keystoreName);

        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory("aab");
        string output = Path.Combine("aab", "ShipWar-" + PlayerSettings.bundleVersion + ".aab");

        Debug.Log($"[AndroidBuild] {PlayerSettings.applicationIdentifier} v{PlayerSettings.bundleVersion} " +
                  $"(code {PlayerSettings.Android.bundleVersionCode}) -> {output}; scenes: {string.Join(", ", scenes)}");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });

        // Never leave the passwords in memory-backed settings that may get saved.
        PlayerSettings.Android.keystorePass = "";
        PlayerSettings.Android.keyaliasPass = "";
        AssetDatabase.SaveAssets();

        if (report.summary.result != BuildResult.Succeeded)
            Fail($"Build {report.summary.result}, {report.summary.totalErrors} error(s)");
        Debug.Log($"[AndroidBuild] SUCCESS {output} ({report.summary.totalSize / (1024 * 1024)} MB)");
    }

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void Fail(string msg)
    {
        Debug.LogError("[AndroidBuild] FAILED: " + msg);
        if (Application.isBatchMode) EditorApplication.Exit(1);
        throw new Exception(msg);
    }
}

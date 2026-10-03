using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildWebGL
{
    public static void BuildPlayable()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/GameCoreScene.unity" },
            locationPathName = "Build/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception($"WebGL build failed: {report.summary.result}");
    }
}

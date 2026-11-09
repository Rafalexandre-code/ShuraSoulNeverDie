using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shura.EditorTools
{
    /// <summary>
    /// One-click project configuration and builds. Also callable from the command line:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Shura.EditorTools.ProjectSetup.Run
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Shura.EditorTools.ProjectSetup.BuildWindowsDevelopment
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/_Shura/Scenes/Main.unity";
        const string BuildPath = "Builds/Windows/Shura.exe";

        [MenuItem("Shura/Configurar projeto")]
        public static void Run()
        {
            PlayerSettings.companyName = "Shura";
            PlayerSettings.productName = "Shura";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Game");
                go.AddComponent<Game>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[Shura] Cena criada em " + ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Shura] Projeto configurado.");
        }

        [MenuItem("Shura/Abrir cena principal")]
        public static void OpenMainScene()
        {
            if (!File.Exists(ScenePath)) Run();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Shura/Build Windows (desenvolvimento)")]
        public static void BuildWindowsDevelopment()
        {
            Build(BuildOptions.Development);
        }

        [MenuItem("Shura/Build Windows (final)")]
        public static void BuildWindowsRelease()
        {
            Build(BuildOptions.None);
        }

        static void Build(BuildOptions options)
        {
            Run();
            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = options,
            };
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;
            // Online through Steam: the Spacewar app id must sit next to the executable.
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BuildPath), "steam_appid.txt"), "480");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Shura] steam_appid.txt: " + e.Message);
            }
            Debug.Log("[Shura] Build " + summary.result + " em " + summary.totalTime + " (" + summary.totalErrors + " erros) → " + BuildPath);
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}

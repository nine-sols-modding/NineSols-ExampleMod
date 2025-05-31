using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExampleMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class ExampleMod : BaseUnityPlugin {
    private ConfigEntry<KeyboardShortcut> loadBundleShortcut = null!;

    private CancellationTokenSource unloadToken = new();
    private Harmony harmony = null!;

    private AssetBundle? bundle;
    private CancellationTokenSource? cancellation;
    private List<GameObject> loadedObjects = [];

    private void Awake() {
        Log.Init(Logger);

        harmony = Harmony.CreateAndPatchAll(typeof(ExampleMod).Assembly);

        loadBundleShortcut = Config.Bind("General.Something",
            "Shortcut",
            new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl),
            "Shortcut to execute");

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void Update() {
        if (loadBundleShortcut.Value.IsDown()) {
            Log.Info("Loading asset bundle");
            LoadAssetBundle();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        TrySkipLoadingScreen(scene);
    }

    private static void TrySkipLoadingScreen(Scene scene) {
        if (scene.name == "Pre_Menu_Intro") {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu_Title");
        }
    }

    private void LoadAssetBundle() {
        // The bundle is defined in the .csproj as <EmbeddedResource />
        // var assetBundle = AssemblyUtils.GetEmbeddedAssetBundle("ExampleMod.bundle.unity3d");
        // const string path = @"/home/jakob/dev/unity/unity-scene-repacker/out/hollowknight.unity3d";
        const string path = @"/home/jakob/dev/unity/unity-scene-repacker/out/hollowknight.unity3d";
        Log.Info(path);

        var allBytes = File.ReadAllBytes(path);
        var sw = Stopwatch.StartNew();

        cancellation?.Cancel();
        cancellation = new CancellationTokenSource();
        bundle?.Unload(true);

        bundle = AssetBundle.LoadFromMemory(allBytes);

        Log.Info($"LoadFromMemory took {sw.ElapsedMilliseconds}ms");

        // In a real mod you probably want to load the assetbundle once when you want to use it,
        // and keep the spawned scene in memory if they're not too big.
        // There's a bunch of optimizations you can figure out here.
        if (!bundle) {
            Log.Info("Failed to load AssetBundle");
            return;
        }

        StartCoroutine(Stream(bundle, cancellation.Token, 0, false));
    }


    // Loads all the scenes contained in the AssetBundle, and spawn copies of their objects
    private IEnumerator Stream(AssetBundle bundle, CancellationToken token, float delay = 0.5f, bool oneByOne = true) {
        foreach (var scenePath in bundle.GetAllScenePaths()) {
            if (token.IsCancellationRequested) break;

            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);

            foreach (var prefab in scene.GetRootGameObjects()) {
                if (!prefab) {
                    Log.Info("Prefab is null");
                    continue;
                }

                if (oneByOne) loadedObjects.ForEach(Destroy);
                var instantiation = Instantiate(prefab);
                instantiation.SetActive(true);

                var pos = HeroController.instance.transform.position + Vector3.right * 5;
                instantiation.transform.position = pos;
                loadedObjects.Add(instantiation);

                yield return new WaitForSeconds(delay);
            }

            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }

        loadedObjects.ForEach(Destroy);
    }


    // Loads all the scenes contained in the AssetBundle, and spawn copies of their objects
    private static IEnumerator LoadAllThenSpawn(AssetBundle bundle) {
        var sceneNames = bundle.GetAllScenePaths()
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        var start = Time.time;
        var ops = sceneNames
            .Select(sceneName =>
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive))
            .ToList();

        while (ops.Any(op => op.progress < 1)) {
            var progress = ops.Sum(op => op.progress) / ops.Count;
            Log.Info($"{progress * 100:F0}%");
            yield return null;
        }

        Log.Info($"Scene load took {(Time.time - start) * 1000:F0}ms");
        bundle.Unload(false);

        try {
            List<GameObject> allObjects = [];
            foreach (var prefab in sceneNames.Select(UnityEngine.SceneManagement.SceneManager.GetSceneByName)
                         .SelectMany(scene => scene.GetRootGameObjects())) {
                var instantiation = Instantiate(prefab);
                // instantiation.SetActive(true);
                instantiation.transform.position = HeroController.instance.transform.position
                                                   + Vector3.right * Random.Range(-10, 10);
                allObjects.Add(instantiation);
            }

            yield return new WaitForSeconds(0.5f);

            allObjects.ForEach(Destroy);
        } finally {
            sceneNames.ForEach(sceneName => UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName));
        }
    }


    private void OnDestroy() {
        // Make sure to clean up resources here to support hot reloading
        unloadToken.Cancel();

        Log.Info($"unloading {bundle?.name}");
        bundle?.Unload(true);


        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        harmony.UnpatchSelf();
    }
}
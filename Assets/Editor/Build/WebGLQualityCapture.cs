using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClayWars.Build
{
    /// <summary>
    /// Renders fixed shots used to compare image quality before and after the
    /// WebGL texture work.
    ///
    /// The editor imports textures for the active build target, so these shots
    /// only reflect the WebGL overrides while WebGL is active. The active target
    /// is written to the log with every capture; check it before trusting a pair.
    ///
    ///   unity run &lt;project&gt; -- -executeMethod ClayWars.Build.WebGLQualityCapture.CaptureAll
    ///
    /// Output goes to Logs/captures/&lt;label&gt;_&lt;shot&gt;.png, with the label taken from
    /// the CLAYWARS_CAPTURE_LABEL environment variable (default "capture").
    /// </summary>
    public static class WebGLQualityCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const string BattleScene = "Assets/Scenes/Battle.unity";

        private static readonly string[] UnitPrefabs =
        {
            "Assets/Prefabs/Units/UnitWarrior.prefab",
            "Assets/Prefabs/Units/UnitArcher.prefab",
            "Assets/Prefabs/Units/UnitHunter.prefab",
            "Assets/Prefabs/Units/UnitAssassin.prefab",
        };

        public static void CaptureAll()
        {
            Debug.Log($"[Capture] activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}");
            string label = Environment.GetEnvironmentVariable("CLAYWARS_CAPTURE_LABEL") ?? "capture";
            EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Single);
            CaptureBattle(label);
            CaptureUnits(label);
        }

        /// <summary>The battlefield as the game frames it: ground, buildings and the rocks.</summary>
        private static void CaptureBattle(string label)
        {
            Camera camera = FindCamera();
            if (camera == null) { Debug.LogError("[Capture] Battle scene has no camera."); return; }
            Save(camera, label, "battle");
        }

        /// <summary>
        /// A close-up of the four unit prefabs, which carry the diffuse and normal
        /// maps the resolution and Crunch stages changed most.
        ///
        /// The props are added to the battle scene rather than a fresh one. A scene
        /// created with NewScene has no path, so leaving it behind makes the editor
        /// raise a Save As dialog on the way out, which hangs a headless run until
        /// the timeout kills it. Everything added here is destroyed before returning
        /// and the scene is never saved.
        /// </summary>
        private static void CaptureUnits(string label)
        {
            // Well clear of the battlefield, so scene geometry cannot wander into frame.
            var origin = new Vector3(0f, 1000f, 0f);
            var temporary = new System.Collections.Generic.List<GameObject>();
            try
            {
                var lightObject = new GameObject("Capture Light");
                temporary.Add(lightObject);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                lightObject.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

                float offset = -2.1f;
                foreach (string path in UnitPrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) { Debug.LogWarning($"[Capture] Missing prefab {path}"); continue; }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    temporary.Add(instance);
                    instance.transform.position = origin + new Vector3(offset, 0f, 0f);
                    instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    offset += 1.4f;
                }

                var cameraObject = new GameObject("Capture Camera");
                temporary.Add(cameraObject);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = origin + new Vector3(0f, 1.0f, -4.8f);
                camera.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
                camera.fieldOfView = 40f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.16f, 0.18f);
                Save(camera, label, "units");
            }
            finally
            {
                foreach (GameObject item in temporary)
                    if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static Camera FindCamera() =>
            Camera.main ?? UnityEngine.Object
                .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.enabled);

        private static void Save(Camera camera, string label, string shot)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                // Unity 6 render requests are the supported path under SRP;
                // camera.Render() is kept as the fallback for anything that
                // reports the request as unsupported.
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                }

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();

                string directory = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, "Logs", "captures");
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, $"{label}_{shot}.png");
                File.WriteAllBytes(file, readback.EncodeToPNG());
                Debug.Log($"[Capture] Wrote {file}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(readback);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

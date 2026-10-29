using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ClayWars.Build
{
    /// <summary>
    /// Reports and changes the WebGL code-generation settings that decide how big
    /// the wasm ends up. Each stage is applied on its own so a size change can be
    /// attributed to one setting.
    ///
    /// Values are written to ProjectSettings rather than forced at build time, so
    /// the setting is visible in review and CI builds get the same thing.
    /// </summary>
    public static class WebGLCodeSettings
    {
        private static readonly NamedBuildTarget Target = NamedBuildTarget.WebGL;

        [MenuItem("ClayWars/WebGL/Report code settings")]
        public static void Report()
        {
            Debug.Log(
                "[WebGL] code settings: " +
                $"il2cppCodeGeneration={PlayerSettings.GetIl2CppCodeGeneration(Target)} " +
                $"managedStrippingLevel={PlayerSettings.GetManagedStrippingLevel(Target)} " +
                $"stripEngineCode={PlayerSettings.stripEngineCode} " +
                $"exceptionSupport={PlayerSettings.WebGL.exceptionSupport} " +
                $"apiCompatibility={PlayerSettings.GetApiCompatibilityLevel(Target)} " +
                $"linkerTarget={PlayerSettings.WebGL.linkerTarget} " +
                $"debugSymbols={PlayerSettings.WebGL.debugSymbolMode}");
        }

        public static void ApplyOptimizeSize() => Set(() =>
            PlayerSettings.SetIl2CppCodeGeneration(Target, Il2CppCodeGeneration.OptimizeSize));

        public static void ApplyOptimizeSpeed() => Set(() =>
            PlayerSettings.SetIl2CppCodeGeneration(Target, Il2CppCodeGeneration.OptimizeSpeed));

        public static void ApplyStrippingLow() => Set(() =>
            PlayerSettings.SetManagedStrippingLevel(Target, ManagedStrippingLevel.Low));

        public static void ApplyStrippingMedium() => Set(() =>
            PlayerSettings.SetManagedStrippingLevel(Target, ManagedStrippingLevel.Medium));

        public static void ApplyStrippingHigh() => Set(() =>
            PlayerSettings.SetManagedStrippingLevel(Target, ManagedStrippingLevel.High));

        private static void Set(Action change)
        {
            change();
            AssetDatabase.SaveAssets();
            Report();
        }
    }
}

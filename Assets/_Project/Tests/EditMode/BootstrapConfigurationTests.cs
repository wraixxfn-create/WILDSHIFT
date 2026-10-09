using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.Bootstrap;

namespace Wildshift.Tests
{
    /// <summary>Verifies the bootstrap configuration and Build Settings that the entry point depends on.</summary>
    public sealed class BootstrapConfigurationTests
    {
        private const string BootstrapSettingsPath = "Assets/_Project/Settings/BootstrapSettings.asset";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype.unity";

        [Test]
        public void DefaultTargetSceneIsPrototype()
        {
            BootstrapSettings settings = ScriptableObject.CreateInstance<BootstrapSettings>();
            try
            {
                Assert.That(settings.TargetSceneName, Is.EqualTo("Prototype"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildSettingsStartWithBootstrapThenPrototype()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2), "Build Settings must list at least two scenes.");
            Assert.That(scenes[0].path, Is.EqualTo(BootstrapScenePath));
            Assert.That(scenes[1].path, Is.EqualTo(PrototypeScenePath));
            Assert.That(scenes[0].enabled, Is.True, "Bootstrap must be enabled in Build Settings.");
            Assert.That(scenes[1].enabled, Is.True, "Prototype must be enabled in Build Settings.");
        }

        [Test]
        public void ConfiguredTargetSceneIsEnabledInBuildSettings()
        {
            BootstrapSettings settings = AssetDatabase.LoadAssetAtPath<BootstrapSettings>(BootstrapSettingsPath);
            Assert.That(settings, Is.Not.Null, $"Missing BootstrapSettings asset at {BootstrapSettingsPath}.");

            bool found = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && Path.GetFileNameWithoutExtension(scene.path) == settings.TargetSceneName)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True, $"Target scene '{settings.TargetSceneName}' is not enabled in Build Settings.");
        }
    }
}

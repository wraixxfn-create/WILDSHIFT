using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wildshift.Player.Camera;
using Wildshift.Player.Input;

namespace Wildshift.Tests
{
    /// <summary>Exercises real lifecycle/cursor state in the authored scene, in an interactive editor.</summary>
    public sealed class CameraCursorTests
    {
        private Scene _scene;
        private CursorLockMode _originalLock;
        private bool _originalVisible;

        [UnityTest]
        public IEnumerator GameplayMenuDisableAndTargetRemovalRestoreCursor()
        {
            if (Application.isBatchMode)
                Assert.Ignore("OS cursor verification requires an interactive focused Game view, not batch mode.");

            _originalLock = Cursor.lockState;
            _originalVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return SceneManager.LoadSceneAsync("Prototype", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByName("Prototype");
            ThirdPersonCamera camera = null;
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ThirdPersonCamera foundCamera)) camera = foundCamera;
                if (root.TryGetComponent(out PlayerInputReader foundInput)) input = foundInput;
            }
            Assert.That(camera, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            camera.ResumeGameplayCursor();
            yield return null;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(Cursor.visible, Is.False);

            input.SetGameplayInputEnabled(false);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Menus release synchronously, not one frame later.");
            Assert.That(Cursor.visible, Is.True);
            input.SetGameplayInputEnabled(true);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));

            camera.enabled = false;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            camera.enabled = true;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            camera.Target = null;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            if (!Application.isBatchMode)
            {
                Cursor.lockState = _originalLock;
                Cursor.visible = _originalVisible;
            }
        }
    }
}

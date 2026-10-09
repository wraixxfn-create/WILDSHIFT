using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.World.Clock;

namespace Wildshift.Tests
{
    /// <summary>Exercises the scene-owned world clock host in Play Mode: frame-driven advancement, pause, and duplicate rejection.</summary>
    public sealed class WorldClockHostTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _objects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator HostDrivesOneAuthoritativeClockFromFrameTime()
        {
            GameObject clockObject = CreateObject("World Clock");
            WorldClockHost host = clockObject.AddComponent<WorldClockHost>();

            Assert.That(host.Clock, Is.Not.Null, "The clock is created during Awake.");
            Assert.That(host.Clock.IsPaused, Is.False);

            yield return null;
            yield return null;
            Assert.That(host.Clock.ElapsedTime, Is.GreaterThan(0d), "The host should forward frame time to the clock.");

            host.Clock.Pause();
            double pausedAt = host.Clock.ElapsedTime;
            yield return null;
            yield return null;
            Assert.That(host.Clock.ElapsedTime, Is.EqualTo(pausedAt), "A paused clock must hold still across frames.");

            host.Clock.Resume();
            yield return null;
            yield return null;
            Assert.That(host.Clock.ElapsedTime, Is.GreaterThan(pausedAt), "Resuming should let frame time advance the clock again.");
        }

        [UnityTest]
        public IEnumerator SecondHostIsRejectedWhileTheFirstIsAlive()
        {
            GameObject firstObject = CreateObject("World Clock");
            WorldClockHost first = firstObject.AddComponent<WorldClockHost>();
            Assert.That(first.Clock, Is.Not.Null);

            LogAssert.Expect(LogType.Error, new Regex("second WorldClockHost"));
            GameObject duplicateObject = CreateObject("World Clock Duplicate");
            WorldClockHost duplicate = duplicateObject.AddComponent<WorldClockHost>();
            yield return null;

            Assert.That(duplicate == null, Is.True, "The duplicate host must destroy itself.");
            Assert.That(first.Clock, Is.Not.Null, "The authoritative clock must survive the rejected duplicate.");
        }

        private GameObject CreateObject(string name)
        {
            GameObject created = new GameObject(name);
            _objects.Add(created);
            return created;
        }
    }
}

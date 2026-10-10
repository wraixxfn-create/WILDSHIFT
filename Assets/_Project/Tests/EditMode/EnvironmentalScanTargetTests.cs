using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.Environment.Scanning;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies that a scan target exposes authored identity and text from its definition and
    /// rejects incomplete authoring without depending on scene hierarchy names.
    /// </summary>
    public sealed class EnvironmentalScanTargetTests
    {
        private GameObject _object;
        private EnvironmentalScanTarget _target;
        private EnvironmentalScanDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnvironmentalScanDefinition>();
            SetDefinitionField(_definition, "_stableId", "test/scan/soil-a");
            SetDefinitionField(_definition, "_displayName", "Test soil scraping");
            SetDefinitionField(_definition, "_description", "A faint pearlescent scraping.");
            SetDefinitionField(_definition, "_scanResult", "Moisture high. Residue recent.");

            _object = new GameObject("Scan Target Test");
            _object.SetActive(false);
            _target = _object.AddComponent<EnvironmentalScanTarget>();
            SetObjectReference(_target, "_definition", _definition);
            _object.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ExposesAuthoredIdentityAndScanText()
        {
            Assert.That(_target.TargetId, Is.EqualTo("test/scan/soil-a"));
            Assert.That(_target.DisplayName, Is.EqualTo("Test soil scraping"));
            Assert.That(_target.Description, Is.EqualTo("A faint pearlescent scraping."));
            Assert.That(_target.ScanResult, Is.EqualTo("Moisture high. Residue recent."));
            Assert.That(_target.IsScanAvailable, Is.True);
        }

        [Test]
        public void IsUnavailableWhenTheStableIdIsBlank()
        {
            SetDefinitionField(_definition, "_stableId", "   ");
            Assert.That(_target.IsScanAvailable, Is.False);
        }

        [Test]
        public void IsUnavailableWhenDisabledOrMissingADefinition()
        {
            _target.enabled = false;
            Assert.That(_target.IsScanAvailable, Is.False);

            _target.enabled = true;
            SetObjectReference(_target, "_definition", null);
            Assert.That(_target.IsScanAvailable, Is.False);
            Assert.That(_target.TargetId, Is.Null);
        }

        private static void SetObjectReference(Object component, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDefinitionField(ScriptableObject definition, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

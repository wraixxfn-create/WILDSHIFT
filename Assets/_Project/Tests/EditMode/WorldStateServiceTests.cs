using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.World;

namespace Wildshift.Tests
{
    /// <summary>Verifies stable-ID lookup, runtime updates, error reporting, and state isolation.</summary>
    public sealed class WorldStateServiceTests
    {
        private readonly List<RegionDefinition> _definitions = new List<RegionDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (RegionDefinition definition in _definitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            _definitions.Clear();
        }

        [Test]
        public void RegistersAndRetrievesARegionByItsStableId()
        {
            RegionDefinition definition = CreateDefinition("nacre/coast/north", "North Coast", 7);
            definition.name = "A_Different_Scene_Object_Name";
            WorldStateService service = new WorldStateService();

            Assert.That(service.TryRegisterRegion(definition, out string registerError), Is.True, registerError);
            Assert.That(service.RegisteredRegionCount, Is.EqualTo(1));
            Assert.That(service.TryGetRegion("nacre/coast/north", out RegionState state, out string getError),
                Is.True, getError);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.StableId, Is.EqualTo("nacre/coast/north"));
            Assert.That(state.DisplayName, Is.EqualTo("North Coast"));
            Assert.That(state.TestValue, Is.EqualTo(7));
        }

        [Test]
        public void TestValueUpdatesChangeRuntimeStateWithoutChangingTheDefinition()
        {
            RegionDefinition definition = CreateDefinition("nacre/upland/east", "East Upland", 3);
            WorldStateService service = new WorldStateService();
            Assert.That(service.TryRegisterRegion(definition, out string registerError), Is.True, registerError);

            Assert.That(service.TryUpdateTestValue("nacre/upland/east", 42, out string updateError),
                Is.True, updateError);
            Assert.That(service.TryGetRegion("nacre/upland/east", out RegionState state, out string getError),
                Is.True, getError);

            Assert.That(state.TestValue, Is.EqualTo(42));
            Assert.That(definition.InitialTestValue, Is.EqualTo(3),
                "A runtime update must not write back into the shared authored asset.");
        }

        [Test]
        public void DifferentRegionIdsHaveIndependentMutableState()
        {
            RegionDefinition firstDefinition = CreateDefinition("nacre/valley/west", "West Valley", 1);
            RegionDefinition secondDefinition = CreateDefinition("nacre/reef/south", "South Reef", 10);
            WorldStateService service = new WorldStateService();
            Assert.That(service.TryRegisterRegion(firstDefinition, out string firstError), Is.True, firstError);
            Assert.That(service.TryRegisterRegion(secondDefinition, out string secondError), Is.True, secondError);
            Assert.That(service.TryGetRegion("nacre/valley/west", out RegionState firstState, out string firstGetError),
                Is.True, firstGetError);
            Assert.That(service.TryGetRegion("nacre/reef/south", out RegionState secondState, out string secondGetError),
                Is.True, secondGetError);
            Assert.That(firstState, Is.Not.SameAs(secondState));

            Assert.That(service.TryUpdateTestValue("nacre/valley/west", 55, out string updateError),
                Is.True, updateError);

            Assert.That(firstState.TestValue, Is.EqualTo(55));
            Assert.That(secondState.TestValue, Is.EqualTo(10), "Updating one region must not alter another region.");
        }

        [Test]
        public void SeparateServicesDoNotShareMutableStateFromOneDefinition()
        {
            RegionDefinition definition = CreateDefinition("nacre/basin/central", "Central Basin", 5);
            WorldStateService firstSession = new WorldStateService();
            WorldStateService secondSession = new WorldStateService();
            Assert.That(firstSession.TryRegisterRegion(definition, out string firstError), Is.True, firstError);
            Assert.That(secondSession.TryRegisterRegion(definition, out string secondError), Is.True, secondError);

            Assert.That(firstSession.TryUpdateTestValue("nacre/basin/central", 99, out string updateError),
                Is.True, updateError);
            Assert.That(secondSession.TryGetRegion("nacre/basin/central", out RegionState secondState, out string getError),
                Is.True, getError);

            Assert.That(secondState.TestValue, Is.EqualTo(5));
            Assert.That(definition.InitialTestValue, Is.EqualTo(5));
        }

        [Test]
        public void UnknownRegionReturnsAnErrorWithoutThrowing()
        {
            WorldStateService service = new WorldStateService();

            bool found = service.TryGetRegion("nacre/unknown", out RegionState state, out string getError);
            Assert.That(found, Is.False);
            Assert.That(state, Is.Null);
            Assert.That(getError, Does.Contain("nacre/unknown"));

            bool updated = service.TryUpdateTestValue("nacre/unknown", 12, out string updateError);
            Assert.That(updated, Is.False);
            Assert.That(updateError, Does.Contain("nacre/unknown"));
        }

        [Test]
        public void DuplicateStableIdsAreRejectedWithoutReplacingExistingState()
        {
            RegionDefinition original = CreateDefinition("nacre/highland/north", "North Highland", 2);
            RegionDefinition duplicate = CreateDefinition("nacre/highland/north", "Not the same region", 99);
            WorldStateService service = new WorldStateService();
            Assert.That(service.TryRegisterRegion(original, out string registerError), Is.True, registerError);

            bool registered = service.TryRegisterRegion(duplicate, out string duplicateError);
            Assert.That(registered, Is.False);
            Assert.That(duplicateError, Does.Contain("nacre/highland/north"));
            Assert.That(service.RegisteredRegionCount, Is.EqualTo(1));
            Assert.That(service.TryGetRegion("nacre/highland/north", out RegionState state, out string getError),
                Is.True, getError);
            Assert.That(state.DisplayName, Is.EqualTo("North Highland"));
            Assert.That(state.TestValue, Is.EqualTo(2));
        }

        private RegionDefinition CreateDefinition(string stableId, string displayName, int initialTestValue)
        {
            RegionDefinition definition = ScriptableObject.CreateInstance<RegionDefinition>();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("_stableId").stringValue = stableId;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_initialTestValue").intValue = initialTestValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _definitions.Add(definition);
            return definition;
        }
    }
}

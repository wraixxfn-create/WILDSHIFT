using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wildshift.World;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>
    /// Creates region assets and scene objects for Edit Mode tests and destroys them at teardown. Authored fields are
    /// written through SerializedObject, the same path the Inspector uses, so tests exercise the real serialized data.
    /// </summary>
    internal sealed class WorldRegionTestObjects
    {
        private readonly List<Object> _roots = new List<Object>();
        private int _assetCounter;

        /// <summary>Creates a RegionDefinition asset in memory with the given authored values.</summary>
        public RegionDefinition CreateDefinition(string stableId, string displayName = "Test Region", string assetName = null)
        {
            RegionDefinition definition = ScriptableObject.CreateInstance<RegionDefinition>();
            _assetCounter++;
            definition.name = assetName ?? "RegionDefinition_" + _assetCounter;
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("_stableId").stringValue = stableId;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _roots.Add(definition);
            return definition;
        }

        /// <summary>Creates a WorldRegionCatalog listing the given definitions in order. Null entries are allowed for tests.</summary>
        public WorldRegionCatalog CreateCatalog(params RegionDefinition[] regions)
        {
            WorldRegionCatalog catalog = ScriptableObject.CreateInstance<WorldRegionCatalog>();
            _assetCounter++;
            catalog.name = "WorldRegionCatalog_" + _assetCounter;
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("_regions");
            list.arraySize = regions.Length;
            for (int i = 0; i < regions.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = regions[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            _roots.Add(catalog);
            return catalog;
        }

        /// <summary>Creates a GameObject. Objects created without a parent are destroyed by <see cref="DestroyAll"/>.</summary>
        public GameObject CreateObject(string name, Transform parent = null)
        {
            GameObject gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }
            else
            {
                _roots.Add(gameObject);
            }

            return gameObject;
        }

        /// <summary>Creates a child object with a WorldRegionVolume configured through its serialized fields.</summary>
        public WorldRegionVolume CreateVolume(Transform parent, RegionDefinition definition, Vector3 localCenter,
            Vector3 localSize, int priority, string name = "Volume")
        {
            GameObject gameObject = CreateObject(name, parent);
            WorldRegionVolume volume = gameObject.AddComponent<WorldRegionVolume>();
            SerializedObject serialized = new SerializedObject(volume);
            serialized.FindProperty("_definition").objectReferenceValue = definition;
            serialized.FindProperty("_localCenter").vector3Value = localCenter;
            serialized.FindProperty("_localSize").vector3Value = localSize;
            serialized.FindProperty("_priority").intValue = priority;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return volume;
        }

        /// <summary>Replaces the locator's Volumes list. Null entries are allowed for tests.</summary>
        public static void SetLocatorVolumes(WorldRegionLocator locator, IReadOnlyList<WorldRegionVolume> volumes)
        {
            SerializedObject serialized = new SerializedObject(locator);
            SerializedProperty list = serialized.FindProperty("_volumes");
            list.arraySize = volumes.Count;
            for (int i = 0; i < volumes.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = volumes[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Reads the locator's Volumes list back through SerializedObject, including empty entries.</summary>
        public static List<WorldRegionVolume> GetLocatorVolumes(WorldRegionLocator locator)
        {
            SerializedObject serialized = new SerializedObject(locator);
            SerializedProperty list = serialized.FindProperty("_volumes");
            List<WorldRegionVolume> volumes = new List<WorldRegionVolume>(list.arraySize);
            for (int i = 0; i < list.arraySize; i++)
            {
                volumes.Add((WorldRegionVolume)list.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            return volumes;
        }

        /// <summary>Assigns the locator's Region Catalog.</summary>
        public static void SetLocatorCatalog(WorldRegionLocator locator, WorldRegionCatalog catalog)
        {
            SerializedObject serialized = new SerializedObject(locator);
            serialized.FindProperty("_catalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Destroys every root object this helper created, including the children of those roots.</summary>
        public void DestroyAll()
        {
            foreach (Object root in _roots)
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }

            _roots.Clear();
        }
    }
}

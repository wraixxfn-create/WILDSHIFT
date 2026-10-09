using System;
using UnityEngine;

namespace Wildshift.Persistence.Model
{
    /// <summary>
    /// Durable copy of one named numeric parameter attached to a saved world event.
    /// Mirrors <c>PlayerActionEventParameter</c>: a stable ID plus a finite number, never free-form text.
    /// </summary>
    [Serializable]
    public sealed class WorldEventParameterSaveData
    {
        [SerializeField] private string id;
        [SerializeField] private float value;

        /// <summary>Creates an empty record; used by the JSON deserializer.</summary>
        public WorldEventParameterSaveData()
        {
        }

        /// <summary>Creates a record for one event parameter.</summary>
        public WorldEventParameterSaveData(string id, float value)
        {
            this.id = id;
            this.value = value;
        }

        /// <summary>Stable parameter ID; unique within its event.</summary>
        public string Id => id;

        /// <summary>Finite parameter value.</summary>
        public float Value => value;
    }
}

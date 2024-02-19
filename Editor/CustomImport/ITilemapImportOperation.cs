using System.Collections.Generic;
using UnityEngine;

namespace ReverseGravity.TiledTransposer {
	/// <summary>
	/// Interface for customizing Tiled layers or objects during the import process.
	/// Implementations of this interface are invoked when layers or objects with custom properties are loaded.
	/// This enables the custom processing of objects based on their properties.
	/// </summary>
	public interface ITilemapImportOperation {
		/// <summary>
		/// Invoked when a Tiled layer or object with custom properties is imported into the scene using the Tilemap TMX Importer.
		/// </summary>
		/// <param name="gameObject">The GameObject to be customized.</param>
		/// <param name="customProperties">A dictionary containing the custom properties associated with the object.</param>
		void HandleCustomProperties(GameObject gameObject, IDictionary<string, string> customProperties);
	}
}

// Example usage (place your class in an editor folder)
/*
class CustomImporterAddBoxCollider : ReverseGravity.ITilemapImportOperation
{
    public void HandleCustomProperties(UnityEngine.GameObject gameObject,
        IDictionary<string, string> props)
    {
        // Add a BoxCollider2D component to the GameObject if the custom property "AddBoxCollider" is set to "true"
        if (props.ContainsKey("AddBoxCollider") && bool.TryParse(props["AddBoxCollider"], out bool addBoxCollider) && addBoxCollider)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }
    }
}

    This example demonstrates how to use the custom property handling system to add a BoxCollider2D component to a
    GameObject when the custom property "AddBoxCollider" is set to "true". This is especially useful when you want to
    add collision data to certain tiles or objects in the Tiled map.

    This usage and property handling system is designed to be consistent with the one found in the Tiled2Unity project.
    This ensures compatibility, allowing user extensions to work seamlessly with both tools.
*/
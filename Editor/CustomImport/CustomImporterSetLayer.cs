using System.Collections.Generic;
using UnityEngine;

namespace ReverseGravity.TiledTransposer {
	/// <summary> A custom importer for setting the layer of a GameObject during the tilemap import process.</summary>
	public class CustomImporterSetLayer : ITilemapImportOperation {
		/// <summary> Handles custom properties to set the layer of a GameObject based on the provided custom properties.</summary>
		/// <param name="gameObject">The GameObject to set the layer for.</param>
		/// <param name="customProperties">A dictionary containing the custom properties for the GameObject.</param>
		public void HandleCustomProperties(GameObject gameObject, IDictionary<string, string> customProperties) {
			if (customProperties.TryGetValue("unity:layer", out var layerName)) {
				var layerID = LayerMask.NameToLayer(layerName);
				if (layerID >= 0)
					gameObject.layer = layerID;
				else
					Debug.LogError("The TMX map is expecting a layer called " + layerName +
					               " to exist, but it is not configured in your Unity project");
			}
		}
	}
}
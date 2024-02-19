using System.Collections.Generic;
using UnityEngine;

namespace ReverseGravity.TiledTransposer {
	/// <summary> A custom importer for setting the tag during the tilemap import process.</summary>
	public class CustomImporterSetTag : ITilemapImportOperation {
		/// <summary> Handles custom properties to set the tag of a GameObject based on the provided custom properties.</summary>
		/// <param name="gameObject">The GameObject to set the tag for.</param>
		/// <param name="customProperties">A dictionary containing the custom properties for the GameObject.</param>
		public void HandleCustomProperties(GameObject gameObject, IDictionary<string, string> customProperties) {
			if (customProperties.TryGetValue("unity:tag", out var tagValue)) gameObject.tag = tagValue;
		}
	}
}
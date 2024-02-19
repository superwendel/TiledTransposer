using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ReverseGravity.TiledTransposer {
	/// <summary> Custom importer for spawning prefabs on tiles or objects.</summary>
	public class CustomImporterSpawnPrefabsOnTiles : ITilemapImportOperation {
		/// <summary> Handles custom properties for the game object.</summary>
		/// <param name="gameObject">The game object to handle custom properties for.</param>
		/// <param name="customProperties">The custom properties for the game object.</param>
		public void HandleCustomProperties(GameObject gameObject, IDictionary<string, string> customProperties) {
			if (customProperties.ContainsKey("unity:prefab") || customProperties.ContainsKey("unity:prefabReplace")) {
				var replace = false;
				string prefabName;
				if (customProperties.ContainsKey("unity:prefabReplace")) {
					prefabName = customProperties["unity:prefabReplace"];
					replace = true;
				}
				else {
					prefabName = customProperties["unity:prefab"];
				}

				var toSpawnPath = "Assets" + Path.AltDirectorySeparatorChar + prefabName + ".prefab";
				var toSpawn = AssetDatabase.LoadMainAssetAtPath(toSpawnPath) as GameObject;
				if (toSpawn is null) {
					Debug.LogError(
						$"Could not find prefab \"{prefabName}\" in the assets folder at path \"{toSpawnPath}\" for object \"{gameObject.name}\"");
					return;
				}

				var tilemap = gameObject.GetComponent<Tilemap>();
				if (tilemap is not null) {
					// Look for tile instances, and spawn a prefab on each tile
					for (var x = tilemap.cellBounds.xMin; x < tilemap.cellBounds.xMax; x++)
					for (var y = tilemap.cellBounds.yMin; y < tilemap.cellBounds.yMax; y++) {
						var tileCoord = new Vector3Int(x, y, 0);
						if (tilemap.HasTile(tileCoord)) {
							var worldCoord = tilemap.layoutGrid.GetCellCenterWorld(tileCoord);
							SpawnPrefabOnTile(toSpawn, gameObject, worldCoord);

							if (replace) tilemap.SetTile(tileCoord, null);
						}
					}
				}
				else {
					// Just spawn as a child of the object (or on each child if there are children)
					if (gameObject.transform.childCount > 0) {
						var children = new GameObject[gameObject.transform.childCount];
						for (var i = 0; i < gameObject.transform.childCount; i++)
							children[i] = gameObject.transform.GetChild(i).gameObject;
						for (var i = 0; i < children.Length; i++) SpawnPrefabOnObject(toSpawn, children[i], replace);
					}
					else {
						SpawnPrefabOnObject(toSpawn, gameObject, replace);
					}
				}
			}
		}

		/// <summary> Spawns a prefab on a tile.</summary>
		/// <param name="toSpawn">The prefab to spawn.</param>
		/// <param name="gameObject">The game object to spawn the prefab on.</param>
		/// <param name="worldCoord">The world coordinate of the tile to spawn the prefab on.</param>
		private static void SpawnPrefabOnTile(GameObject toSpawn, GameObject gameObject, Vector3 worldCoord) {
			var newObject = PrefabUtility.InstantiatePrefab(toSpawn) as GameObject;
			if (newObject is null) {
				Debug.LogError($"Could not instantiate prefab \"{toSpawn.name}\" for object \"{gameObject.name}\"");
				return;
			}

			newObject.transform.SetParent(gameObject.transform, false);
			newObject.transform.position += worldCoord;
		}

		/// <summary> Spawns a prefab on a game object.</summary>
		/// <param name="toSpawn">The prefab to spawn.</param>
		/// <param name="gameObject">The game object to spawn the prefab on.</param>
		/// <param name="replace">Whether to replace the game object with the prefab.</param>
		private static void SpawnPrefabOnObject(GameObject toSpawn, GameObject gameObject, bool replace) {
			var newObject = PrefabUtility.InstantiatePrefab(toSpawn) as GameObject;
			if (newObject is null) {
				Debug.LogError($"Could not instantiate prefab \"{toSpawn.name}\" for object \"{gameObject.name}\"");
				return;
			}

			newObject.transform.SetParent(gameObject.transform, false);
			if (gameObject.GetComponent<Renderer>() is not null)
				newObject.transform.position = gameObject.GetComponent<Renderer>().bounds.center;
			else if (gameObject.GetComponent<Collider>() is not null)
				newObject.transform.position = gameObject.GetComponent<Collider>().bounds.center;

			if (!replace) return;
			newObject.transform.SetParent(gameObject.transform.parent, true);
			Object.DestroyImmediate(gameObject);
		}
	}
}
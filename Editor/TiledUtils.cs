using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ReverseGravity.TiledTransposer {
	public class TiledUtils {
		private const uint FlippedHorizontallyFlag = 0x80000000;
		private const uint FlippedVerticallyFlag = 0x40000000;
		private const uint FlippedDiagonallyFlag = 0x20000000;

		private static Dictionary<Type, XmlSerializer> XMLSerializers = new();

		/// <summary>
		/// Reads an XML file from disk and deserializes it into an object of the specified type.
		/// </summary>
		/// <typeparam name="T">The type of the object to deserialize the XML into.</typeparam>
		/// <param name="path">The path to the XML file.</param>
		/// <returns>The deserialized object of type T, or the default value of T if the file doesn't exist or couldn't be deserialized.</returns>
		public static T ReadXMLIntoObject<T>(string path) {
			if (!File.Exists(path)) return default;

			if (!XMLSerializers.TryGetValue(typeof(T), out var serializer)) {
				serializer = new XmlSerializer(typeof(T));
				XMLSerializers[typeof(T)] = serializer;
			}

			try {
				using (var streamReader = File.OpenText(path))
				using (var xmlReader = XmlReader.Create(streamReader)) {
					object deserialized = null;
					while (xmlReader.Read())
						if (xmlReader.NodeType == XmlNodeType.Element && !serializer.CanDeserialize(xmlReader)) {
							xmlReader.Skip();
						}
						else {
							deserialized = serializer.Deserialize(xmlReader);
							break;
						}

					return (T)deserialized;
				}
			}
			catch (Exception e) {
				Debug.LogError(e.Message);
				return default;
			}
		}

		/// <summary>
		/// Prompts the user to create a new folder at the specified path if it doesn't already exist.
		/// </summary>
		/// <param name="path">The path where the folder should be created.</param>
		/// <returns>True if the folder exists (either before or after the method call), false otherwise.</returns>
		public static bool CreateAssetFolderPrompt(string path) {
			if (AssetDatabase.IsValidFolder(path)) return true;

			if (!EditorUtility.DisplayDialog(path + " not found", "Create new directory?", "OK", "Cancel")) {
				Debug.LogError("Permission not given to create folder by user");
				return false;
			}

			var assetsFolder = Application.dataPath;
			var parent = Directory.GetParent(path);
			var parentPath = parent.ToString();
			parentPath = parentPath[assetsFolder.Length..];

			if (parentPath.Length == 0) parentPath = "Assets";
			else parentPath = "Assets" + Path.DirectorySeparatorChar + parentPath;

			var folderName = Path.GetFileName(path);
			var guid = AssetDatabase.CreateFolder(parentPath, folderName);

			if (guid is not null && guid.Length != 0) return true;

			Debug.LogError($"Unable to create asset folder {folderName} at {parentPath}");
			return false;
		}

		/// <summary>
		/// Creates a new folder at the specified path if it doesn't already exist, without prompting the user.
		/// </summary>
		/// <param name="path">The path where the folder should be created.</param>
		public static void CreateAssetFolderNoPrompt(string path) {
			if (AssetDatabase.IsValidFolder(path)) return;

			var assetsFolder = Application.dataPath;
			var parent = Directory.GetParent(path);
			var parentPath = parent.ToString();
			parentPath = parentPath[assetsFolder.Length..];

			if (parentPath.Length == 0) parentPath = "Assets";
			else parentPath = "Assets" + Path.DirectorySeparatorChar + parentPath;

			var folderName = Path.GetFileName(path);
			var guid = AssetDatabase.CreateFolder(parentPath, folderName);

			if (guid is null || guid.Length == 0)
				Debug.LogError($"Unable to create asset folder {folderName} at {parentPath}");
		}

		/// <summary>
		/// Finds the ImportedTile and transformation matrix associated with a given global tile ID, in a list of imported tilesets.
		/// </summary>
		/// <param name="gid">The global tile ID to search for.</param>
		/// <param name="importedTilesets">An array of imported tilesets to search through.</param>
		/// <param name="cellWidth">The width of a single tile cell, in pixels.</param>
		/// <param name="cellHeight">The height of a single tile cell, in pixels.</param>
		/// <param name="importedTile">The ImportedTile object that corresponds to the global tile ID, if found. Null otherwise.</param>
		/// <param name="matrix">The transformation matrix that represents any flips or rotations applied to the tile, if found. Identity matrix otherwise.</param>
		public static void FindTileDataAndMatrix(uint gid, ImportedTileset[] importedTilesets, int cellWidth,
			int cellHeight, out ImportedTile importedTile, out Matrix4x4 matrix) {
			importedTile = null;
			matrix = Matrix4x4.identity;

			var flippedHorizontally = (gid & FlippedHorizontallyFlag) != 0;
			var flippedVertically = (gid & FlippedVerticallyFlag) != 0;
			var flippedDiagonally = (gid & FlippedDiagonallyFlag) != 0;

			gid &= ~(FlippedHorizontallyFlag | FlippedVerticallyFlag | FlippedDiagonallyFlag);

			ImportedTileset tilesetContainingID = null;
			for (var j = importedTilesets.Length - 1; j >= 0; --j) {
				var firstGID = importedTilesets[j].FirstGid;
				if (firstGID <= gid) {
					tilesetContainingID = importedTilesets[j];
					break;
				}
			}

			if (tilesetContainingID is not null) {
				var relativeID = (int)gid - tilesetContainingID.FirstGid;

				if (tilesetContainingID.Tileset.IsImageOnlyTileset()) {
					// A single-image-tileset will just order tiles from 0-n
					if (relativeID >= tilesetContainingID.Tiles.Length) {
						Debug.Log($"Relative ID: {relativeID}");
						return;
					}

					importedTile = tilesetContainingID.Tiles[relativeID];
				}
				else {
					for (var t = 0; t < tilesetContainingID.Tileset.tiles.Length; t++) {
						var tile = tilesetContainingID.Tileset.tiles[t];
						var id = tile.id;
						if (id == relativeID) {
							importedTile = tilesetContainingID.Tiles[t];
							break;
						}
					}
				}

				if (importedTile is not null) {
					importedTile.Tile ??= AssetDatabase.LoadAssetAtPath<Tile>(importedTile.Path);
					if (flippedHorizontally || flippedVertically || flippedDiagonally) {
						if (flippedDiagonally) {
							matrix = Matrix4x4.Rotate(Quaternion.Euler(0.0f, 0.0f, 90.0f));
							matrix = Matrix4x4.Scale(new Vector3(1.0f, -1.0f, 1.0f)) * matrix;
						}

						if (flippedHorizontally) matrix = Matrix4x4.Scale(new Vector3(-1.0f, 1.0f, 1.0f)) * matrix;
						if (flippedVertically) matrix = Matrix4x4.Scale(new Vector3(1.0f, -1.0f, 1.0f)) * matrix;

						var rect = importedTile.Tile.sprite.rect;
						rect.x = -cellWidth * 0.5f;
						rect.y = -cellHeight * 0.5f;

						Vector2[] corners = {
							matrix * new Vector2(rect.x, rect.y),
							matrix * new Vector2(rect.x + rect.width, rect.y),
							matrix * new Vector2(rect.x, rect.y + rect.height),
							matrix * new Vector2(rect.x + rect.width, rect.y + rect.height)
						};

						var bottomLeftCorner = corners[0];
						for (var i = 1; i < corners.Length; i++) {
							if (corners[i].x < bottomLeftCorner.x) bottomLeftCorner.x = corners[i].x;
							if (corners[i].y < bottomLeftCorner.y) bottomLeftCorner.y = corners[i].y;
						}

						var offsetNeededUnits = new Vector2(-0.5f, -0.5f) - new Vector2(bottomLeftCorner.x / cellWidth,
							bottomLeftCorner.y / cellHeight);
						matrix = Matrix4x4.Translate(offsetNeededUnits) * matrix;
					}
				}
			}
		}
	}

	public static class GUIIcons {
		public static GUIContent TileIcon = EditorGUIUtility.IconContent("Tile Icon");
	}

	public static class GUIElements {
		public static GUIStyle DragDropBox = new(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
		public static Color EditorGreen = new(0.22f, 0.86f, 0.35f, 1f);
	}
}
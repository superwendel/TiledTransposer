using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReverseGravity.TiledTransposer.TSX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Grid = UnityEngine.Grid;
using Tile = UnityEngine.Tilemaps.Tile;

namespace ReverseGravity.TiledTransposer {
	public class TSXImporter {
		/// <summary>
		/// Copies images from the source paths to the target paths and imports them as assets into the Unity project.
		/// </summary>
		/// <param name="imageSourcePaths">An array of source image file paths.</param>
		/// <param name="imageTargetPaths">An array of target image file paths where the images will be copied to.</param>
		public static void CopyImages(string[] imageSourcePaths, string[] imageTargetPaths) {
			const int updateInterval = 128;

			// Cache lengths
			var imageSourcePathsLength = imageSourcePaths.Length;

			for (var i = 0; i < imageSourcePathsLength; i++) {
				var imageSourcePath = imageSourcePaths[i];
				var imageTargetPath = imageTargetPaths[i];

				// Check if the current iteration is a multiple of the update interval
				if (i % updateInterval == 0)
					// Update the progress bar
					EditorUtility.DisplayProgressBar("Copying...", $"{imageSourcePath} to {imageTargetPath}",
						i / (float)imageSourcePathsLength);

				// Check if the source image exists and if it doesn't exist in the target location or it's newer than the target file
				if (File.Exists(imageSourcePath) && (!File.Exists(imageTargetPath) ||
				                                     !File.GetLastWriteTime(imageSourcePath)
					                                     .Equals(File.GetLastWriteTime(imageTargetPath)))) {
					File.Copy(imageSourcePath, imageTargetPath, true);
					AssetDatabase.ImportAsset(imageTargetPath, ImportAssetOptions.ForceSynchronousImport);

					// Try and make to import the texture as a Sprite
					var importer = AssetImporter.GetAtPath(imageTargetPath) as TextureImporter;
					if (importer is not null && importer.spriteImportMode == SpriteImportMode.None) {
						importer.textureType = TextureImporterType.Sprite;
						importer.spriteImportMode = SpriteImportMode.Single;
						EditorUtility.SetDirty(importer);
						importer.SaveAndReimport();
					}
				}
				// Check if the source image exists
				else if (!File.Exists(imageSourcePath)) {
					throw new FileNotFoundException($"Source image not found: {imageSourcePath}.");
				}
			}
		}

		/// <summary>
		/// Imports a Tiled TMX tileset reference and creates an ImportedTileset object that includes an array of ImportedTile objects and metadata.
		/// </summary>
		/// <param name="tilesetReference">The Tiled TMX TilesetReference object to import</param>
		/// <param name="baseFolder">The base folder containing the TMX file</param>
		/// <param name="tilesetDir">The directory in which to store the imported tileset files</param>
		/// <param name="cellWidth">The width of the cell</param>
		/// <param name="cellHeight">The height of the cell</param>
		/// <param name="pixelsPerUnit">The number of pixels per Unity unit</param>
		/// <returns>An ImportedTileset object containing an array of ImportedTile objects and metadata</returns>
		public static ImportedTileset ImportFromTilesetReference(TMX.TilesetReference tilesetReference,
			string baseFolder, string tilesetDir, int cellWidth, int cellHeight, int pixelsPerUnit) {
			// The source path in the tileset reference is recorded relative to the tmx
			Tileset actualTileset;
			ImportedTile[] importedTiles;
			if (tilesetReference.source is not null) {
				var tsxPath = Path.Combine(baseFolder, tilesetReference.source);

				if (!TiledUtils.CreateAssetFolderPrompt(tilesetDir)) return null;

				Debug.Log($"Loading the TSX file from {tsxPath} into the {tilesetDir} directory.");

				var tileset = TiledUtils.ReadXMLIntoObject<Tileset>(tsxPath);
				if (tileset is null) return null;
				actualTileset = tileset;

				Debug.Log($"Loading TileSet {actualTileset.name}");

				importedTiles = ImportTileset(tileset, tilesetDir, Path.GetDirectoryName(tsxPath), cellWidth,
					cellHeight, pixelsPerUnit);
			}
			else {
				// Import Embedded Tileset
				var tileset = new Tileset(tilesetReference);
				actualTileset = tileset;

				if (!TiledUtils.CreateAssetFolderPrompt(tilesetDir)) return null;

				Debug.Log($"Loading embedded tileset: {tileset.name}");

				importedTiles = ImportTileset(tileset, tilesetDir, baseFolder, cellWidth, cellHeight, pixelsPerUnit);
			}

			if (importedTiles is not null && (importedTiles.Length <= 0 || importedTiles[0] is not null))
				return new ImportedTileset(importedTiles, tilesetReference.firstgid, actualTileset);

			var tilesetName = tilesetReference.source ?? tilesetReference.name;
			Debug.LogError($"Failed to import the {tilesetName} tileset properly.");
			return null;
		}

		/// <summary> Imports a Tiled tileset and returns the corresponding ImportedTiles.</summary>
		/// <param name="tileset">The Tileset to import.</param>
		/// <param name="tilesetDir">The directory where the Tileset and its assets will be stored.</param>
		/// <param name="sourceTilesetDirectory">The directory where the original Tiled Tileset assets are located.</param>
		/// <param name="cellWidth">The width of the cell.</param>
		/// <param name="cellHeight">The height of the cell.</param>
		/// <param name="pixelsPerUnit">The number of pixels per unit.</param>
		/// <returns>The ImportedTiles corresponding to the imported Tileset.</returns>
		private static ImportedTile[] ImportTileset(Tileset tileset, string tilesetDir, string sourceTilesetDirectory,
			int cellWidth, int cellHeight, int pixelsPerUnit) {
			const int updateInterval = 128;

			var tilesetSpriteTargetDir = Path.Combine(tilesetDir, tileset.name);
			TiledUtils.CreateAssetFolderNoPrompt(tilesetSpriteTargetDir);

			var tilesetTileTargetDir = Path.Combine(tilesetSpriteTargetDir, "TileAssets");
			TiledUtils.CreateAssetFolderNoPrompt(tilesetTileTargetDir);

			var tiles = tileset.tiles;

			Sprite[] tileSprites = null;
			string[] tileTargetPaths = null;
			var isImageOnlyTileset = tileset.IsImageOnlyTileset();
			if (isImageOnlyTileset) {
				if (tileset.image is not null) {
					//Create Single Image TileSet Paths
					var imageSourcePath = tileset.image.source;
					imageSourcePath = Path.Combine(sourceTilesetDirectory, imageSourcePath);

					var imageName = Path.GetFileName(imageSourcePath);
					var imageTargetPath = Path.Combine(tilesetSpriteTargetDir, imageName);

					CopyImages(new[] { imageSourcePath }, new[] { imageTargetPath });
					var realWidth = 0;
					var realHeight = 0;

					var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imageTargetPath);
					if (texture is not null) {
						realWidth = texture.width;
						realHeight = texture.height;
					}
					else {
						Debug.LogError($"Failed to load texture at path: {imageTargetPath}");
					}

					if (realWidth != tileset.image.width)
						Debug.LogError(
							$"The width of the image in Tileset {tileset.name} ({tileset.image.width}) does not match the actual width ({realWidth}).");
					if (realHeight != tileset.image.height)
						Debug.LogError(
							$"The height of the image in Tileset {tileset.name} ({tileset.image.height}) does not match the actual height ({realHeight}).");
					if (realWidth > 8192 || realHeight > 8192)
						Debug.LogError(
							$"The Tileset image ({tileset.image.source}) is larger than Unity's maximum texture size (8192x8192)! This will impact tile quality. Try packing the tiles into a more square texture within 8192x8192.");

					var tileCount = tileset.CalculateTileCount();
					var actualTileCount = tileset.CalculateTileCountFromDimensions(realWidth, realHeight);
					if (tileCount != actualTileCount)
						Debug.LogError(
							$"The tile count in Tileset {tileset.name} ({tileset.CalculateTileCount()}) does not match the actual tile count based on the image dimensions ({actualTileCount}).");

					// Create Single Image Tile TargetPaths
					tileTargetPaths = new string[tileCount];
					for (var i = 0; i < tileCount; i++)
						tileTargetPaths[i] = Path.Combine(tilesetTileTargetDir,
							$"{Path.GetFileNameWithoutExtension(imageSourcePath)}_{i}.asset");

					var subSpriteNameBase = Path.GetFileNameWithoutExtension(imageSourcePath);

					// Create Tilemap Sprite
					var ti = AssetImporter.GetAtPath(imageTargetPath) as TextureImporter;

					// If the TextureImporter is null, log an error and return
					if (ti is not null) {
						ti.maxTextureSize = 8192; // Good max size
					}
					else {
						Debug.LogError($"Cannot find the sprite file for the TileMap at {imageTargetPath}.");
						return null;
					}

					// Create a new TextureImporterSettings object and read the current settings from the TextureImporter
					var textureSettings = new TextureImporterSettings();
					ti.ReadTextureSettings(textureSettings);

					// Set the pivot point for the sprites to middle center
					var pivot = new Vector2(0.5f, 0.5f);

					textureSettings.spritePixelsPerUnit = pixelsPerUnit;
					textureSettings.spriteMeshType = SpriteMeshType.FullRect;
					textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
					textureSettings.spritePivot = pivot;
					textureSettings.filterMode = FilterMode.Point;
					textureSettings.spriteMode = (int)SpriteImportMode.Multiple;
					textureSettings.spriteGenerateFallbackPhysicsShape = true;
					ti.SetTextureSettings(textureSettings);
					var tileMapSpriteTileCount = tileset.CalculateTileCount();
					var newData = new List<SpriteMetaData>(tileMapSpriteTileCount);
					var spriteSuffix = 0;

					// Iterate over the sprite image, creating a sprite for each tile
					for (var y = realHeight - tileset.margin;
					     y - tileset.tileheight >= tileset.margin;
					     y -= tileset.tileheight + tileset.spacing)
					for (var x = tileset.margin;
					     x + tileset.tilewidth <= realWidth - tileset.margin;
					     x += tileset.tilewidth + tileset.spacing) {
						var data = new SpriteMetaData {
							name = subSpriteNameBase + "_" + spriteSuffix,
							alignment = (int)SpriteAlignment.Custom,
							pivot = GetPivot(tileset.tilewidth, tileset.tileheight, cellWidth, cellHeight,
								tileset.tileoffset),
							rect = new Rect(x, y - tileset.tileheight, tileset.tilewidth, tileset.tileheight)
						};
						newData.Add(data);
						spriteSuffix++;
					}

					if (tileMapSpriteTileCount != spriteSuffix)
						Debug.LogWarning(
							$"The TileSet specifies a TileCount of {tileMapSpriteTileCount}, but there are {spriteSuffix} sprites available!");

					ti.spritesheet = newData.ToArray();

					// Set the import settings as dirty and save the changes
					EditorUtility.SetDirty(ti);
					ti.SaveAndReimport();

					// Unity does not guarantee a sorting order so we sort to ensure it's in the correct order.
					// Load all the sprites at the target path and sort them
					var allAssets = AssetDatabase.LoadAllAssetsAtPath(imageTargetPath);

					// Maps each Sprite object to the parsed numeric value of its name.
					// parse the numeric value of each Sprite name, and add it to the dictionary with the Sprite
					// object as the key and the parsed numeric value as the value.
					var parsedNumbers = new Dictionary<Sprite, int>();
					for (var i = 0; i < allAssets.Length; i++) {
						var asset = allAssets[i];
						if (asset is Sprite sprite) {
							var underscorePos = sprite.name.LastIndexOf('_');
							var numberString = sprite.name[(underscorePos + 1)..];
							var number = int.Parse(numberString);
							parsedNumbers.Add(sprite, number);
						}
					}

					// Create an array from the dictionary keys
					var subSprites = new Sprite[parsedNumbers.Count];
					parsedNumbers.Keys.CopyTo(subSprites, 0);

					Array.Sort(subSprites, (a, b) => parsedNumbers[a].CompareTo(parsedNumbers[b]));
					tileSprites = subSprites;
				}
				else {
					Debug.LogError($"The TileSet {tileset.name} is empty!");
				}
			}
			else {
				var tilesLength = tiles.Length;
				var imageSourcePaths = new string[tilesLength];
				var imageTargetPaths = new string[tilesLength];
				tileTargetPaths = new string[tilesLength];

				// Create Tile Paths
				for (var i = 0; i < tilesLength; i++) {
					var tile = tiles[i];
					var imageSourcePath = tile.image.source;

					// Combine the pathWithoutFile with the image source path to get the full path to the image
					imageSourcePath = Path.Combine(sourceTilesetDirectory, imageSourcePath);
					imageSourcePaths[i] = imageSourcePath;

					// Get the image name and create the target path for the sprite
					var imageName = Path.GetFileName(imageSourcePath);
					var imageTargetPath = Path.Combine(tilesetSpriteTargetDir, imageName);
					imageTargetPaths[i] = imageTargetPath;

					// Create the tile asset name and the target path for the tile
					var tileName = Path.GetFileNameWithoutExtension(imageSourcePath) + ".asset";
					tileTargetPaths[i] = Path.Combine(tilesetSpriteTargetDir, tileName);
				}

				CopyImages(imageSourcePaths, imageTargetPaths);

				tileSprites = new Sprite[tiles.Length];
				AssetDatabase.StartAssetEditing();

				for (var i = 0; i < tiles.Length; i++) {
					if (i % updateInterval == 0)
						EditorUtility.DisplayProgressBar($"Importing {tileset.name} TileSet sprites...",
							$"Progress: {i}/{tilesLength}", (float)i / tilesLength);

					if (!File.Exists(imageTargetPaths[i])) continue;

					// Create Tile Sprite
					// Get the TextureImporter at the given path
					var tile = tiles[i];

					// Create a new TextureImporterSettings
					var ti = AssetImporter.GetAtPath(imageTargetPaths[i]) as TextureImporter;

					// Create a new TextureImporterSettings
					var textureSettings = new TextureImporterSettings();

					// Read the current texture settings into the TextureImporterSettings object
					ti.ReadTextureSettings(textureSettings);
					textureSettings.textureType = TextureImporterType.Sprite;
					textureSettings.spritePixelsPerUnit = pixelsPerUnit;
					textureSettings.spriteMeshType = SpriteMeshType.FullRect;
					textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
					textureSettings.spritePivot = GetPivot(tile.image.width, tile.image.height, cellWidth, cellHeight,
						tileset.tileoffset);
					textureSettings.filterMode = FilterMode.Point;
					textureSettings.spriteGenerateFallbackPhysicsShape = true;

					// Apply the new settings
					ti.SetTextureSettings(textureSettings);

					// Set the TextureImporter as dirty and save the changes
					EditorUtility.SetDirty(ti);
					ti.SaveAndReimport();

					var newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(imageTargetPaths[i]);
					if (newSprite is null) {
						// Try again in case some changes don't get picked up
						AssetDatabase.StopAssetEditing();
						newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(imageTargetPaths[i]);
						AssetDatabase.StartAssetEditing();
					}

					Debug.Assert(newSprite is not null, $"The asset at {imageTargetPaths[i]} is not a valid sprite!");
					tileSprites[i] = newSprite;
				}

				AssetDatabase.StopAssetEditing();
			}

			if (tileSprites is null) {
				Debug.LogError($"The tile sprites are null when importing the {tileset.name} tileset.");
				return null;
			}

			if (tileSprites.Length == 0) {
				Debug.LogError($"No tile sprites found from texture assets for the {tileset.name} tileset.");
				return null;
			}

			// Create Tile Assets
			AssetDatabase.StartAssetEditing();
			// Ensure tileSprites and tileTargetPaths are not null
			Debug.Assert(tileSprites is not null && tileTargetPaths is not null);

			// Initialize outputTiles array
			var tileAssets = new ImportedTile[tileTargetPaths.Length];

			// Cache lengths
			var tileSpritesLength = tileSprites.Length;
			var tileTargetPathsLength = tileTargetPaths.Length;

			// Check if the number of sprites and the number of target paths are the same
			if (tileSpritesLength != tileTargetPathsLength)
				Debug.LogWarning(
					$"The number of sprites ({tileSpritesLength}) does not match the number of tiles to be created ({tileTargetPathsLength}) for the {tileset.name} tileset.");

			for (var i = 0; i < tileTargetPathsLength; i++) {
				if (i % updateInterval == 0)
					EditorUtility.DisplayProgressBar($"Importing tiles for the {tileset.name} tileset...",
						$"Progress: {i}/{tileTargetPathsLength}", (float)i / tileTargetPathsLength);

				if (i < tileSpritesLength && tileSprites[i] is not null) {
					var colliderType = Tile.ColliderType.None;

					// Tile Collision
					if (tiles is not null) {
						TSX.Tile collisionTile = null;
						if (isImageOnlyTileset)
							for (var j = 0; j < tiles.Length; j++) {
								var tile = tiles[j];
								if (tile.id == i) {
									collisionTile = tile;
									break;
								}
							}
						else
							collisionTile = tiles[i];

						if (collisionTile is not null && collisionTile.HasCollisionData()) {
							if (TMXImporter.ColliderImport == ColliderImportType.Grid)
								colliderType = Tile.ColliderType.Grid;
							else if (TMXImporter.ColliderImport == ColliderImportType.Sprite)
								colliderType = Tile.ColliderType.Sprite;
							else if (TMXImporter.ColliderImport == ColliderImportType.Tiled)
								colliderType =
									Tile.ColliderType
										.None; // Set Sprite if there is a collision object then override sprite

							/*
							var physicsShapes = new List<Vector2[]>();
							var scaleForPhysicsShape = new Vector2(1, -1);

							for (var index = 0; index < collisionTile.objectgroup.objects.Length; index++) {
								var mapObject = collisionTile.objectgroup.objects[index];

								Debug.Log(tileSprites[i] + " " + mapObject.x + " " + mapObject.y);

								mapObject.InitialiseUnsetValues();

								Debug.Log(tileSprites[i] + " " + mapObject.x + " " + mapObject.y);

								if (mapObject.rotation != 0.0f) Debug.LogError("Rotated Objects");
								string pointsString = null;
								if (mapObject.polygon is not null)
									pointsString = mapObject.polygon.points;
								else if (mapObject.polyline is not null)
									pointsString = mapObject.polyline.points;
								else if (mapObject.ellipse is not null) Debug.LogWarning("ellipse");

								if (mapObject.rectangle is not null)
									Debug.LogError("LETS GO");
								else if (mapObject.rectangle is null) Debug.LogError("null");
							}

							if (physicsShapes.Count > 0) {
								colliderType = Tile.ColliderType.Sprite;
								SetPhysicsShapeForSprite(tileSprites[i], physicsShapes, i);
							}
							*/
						}
					}

					// Tile Animation
					TSX.Tile animationTile = null;
					if (tiles is not null) {
						if (isImageOnlyTileset)
							// Have to find relevant tile
							for (var j = 0; j < tiles.Length; j++) {
								var tile = tiles[j];
								if (tile.id == i && tile.animation is not null) {
									animationTile = tile;
									break;
								}
							}
						else if (tiles[i].animation is not null) animationTile = tiles[i];
					}

					if (animationTile is null) {
						// Create Tile Asset
						var tile = AssetDatabase.LoadAssetAtPath<Tile>(tileTargetPaths[i]);

						if (tile is null) {
							tile = ScriptableObject.CreateInstance<Tile>();
							tile.sprite = tileSprites[i];
							tile.colliderType = colliderType;

							if (TMXImporter.ColliderImport == ColliderImportType.Tiled)
								Debug.LogWarning("GOODMORNING CRONO");

							AssetDatabase.CreateAsset(tile, tileTargetPaths[i]);
						}

						if (tile.sprite != tileSprites[i]) {
							tile.sprite = tileSprites[i];
							EditorUtility.SetDirty(tile);
						}

						if (tile.colliderType != colliderType) {
							tile.colliderType = colliderType;
							EditorUtility.SetDirty(tile);
						}

						tileAssets[i] = new ImportedTile(tile, tileTargetPaths[i]);
					}
					else {
						var frames = animationTile.animation.frames;
						var animationSprites = new Sprite[frames.Length];
						for (var f = 0; f < frames.Length; f++) animationSprites[f] = tileSprites[frames[f].tileid];
						// Assume a constant animation speed due to Unity's Tilemap animation implementation
						var animationSpeed = 1.0f / (frames[0].duration * 0.001f);

						// Create Animated Tile Asset
						var tile = AssetDatabase.LoadAssetAtPath<AnimatedTMXTile>(tileTargetPaths[i]);

						if (tile is null) {
							tile = ScriptableObject.CreateInstance<AnimatedTMXTile>();
							tile.animatedSprites = animationSprites;
							tile.animationStartTime = 0.0f;
							tile.speed = animationSpeed;
							tile.sprite = tileSprites[i];
							tile.colliderType = colliderType;

							AssetDatabase.CreateAsset(tile, tileTargetPaths[i]);
						}
						else if (tile.animatedSprites is null == animationSprites is null
						         || tile.animatedSprites is null
						         || animationSprites is null
						         || !tile.animatedSprites.SequenceEqual(animationSprites)
						         || tile.speed != animationSpeed
						         || tile.sprite != tileSprites[i]
						         || tile.colliderType != colliderType) {
							tile.animatedSprites = animationSprites;
							tile.speed = animationSpeed;
							tile.sprite = tileSprites[i];
							tile.colliderType = colliderType;
							EditorUtility.SetDirty(tile);
						}

						tileAssets[i] = new ImportedTile(tile, tileTargetPaths[i]);
					}
				}
				else {
					Debug.LogWarning(
						$"The sprite for tile {tileTargetPaths[i]} is null when creating a tile for the {tileset.name} tileset.");
				}
			}

			AssetDatabase.StopAssetEditing();
			EditorUtility.ClearProgressBar();

			//Create Palette
			var newPaletteGO = new GameObject(tileset.name, typeof(Grid));
			newPaletteGO.GetComponent<Grid>().cellSize = new Vector3(1.0f, 1.0f, 0.0f);
			var paletteTilemapGO = new GameObject("Layer1", typeof(Tilemap), typeof(TilemapRenderer));
			paletteTilemapGO.transform.SetParent(newPaletteGO.transform);

			paletteTilemapGO.GetComponent<TilemapRenderer>().enabled = false;

			var paletteTilemap = paletteTilemapGO.GetComponent<Tilemap>();
			paletteTilemap.tileAnchor = GetPivot(tileset.tilewidth, tileset.tileheight, cellWidth, cellHeight,
				tileset.tileoffset);
			if (tileset.columns <= 0) tileset.columns = 5;

			if (isImageOnlyTileset) {
				var tileAssetsLength = tileAssets.Length;
				for (var i = 0; i < tileAssetsLength; i++) {
					if (tileAssets[i] is null || tileAssets[i].Tile is null) continue;
					var sprite = tileAssets[i].Tile.sprite;
					var rect = sprite.rect;
					var x = (int)rect.x / tileset.tilewidth;
					var y = (int)rect.y / tileset.tileheight;
					paletteTilemap.SetTile(new Vector3Int(x, y, 0), tileAssets[i].Tile);
				}
			}
			else {
				var x = 0;
				var y = 0;
				var tileCount = tileAssets.Length;
				var tileArray = new TileBase[tileCount];
				var positionArray = new Vector3Int[tileCount];

				for (var i = 0; i < tileCount; i++) {
					if (tileAssets[i] is null || tileAssets[i].Tile is null) continue;

					tileArray[i] = tileAssets[i].Tile;
					positionArray[i] = new Vector3Int(x, y, 0);

					x++;
					if (x >= tileset.columns) {
						x = 0;
						y--;
					}
				}

				paletteTilemap.SetTiles(positionArray, tileArray);
			}

			var palettePath = tilesetTileTargetDir + Path.DirectorySeparatorChar + tileset.name + ".prefab";
			palettePath = palettePath.Replace('\\', '/');

			var newPrefab = PrefabUtility.SaveAsPrefabAsset(newPaletteGO, palettePath, out var createdPrefab);
			if (!createdPrefab)
				Debug.Log($"Unable to create tile palette asset at the following location: {palettePath}");

			AssetDatabase.SaveAssets();
			UnityEngine.Object.DestroyImmediate(newPaletteGO);

			// Clear out any old subassets
			var assets = AssetDatabase.LoadAllAssetsAtPath(palettePath);
			for (var i = 0; i < assets.Length; i++) {
				var asset = assets[i];
				if (!AssetDatabase.IsMainAsset(asset) && asset is GridPalette)
					UnityEngine.Object.DestroyImmediate(asset, true);
			}

			var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
			gridPalette.cellSizing = GridPalette.CellSizing.Automatic;
			gridPalette.name = "PaletteSettings";
			AssetDatabase.AddObjectToAsset(gridPalette, newPrefab);
			AssetDatabase.SaveAssets();

			return tileAssets;
		}

		/*
		public static Vector2[] PointsFromString(string pointsString, Vector2 scale, Vector2 offset) {
			var pointsSplit = pointsString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var points = new Vector2[pointsSplit.Length];


			for (var i = 0; i < points.Length; i++) {
				var vectorComponents = pointsSplit[i].Split(',');
				Debug.Assert(vectorComponents.Length == 2,
					"This string should have 2 components separated by a comma: " + pointsSplit[i]);

				var x = float.Parse(vectorComponents[0]) * scale.x + offset.x;
				var y = float.Parse(vectorComponents[1]) * scale.y + offset.y;

				points[i] = new Vector2(x, y);
			}

			return points;
		}
		*/


		/*
		private static void SetPhysicsShapeForSprite(Sprite sprite, List<Vector2[]> physicsShapes, int index) {
			Debug.Log("sprite: " + sprite.name + " physicsShapes: " + physicsShapes.Count + " index: " + index);

			// Loop through the physicsShapes and print each one
			for (var i = 0; i < physicsShapes.Count; i++)
				Debug.Log("PhysicsShape " + i + ": " + string.Join(", ", physicsShapes[i].Select(v => v.ToString())));

			var path = AssetDatabase.GetAssetPath(sprite.texture);
			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			var spritePhysicsShapeImporter = new SpritePhysicsShapeImporter(importer);

			spritePhysicsShapeImporter.SetPhysicsShape(index, physicsShapes);

			// Save the changes made to the importer
			spritePhysicsShapeImporter.Save();
		}
		*/

		/// <summary>
		/// Calculates the proportional pivot needed to align the bottom-left corner of the tile with the bottom-left corner of the cell,
		/// taking into account the image dimensions, cell dimensions, and tile offset.
		/// </summary>
		/// <param name="imageWidth">The width of the image in pixels.</param>
		/// <param name="imageHeight">The height of the image in pixels.</param>
		/// <param name="cellWidth">The width of the cell in pixels.</param>
		/// <param name="cellHeight">The height of the cell in pixels.</param>
		/// <param name="tileOffset">The tile offset in pixels.</param>
		/// <returns>The proportional pivot needed for the tile alignment.</returns>
		private static Vector2 GetPivot(int imageWidth, int imageHeight, int cellWidth, int cellHeight,
			TileOffset tileOffset) {
			// Calculate the pivot point in the [0, 1] range by scaling the cell size with the reciprocal of the tile size.
			var pivot = new Vector2(cellWidth / (2.0f * imageWidth), cellHeight / (2.0f * imageHeight));

			// If there's a tile offset, apply it to the pivot point.
			if (tileOffset is not null)
				// Add the scaled offset to the pivot point.
				pivot += new Vector2(tileOffset.x / (float)imageWidth, tileOffset.y / (float)imageHeight);

			return pivot;
		}
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ReverseGravity.TiledTransposer.TMX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ReverseGravity.TiledTransposer {
	public static class TMXImporter {
		internal static bool ForceGridColliders;

		public static ColliderImportType ColliderImport;

		private static int _cellWidth;
		private static int _cellHeight;
		private static int _pixelsPerUnit;
		private static string _tmxParentFolder;
		private static string _tilesetDir;
		private static ImportedTileset[] _importedTilesets;
		private static Map _map;
		private static string _imageLayerSpriteDir;

		private static bool _setHiddenLayersToInactive;
		private static bool _useCompositeColliders;
		private static bool _allowPrefabEdits;
		private static string _targetPrefabToSaveAs;

		private static GameObject _gridGo;
		private static TilemapRenderer.SortOrder _sortOrder;
		private static ITilemapImportOperation[] _importOperations;
		private static int _orderInLayer;

		private static StaggerAxis _staggerAxis;
		private static bool _needsStaggerOddToEvenConversion;
		private static bool _needsGridRotationToMatchUnityIsometric;
		private static bool _needsGridRotationToMatchUnityStaggeredIsometric;

		private static Dictionary<string, ImportedTemplate> _importedTemplates;

		public static bool ImportTMXFile(Grid targetGrid, string path, string inTilesetDir,
			string inImageLayerSpriteDir,
			bool setHiddenLayersToInactive, bool forceGridColliders, bool useCompositeColliders, bool allowPrefabEdits,
			ColliderImportType colliderImportType) {
			_tmxParentFolder = Path.GetDirectoryName(path);
			var filename = Path.GetFileNameWithoutExtension(path);
			_tilesetDir = Path.Combine("Assets",
				inTilesetDir.Substring(Application.dataPath.Length).TrimStart('/', '\\'));
			_imageLayerSpriteDir = Path.Combine("Assets", inImageLayerSpriteDir.TrimStart('/', '\\'));
			_setHiddenLayersToInactive = setHiddenLayersToInactive;

			ColliderImport = colliderImportType;
			ForceGridColliders = forceGridColliders;
			_useCompositeColliders = useCompositeColliders;
			_allowPrefabEdits = allowPrefabEdits;
			_orderInLayer = 0;

			_map = TiledUtils.ReadXMLIntoObject<Map>(path);
			if (_map is null) return false;

			if (_map.backgroundcolor != null)
				if (ColorUtility.TryParseHtmlString(_map.backgroundcolor, out var backgroundColor))
					Camera.main.backgroundColor = backgroundColor;
			if (_map.tilesets is not null) {
				// First we need to load (or import) all the tilesets referenced by the TMX file...
				_cellWidth = _map.tilewidth;
				_cellHeight = _map.tileheight;
				_pixelsPerUnit = Mathf.Max(_map.tilewidth, _map.tileheight);
				var tilesetsLength = _map.tilesets.Length;

				_importedTilesets = new ImportedTileset[tilesetsLength];
				for (var i = 0; i < tilesetsLength; i++) {
					_importedTilesets[i] = TSXImporter.ImportFromTilesetReference(_map.tilesets[i], _tmxParentFolder,
						_tilesetDir, _cellWidth, _cellHeight, _pixelsPerUnit);
					if (_importedTilesets[i] is null || _importedTilesets[i].Tiles is null ||
					    _importedTilesets[i].Tiles[0] is null) {
						Debug.LogError("Imported tileset is incomplete");
						return false;
					}
				}

				// Set up static variables for the tile layer importer
				// Check if we need to convert the stagger index from odd to even for hexagonal or staggered maps in Unity's grid system
				_needsStaggerOddToEvenConversion =
					(_map.orientation == "hexagonal" || _map.orientation == "staggered") && _map.staggerindex == "odd";

				// Check if we need to change the cell swizzle from XYZ to YXZ and rotate/flip the cells to compensate for the lack of a stagger axis option in Unity's hex grid system
				_staggerAxis =
					_map.staggeraxis is not null &&
					string.Equals(_map.staggeraxis, "x", StringComparison.OrdinalIgnoreCase)
						? StaggerAxis.X
						: StaggerAxis.Y;
				if (!(_map.orientation == "hexagonal" || _map.orientation == "staggered"))
					// If the map orientation is not hexagonal or staggered, there is no need to swizzle the cells
					_staggerAxis = StaggerAxis.None;

				// Check if we need to rotate the grid clockwise 90 degrees to match Unity's isometric rendering
				_needsGridRotationToMatchUnityIsometric = _map.orientation == "isometric";

				// Check if we need to rotate the grid to match Unity's staggered isometric mode
				_needsGridRotationToMatchUnityStaggeredIsometric = _map.orientation == "staggered";

				// If the map is staggered, set the stagger axis accordingly
				if (_map.orientation == "staggered")
					_staggerAxis =
						_map.staggeraxis is not null &&
						string.Equals(_map.staggeraxis, "x", StringComparison.OrdinalIgnoreCase)
							? StaggerAxis.X
							: StaggerAxis.Y;

				// Set up the Grid to store everything in
				GameObject newGrid;
				if (targetGrid != null) {
					newGrid = targetGrid.gameObject;
					if (PrefabUtility.IsPartOfPrefabInstance(newGrid)) {
						Debug.LogWarning(
							"Editing prefab instances may cause Unity to raise warnings. To avoid this, either import into the prefab in Prefab Edit Mode or allow the importer to edit the source prefab directly.");
						if (_allowPrefabEdits) {
							var rootOfInstance = PrefabUtility.GetNearestPrefabInstanceRoot(newGrid);
							if (rootOfInstance != newGrid.gameObject) {
								Debug.LogError(
									"Importing into a prefab instance is only supported when Grid is the top-level object within the prefab hierarchy.");
								return newGrid;
							}

							var prefab = PrefabUtility.GetCorrespondingObjectFromSource(rootOfInstance);
							if (prefab is not null) {
								var prefabPath = AssetDatabase.GetAssetPath(prefab);
								var loadedPrefab = PrefabUtility.LoadPrefabContents(prefabPath);
								newGrid = loadedPrefab;
								_targetPrefabToSaveAs = prefabPath;
							}
							else {
								Debug.LogError(
									"It appears you have a disconnected prefab. Consider recreating the prefab or using the 'Unpack Completely' option from the right-click context menu to detach it.");
							}
						}
						else {
							Debug.LogWarning(
								"Allowing the importer to edit the source prefab directly is recommended to avoid raising warnings. To do so, enable the 'Allow Prefab Edits' option.");
						}
					}

					for (var i = newGrid.transform.childCount - 1; i >= 0; --i)
						Undo.DestroyObjectImmediate(newGrid.transform.GetChild(i).gameObject);
				}
				else {
					newGrid = new GameObject(filename, typeof(Grid));
					Undo.RegisterCreatedObjectUndo(newGrid, "Import Map to New Grid");
				}

				newGrid.TryGetComponent<Grid>(out var newTileGrid);

				var cellSize = new Vector3(1.0f, 1.0f, 0.0f);
				var orientation = _map.orientation;
				switch (orientation) {
					case "orthogonal":
						newTileGrid.cellLayout = GridLayout.CellLayout.Rectangle;
						break;
					case "hexagonal":
					case "isometric":
					case "staggered": {
						cellSize.y = (float)_map.tileheight / _map.tilewidth;
						if (orientation == "hexagonal") {
							newTileGrid.cellLayout = GridLayout.CellLayout.Hexagon;
							newTileGrid.cellSwizzle = _staggerAxis == StaggerAxis.X
								? GridLayout.CellSwizzle.YXZ
								: GridLayout.CellSwizzle.XYZ;
						}
						else {
							newTileGrid.cellLayout = GridLayout.CellLayout.Isometric;
						}

						break;
					}
					default:
						Debug.LogError($"The TMX has an orientation of {_map.orientation}, which is not supported.");
						break;
				}

				newTileGrid.cellSize = cellSize;
				_gridGo = newGrid;

				var importOperationType = typeof(ITilemapImportOperation);
				var importOperationTypes = new List<ITilemapImportOperation>();

				for (var i = 0; i < AppDomain.CurrentDomain.GetAssemblies().Length; i++) {
					var assembly = AppDomain.CurrentDomain.GetAssemblies()[i];
					var types = assembly.GetTypes();
					for (var j = 0; j < types.Length; j++) {
						var type = types[j];
						if (type.IsClass && importOperationType.IsAssignableFrom(type) && !type.IsInterface &&
						    !type.IsAbstract) {
							var instance = (ITilemapImportOperation)Activator.CreateInstance(type);
							importOperationTypes.Add(instance);
						}
					}
				}

				_importOperations = importOperationTypes.ToArray();

				_sortOrder = TilemapRenderer.SortOrder.TopLeft;
				if (_map.renderorder is not null)
					_sortOrder = _map.renderorder switch {
						"right-down" => TilemapRenderer.SortOrder.TopLeft,
						"right-up" => TilemapRenderer.SortOrder.BottomLeft,
						"left-down" => TilemapRenderer.SortOrder.TopRight,
						"left-up" => TilemapRenderer.SortOrder.BottomRight,
						_ => _sortOrder
					};

				_sortOrder = _map.orientation switch {
					// Unity's isometric rendering only works well with TopRight sortorder
					"isometric" => TilemapRenderer.SortOrder.TopRight,
					"staggered" => TilemapRenderer.SortOrder.TopRight,
					_ => _sortOrder
				};

				_importedTemplates = new Dictionary<string, ImportedTemplate>();

				var loadedLayers = _map.topLevelLayers is null || ProcessLayers(_gridGo, _map.topLevelLayers);

				// Handle the complete map's properties
				if (loadedLayers) HandleCustomProperties(_gridGo, _map.properties);

				if (_targetPrefabToSaveAs is not null) {
					PrefabUtility.SaveAsPrefabAsset(_gridGo, _targetPrefabToSaveAs, out var prefabSavedSuccessfully);
					if (!prefabSavedSuccessfully)
						Debug.LogError("Importer failed to save the prefab to " + _targetPrefabToSaveAs);
					PrefabUtility.UnloadPrefabContents(_gridGo);
				}
			}

			return true;
		}

		private static void SetupLayerOffset(GameObject newLayer, float offsetX, float offsetY) {
			// Offset Y needs flipping because y+ is down in Tiled but up in Unity
			_gridGo.TryGetComponent<Grid>(out var targetGrid);
			var cellSize = targetGrid.cellSize;
			newLayer.transform.localPosition = new Vector3(offsetX * cellSize.x / _cellWidth,
				-offsetY * cellSize.y / _cellHeight, 0.0f);
		}

		/// <summary> Processes TMX.BaseLayerElement layers and generates the corresponding GameObjects in the Unity scene.</summary>
		/// <param name="parent">The parent GameObject for the generated layer GameObjects.</param>
		/// <param name="layers">An array of TMX.BaseLayerElement objects representing the layers to be processed.</param>
		/// <returns>True if the layers have been successfully processed, otherwise false.</returns>
		private static bool ProcessLayers(GameObject parent, BaseLayerElement[] layers) {
			var layersLength = layers.Length;
			for (var i = 0; i < layersLength; i++) {
				var layer = layers[i];
				GameObject layerObject = null;

				// Import Tile Layer
				if (layer is Layer tmxLayer) {
					// Validate map's infinite setting against presence of chunks in layer data
					if (_map.infinite && (tmxLayer.data.chunks is null || tmxLayer.data.chunks.Length == 0)) {
						Debug.LogWarning(
							$"The map is set to infinite, but no chunks are found in layer {tmxLayer.name}. If this is an empty layer, this will attempt to be imported.");

						// Create gameobject representing the layer as it might just be an empty layer and this was intentional by the user.
						layerObject = new GameObject(tmxLayer.name, typeof(Tilemap), typeof(TilemapRenderer));
						layerObject.transform.SetParent(parent.transform, false);
						continue;
					}

					// Create new GameObject with Tilemap and TilemapRenderer components and set its parent
					layerObject = new GameObject(tmxLayer.name, typeof(Tilemap), typeof(TilemapRenderer));
					layerObject.transform.SetParent(parent.transform, false);

					// Configure layer offset based on layer's offset values
					SetupLayerOffset(layerObject, tmxLayer.offsetx, tmxLayer.offsety);

					// Obtain Tilemap and Grid components
					layerObject.TryGetComponent<Tilemap>(out var layerTilemap);
					_gridGo.TryGetComponent<Grid>(out var tileGrid);

					// Determine tile anchor based on cell layout
					layerTilemap.tileAnchor = tileGrid.cellLayout == GridLayout.CellLayout.Hexagon
						? new Vector3(0.0f, 0.0f)
						: new Vector2(0.5f, 0.5f);

					// Set tilemap color according to layer's opacity
					if (tmxLayer.opacity < 1.0f) layerTilemap.color = new Color(1.0f, 1.0f, 1.0f, tmxLayer.opacity);

					// Initialize grid offset values and compute position offset based on stagger axis
					var gridXOffset = 0;
					var gridYOffset = 0;
					var offsetPosition = layerObject.transform.position;
					var cellSize = tileGrid.cellSize;
					if (_staggerAxis == StaggerAxis.X) {
						gridXOffset = 1;
						gridYOffset = 0;
						offsetPosition = _needsStaggerOddToEvenConversion
							? new Vector3(cellSize.x * -0.25f, 0)
							: new Vector3(cellSize.x * -0.25f, cellSize.y * -0.5f);
					}
					else if (_staggerAxis == StaggerAxis.Y) // StaggerAxis Y
					{
						gridXOffset = 0;
						gridYOffset = _needsStaggerOddToEvenConversion ? 1 : 0;
						offsetPosition = _needsStaggerOddToEvenConversion
							? new Vector3(cellSize.x * 0.5f, cellSize.y * 1.0f)
							: new Vector3(cellSize.x * 0.5f, cellSize.y * 0.25f);
					}

					// Update layer's position with calculated offset
					layerObject.transform.position = offsetPosition;

					// If chunks exist, add each chunk to the tilemap
					if (tmxLayer.data.chunks is not null)
						for (var c = 0; c < tmxLayer.data.chunks.Length; c++) {
							var chunk = tmxLayer.data.chunks[c];
							AddChunkToTilemap(layerTilemap, tmxLayer.data.encoding,
								tmxLayer.data.compression, chunk.tiles, chunk.text, chunk.x + gridXOffset,
								chunk.y + gridYOffset, chunk.width, chunk.height);
						}
					else // If chunks are not present, add the entire layer to the tilemap
						AddChunkToTilemap(layerTilemap, tmxLayer.data.encoding,
							tmxLayer.data.compression, tmxLayer.data.tiles, tmxLayer.data.text, gridXOffset,
							gridYOffset, layer.width, layer.height);

					// Get the TilemapRenderer component and set its sorting order and sort order based on the layerID and TMXImporter.SortOrder
					layerObject.TryGetComponent<TilemapRenderer>(out var renderer);
					renderer.sortingOrder = _orderInLayer;
					renderer.sortOrder = _sortOrder;
					_orderInLayer++;
				}
				else if (layer is ObjectGroup objectGroup) {
					layerObject = null;
					if (objectGroup?.objects?.Length > 0) {
						layerObject = new GameObject(objectGroup.name);
						layerObject.transform.SetParent(parent.transform, false);
						SetupLayerOffset(layerObject, objectGroup.offsetx, objectGroup.offsety);
						for (var o = 0; o < objectGroup.objects.Length; o++) {
							var mapObject = objectGroup.objects[o];

							//Import Map Object
							{
								ImportedTileset replacementTileset = null;

								// If the map object has a template, apply the template data to it and retrieve the replacement tileset if necessary
								if (mapObject.template is not null) {
									// Obtain the full template path, which is relative to the TMX file's location
									var templatePath =
										Path.GetFullPath(Path.Combine(_tmxParentFolder, mapObject.template));
									ImportedTemplate importedTemplate;

									// Use the existing imported template if it's already in the dictionary
									if (_importedTemplates.TryGetValue(templatePath, out var template1)) {
										importedTemplate = template1;
									}
									else {
										// Load and parse the Tiled template file (.tx) into a TXTypes.Template object
										var template = TiledUtils.ReadXMLIntoObject<TXTypes.Template>(templatePath);
										ImportedTileset tileset;

										// Check if the template has an associated tileset
										if (template.tileset is not null) {
											// Import the tileset using the TSXImporter
											var baseFolder = Path.GetDirectoryName(templatePath);
											tileset = TSXImporter.ImportFromTilesetReference(template.tileset,
												baseFolder, _tilesetDir, _cellWidth, _cellHeight, _pixelsPerUnit);
										}
										else {
											// Create an ImportedTemplate object containing the imported Tiled template and tileset data
											tileset = null;
										}

										// Return an ImportedTemplate object containing the imported Tiled template and tileset data
										importedTemplate = new ImportedTemplate(template, tileset);
										_importedTemplates.Add(templatePath, importedTemplate);
									}

									// Assign the replacement tileset based on whether the mapObject has a gid value
									replacementTileset =
										!mapObject.gid.HasValue ? importedTemplate.ImportedTileset : null;
									var combinedMapObject =
										mapObject.GetVersionWithTemplateApplied(importedTemplate.Template
											.TempleateTiledObject);

									// If the template could not be applied, log an error and return false
									if (combinedMapObject is not null) mapObject = combinedMapObject;
									else Debug.LogError("Could not load template for map object " + mapObject);
								}

								// Initialize unset values in the map object with defaults after merging with the template (if applicable)
								mapObject.InitialiseUnsetValues();

								// Use the template's tileset (and the gid that's been set by ApplyTemplate)
								if (replacementTileset is not null) _importedTilesets = new[] { replacementTileset };

								TiledUtils.FindTileDataAndMatrix(mapObject.gid.Value, _importedTilesets, _cellWidth,
									_cellHeight, out var importedTile, out var matrix);
								var pixelsToUnits = new Vector2(1.0f / _map.tilewidth, -1.0f / _map.tileheight);
								var newObject = new GameObject(mapObject.name);
								newObject.transform.SetParent(layerObject.transform, false);

								// Gain tile rotation/flipping
								// Extract scale
								Vector3 column0 = matrix.GetColumn(0);
								Vector3 column1 = matrix.GetColumn(1);
								Vector3 column2 = matrix.GetColumn(2);
								var matrixScale = new Vector3(column0.magnitude, column1.magnitude, column2.magnitude);
								if (Vector3.Cross(column0, column1).normalized != column2.normalized)
									matrixScale.x *= -1;

								// Extract rotation
								Vector3 forward;
								forward.x = matrix.m02;
								forward.y = matrix.m12;
								forward.z = matrix.m22;
								Vector3 upwards;
								upwards.x = matrix.m01;
								upwards.y = matrix.m11;
								upwards.z = matrix.m21;
								var rotation = Quaternion.LookRotation(forward, upwards);

								// Extract position
								Vector3 position;
								position.x = matrix.m03;
								position.y = matrix.m13;
								position.z = matrix.m23;

								// Assign extracted values to the transform
								newObject.transform.localScale = matrixScale;
								newObject.transform.localRotation = rotation;
								newObject.transform.localPosition = position;

								// Calculate corner and pivot positions for the object
								var mapVector = new Vector2(mapObject.x.Value, mapObject.y.Value);
								var x = mapVector.x * pixelsToUnits.x;
								var y = mapVector.y * pixelsToUnits.y;
								var corner = new Vector2(x, y);

								var pivotScaler = pixelsToUnits;
								if (_needsGridRotationToMatchUnityIsometric) {
									var tileSpace = new Vector2(corner.x / _map.tileheight,
										-corner.y / (_map.tileheight * 2.0f));

									// Calculate the difference and negated sum of the isometricSpacePos components
									var xMinusY = tileSpace.x - tileSpace.y;
									var xPlusY = -(tileSpace.x + tileSpace.y);

									// Extra offset to bump it up half a cell (and since tiles are typically 2 tiles high, it's a quarter of tile height)
									const float offsetY = 0.25f;

									// Calculate the screen position by multiplying the xMinusY and xPlusY values by the half tile width and height respectively,
									// and adding the offsetY to the y-component of the resulting Vector2
									corner = new Vector2(xMinusY * (_map.tilewidth * 0.5f),
										xPlusY * (_map.tilewidth * 0.5f) + offsetY);
									pivotScaler =
										Vector2.zero; // Pivot offsets are unneeded for objects on isometric grids
								}

								// If the map object is a tile, set up its sprite and collider
								if (importedTile is not null) {
									var unityTile = importedTile.Tile;
									var pivotProportion =
										new Vector2(unityTile.sprite.pivot.x / unityTile.sprite.rect.width,
											unityTile.sprite.pivot.y / unityTile.sprite.rect.height);

									var mapSizeScaledByPivot = new Vector2(
										mapObject.width.Value * pivotProportion.x,
										mapObject.height.Value * -pivotProportion.y
									);

									var scaleX = mapSizeScaledByPivot.x * pivotScaler.x;
									var scaleY = mapSizeScaledByPivot.y * pivotScaler.y;
									var scaledVector = new Vector2(scaleX, scaleY);

									Vector3 pivotWorldPosition = corner + scaledVector;

									newObject.transform.localPosition += pivotWorldPosition;
									var renderer = newObject.AddComponent<SpriteRenderer>();
									renderer.sprite = unityTile.sprite;
									renderer.sortingOrder = _orderInLayer;

									// Add appropriate collider based on the tile's collider type
									if (unityTile.colliderType == UnityEngine.Tilemaps.Tile.ColliderType.Sprite)
										newObject.AddComponent<PolygonCollider2D>();
									else if (unityTile.colliderType == UnityEngine.Tilemaps.Tile.ColliderType.Grid)
										newObject.AddComponent<BoxCollider2D>();

									var scale = new Vector2(mapObject.width.Value / unityTile.sprite.rect.width,
										mapObject.height.Value / unityTile.sprite.rect.height);

									var currentScale = newObject.transform.localScale;
									var newScaleX = currentScale.x * scale.x;
									var newScaleY = currentScale.y * scale.y;
									newObject.transform.localScale = new Vector3(newScaleX, newScaleY, 1.0f);

									// If the tile is animated, add an animation script to the object
									if (unityTile is AnimatedTMXTile) {
										// Then add a script to do some animation
										var objectAnimator = newObject.AddComponent<AnimatedTileObject>();
										objectAnimator.tileAsset = unityTile as AnimatedTMXTile;
									}
								}
								else {
									// If the map object is not a tile, set up its non-tile properties (collision, text, etc.)
									var mapSizeScaledByHalf = new Vector2(
										mapObject.width.Value * 0.5f,
										mapObject.height.Value * 0.5f
									);

									var pivotScaledX = mapSizeScaledByHalf.x * pivotScaler.x;
									var pivotScaledY = mapSizeScaledByHalf.y * pivotScaler.y;
									var scaledVector = new Vector2(pivotScaledX, pivotScaledY);

									Vector3 pivotWorldPosition = corner + scaledVector;
									newObject.transform.localPosition += pivotWorldPosition;

									//TODO(wendel): handle rectangle objects
									if (mapObject.ellipse is not null) {
										var collider = newObject.AddComponent<EllipseCollider2D>();
										collider.RadiusX = mapObject.width.Value * 0.5f / _map.tilewidth;
										collider.RadiusY = mapObject.height.Value * 0.5f / _map.tileheight;
									}
									else if (mapObject.polygon is not null) {
										var collider = newObject.AddComponent<PolygonCollider2D>();
										var points = mapObject.polygon.points;
										collider.points = PointsFromString(points, pixelsToUnits);
									}
									else if (mapObject.polyline is not null) {
										var collider = newObject.AddComponent<EdgeCollider2D>();
										var points = mapObject.polyline.points;
										collider.points = PointsFromString(points, pixelsToUnits);
									}
									else if (mapObject.text is not null) {
										var textMesh = newObject.AddComponent<TextMesh>();
										textMesh.text = mapObject.text.text;
										textMesh.anchor = TextAnchor.MiddleCenter;
										var color = Color.white;
										if (mapObject.text.color is not null)
											ColorUtility.TryParseHtmlString(mapObject.text.color, out color);

										textMesh.color = color;
										textMesh.fontSize = mapObject.text.pixelsize;
										var targetWorldTextHeight = (float)mapObject.text.pixelsize / _map.tileheight;
										textMesh.characterSize = targetWorldTextHeight * 10.0f / textMesh.fontSize;
										textMesh.TryGetComponent<MeshRenderer>(out var renderer);
										renderer.sortingOrder = _orderInLayer;
										renderer.sortingLayerID = SortingLayer.GetLayerValueFromName("Default");
									}
									else {
										// If no specific object type is defined, add a regular box collider
										var collider = newObject.AddComponent<BoxCollider2D>();
										collider.size = new Vector2(mapObject.width.Value / _map.tilewidth,
											mapObject.height.Value / _map.tileheight);
									}
								}

								// Apply object's rotation if it has a non-zero value
								if (mapObject.rotation != 0.0f)
									newObject.transform.RotateAround(corner, new Vector3(0.0f, 0.0f, 1.0f),
										-mapObject.rotation.Value);

								// Set object visibility based on the mapObject's visibility setting
								if (mapObject.visible == false) {
									if (_setHiddenLayersToInactive) {
										newObject.SetActive(false);
									}
									else {
										newObject.TryGetComponent<Renderer>(out var renderer);
										if (renderer is not null) renderer.enabled = false;
									}
								}

								// Process and apply custom properties of the map object
								HandleCustomProperties(newObject, mapObject.properties);
							}
						}

						var objectColor = Color.white;
						if (objectGroup.color is not null)
							ColorUtility.TryParseHtmlString(objectGroup.color, out objectColor);

						if (objectGroup.opacity != 1.0f) objectColor.a = objectGroup.opacity;

						var renderers = layerObject.GetComponentsInChildren<SpriteRenderer>();
						for (var j = 0; j < renderers.Length; j++) {
							var r = renderers[j];
							r.color = objectColor;
						}
					}

					_orderInLayer++;
				}
				else if (layer is ImageLayer imageLayer) // Import Image Layer
				{
					if (!TiledUtils.CreateAssetFolderPrompt(_imageLayerSpriteDir)) break;

					layerObject = new GameObject(imageLayer.name, typeof(SpriteRenderer));
					layerObject.transform.SetParent(parent.transform, false);
					SetupLayerOffset(layerObject, imageLayer.offsetx, imageLayer.offsety);
					var relativeSource = imageLayer.image.source;
					// Import Path As Sprite
					Sprite importedSprite = null;
					var imageSourcePath = relativeSource;
					imageSourcePath = Path.Combine(_tmxParentFolder, imageSourcePath);
					var imageName = Path.GetFileName(imageSourcePath);
					var imageTargetPath = Path.Combine(_imageLayerSpriteDir, imageName);
					TSXImporter.CopyImages(new[] { imageSourcePath }, new[] { imageTargetPath });
					if (File.Exists(imageTargetPath)) {
						var ti = AssetImporter.GetAtPath(imageTargetPath) as TextureImporter;
						var textureSettings = new TextureImporterSettings();
						ti.ReadTextureSettings(textureSettings);
						textureSettings.spritePixelsPerUnit = _pixelsPerUnit;
						textureSettings.spriteMeshType = SpriteMeshType.FullRect;
						textureSettings.spriteAlignment = (int)SpriteAlignment.TopLeft;
						textureSettings.filterMode = FilterMode.Point;
						ti.SetTextureSettings(textureSettings);
						EditorUtility.SetDirty(ti);
						ti.SaveAndReimport();
						importedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(imageTargetPath);
						Debug.Assert(importedSprite is not null);
					}

					layerObject.TryGetComponent<SpriteRenderer>(out var renderer);
					renderer.sprite = importedSprite;
					renderer.sortingOrder = _orderInLayer;
					_orderInLayer++;
				}
				else if (layer is GroupLayer groupLayer) {
					var newGroupLayer = new GameObject(groupLayer.name);
					newGroupLayer.transform.SetParent(parent.transform, false);
					layerObject = newGroupLayer;
					SetupLayerOffset(newGroupLayer, groupLayer.offsetx, groupLayer.offsety);
					if (groupLayer.childLayers is not null) ProcessLayers(newGroupLayer, groupLayer.childLayers);
				}

				if (layerObject is not null) {
					if (!layer.visible) {
						if (_setHiddenLayersToInactive) {
							layerObject.SetActive(false);
						}
						else {
							var renderers = layerObject.GetComponentsInChildren<Renderer>();
							for (var j = 0; j < renderers.Length; j++) {
								var r = renderers[j];
								r.enabled = false;
							}
						}
					}

					// Apply custom properties to the layerObject
					HandleCustomProperties(layerObject, layer.properties);

					// Register the created layerObject for Undo functionality in the editor
					Undo.RegisterCreatedObjectUndo(layerObject, "Import layer " + layerObject.name);
				}
			}

			return true;
		}

		public static Vector2[] PointsFromString(string pointsString, Vector2 scale) {
			var pointsSplit = pointsString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var points = new Vector2[pointsSplit.Length];

			for (var i = 0; i < points.Length; i++) {
				var vectorComponents = pointsSplit[i].Split(',');
				Debug.Assert(vectorComponents.Length == 2,
					"This string should have 2 components separated by a comma: " + pointsSplit[i]);

				var x = float.Parse(vectorComponents[0]);
				var y = float.Parse(vectorComponents[1]);

				points[i] = new Vector2(x * scale.x, y * scale.y);
			}

			return points;
		}

		/// <summary> Handles custom properties of a TMX layer by applying them to the corresponding GameObject.</summary>
		/// <param name="gameObject">The GameObject to apply the custom properties to.</param>
		/// <param name="tmxProperties">The TMX.Properties object containing the custom properties.</param>
		private static void HandleCustomProperties(GameObject gameObject, Properties tmxProperties) {
			// If tmxProperties is null, use an empty dictionary for properties
			var properties = tmxProperties?.ToDictionary() ?? new Dictionary<string, string>();

			// Iterate through all import operations
			for (var i = 0; i < _importOperations.Length; i++) {
				var operation = _importOperations[i];
				// Apply custom properties to the gameObject using the current operation
				operation.HandleCustomProperties(gameObject, properties);
			}
		}

		private static void AddChunkToTilemap(Tilemap layerTilemap, string encoding, string compression,
			TMX.Tile[] plainTiles, string dataText, int x, int y, int width, int height) {
			uint[] gIDData = null;
			if (encoding is null) {
				//Load Plain Tiles
				var tilesLength = plainTiles.Length;
				if (tilesLength != width * height)
					Debug.LogError(
						"The plain tiles array length isn't equal to the width times height in the TMX layer");
				gIDData = new uint[tilesLength];

				for (var i = 0; i < tilesLength; i++) {
					if (plainTiles[i] is null) Debug.LogError("Null plain tile detected");
					gIDData[i] = plainTiles[i].gid;
				}
			}
			else if (encoding.Equals("csv")) {
				var numbersAsStrings = dataText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				if (numbersAsStrings.Length != width * height)
					Debug.LogError("The CSV length isn't equal to the width times height in the TMX layer");

				gIDData = new uint[numbersAsStrings.Length];

				for (var i = 0; i < numbersAsStrings.Length; i++) {
					if (!uint.TryParse(numbersAsStrings[i], out var value))
						Debug.LogError("Could not parse GID " + numbersAsStrings[i]);
					gIDData[i] = value;
				}
			}
			else if (encoding.Equals("base64")) {
				var decoded = Convert.FromBase64String(dataText);

				if (compression is not null) {
					if (compression.Equals("zlib"))
						using (var memoryStream = new MemoryStream(decoded, 2, decoded.Length - 2)) // Skip zlib header
						{
							using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress)) {
								using (var outputStream = new MemoryStream()) {
									const int bufferSize = 32768;
									var buffer = new byte[bufferSize];
									int bytesRead;

									while ((bytesRead = deflateStream.Read(buffer, 0, buffer.Length)) != 0)
										outputStream.Write(buffer, 0, bytesRead);

									decoded = outputStream.ToArray();
								}
							}
						}

					else if (compression.Equals("gzip"))
						using (var memoryStream = new MemoryStream(decoded)) {
							using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress)) {
								var output = new MemoryStream();
								var buffer = new byte[32678];
								int amountRead;
								while ((amountRead = gZipStream.Read(buffer, 0, buffer.Length)) > 0)
									output.Write(buffer, 0, amountRead);

								decoded = output.ToArray();
							}
						}
				}

				if (decoded.Length != width * height * 4)
					Debug.LogError(
						$"The decoded byte data length ({decoded.Length}) does not match the expected length (width * height * 4 = {width * height * 4}) for the TMX layer.");

				gIDData = new uint[decoded.Length / 4];
				for (var i = 0; i < gIDData.Length; i++) {
					var bytePos = i * 4;
					gIDData[i] = decoded[bytePos++] | ((uint)decoded[bytePos++] << 8) |
					             ((uint)decoded[bytePos++] << 16) | ((uint)decoded[bytePos] << 24);
				}
			}

			if (gIDData is null)
				Debug.LogError($"Layer data for layer '{layerTilemap.gameObject.name}' could not be decoded.");

			// Fill Tilemap From Data
			var anyTilesWithCollision = false;
			var dataLength = gIDData.Length;

			var tilePositions = new Vector3Int[dataLength];
			var tileBases = new TileBase[dataLength];
			var currentIndex = 0;

			for (var i = 0; i < dataLength; i++) {
				var value = gIDData[i];
				TiledUtils.FindTileDataAndMatrix(value, _importedTilesets, _cellWidth, _cellHeight,
					out var importedTile, out var matrix);
				if (importedTile is not null && importedTile.Tile is not null) {
					var tileX = x + i % width;
					var tileY = -(y + i / width + 1);

					Vector3Int pos;

					if (_needsGridRotationToMatchUnityIsometric) {
						// Rotate 2D grid coordinates 90 degrees clockwise
						pos = new Vector3Int(tileY, -(tileX + 1), 0);
					}
					else if (_needsGridRotationToMatchUnityStaggeredIsometric) {
						var staggerIndex = _needsStaggerOddToEvenConversion ? 0 : 1;
						var gridX = x + i % width;
						var gridY = y + i / width;

						if (_staggerAxis == StaggerAxis.Y) {
							var halfGridY = gridY / 2;
							pos = new Vector3Int(gridX - halfGridY, -halfGridY - gridX, 0);

							if (gridY % 2 == staggerIndex) pos.x += 1;
						}
						else {
							pos = new Vector3Int(gridX, gridY, 0);
						}
					}
					else if (_staggerAxis == StaggerAxis.X) {
						// Rotate 2D grid coordinates 90 degrees clockwise but also flip on the x axis (relative to yxz cell swizzle)
						pos = new Vector3Int(tileY, tileX, 0);
					}
					else {
						pos = new Vector3Int(tileX, tileY, 0);
					}

					// Most tiles can be batched together, but tiles where transform matrix isn't equal to the identity
					// matrix need to be set individually
					if (matrix == Matrix4x4.identity) {
						tilePositions[currentIndex] = pos;
						tileBases[currentIndex] = importedTile.Tile;
						currentIndex++;
					}
					else {
						layerTilemap.SetTile(pos, importedTile.Tile);
						layerTilemap.SetTransformMatrix(pos, matrix);
					}

					if (importedTile.Tile.colliderType != UnityEngine.Tilemaps.Tile.ColliderType.None)
						anyTilesWithCollision = true;
				}
				else if (value > 0) {
					Debug.LogError($"Could not find tile {value} in tilemap {layerTilemap.name}");
				}
			}

			layerTilemap.SetTiles(tilePositions, tileBases);

			// Apply transform matrices for the batched tiles (assuming identity matrix)
			for (var i = 0; i < currentIndex; i++)
				layerTilemap.SetTransformMatrix(tilePositions[i], Matrix4x4.identity);

			if (!anyTilesWithCollision) return;
			if (layerTilemap.gameObject.TryGetComponent<TilemapCollider2D>(out _)) return;
			if (_useCompositeColliders) {
				var collider = layerTilemap.gameObject.AddComponent<TilemapCollider2D>();
				layerTilemap.gameObject.AddComponent<CompositeCollider2D>();
				if (layerTilemap.gameObject.TryGetComponent<Rigidbody2D>(out var rigidbody2D))
					rigidbody2D.bodyType = RigidbodyType2D.Static;
				collider.usedByComposite = true;
			}
			else {
				layerTilemap.gameObject.AddComponent<TilemapCollider2D>();
			}
		}
	}

	internal enum StaggerAxis {
		X = 0,
		Y = 1,
		None = 2
	}
}
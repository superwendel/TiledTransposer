using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ReverseGravity.TiledTransposer {
	public class TMXImporterWindow : EditorWindow {
		private string _sourcePath;
		private List<string> _pathHistory;
		private Grid _targetGrid;
		private string _tileSetDir;
		private string _imageLayerSpriteDir = "ImageLayerSprites";

		private ColliderImportType _colliderImportType;
		private bool _setHiddenLayersToInactive;
		private bool _forceGridColliders;
		private bool _useCompositeColliders;
		private bool _allowPrefabEdits;

		private static string _defaultTileSetDirectory;

		private const string TMXSourcePathKey = "TMX.SourcePath";
		private const string TileSetDirKey = "TMX.TilesetDir";
		private const string PathHistoryKey = "TMX.PathHistory";
		private const string ImageLayerSpriteDirKey = "TMX.ImageLayerSpriteDir";
		private const string SetHiddenLayersToInactiveKey = "TMX.SetHiddenLayersToInactive";
		private const string ForceGridCollidersKey = "TMX.ForceGridColliders";
		private const string UseCompositeCollidersKey = "TMX.UseCompositeColliders";
		private const string AllowPrefabEditsKey = "TMX.AllowPrefabEdits";

		private static GUIContent ImportButton = EditorGUIUtility.TrTextContent("Import",
			"Imports the selected TMX file and its referenced TSX TileSets");

		private static GUIContent ClearPathButton =
			EditorGUIUtility.TrTextContent("Clear Path", "Deselects the currently chosen TMX file");

		private static GUIContent ChangeTileSetDirectoryButton =
			EditorGUIUtility.TrTextContent("Modify TileSet Directory",
				"Specify the location to save generated Unity Sprite and Tile assets");

		private static GUIContent ChooseDefaultTileSetDirectoryButton =
			EditorGUIUtility.TrTextContent("Select default TileSet directory",
				"Reverts target TileSet directory to its default value");

		private static GUIContent TargetTileMapGridLabel =
			EditorGUIUtility.TrTextContent("Destination TileMap Grid:",
				"The grid in the scene where the map will be placed. If left empty, a new grid object will be created with the imported tile");

		private static GUIContent SetHiddenLayersLabel =
			EditorGUIUtility.TrTextContent("Configure Hidden Layers As Inactive",
				"Determines whether hidden layers in Tiled should be set to inactive in the scene rather than just invisible");

		private static GUIContent ForceGridCollidersLabel =
			EditorGUIUtility.TrTextContent("Collider Import Mode",
				"Determines whether all imported tiles with collision data should utilize Tiled's collider info, 'Grid' colliders, or 'Sprite' colliders");

		private static GUIContent UseCompositeCollidersLabel =
			EditorGUIUtility.TrTextContent("Employ Composite Colliders",
				"Determines whether a composite collider should be utilized for tile layers with collision data");

		private static GUIContent AllowPrefabEditsLabel = EditorGUIUtility.TrTextContent(
			"Permit Prefab Modifications",
			"Determines whether the importer should apply changes directly to the prefab. This is necessary to prevent the error 'InvalidOperationException: Destroying a GameObject inside a Prefab instance is not allowed.' when importing into a prefab instance");

		[MenuItem("Window/Tiled Transposer")]
		public static void ShowWindow() {
			GetWindow<TMXImporterWindow>(false, "TMX Transposer", true);
		}

		private void OnEnable() {
			// Load the icon and set it as the title content for the window
			var icon = (Texture)EditorGUIUtility.Load("d_PreTextureMipMapLow");
			titleContent = new GUIContent("TMX Transposer", icon);

			// Set the default directory for the tile sets
			_defaultTileSetDirectory = Path.Combine(Application.dataPath, "TileSets");

			_tileSetDir = _defaultTileSetDirectory;
			_pathHistory = new List<string>();

			// Load previously saved preferences from the editor
			_pathHistory = EditorPrefs.HasKey(PathHistoryKey)
				? new List<string>(EditorPrefs.GetString(PathHistoryKey).Split('|'))
				: new List<string>();

			if (EditorPrefs.HasKey(TMXSourcePathKey)) _sourcePath = EditorPrefs.GetString(TMXSourcePathKey);
			if (EditorPrefs.HasKey(TileSetDirKey)) _tileSetDir = EditorPrefs.GetString(TileSetDirKey);
			if (EditorPrefs.HasKey(ImageLayerSpriteDirKey))
				_imageLayerSpriteDir = EditorPrefs.GetString(ImageLayerSpriteDirKey);
			if (EditorPrefs.HasKey(SetHiddenLayersToInactiveKey))
				_setHiddenLayersToInactive = EditorPrefs.GetBool(SetHiddenLayersToInactiveKey);
			if (EditorPrefs.HasKey(ForceGridCollidersKey))
				_forceGridColliders = EditorPrefs.GetBool(ForceGridCollidersKey);
			if (EditorPrefs.HasKey(UseCompositeCollidersKey))
				_useCompositeColliders = EditorPrefs.GetBool(UseCompositeCollidersKey);
			if (EditorPrefs.HasKey(AllowPrefabEditsKey)) _allowPrefabEdits = EditorPrefs.GetBool(AllowPrefabEditsKey);

			// Refresh the selection and grid
			UpdateActiveGrid();

			EditorApplication.hierarchyChanged += UpdateActiveGrid;
		}

		private void OnDisable() {
			// Unsubscribe from the hierarchyChanged event
			EditorApplication.hierarchyChanged -= UpdateActiveGrid;

			// Create a new list that only contains the last 5 elements of the path history
			if (_pathHistory.Count > 5) _pathHistory.RemoveRange(0, _pathHistory.Count - 5);

			var totalString = string.Join("|", _pathHistory);

			EditorPrefs.SetString(PathHistoryKey, totalString);
			EditorPrefs.SetString(TMXSourcePathKey, _sourcePath);
			EditorPrefs.SetString(TileSetDirKey, _tileSetDir);
			EditorPrefs.SetString(ImageLayerSpriteDirKey, _imageLayerSpriteDir);
			EditorPrefs.SetBool(SetHiddenLayersToInactiveKey, _setHiddenLayersToInactive);
			EditorPrefs.SetBool(ForceGridCollidersKey, _forceGridColliders);
			EditorPrefs.SetBool(UseCompositeCollidersKey, _useCompositeColliders);
			EditorPrefs.SetBool(AllowPrefabEditsKey, _allowPrefabEdits);
		}

		/// <summary> Refreshes the selected grid and its tiles.</summary>
		private void UpdateActiveGrid() {
			// Find all Grid objects in the scene
			var gridsInScene = FindObjectsOfType<Grid>();

			// If no Grid objects were found, return
			if (gridsInScene is null) return;

			// If there is only one Grid in the scene, set it as the target grid
			if (gridsInScene.Length == 1) _targetGrid = gridsInScene[0];
			else
				// If there are multiple Grids in the scene, toggle the enabled state of each grid
				for (var i = 0; i < gridsInScene.Length; i++) {
					var grid = gridsInScene[i];
					grid.enabled = !grid.enabled;
					grid.enabled = !grid.enabled; // Unity's Grid doesn't update on redo, force it.
				}
		}

		private void OnGUI() {
			// Display a label and a box for dragging and dropping a TMX file
			GUILayout.Label("Drag & Drop a *.TMX TileMap file", EditorStyles.centeredGreyMiniLabel);
			GUILayout.Box(GUIIcons.TileIcon, GUIElements.DragDropBox, GUILayout.ExpandWidth(true),
				GUILayout.Height(72));

			// Get the type of the current event
			var e = Event.current.type;

			// If the current event is a DragUpdated or DragPerform event
			if (e is EventType.DragUpdated or EventType.DragPerform) {
				// Set the visual mode of the drag and drop operation to Copy
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

				// If the current event is a DragPerform event
				if (e == EventType.DragPerform) {
					// Accept the drag and drop operation and set the source path to the first element in the paths array
					DragAndDrop.AcceptDrag();
					_sourcePath = DragAndDrop.paths[0];
					;
				}

				// Consume the current event
				Event.current.Use();
			}

			// Set a flag to indicate whether the source path exists and is a valid file
			var pathExists = false;

			// If the source path is not null and has a length greater than 0
			if (_sourcePath is not null && _sourcePath.Length > 0) {
				// Set the flag to true
				pathExists = true;

				// If the source path does not exist or is not a valid file, set the flag to false
				if (!File.Exists(_sourcePath)) pathExists = false;

				// If the source path exists and is not already in the path history, add it to the path history
				if (pathExists && !_pathHistory.Contains(_sourcePath)) _pathHistory.Add(_sourcePath);

				// Display the source path
				GUILayout.Label(_sourcePath);

				// If the user clicks the "Clear Path" button, set the source path to null
				if (GUILayout.Button(ClearPathButton)) _sourcePath = null;
			}

			// History Drop Down
			if (_pathHistory is not null && _pathHistory.Count > 0) {
				// Convert the path history to an array of strings
				var options = _pathHistory.ToArray();

				// Replace all forward slashes with backslashes in the array of strings
				for (var i = 0; i < options.Length; i++) options[i] = options[i].Replace('/', '\\');

				// Display a dropdown menu containing the elements of the path history
				var selected = _pathHistory.IndexOf(_sourcePath);
				selected = EditorGUILayout.Popup("History:", selected, options);

				// If the user selects an element from the dropdown menu, set the source path to the selected element
				if (selected >= 0) _sourcePath = _pathHistory[selected];
			}

			_targetGrid = EditorGUILayout.ObjectField(TargetTileMapGridLabel, _targetGrid, typeof(Grid), true) as Grid;
			GUILayout.Label("(Leave empty to spawn a new Grid in the scene)", EditorStyles.centeredGreyMiniLabel);
			GUILayout.Space(10);

			GUILayout.Label(new GUIContent("Target Tileset directory: " + _tileSetDir, _tileSetDir),
				EditorStyles.linkLabel);
			if (GUILayout.Button(ChangeTileSetDirectoryButton)) {
				var newTilesetDir = EditorUtility.OpenFolderPanel("Choose TileSet Directory", "Assets", "TileSets");
				if (newTilesetDir is not null && newTilesetDir.Length > 0) {
					_tileSetDir = newTilesetDir;
					AssetDatabase.Refresh();
				}
			}

			if (!_tileSetDir.Equals(_defaultTileSetDirectory))
				if (GUILayout.Button(ChooseDefaultTileSetDirectoryButton))
					_tileSetDir = _defaultTileSetDirectory;

			GUILayout.Space(10);

			var tilesetDirValid = _tileSetDir is not null && _tileSetDir.Length > 0 &&
			                      _tileSetDir.StartsWith(Application.dataPath) &&
			                      _tileSetDir.Length > Application.dataPath.Length;

			if (pathExists && tilesetDirValid) {
				GUI.backgroundColor = GUIElements.EditorGreen;
				GUI.enabled = true;
			}
			else {
				if (!pathExists) {
					EditorGUILayout.HelpBox("Drag and drop a TMX file from your file system", MessageType.Warning);
				}
				else if (!tilesetDirValid) {
					if (!_tileSetDir.StartsWith(Application.dataPath))
						EditorGUILayout.HelpBox(
							"The imported tileset folder must be located within the project's assets folder.",
							MessageType.Warning);
					else if (_tileSetDir.Length <= Application.dataPath.Length)
						EditorGUILayout.HelpBox(
							"The imported tileset folder must reside as a subdirectory within the project's assets folder.",
							MessageType.Warning);
					else
						EditorGUILayout.HelpBox("Choose a directory for saving the imported tilesets.",
							MessageType.Warning);
				}

				GUI.enabled = false;
				GUI.backgroundColor = Color.grey;
			}

			if (GUILayout.Button(ImportButton)) {

				EditorUtility.UnloadUnusedAssetsImmediate(true);
				// Attempt to import the TMX file
				if (TMXImporter.ImportTMXFile(_targetGrid, _sourcePath, _tileSetDir, _imageLayerSpriteDir,
					    _setHiddenLayersToInactive, _forceGridColliders, _useCompositeColliders, _allowPrefabEdits,
					    _colliderImportType)) {
					Debug.Log("TileMap Import Success");
					UpdateActiveGrid();
				}
				// If the import failed, log an error message
				else {
					Debug.LogError("Import Failed");
				}
				
				EditorUtility.ClearProgressBar();
			}

			GUI.enabled = true;
			GUI.backgroundColor = Color.white;

			_colliderImportType =
				(ColliderImportType)EditorGUILayout.EnumPopup(ForceGridCollidersLabel, _colliderImportType);
			_setHiddenLayersToInactive = GUILayout.Toggle(_setHiddenLayersToInactive, SetHiddenLayersLabel);
			//_forceGridColliders = GUILayout.Toggle(_forceGridColliders, ForceGridCollidersLabel);

			_useCompositeColliders = GUILayout.Toggle(_useCompositeColliders, UseCompositeCollidersLabel);
			_allowPrefabEdits = GUILayout.Toggle(_allowPrefabEdits, AllowPrefabEditsLabel);
		}
	}
}
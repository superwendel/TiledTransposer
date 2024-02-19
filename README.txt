This is the same version published on the Unity Asset Store. If you would like support, to support, have a feature request, or wish to use it as a package please purchase there:
https://assetstore.unity.com/packages/tools/sprite-management/tiled-transposer-254232

Tiled Transposer is a tool that allows you to easily load Tiled TMX tilemap and TSX tileset files into Unity's Tilemap system. If you're new to Tiled or Unity's Tilemap system, you may want to check out these resources first:

Tiled documentation: https://doc.mapeditor.org/en/stable/
Unity Tilemap documentation: https://docs.unity3d.com/Manual/class-Tilemap.html
To use the Tiled TMX Importer, follow these steps:

1) Open the Tiled TMX Importer by selecting Window/Tiled Transposer from the Unity editor menu.
2) Drag and drop a TMX file from your operating system into the importer window.
3) Choose a target tilemap Grid object to load the tilemap into. You can select an existing Grid object in your scene, or leave the box blank to create a new Grid.
4) Select a target tileset directory. This is where the Sprite and Tile assets will be created from the tilesets referenced in the TMX file. You can use the "Change TileSet Directory" button to select a different directory if you prefer.
Optional: If you want to customize the import process, you can write a class that implements the ITilemapImportOperation interface. This allows you to handle custom properties or perform additional processing during the import. Place your class in an Editor folder in your Unity project to make it available to the importer.
5) Click the "Import" button to start the import process.
Imported tilesets are placed into the target tileset directory, which by default is Assets/TileSets. If the TMX tilemap file references multiple tilesets, each tileset is given its own sub-directory during import.

It is traditional for the creator to magnanimously accept the blame for whatever deficiencies remain. I don’t.
Any errors, deficiencies, or problems in this package are somebody else’s fault, but I would appreciate knowing about them so as to determine who is to blame.

-Wendel
swendel@reversegravitygames.com

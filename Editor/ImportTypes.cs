using System;

namespace ReverseGravity.TiledTransposer {
	public class ImportedTemplate {
		public readonly TXTypes.Template Template;
		public readonly ImportedTileset ImportedTileset;

		public ImportedTemplate(TXTypes.Template template, ImportedTileset tileset) {
			Template = template;
			ImportedTileset = tileset;
		}
	}

	/// <summary>
	/// Represents an imported TileSet containing an array of ImportedTile objects, a firstGID value, and a TSX.Tileset object.
	/// </summary>
	public class ImportedTileset {
		/// <summary> An array of ImportedTile objects.</summary>
		public readonly ImportedTile[] Tiles;

		/// <summary> The first global identifier (GID) assigned to the tileset.</summary>
		public readonly int FirstGid;

		/// <summary> The TSX.Tileset object representing the tileset.</summary>
		public readonly TSX.Tileset Tileset;

		/// <summary>
		/// Initializes a new instance of the ImportedTileset class with the specified tiles, firstGID value, and TSX.Tileset object.
		/// </summary>
		/// <param name="tilesIn">An array of ImportedTile objects to be included in the tileset.</param>
		/// <param name="firstGIDIn">The first global identifier (GID) assigned to the tileset.</param>
		/// <param name="tilesetIn">The TSX.Tileset object representing the tileset.</param>
		public ImportedTileset(ImportedTile[] tilesIn, int firstGIDIn, TSX.Tileset tilesetIn) {
			Tiles = tilesIn;
			FirstGid = firstGIDIn;
			Tileset = tilesetIn;
		}
	}

	/// <summary>
	/// Stores an imported tile, which consists of a <see cref="UnityEngine.Tilemaps.Tile"/> object
	/// and the path to the image used by the tile.
	/// </summary>
	public class ImportedTile {
		/// <summary> The tile.</summary>
		public UnityEngine.Tilemaps.Tile Tile;

		/// <summary> The path to the tile.</summary>
		public readonly string Path;

		/// <summary> Constructs a new ImportedTile.</summary>
		/// <param name="tileIn">The tile.</param>
		/// <param name="pathIn">The path to the tile.</param>
		public ImportedTile(UnityEngine.Tilemaps.Tile tileIn, string pathIn) {
			Tile = tileIn;
			Path = pathIn;
		}
	}

	[Flags]
	public enum ColliderImportType {
		Tiled,
		Sprite,
		Grid
	}
}
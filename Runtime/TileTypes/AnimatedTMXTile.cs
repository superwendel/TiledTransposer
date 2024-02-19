using System;

namespace UnityEngine.Tilemaps {
	/// <summary> A custom Unity tile representing a Tiled tile.</summary>
	[Serializable]
	public class AnimatedTMXTile : Tile {
		/// <summary> The array of sprites to use for the animation.</summary>
		public Sprite[] animatedSprites;

		/// <summary> The speed of the animation.</summary>
		public float speed = 1f;

		/// <summary> The time at which the animation should start.</summary>
		public float animationStartTime;

		/// <summary> Retrieves basic tile data for the tile at the specified location in the tilemap.</summary>
		/// <param name="location">The position of the tile in the tilemap.</param>
		/// <param name="tileMap">The ITilemap object containing the tile.</param>
		/// <param name="tileData">The TileData object to be populated with data.</param>
		public override void GetTileData(Vector3Int location, ITilemap tileMap, ref TileData tileData) {
			tileData.transform = Matrix4x4.identity;
			tileData.color = Color.white;
			tileData.colliderType = colliderType;
			if (animatedSprites is not null && animatedSprites.Length > 0) tileData.sprite = animatedSprites[^1];
		}

		/// <summary> Retrieves animation data for the tile at the specified location in the tilemap.</summary>
		/// <param name="location">The position of the tile in the tilemap.</param>
		/// <param name="tileMap">The ITilemap object containing the tile.</param>
		/// <param name="tileAnimationData">The TileAnimationData object to be populated with data.</param>
		/// <returns>True if the tile has animation data, false otherwise.</returns>
		public override bool GetTileAnimationData(Vector3Int location, ITilemap tileMap,
			ref TileAnimationData tileAnimationData) {
			if (animatedSprites.Length <= 0) return false;
			tileAnimationData.animatedSprites = animatedSprites;
			tileAnimationData.animationSpeed = speed;
			tileAnimationData.animationStartTime = animationStartTime;
			return true;
		}
	}
}
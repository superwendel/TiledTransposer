using UnityEngine;
using UnityEngine.Tilemaps;

namespace ReverseGravity.TiledTransposer {
	/// <summary> Tiled animated tiles can be used in a tile layer or in an object, animate an object using an animated tile.</summary>
	public class AnimatedTileObject : MonoBehaviour {
		/// <summary> The animated tile asset to use for this object.</summary>
		public AnimatedTMXTile tileAsset;

		/// <summary> The sprite renderer for this object.</summary>
		private SpriteRenderer _renderer;

		/// <summary> Timer for tracking animation progress.</summary>
		private float _timer;

		/// <summary> Speed of the animation.</summary>
		private float _animationSpeed;

		/// <summary> Initializes the object and gets the sprite renderer.</summary>
		private void Start() {
			_renderer = GetComponent<SpriteRenderer>();
			_timer = tileAsset.animationStartTime;
			_animationSpeed = tileAsset.speed;
		}

		/// <summary> Updates the object's sprite based on the current frame of the animation.</summary>
		private void Update() {
			_timer += Time.deltaTime * _animationSpeed;
			var frameNumber = (int)_timer % tileAsset.animatedSprites.Length;
			_renderer.sprite = tileAsset.animatedSprites[frameNumber];
		}
	}
}
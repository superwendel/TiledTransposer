using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ReverseGravity.TiledTransposer {
	[CustomEditor(typeof(AnimatedTMXTile))]
	public class AnimatedTMXITileInspector : Editor {
		private AnimatedTMXTile Tile => target as AnimatedTMXTile;

		[MenuItem("Assets/Create/Animated Tile")]
		public static void CreateAnimatedTile() {
			var path = EditorUtility.SaveFilePanelInProject("Save Animated Tile", "New Animated Tile", "asset",
				"Save Animated Tile", "Assets");
			if (path == "") return;

			AssetDatabase.CreateAsset(CreateInstance<AnimatedTMXTile>(), path);
		}

		public override void OnInspectorGUI() {
			EditorGUI.BeginChangeCheck();
			var count = EditorGUILayout.DelayedIntField("Number of Animated Sprites",
				Tile.animatedSprites?.Length ?? 0);
			if (count < 0) count = 0;
			if (Tile.animatedSprites is null || Tile.animatedSprites.Length != count)
				Array.Resize(ref Tile.animatedSprites, count);

			if (count == 0) return;
			EditorGUILayout.LabelField("Place sprites shown based on the order of animation.");
			EditorGUILayout.Space();
			for (var i = 0; i < count; i++)
				Tile.animatedSprites[i] = (Sprite)EditorGUILayout.ObjectField("Sprite " + (i + 1),
					Tile.animatedSprites[i], typeof(Sprite), false, null);

			var tileSpeed = EditorGUILayout.FloatField("Speed", Tile.speed);
			if (tileSpeed < 0.0f) tileSpeed = 0.0f;
			Tile.speed = tileSpeed;
			Tile.animationStartTime = EditorGUILayout.FloatField("Start Time", Tile.animationStartTime);
			if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(Tile);
		}
	}
}
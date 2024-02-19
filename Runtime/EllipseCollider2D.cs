using UnityEngine;

namespace ReverseGravity.TiledTransposer {
	/// <summary> A custom 2D Ellipse collider for Unity that utilizes polygon collider.</summary>
	[AddComponentMenu("Physics 2D/Ellipse Collider 2D")]
	[RequireComponent(typeof(PolygonCollider2D))]
	[System.Serializable]
	public class EllipseCollider2D : MonoBehaviour {
		[Range(10, 90)] [SerializeField]
		// Controls the number of points used to approximate the shape of the collider.
		protected int smoothness = 24;

		[Range(1, 25)] [HideInInspector] [SerializeField]
		private float radiusX = 1, radiusY = 2;

		/// <summary> Gets or sets the horizontal radius of the ellipse.</summary>
		public float RadiusX {
			get => radiusX;
			set {
				radiusX = value;
				UpdateCollider();
			}
		}

		/// <summary> Gets or sets the vertical radius of the ellipse.</summary>
		public float RadiusY {
			get => radiusY;
			set {
				radiusY = value;
				UpdateCollider();
			}
		}

		/// <summary> Generates an array of points representing the ellipse's shape.</summary>
		/// <returns>An array of 2D points that define the ellipse's shape.</returns>
		public Vector2[] GetPoints() {
			var points = new Vector2[smoothness];
			var angleIncrementDegrees = 360f / smoothness;
			var angleIncrementRadians = angleIncrementDegrees * Mathf.Deg2Rad;

			var cosIncrement = Mathf.Cos(angleIncrementRadians);
			var sinIncrement = Mathf.Sin(angleIncrementRadians);

			var cosAngle = 1f;
			var sinAngle = 0f;

			for (var i = 0; i < smoothness; i++) {
				// Calculate the x and y coordinates of the ellipse point.
				var x = radiusX * cosAngle;
				var y = radiusY * sinAngle;

				// Store the point in the array.
				points[i] = new Vector2(x, y);

				// Update the angle for the next point.
				var cosTemp = cosAngle * cosIncrement - sinAngle * sinIncrement;
				sinAngle = sinAngle * cosIncrement + cosAngle * sinIncrement;
				cosAngle = cosTemp;
			}

			return points;
		}

		/// <summary> Updates the PolygonCollider2D component with the generated points.</summary>
		protected void UpdateCollider() {
			// Get the PolygonCollider2D component attached to the current GameObject.
			var polygonCollider = GetComponent<PolygonCollider2D>();

			// Update the points of the PolygonCollider 2D using the generated points from GetPoints().
			polygonCollider.points = GetPoints();
		}
	}
}
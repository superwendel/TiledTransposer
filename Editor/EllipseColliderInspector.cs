using UnityEngine;
using UnityEditor;

namespace ReverseGravity.TiledTransposer {
	[CustomEditor(typeof(EllipseCollider2D))]
	public class EllipseColliderInspector : Editor {
		private EllipseCollider2D _ec;
		private PolygonCollider2D _polyCollider;
		private Vector2 _off;

		private void OnEnable() {
			_ec = (EllipseCollider2D)target;
			_polyCollider = _ec.GetComponent<PolygonCollider2D>();
			if (_polyCollider is null) _polyCollider = _ec.gameObject.AddComponent<PolygonCollider2D>();

			_polyCollider.points = _ec.GetPoints();
		}

		public override void OnInspectorGUI() {
			GUI.changed = false;
			DrawDefaultInspector();
			_ec.RadiusX = EditorGUILayout.FloatField("RadiusX", _ec.RadiusX);
			_ec.RadiusY = EditorGUILayout.FloatField("RadiusY", _ec.RadiusY);
			if (GUI.changed || !_off.Equals(_polyCollider.offset)) _polyCollider.points = _ec.GetPoints();

			_off = _polyCollider.offset;
		}
	}
}
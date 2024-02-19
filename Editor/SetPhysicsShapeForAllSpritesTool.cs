using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// TODO(wendel) finish this
public class SetPhysicsShapeForAllSpritesTool : Editor
{
	private Vector2Int pivot;
	private bool normalized;

	[MenuItem("CONTEXT/TextureImporter/Set Physics Shape For All Sprites")]
	public static void ShowSetPhysicsShapeForAllSpritesWindow()
	{
// Overwrite physics shape for each sprite in the spritesheet
		//Sprite[] sprites = (Selection.activeObject as Texture2D).GetSprites();
		//if (sprites.IsNullOrEmpty())
		//{
		//	return;
		//}

		string path = AssetDatabase.GetAssetPath(Selection.activeObject);
		TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
		var spritePhysicsShapeImporter = new SpritePhysicsShapeImporter(importer);
		List<Vector2[]> copyShapes = spritePhysicsShapeImporter.GetPhysicsShape(0);
	//	for (int i = 0; i < sprites.Length; ++i)
	//	{
	//		spritePhysicsShapeImporter.SetPhysicsShape(i, copyShapes);
	//	}

// Save the changes made to the importer and refresh the database
		spritePhysicsShapeImporter.Save();
	}
}

public class SpritePhysicsShapeImporter
{
	private TextureImporter m_TI;
	private SerializedObject m_TISerialized;

	public SpritePhysicsShapeImporter(TextureImporter ti)
	{
		m_TI = ti;
		m_TISerialized = new SerializedObject(m_TI);
	}

	public List<Vector2[]> GetPhysicsShape(int index)
	{
		var physicsShapeProperty = GetPhysicsShapeProperty(index);
		var physicsShape = new List<Vector2[]>();
		for (int j = 0; j < physicsShapeProperty.arraySize; ++j)
		{
			SerializedProperty physicsShapePathSP = physicsShapeProperty.GetArrayElementAtIndex(j);
			var o = new Vector2[physicsShapePathSP.arraySize];
			for (int k = 0; k < physicsShapePathSP.arraySize; ++k)
			{
				o[k] = physicsShapePathSP.GetArrayElementAtIndex(k).vector2Value;
			}

			physicsShape.Add(o);
		}

		return physicsShape;
	}

	public void SetPhysicsShape(int index, List<Vector2[]> data)
	{
		var physicsShapeProperty = GetPhysicsShapeProperty(index);
		physicsShapeProperty.ClearArray();
		for (int j = 0; j < data.Count; ++j)
		{
			physicsShapeProperty.InsertArrayElementAtIndex(j);
			var o = data[j];
			SerializedProperty outlinePathSP = physicsShapeProperty.GetArrayElementAtIndex(j);
			outlinePathSP.ClearArray();
			for (int k = 0; k < o.Length; ++k)
			{
				outlinePathSP.InsertArrayElementAtIndex(k);
				outlinePathSP.GetArrayElementAtIndex(k).vector2Value = o[k];
			}
		}

		m_TISerialized.ApplyModifiedPropertiesWithoutUndo();
	}

	public void Save()
	{
		AssetDatabase.ForceReserializeAssets(new string[] { m_TI.assetPath },
			ForceReserializeAssetsOptions.ReserializeMetadata);
		m_TI.SaveAndReimport();
	}

	private SerializedProperty GetPhysicsShapeProperty(int index)
	{
		if (m_TI.spriteImportMode == SpriteImportMode.Multiple)
		{
			var spriteSheetSP = m_TISerialized.FindProperty("m_SpriteSheet.m_Sprites");
			if (index < spriteSheetSP.arraySize)
			{
				var element = spriteSheetSP.GetArrayElementAtIndex(index);
				return element.FindPropertyRelative("m_PhysicsShape");
			}
		}

		return m_TISerialized.FindProperty("m_SpriteSheet.m_PhysicsShape");
	}
}
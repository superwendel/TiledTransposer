using System.Collections.Generic;
using System.Xml.Serialization;

namespace ReverseGravity.TiledTransposer {
	namespace TSX {
		[XmlRoot(ElementName = "tileset")]
		public class Tileset {
			[XmlAttribute] public float version;
			[XmlAttribute] public string tiledversion;
			[XmlAttribute] public string name;
			[XmlAttribute] public int tilewidth;
			[XmlAttribute] public int tileheight;
			[XmlAttribute] public int spacing;
			[XmlAttribute] public int margin;

			/// <summary>
			/// The XML can give a tilecount of 0, so to get a predicted tilecount in those cases, use GetTileCount()
			/// </summary>
			[XmlAttribute] public int tilecount;

			[XmlAttribute] public int columns;
			[XmlElement] public Grid grid;
			[XmlElement(ElementName = "tile")] public Tile[] tiles;
			[XmlElement] public TileOffset tileoffset;
			[XmlElement] public Image image;

			public Tileset(TMX.TilesetReference embeddedTileset) {
				name = embeddedTileset.name;
				tilewidth = embeddedTileset.tilewidth;
				tileheight = embeddedTileset.tileheight;
				tilecount = embeddedTileset.tilecount;
				columns = embeddedTileset.columns;
				grid = embeddedTileset.grid;
				tiles = embeddedTileset.tiles;
				image = embeddedTileset.image;
				tileoffset = embeddedTileset.tileoffset;
			}

			public Tileset() {
			}

			/// <summary> Determines whether this tileset is a single image tileset (with no individual tiles).</summary>
			/// <returns>True if the tileset is a single image tileset, false otherwise.</returns>
			public bool IsImageOnlyTileset() {
				return tiles is null || image is not null;
			}

			/// <summary>
			/// Calculates the tile count for this tileset given the dimensions of the tileset image.
			/// </summary>
			/// <param name="width">The width of the tileset image.</param>
			/// <param name="height">The height of the tileset image.</param>
			/// <returns>The calculated tile count for the tileset.</returns>
			public int CalculateTileCountFromDimensions(int width, int height) {
				// Calculate the number of tiles across and down based on the dimensions of the tileset image.
				// This calculation is derived from solving for the number of tiles (n) in the equation
				// "imageWidth = 2*margin + tileWidth * n + spacing * (n-1)".
				var tilesAcross = (width + spacing - margin * 2) / (spacing + tilewidth);
				var tilesDown = (height + spacing - margin * 2) / (spacing + tileheight);
				return tilesAcross * tilesDown;
			}

			public int CalculateTileCount() {
				return tilecount > 0 ? tilecount : CalculateTileCountFromDimensions(image.width, image.height);
			}
		}

		public class Grid {
		}

		// This allows the serializer to correctly map the data to the C# class without encountering errors or conflicts.
		// Without this attribute, the serializer may fail to correctly deserialize the data, or may encounter errors during serialization.
		[XmlType(AnonymousType = true)]
		public class Tile {
			[XmlAttribute] public int id;
			[XmlElement] public Image image;
			[XmlElement] public ObjectGroup objectgroup;
			[XmlElement] public Animation animation;
			[XmlElement] public TMX.Properties properties;

			public bool HasCollisionData() {
				return objectgroup is not null && objectgroup.objects is not null && objectgroup.objects.Length > 0;
			}
		}

		public class TileOffset {
			[XmlAttribute] public int x;
			[XmlAttribute] public int y;
		}

		/// <summary> Represents an image in a tileset.</summary>
		public class Image {
			/// <summary> The width of the image, in pixels.</summary>
			[XmlAttribute] public int width;

			/// <summary> The height of the image, in pixels.</summary>
			[XmlAttribute] public int height;

			/// <summary> The file path to the image.</summary>
			[XmlAttribute] public string source;
		}

		/// <summary> Represents an object group in a tileset.</summary>
		[XmlType(AnonymousType = true)]
		public class ObjectGroup {
			/// <summary> Array of objects in this object group.</summary>
			[XmlElement(ElementName = "object")] public TMX.TiledObject[] objects;

			/// <summary> Properties of this object group.</summary>
			[XmlElement] public TMX.Properties properties;
		}

		/// <summary> Class representing an animation in a tileset. Contains an array of frames.</summary>
		public class Animation {
			/// <summary> Array of frames in this animation.</summary>
			[XmlElement(ElementName = "frame")] public Frame[] frames;
		}

		///<summary> Class representing a frame of an animated tile.</summary>
		public class Frame {
			/// <summary> The local ID of a tile within the parent tileset.</summary>
			[XmlAttribute] public int tileid;

			/// <summary>
			/// How long (in milliseconds) this frame should be displayed before advancing to the next frame
			/// </summary>
			[XmlAttribute] public int duration;
		}
	}

	namespace TXTypes {
		/// <summary> Class representing a Tiled Object template, containing information about the template's tileset and object properties.</summary>
		[XmlRoot(ElementName = "template")]
		public class Template {
			/// <summary> The tileset associated with this template</summary>
			[XmlElement(ElementName = "tileset")] public TMX.TilesetReference tileset;

			/// <summary> The object associated with this template</summary>
			[XmlElement(ElementName = "object")] public TMX.TiledObject TempleateTiledObject;
		}
	}

	namespace TMX {
		/// <summary>
		/// The Base Layer Element class represents a Tiled layer element and contains information
		/// about the layer's name, size, position, visibility and properties. This class is inherited by other
		/// layer classes such as <see cref="GroupLayer"/> and <see cref="ObjectGroup"/>.
		/// </summary>
		public abstract class BaseLayerElement {
			/// <summary> The name of the layer.</summary>
			[XmlAttribute] public string name;

			/// <summary> The width of the layer in tiles. This attribute is only applicable for tile layers.</summary>
			[XmlAttribute] public int width;

			/// <summary> The height of the layer in tiles. This attribute is only applicable for tile layers.</summary>
			[XmlAttribute] public int height;

			/// <summary> Rendering offset for this layer in pixels. Defaults to 0 since Tiled 0.14</summary>
			[XmlAttribute] public float offsetx;

			/// <summary> Rendering offset for this layer in pixels. Defaults to 0 since Tiled 0.14</summary>
			[XmlAttribute] public float offsety;

			/// <summary> The optional 1-indexed position of the layer in the layer list.</summary>
			[XmlAttribute(AttributeName = "id")] public string idWrapper;

			/// <summary> id represents the optional 1-indexed position of the layer in layer list.</summary>
			public int? id {
				get => idWrapper == null ? null : int.Parse(idWrapper);
				set => idWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> Whether the layer is visible or not. The default value is true.</summary>
			[XmlAttribute] public bool visible = true;


			/// <summary> The opacity of the layer element, between 0 and 1.</summary>
			[XmlAttribute] public float opacity = 1.0f;

			/// <summary> The properties of the layer.</summary>
			[XmlElement] public Properties properties;
		}

		/// <summary> Represents a Tiled map file. Contains information about tilesets and layers in the map.</summary>
		[XmlRoot(ElementName = "map")]
		public class Map {
			/// <summary> The orientation of the map. Only "orthogonal" and "hexagonal" are supported.</summary>
			[XmlAttribute] public string orientation;

			/// <summary> The order in which tiles should be rendered.</summary>
			[XmlAttribute] public string renderorder;

			/// <summary> The width of each tile in pixels.</summary>
			[XmlAttribute] public int tilewidth;

			/// <summary> The height of each tile in pixels.</summary>
			[XmlAttribute] public int tileheight;

			/// <summary> The axis along which staggered isometric maps are staggered.</summary>
			[XmlAttribute] public string staggeraxis;

			/// <summary> The index of the staggered isometric map's stagger axis.</summary>
			[XmlAttribute] public string staggerindex;

			/// <summary> The background color of the map.</summary>
			[XmlAttribute] public string backgroundcolor;

			/// <summary> Whether the map is infinite in size.</summary>
			[XmlAttribute] public bool infinite = false;

			/// <summary> The tilesets used in the map.</summary>
			[XmlElement(ElementName = "tileset")] public TilesetReference[] tilesets;

			/// <summary> An array containing all of the top-level layers in the map.</summary>
			/// <remarks>
			/// This array contains all Tiled 'layer' types in one list, so we can get them in the correct order.
			/// </remarks>
			[XmlElement(ElementName = "layer", Type = typeof(Layer))]
			[XmlElement(ElementName = "imagelayer", Type = typeof(ImageLayer))]
			[XmlElement(ElementName = "objectgroup", Type = typeof(ObjectGroup))]
			[XmlElement(ElementName = "group", Type = typeof(GroupLayer))]
			public BaseLayerElement[] topLevelLayers;

			[XmlElement] public Properties properties;
		}

		/// <summary>
		/// This class represents a reference to a tileset in the Tiled map file. It includes information about the tileset's
		/// name, tile size, and the number of tiles it contains. It also includes a reference to the source file for the
		/// tileset and an array of tile elements.
		/// </summary>
		public class TilesetReference {
			/// <summary> The first tile ID of this tileset (this global ID maps to the first tile in this tileset).</summary>
			[XmlAttribute] public int firstgid;

			/// <summary> An external tileset file.</summary>
			[XmlAttribute] public string source;

			/// <summary> The name of this tileset.</summary>
			[XmlAttribute] public string name;

			/// <summary> The width of the tiles in this tileset, in pixels.</summary>
			[XmlAttribute] public int tilewidth;

			/// <summary> The height of the tiles in this tileset, in pixels</summary>
			[XmlAttribute] public int tileheight;

			/// <summary> The number of tiles in this tileset.</summary>
			[XmlAttribute] public int tilecount;

			/// <summary> The number of tile columns in the tileset.</summary>
			[XmlAttribute] public int columns;

			/// <summary> The grid data for the tileset.</summary>
			[XmlElement] public TSX.Grid grid;

			/// <summary> The tile definitions for the tileset.</summary>
			[XmlElement(ElementName = "tile")] public TSX.Tile[] tiles;

			/// <summary> The offset of each tile in the tileset.</summary>
			[XmlElement] public TSX.TileOffset tileoffset;

			/// <summary> The image used for the tileset.</summary>
			[XmlElement] public TSX.Image image;
		}

		/// <summary> Represents a tile layer in a TMX map. Contains data about the tiles in the layer.</summary>
		public sealed class Layer : BaseLayerElement {
			[XmlElement] public Data data;
		}

		/// <summary>
		/// Represents a group of child layers within a Tiled map.
		/// Can contain other groups or specific layer types such as tile layers, image layers, and object groups.
		/// </summary>
		public sealed class GroupLayer : BaseLayerElement {
			/// <summary> The child layers within the group.</summary>
			[XmlElement(ElementName = "layer", Type = typeof(Layer))]
			[XmlElement(ElementName = "imagelayer", Type = typeof(ImageLayer))]
			[XmlElement(ElementName = "objectgroup", Type = typeof(ObjectGroup))]
			[XmlElement(ElementName = "group", Type = typeof(GroupLayer))]
			public BaseLayerElement[] childLayers;
		}

		/// <summary> Represents a collection of properties in a Tiled map.</summary>
		public class Properties {
			/// <summary> The array of properties.</summary>
			[XmlElement] public Property[] property;

			/// <summary> Converts the properties to a dictionary, mapping property names to values.</summary>
			public IDictionary<string, string> ToDictionary() {
				var dictionary = new Dictionary<string, string>();
				for (var i = 0; i < property.Length; i++) {
					var p = property[i];
					dictionary.Add(p.name, p.value);
				}

				return dictionary;
			}

			/// <summary>
			/// Merges a template set of Properties with an instance set of Properties,
			/// returning a new Properties object that contains the combined set of properties.
			/// If either template or instance is null, the other set of Properties is returned.
			/// </summary>
			/// <param name="template">The template Properties object to use as a base.</param>
			/// <param name="instance">The instance Properties object to merge with the template.</param>
			/// <returns>A new Properties object that contains the combined set of properties.</returns>
			public static Properties MergeTemplateAndInstance(Properties template, Properties instance) {
				if (template is null && instance is null) return null;
				if (template is null) return instance;
				if (instance is null) return template;

				var properties = new Properties();
				var combinedProperties = new List<Property>();

				// Create a shallow clone of each property in the template set and add it to the combined set
				for (var i = 0; i < template.property.Length; i++) {
					var templateProperty = template.property[i];
					combinedProperties.Add(templateProperty.ShallowClone());
				}

				// For each property in the instance set, check if it already exists in the combined set
				// If it does, update the value of the existing property. Otherwise, add the new property to the combined set.
				for (var i = 0; i < instance.property.Length; i++) {
					var instanceProperty = instance.property[i];
					var foundExistingProperty = false;
					for (var j = 0; j < combinedProperties.Count; j++) {
						var existingProperty = combinedProperties[j];
						if (existingProperty.name == instanceProperty.name &&
						    existingProperty.type == instanceProperty.type) {
							foundExistingProperty = true;
							existingProperty.value = instanceProperty.value;
							break;
						}
					}

					if (!foundExistingProperty) combinedProperties.Add(instanceProperty);
				}

				// Convert the combined set of properties to an array and set it as the properties of the new Properties object
				properties.property = combinedProperties.ToArray();
				return properties;
			}
		}

		/// <summary> Represents a property with a name, type, and value.</summary>
		public class Property {
			/// <summary> The name of the property.</summary>
			[XmlAttribute] public string name;

			/// <summary> The type of the property.</summary>
			[XmlAttribute] public string type;

			/// <summary> The value of the property.</summary>
			[XmlAttribute] public string value;

			/// <summary> Creates a shallow clone of the property.</summary>
			/// <returns>A shallow clone of the property.</returns>
			public Property ShallowClone() {
				return (Property)MemberwiseClone();
			}
		}

		/// <summary>
		/// Represents the data element in a layer in a TMX map.
		/// The data element can contain either encoded tile data as text or individual `tile` elements.
		/// </summary>
		public class Data {
			/// <summary> The encoding of the data. Can be "base64" or "csv".</summary>
			[XmlAttribute] public string encoding;

			/// <summary> The compression of the data. If encoding is "base64", compression can be "gzip" or "zlib".</summary>
			[XmlAttribute] public string compression;

			/// <summary> The text data.</summary>
			[XmlText] public string text;

			/// <summary> The array of tiles.</summary>
			[XmlElement(ElementName = "tile")] public Tile[] tiles;

			[XmlElement(ElementName = "chunk")] public Chunk[] chunks;
		}

		/// <summary> Represents a chunk of a layer for infinite maps.</summary>
		public class Chunk {
			/// <summary> The x-coordinate of the chunk in tiles.</summary>
			[XmlAttribute] public int x;

			/// <summary> The y-coordinate of the chunk in tiles.</summary>
			[XmlAttribute] public int y;

			/// <summary> The width of the chunk in tiles.</summary>
			[XmlAttribute] public int width;

			/// <summary> The height of the chunk in tiles.</summary>
			[XmlAttribute] public int height;

			/// <summary> The encoding used for the layer data. Can be "base64" or "csv".</summary>
			[XmlAttribute] public string encoding;

			/// <summary> The compression used for the layer data, if encoding is "base64". Can be "gzip" or "zlib".</summary>
			[XmlAttribute] public string compression;

			/// <summary> The layer data as a string, if encoding is "csv".</summary>
			[XmlText] public string text;

			/// <summary> The tiles in the chunk.</summary>
			[XmlElement(ElementName = "tile")] public Tile[] tiles;
		}

		/// <summary> Represents a tile in a Tiled map.</summary>
		[XmlType(AnonymousType = true)]
		public class Tile {
			/// <summary> Gets or sets the global identifier of the tile.</summary>
			[XmlAttribute] public uint gid;
		}

		/// <summary> Represents a layer consisting of a single image. Inherits from the BaseLayerElement class.</summary>
		public sealed class ImageLayer : BaseLayerElement {
			/// <summary> Gets or sets the image associated with the layer.</summary>
			[XmlElement] public TSX.Image image;
		}

		/// <summary> Represents an object group in a Tiled map.</summary>
		public sealed class ObjectGroup : BaseLayerElement {
			/// <summary> Gets or sets the color of the group.</summary>
			[XmlAttribute] public string color;

			/// <summary> Gets or sets the draw order of the group.</summary>
			[XmlAttribute] public string draworder = "topdown";

			/// <summary> Gets or sets the tiled objects in the group.</summary>
			[XmlElement(ElementName = "object")] public TiledObject[] objects;
		}

		/// <summary> Objects can be mixed with object templates, nullable structure to correctly combine.</summary>
		public class TiledObject {
			/// <summary> The name of the object.</summary>
			[XmlAttribute] public string name;

			/// <summary> The ID of the object, as a string.</summary>
			[XmlAttribute(AttributeName = "id")] public string IDWrapper;

			/// <summary> The ID of the object, as a nullable integer.</summary>
			public int? id {
				get => IDWrapper is null ? null : int.Parse(IDWrapper);
				set => IDWrapper = value.HasValue ? value.ToString() : null;
			}

			// <summary> The x-coordinate of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "x")] public string XWrapper;

			/// <summary> The x-coordinate of the object, as a nullable float.</summary>
			public float? x {
				get => XWrapper is null ? null : float.Parse(XWrapper);
				set => XWrapper = value.HasValue ? value.ToString() : null;
			}

			// <summary> The y-coordinate of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "y")] public string YWrapper;

			/// <summary> The y-coordinate of the object, as a nullable float.</summary>
			public float? y {
				get => YWrapper is null ? null : float.Parse(YWrapper);
				set => YWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> The width of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "width")]
			public string WidthWrapper;

			/// <summary> The width of the object, as a nullable float.</summary>
			public float? width {
				get => WidthWrapper is null ? null : float.Parse(WidthWrapper);
				set => WidthWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> The height of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "height")]
			public string HeightWrapper;

			/// <summary> The height of the object, as a nullable float.</summary>
			public float? height {
				get => HeightWrapper is null ? null : float.Parse(HeightWrapper);
				set => HeightWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> The rotation of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "rotation")]
			public string RotationWrapper;

			/// <summary> The rotation of the object, as a nullable float.</summary>
			public float? rotation {
				get => RotationWrapper is null ? null : float.Parse(RotationWrapper);
				set => RotationWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> The global ID of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "gid")] public string GidWrapper;

			/// <summary> The global ID of the object, as a nullable uint.</summary>
			public uint? gid {
				get => GidWrapper is null ? null : uint.Parse(GidWrapper);
				set => GidWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> The visibility of the object, as a nullable string.</summary>
			[XmlAttribute(AttributeName = "visible")]
			public string VisibleWrapper;

			/// <summary> The visibility of the object, as a nullable bool.</summary>
			public bool? visible {
				get => VisibleWrapper is null ? null : bool.Parse(VisibleWrapper);
				set => VisibleWrapper = value.HasValue ? value.ToString() : null;
			}

			/// <summary> The name of the object template.</summary>
			[XmlAttribute] public string template;

			/// <summary> The properties of the object.</summary>
			[XmlElement] public Properties properties;

			/// <summary> An Ellipse in Tiled.</summary>
			[XmlElement] public Ellipse ellipse;

			/// <summary> A polygon in Tiled.</summary>
			[XmlElement] public Polygon polygon;

			/// <summary> A polyline in Tiled.</summary>
			[XmlElement] public Polyline polyline;

			/// <summary> A rectangle in Tiled.</summary>
			[XmlElement] public Rectangle rectangle;

			/// <summary> A text object in Tiled.</summary>
			[XmlElement] public Text text;

			/// <summary> Initializes any null values with default values.</summary>
			public void InitialiseUnsetValues() {
				id ??= 0;
				x ??= 0.0f;
				y ??= 0.0f;
				width ??= 0.0f;
				height ??= 0.0f;
				rotation ??= 0.0f;
				gid ??= 0;
				visible ??= true;
			}

			/// <summary> Returns a version of the object with its template applied.</summary>
			/// <param name="templateTiledObject">The object template to apply to this object.</param>
			/// <returns>A version of the object with its template applied.</returns>
			public TiledObject GetVersionWithTemplateApplied(TiledObject templateTiledObject) {
				var newObject = (TiledObject)templateTiledObject.MemberwiseClone();
				if (id.HasValue) newObject.id = id;
				if (name is not null) newObject.name = name;
				if (x.HasValue) newObject.x = x;
				if (y.HasValue) newObject.y = y;
				if (width.HasValue) newObject.width = width;
				if (height.HasValue) newObject.height = height;
				if (rotation.HasValue) newObject.rotation = rotation;
				if (gid.HasValue) newObject.gid = gid;
				if (visible.HasValue) newObject.visible = visible;
				if (templateTiledObject.template is null) newObject.template = template;
				if (properties is not null)
					newObject.properties =
						Properties.MergeTemplateAndInstance(templateTiledObject.properties, properties);
				if (ellipse is not null) newObject.ellipse = ellipse;
				if (rectangle is not null) newObject.rectangle = rectangle;
				if (polygon is not null) newObject.polygon = polygon;
				if (polyline is not null) newObject.polyline = polyline;

				if (text is not null) newObject.text = text;
				return newObject;
			}
		}

		/// <summary> Represents an ellipse shape in Tiled.</summary>
		public class Ellipse {
		}

		/// <summary> Represents a Polygon shape in Tiled.</summary>
		public class Polygon {
			/// <summary> A string representation of the polygon's points.</summary>
			[XmlAttribute] public string points;
		}

		/// <summary> Represents a polyline shape in Tiled.</summary>
		public class Polyline {
			/// <summary> A string representation of the polyline's points.</summary>
			[XmlAttribute] public string points;
		}

		/// <summary> Represents a rectangle shape in Tiled.</summary>
		public class Rectangle {
			/// <summary> The X position of the rectangle.</summary>
			[XmlAttribute] public float x;

			/// <summary> The Y position of the rectangle.</summary>
			[XmlAttribute] public float y;

			/// <summary> The width of the rectangle.</summary>
			[XmlAttribute] public float width;

			/// <summary> The height of the rectangle.</summary>
			[XmlAttribute] public float height;
		}

		/// <summary> Represents text in Tiled.</summary>
		public class Text {
			/// <summary> The size of the text in pixels.</summary>
			[XmlAttribute] public int pixelsize;

			/// <summary> The color of the text as a string.</summary>
			[XmlAttribute] public string color;

			/// <summary> The text itself.</summary>
			[XmlText] public string text;
		}
	}
}
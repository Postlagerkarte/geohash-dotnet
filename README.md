# geohash-dotnet <img src="https://github.com/postlagerkarte/geohash-dotnet/raw/master/icon.png" width="32" height="32" />
Easy-to-use and feature-rich geohash library. It has three main components:

1. Geohasher
2. PolygonHasher
3. GeohashCompressor

## Installation

To use the geohash library in your projects run the following command in the Package Manager Console:

 ```
  Install-Package geohash-dotnet
 ```
[![NuGet version (blazor-dragdrop)](https://img.shields.io/nuget/v/geohash-dotnet.svg?style=flat-square)](https://www.nuget.org/packages/geohash-dotnet) [![Nuget](https://img.shields.io/nuget/dt/geohash-dotnet)](https://www.nuget.org/packages/geohash-dotnet)

 
 
## Getting Started

To start using geohash-dotnet, you need to import the Geohash namespace and create a new instance of the Geohasher class.
 

  ```csharp
using Geohash;

// Create a new Geohasher instance
var geohasher = new Geohasher();

// Encode latitude and longitude into a geohash string
string geohash = geohasher.Encode(37.4219999, -122.0840575, 9);

// Decode the geohash back to latitude and longitude
(double latitude, double longitude) = geohasher.Decode(geohash);

// Get subhashes of a geohash
string[] subhashes = geohasher.GetSubhashes(geohash);

// Get neighbors of a geohash
Dictionary<Direction, string> neighbors = geohasher.GetNeighbors(geohash);

// Get parent of a geohash
string parent = geohasher.GetParent(geohash);

// Get the bounding box of a geohash
BoundingBox bbox = geohasher.GetBoundingBox(geohash);
 ```
 
## Geohasher
Geohashing is a spatial indexing technique that allows for the encoding and decoding of geographic coordinates, as well as performing various geospatial operations. The Geohasher class provided in this library offers an easy-to-use implementation of the geohashing algorithm, supporting a range of functionalities such as encoding, decoding, finding neighbors, generating subhashes, calculating parents, and retrieving bounding boxes of geohashes.

### General Information
Geohashes are base32-encoded strings that represent a specific rectangular region on Earth. The length of the geohash determines its precision. Shorter geohashes cover larger areas, while longer geohashes cover smaller areas. The Geohasher class supports geohashes with a precision between 1 and 12 characters.

The geohashing algorithm works by recursively dividing the latitude and longitude intervals into smaller intervals and selecting the corresponding base32 character for each subdivision. The even bits of the geohash represent longitude, while the odd bits represent latitude.

### Finding Neighbors

![](https://github.com/Postlagerkarte/geohash-dotnet/blob/51ef3c07c1a321ac3994a848c3315fb4a3a971f8/neighbors.gif)

You can find the neighbors of a geohash in all eight cardinal directions (north, northeast, east, southeast, south, southwest, west, and northwest) using the GetNeighbors() method:

  ```csharp
Dictionary<Direction, string> neighbors = geohasher.GetNeighbors(geohash);
 ```
The GetNeighbors() method returns a dictionary with keys representing the eight cardinal directions (North, Northeast, East, Southeast, South, Southwest, West, and Northwest). Each key corresponds to a Direction enum value, and each value in the dictionary is a geohash string representing the neighboring geohash in the respective direction.

You can access a specific neighbor using the appropriate Direction enum value:
  ```csharp
string northNeighbor = neighbors[Direction.North];
string southNeighbor = neighbors[Direction.South];
// ... and so on for the other directions
 ```
 
If you need to find the neighbor hash in a specific direction, use the GetNeighbor() method:

  ```csharp
string northNeighbor = geohasher.GetNeighbor(geohash, Direction.North);
 ```
 
### Subhashes
Subhashes represent smaller, equally-sized regions within a geohash's bounding box. To retrieve the 32 subhashes of a geohash, use the GetSubhashes() method:
  ```csharp
string[] subhashes = geohasher.GetSubhashes(geohash);
 ```
### Parent Geohash
The parent of a geohash is the geohash that covers the same area but at a lower precision level. You can get the parent geohash using the GetParent() method:
  ```csharp
string parent = geohasher.GetParent(geohash);
 ```
Keep in mind that the parent geohash will be less precise and cover a larger area.

### Bounding Box
To obtain the bounding box (minimum and maximum latitude and longitude) of a geohash, use the GetBoundingBox() method:
  ```csharp
BoundingBox bbox = geohasher.GetBoundingBox(geohash);
 ```
 The GetBoundingBox() method returns a BoundingBox object that represents the geographical bounding box of a given geohash. A bounding box is defined by two pairs of latitude and longitude coordinates, representing the southwest (bottom-left) and northeast (top-right) corners of the rectangular area.

You can use the bounding box to display the geohash on a Leaflet map by creating a rectangle with the coordinates obtained from the bounding box:
  ```javascript
    // Assuming you have a BoundingBox object `bbox` from geohasher.GetBoundingBox(geohash)
    var southWest = L.latLng(bbox.MinLatitude, bbox.MinLongitude);
    var northEast = L.latLng(bbox.MaxLatitude, bbox.MaxLongitude);
    var bounds = L.latLngBounds(southWest, northEast);

    // Create a rectangle using the bounds and add it to the map
    var rectangle = L.rectangle(bounds, {color: "#ff7800", weight: 1}).addTo(map);
 ```
## PolygonHasher 
The PolygonHasher class is an extension of the Geohasher class that provides the functionality to generate geohashes within a polygon based on the specified precision and inclusion criteria. This class is useful in applications that require spatial indexing of data within a specific polygonal area.

<img src="https://github.com/Postlagerkarte/geohash-dotnet/blob/1d7df8041e61b3a26f496b5d4b526574525e5d51/polygon.gif" height="351" width="auto">


### General Information
The algorithm used by PolygonHasher has a time complexity of O(N^2), where N is the number of geohashes generated within the bounding box. The performance of this algorithm can vary greatly depending on the size and complexity of the polygon, as well as the desired precision of the geohashes. In general, larger polygons and higher precision levels will result in longer processing times.

### Usage

  ```csharp
// Create a new instance of the PolygonHasher class
PolygonHasher polygonHasher = new PolygonHasher();

// Create a polygon
Polygon polygon = new Polygon(new LinearRing(new Coordinate[] {
    new Coordinate(-122.4183, 37.7755),
    new Coordinate(-122.4183, 37.7814),
    new Coordinate(-122.4085, 37.7814),
    new Coordinate(-122.4085, 37.7755),
    new Coordinate(-122.4183, 37.7755) // Close the ring
}));

// Get geohashes with precision 6 that intersect the polygon
HashSet<string> geohashes = polygonHasher.GetHashes(polygon, 6, PolygonHasher.GeohashInclusionCriteria.Intersects);

// Print the geohashes
foreach (string geohash in geohashes)
{
    Console.WriteLine(geohash);
}

 ```

## GeohashCompressor  
The GeohashCompressor class is designed to compress an array of geohashes by finding the smallest possible set of geohashes that still cover the same area.

### General Information
The GeohashCompressor algorithm has a time complexity of O(N^2), where N is the number of geohashes in the input array. The algorithm iteratively checks each input geohash and its parent geohash to determine if all subhashes are present in the input set. If they are, it replaces the input geohashes with the parent geohash, effectively compressing the geohashes. This process is repeated until no further compression can be achieved, resulting in a smaller set of geohashes that still covers the same area as the input geohashes.The performance of this algorithm can vary greatly depending on the size and complexity of the area covered by the input geohashes, as well as the desired precision of the compressed geohashes (determined by minlevel and maxlevel). In general, larger areas and higher precision levels will result in longer processing times.

### Usage

  ```csharp
GeohashCompressor compressor = new GeohashCompressor();
 var input = new[] {
         "tdnu20", "tdnu21", "tdnu22", "tdnu23", "tdnu24", "tdnu25", "tdnu26", "tdnu27", "tdnu28", "tdnu29",
         "tdnu2b", "tdnu2c", "tdnu2d", "tdnu2e", "tdnu2f", "tdnu2g", "tdnu2h", "tdnu2j", "tdnu2k", "tdnu2m",
         "tdnu2n", "tdnu2p", "tdnu2q", "tdnu2r", "tdnu2s", "tdnu2t", "tdnu2u", "tdnu2v", "tdnu2w", "tdnu2x",
         "tdnu2y", "tdnu2z"
     };

 var result = compressor.Compress(input); // Will return "tdnu2"

 ```



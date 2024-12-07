# geohash-dotnet <img src="https://github.com/postlagerkarte/geohash-dotnet/raw/master/icon.png" width="32" height="32" />
An easy-to-use, feature-rich geohash library for .NET

## Installation

Install via NuGet Package Manager:

 ```
  Install-Package geohash-dotnet
 ```
[![NuGet version (blazor-dragdrop)](https://img.shields.io/nuget/v/geohash-dotnet.svg?style=flat-square)](https://www.nuget.org/packages/geohash-dotnet) [![Nuget](https://img.shields.io/nuget/dt/geohash-dotnet)](https://www.nuget.org/packages/geohash-dotnet)

## Features
### Geohasher
- Encode/Decode: Convert between geographic coordinates and geohash strings.
- Neighbors: Find adjacent geohashes in all cardinal directions.
- Subhashes: Retrieve finer-grained geohashes within a parent geohash.
- Parent Geohash: Get the less precise parent of a given geohash.
- Bounding Box: Obtain the geographic bounding box of a geohash.

### PolygonHasher
- Generate geohashes within a specified polygon based on precision and inclusion criteria.
- Ideal for spatial indexing and geospatial queries within complex areas.

### GeohashCompressor
- Compresses a set of geohashes to a minimal set that still covers the same area.
- Optimizes storage and improves performance for spatial queries.


## Geohasher
Generate geohashes that cover a specific polygon:
  ```csharp
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

## PolygonHasher
  ```csharp

// Initialize PolygonHasher
var polygonHasher = new PolygonHasher();

// Define a polygon
Polygon polygon = new Polygon(new LinearRing(new Coordinate[] {
    new Coordinate(-122.4183, 37.7755),
    new Coordinate(-122.4183, 37.7814),
    new Coordinate(-122.4085, 37.7814),
    new Coordinate(-122.4085, 37.7755),
    new Coordinate(-122.4183, 37.7755) // Closing the ring
}));

// Get geohashes of precision 6 that intersect with the polygon
var geohashes = polygonHasher.GetHashes(polygon, 6, PolygonHasher.GeohashInclusionCriteria.Intersects);

// Output the geohashes
foreach (string hash in geohashes)
{
    Console.WriteLine(hash);
}
```

## GeohashCompressor
Compress a list of geohashes into a minimal covering set:
  ```csharp
// Initialize GeohashCompressor
var compressor = new GeohashCompressor();

// Define input geohashes
var inputGeohashes = new[] {
    "tdnu20", "tdnu21", "tdnu22", "tdnu23", "tdnu24", "tdnu25",
    "tdnu26", "tdnu27", "tdnu28", "tdnu29", "tdnu2b", "tdnu2c",
    // ... additional geohashes
};

// Compress the geohashes
var compressedGeohashes = compressor.Compress(inputGeohashes);

// Output the compressed geohashes
foreach (var hash in compressedGeohashes)
{
    Console.WriteLine(hash);
}
  ```

### Finding Neighbors

![](https://github.com/Postlagerkarte/geohash-dotnet/blob/51ef3c07c1a321ac3994a848c3315fb4a3a971f8/neighbors.gif)


  ```csharp
Dictionary<Direction, string> neighbors = geohasher.GetNeighbors(geohash);
 ```

  ```csharp
string northNeighbor = neighbors[Direction.North];
string southNeighbor = neighbors[Direction.South];
// ... and so on for the other directions
 ```
 
find the neighbor hash in a specific direction, use the GetNeighbor() method:

  ```csharp
string northNeighbor = geohasher.GetNeighbor(geohash, Direction.North);
 ```
 
### Subhashes
Subhashes represent smaller, equally-sized regions within a geohash's bounding box:
  ```csharp
string[] subhashes = geohasher.GetSubhashes(geohash);
 ```
### Parent Geohash
The parent of a geohash is the geohash that covers the same area but at a lower precision level:
  ```csharp
string parent = geohasher.GetParent(geohash);
 ```
Keep in mind that the parent geohash will be less precise and cover a larger area.

### Bounding Box
To obtain the bounding box (minimum and maximum latitude and longitude):
  ```csharp
BoundingBox bbox = geohasher.GetBoundingBox(geohash);
 ```


### Performance Notes
PolygonHasher and GeohashCompressor have a time complexity of O(N²) relative to the number of geohashes processed.



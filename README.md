# geohash-dotnet <img src="https://github.com/postlagerkarte/geohash-dotnet/raw/master/icon.png" width="32" height="32" />
lightweight geohash library written in C# 


[![Build status](https://ci.appveyor.com/api/projects/status/pidjjvq7oeb2ai34?svg=true)](https://ci.appveyor.com/project/Postlagerkarte/geohash-dotnet)
![alt text][logo]

[logo]: https://img.shields.io/nuget/v/geohash-dotnet.svg

### Installation

To use the geohash library in your projects run the following command in the Package Manager Console:

 ```
  Install-Package geohash-dotnet
 ```
 
 ### Usage Examples
  
 #### Encode a latitude and longitude to a geohash:
 
 ```csharp
    var hasher = new Geohasher();
    var hash6 = hasher.Encode(52.5174, 13.409);  // default precision 6 
    var hash12 = hasher.Encode(52.5174, 13.409, 12);  // precision 12
 ```
 
  #### Decode a geohash to latitude and longitude:
 
 ```csharp
     var hasher = new Geohasher();
     var decoded = hasher.Decode("u33dc0");
     var latitude = decoded.Item1;
     var longitude = decoded.Item2;
 ```
 

#### Get neighbors for a hash

<img src="https://github.com/Postlagerkarte/geohash-dotnet/blob/master/neighbors.png" width="64" height="64" />

 ```csharp
     var hasher = new Geohasher();
     var neighbors         = hasher.GetNeighbors("m");
     var northNeighbor     = neighbors[Direction.North];
     var northEastNeighbor = neighbors[Direction.NorthEast]);
     var eastNeighbor      = neighbors[Direction.East]);
     var southEastNeighbor = neighbors[Direction.SouthEast]);
     var southNeighbor     = neighbors[Direction.South]);
     var southWestNeighbor = neighbors[Direction.SouthWest]);
     var westNeighbor      = neighbors[Direction.West]);
     var northWestNeighbor = neighbors[Direction.NorthWest]);
 ```



# geohash-dotnet <img src="https://github.com/postlagerkarte/geohash-dotnet/raw/master/icon.png" width="32" height="32" />
Easy-to-use and feature-rich geohash library 


[![Build Status](https://dev.azure.com/postlagerkarte/geohash-dotnet/_apis/build/status/Postlagerkarte.geohash-dotnet?branchName=master)](https://dev.azure.com/postlagerkarte/geohash-dotnet/_build/latest?definitionId=4&branchName=master)

If you want to see the library in action visit https://geohash.azurewebsites.net/


### Installation

To use the geohash library in your projects run the following command in the Package Manager Console:

 ```
  Install-Package geohash-dotnet
 ```
 
 ### Usage Examples
 
 #### Create a Geohasher object
 ```csharp
 var hasher = new Geohasher();
 ```
  
 #### Encode a latitude and longitude to a geohash
 
 ```csharp
    var hash  = hasher.Encode(52.5174, 13.409);  // default precision 6 
    var hash2 = hasher.Encode(52.5174, 13.409, 12);  // precision 12
 ```
 
  #### Decode a geohash to latitude and longitude
 
 ```csharp
     var decoded = hasher.Decode("u33dc0");
     var latitude = decoded.Item1;
     var longitude = decoded.Item2;
 ```
 

#### Get neighbors for a hash

 ```csharp
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
 
 #### Get specific neighbor for a hash
 
  ```csharp
 var southEastNeighbor = hasher.GetNeighbor("u33dc0", Direction.SouthEast));
 ```
 
 #### Get all 32 subhashes for a hash
 
 ```csharp
 var subhashes = hasher.GetSubhashes("u33dc0");
 ```
 
  #### Get parent of a hash
 
 ```csharp
 var parentHash = hasher.GetParent("u33dbc")
 ```
 
 



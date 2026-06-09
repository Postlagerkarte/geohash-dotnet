using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace Geohash.Tests
{
    public class RadiusHasherTests
    {
        private const double Rad = Math.PI / 180.0;

        private readonly RadiusHasher _sut = new RadiusHasher();
        private readonly Geohasher _geohasher = new Geohasher();

        // =====================================================================
        // Input validation
        // =====================================================================

        [Theory]
        [InlineData(double.NaN, 0, 1000)]
        [InlineData(0, double.NaN, 1000)]
        [InlineData(0, 0, double.NaN)]
        public void GetHashes_NaNInput_Throws(double lat, double lng, double radius)
        {
            Assert.Throws<ArgumentException>(() => _sut.GetHashes(lat, lng, radius, 6));
        }

        [Theory]
        [InlineData(90.001)]
        [InlineData(-90.001)]
        public void GetHashes_LatitudeOutOfRange_Throws(double lat)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetHashes(lat, 0, 1000, 6));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(double.PositiveInfinity)]
        public void GetHashes_InvalidRadius_Throws(double radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetHashes(0, 0, radius, 6));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        public void GetHashes_PrecisionOutOfRange_Throws(int precision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetHashes(0, 0, 1000, precision));
        }

        [Fact]
        public void GetHashes_ExceedsMaxCandidateCells_ThrowsWithActionableMessage()
        {
            // 500 km radius at precision 12 would enumerate billions of cells.
            var ex = Assert.Throws<ArgumentException>(
                () => _sut.GetHashes(0, 0, 500_000, 12));

            Assert.Contains("precision", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetHashes_PreCancelledToken_ThrowsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(
                () => _sut.GetHashes(52.5, 13.4, 5_000, 7, cancellationToken: cts.Token));
        }

        // =====================================================================
        // Basic semantics
        // =====================================================================

        [Fact]
        public void GetHashes_ZeroRadius_Intersects_ReturnsExactlyContainingCell()
        {
            var result = _sut.GetHashes(52.5163, 13.3777, 0, 7,
                GeohashInclusionCriteria.Intersects);

            string expected = _geohasher.Encode(52.5163, 13.3777, 7);
            Assert.Single(result);
            Assert.Contains(expected, result);
        }

        [Fact]
        public void GetHashes_ZeroRadius_Contains_ReturnsEmpty()
        {
            var result = _sut.GetHashes(52.5163, 13.3777, 0, 7,
                GeohashInclusionCriteria.Contains);

            Assert.Empty(result);
        }

        [Fact]
        public void GetHashes_CenterCell_AlwaysIncludedForIntersects()
        {
            var result = _sut.GetHashes(48.8566, 2.3522, 250, 7);
            Assert.Contains(_geohasher.Encode(48.8566, 2.3522, 7), result);
        }

        [Fact]
        public void GetHashes_AllResults_AreValidWithRequestedPrecision()
        {
            var result = _sut.GetHashes(35.6895, 139.6917, 3_000, 6);

            Assert.NotEmpty(result);
            Assert.All(result, h =>
            {
                Assert.Equal(6, h.Length);
                Assert.True(_geohasher.IsValid(h));
            });
        }

        // =====================================================================
        // Geometric correctness (verified against independent haversine math)
        // =====================================================================

        [Theory]
        [InlineData(52.5163, 13.3777, 5_000, 6)]   // Berlin
        [InlineData(-33.8688, 151.2093, 2_000, 7)]  // Sydney (southern hemisphere)
        [InlineData(0.0, 0.0, 10_000, 5)]           // Equator/meridian origin
        [InlineData(64.13, -21.90, 8_000, 6)]       // Reykjavik (high latitude)
        public void Intersects_EveryReturnedCell_ActuallyTouchesCircle(
            double lat, double lng, double radius, int precision)
        {
            var result = _sut.GetHashes(lat, lng, radius, precision,
                GeohashInclusionCriteria.Intersects);

            Assert.NotEmpty(result);
            Assert.All(result, h =>
            {
                double nearest = NearestDistanceToCell(lat, lng, _geohasher.GetBoundingBox(h));
                Assert.True(nearest <= radius * (1 + 1e-9) + 1e-6,
                    $"Cell {h} nearest point is {nearest:F2} m away, radius {radius} m.");
            });
        }

        [Theory]
        [InlineData(52.5163, 13.3777, 5_000, 6)]
        [InlineData(-33.8688, 151.2093, 2_000, 7)]
        public void Contains_EveryReturnedCell_LiesEntirelyInsideCircle(
            double lat, double lng, double radius, int precision)
        {
            var result = _sut.GetHashes(lat, lng, radius, precision,
                GeohashInclusionCriteria.Contains);

            Assert.NotEmpty(result);
            Assert.All(result, h =>
            {
                var box = _geohasher.GetBoundingBox(h);
                double farthest = Max(
                    RadiusHasher.GetDistanceMeters(lat, lng, box.MinLat, box.MinLng),
                    RadiusHasher.GetDistanceMeters(lat, lng, box.MinLat, box.MaxLng),
                    RadiusHasher.GetDistanceMeters(lat, lng, box.MaxLat, box.MinLng),
                    RadiusHasher.GetDistanceMeters(lat, lng, box.MaxLat, box.MaxLng));

                Assert.True(farthest <= radius * (1 + 1e-9) + 1e-6,
                    $"Cell {h} farthest corner is {farthest:F2} m away, radius {radius} m.");
            });
        }

        [Fact]
        public void Contains_IsAlwaysSubsetOf_Intersects()
        {
            var rng = new Random(42);

            for (int i = 0; i < 25; i++)
            {
                double lat = rng.NextDouble() * 160 - 80;       // avoid extreme poles here
                double lng = rng.NextDouble() * 360 - 180;
                double radius = rng.NextDouble() * 20_000 + 100;
                int precision = rng.Next(4, 8);

                var contains = _sut.GetHashes(lat, lng, radius, precision,
                    GeohashInclusionCriteria.Contains);
                var intersects = _sut.GetHashes(lat, lng, radius, precision,
                    GeohashInclusionCriteria.Intersects);

                Assert.True(contains.IsSubsetOf(intersects),
                    $"Contains ⊄ Intersects for ({lat:F4}, {lng:F4}), r={radius:F0}, p={precision}");
            }
        }

        [Fact]
        public void Intersects_IsComplete_EveryInteriorPointIsCovered()
        {
            const double lat = 40.7128, lng = -74.0060, radius = 4_000;
            const int precision = 7;

            var result = _sut.GetHashes(lat, lng, radius, precision);
            var rng = new Random(1337);

            // Random points strictly inside the circle must land in a returned cell.
            for (int i = 0; i < 500; i++)
            {
                double bearing = rng.NextDouble() * 360;
                double distance = radius * Math.Sqrt(rng.NextDouble()) * 0.999;
                var (pLat, pLng) = Destination(lat, lng, bearing, distance);

                string hash = _geohasher.Encode(pLat, pLng, precision);
                Assert.True(result.Contains(hash),
                    $"Point ({pLat:F6}, {pLng:F6}) at {distance:F1} m is not covered by {hash}.");
            }
        }

        // =====================================================================
        // Antimeridian & poles
        // =====================================================================

        [Fact]
        public void GetHashes_CircleOnDateLine_CoversBothSides()
        {
            var result = _sut.GetHashes(0, 179.99, 50_000, 5);

            Assert.NotEmpty(result);
            var centers = result.Select(h => _geohasher.Decode(h)).ToList();

            Assert.Contains(centers, c => c.longitude > 0);   // western Pacific side
            Assert.Contains(centers, c => c.longitude < 0);   // eastern Pacific side
            Assert.All(centers, c =>
                Assert.True(c.longitude >= -180 && c.longitude <= 180));
        }

        [Fact]
        public void GetHashes_CircleOnDateLine_IsCompleteAcrossTheSeam()
        {
            const double lat = 10, lng = -179.95, radius = 30_000;
            const int precision = 6;

            var result = _sut.GetHashes(lat, lng, radius, precision);

            // A point just across the seam (positive longitude) must be covered.
            var (pLat, pLng) = Destination(lat, lng, bearingDeg: 270, distanceMeters: radius * 0.5);
            Assert.True(pLng > 0, "Sanity: test point should have wrapped to positive longitude.");
            Assert.Contains(_geohasher.Encode(pLat, pLng, precision), result);
        }

        [Fact]
        public void GetHashes_CircleCoveringNorthPole_SpansAllLongitudes()
        {
            // 100 km around (89.5, 0) reaches past the pole.
            var result = _sut.GetHashes(89.5, 0, 100_000, 3);

            Assert.Contains(_geohasher.Encode(89.9, 90, 3), result);
            Assert.Contains(_geohasher.Encode(89.9, -90, 3), result);
            Assert.Contains(_geohasher.Encode(89.9, 179, 3), result);
        }

        [Fact]
        public void GetHashes_CircleCoveringSouthPole_DoesNotThrow_AndIsNonEmpty()
        {
            var result = _sut.GetHashes(-89.8, 45, 50_000, 3);
            Assert.NotEmpty(result);
        }

        // Add to the InlineData of Intersects_EveryReturnedCell_ActuallyTouchesCircle:
        [InlineData(89.5, 0.0, 100_000, 3)]         // pole-covering circle (regression)

        [Fact]
        public void Intersects_NearPoleCell_AcrossThePole_IsIncluded_Regression()
        {
            // Cell "zzz" touches the North Pole at ~179°E. From (89.5, 0) the shortest
            // path to it crosses the pole (~55.6 km), not the 179°-long parallel (~111 km).
            // Naive lat/lng clamping rejected this cell. See: nearest point on a
            // meridian edge satisfies tan φ* = tan φ1 / cos Δλ.
            var result = _sut.GetHashes(89.5, 0, 100_000, 3);

            Assert.Contains("zzz", result);
            Assert.Contains(_geohasher.Encode(89.9, 179, 3), result); // same cell, via Encode
        }

        // =====================================================================
        // Auto precision overload
        // =====================================================================

        [Fact]
        public void GetHashes_AutoPrecision_UsesGetPrecisionForRadius()
        {
            const double lat = 52.5163, lng = 13.3777, radius = 5_000;

            var result = _sut.GetHashes(lat, lng, radius);
            int expectedPrecision = RadiusHasher.GetPrecisionForRadius(radius, lat);

            Assert.NotEmpty(result);
            Assert.All(result, h => Assert.Equal(expectedPrecision, h.Length));
        }

        [Fact]
        public void GetHashes_AutoPrecision_ProducesReasonableCellCount()
        {
            var result = _sut.GetHashes(52.5163, 13.3777, 5_000);

            // Cell size <= radius/2 guarantees a sensible covering: not 1 giant
            // cell, not millions of tiny ones.
            Assert.InRange(result.Count, 9, 10_000);
        }

        // =====================================================================
        // GetPrecisionForRadius / GetCellSizeMeters
        // =====================================================================

        [Fact]
        public void GetPrecisionForRadius_KnownValue_5km_AtEquator()
        {
            // p6 cells are ~611 x 1222 m; max dimension 1222 <= 2500 = 5000/2. p5 is too big.
            Assert.Equal(6, RadiusHasher.GetPrecisionForRadius(5_000));
        }

        [Fact]
        public void GetPrecisionForRadius_IsMonotonic_SmallerRadiusNeverLowersPrecision()
        {
            int previous = 1;
            foreach (double radius in new[] { 5_000_000.0, 500_000, 50_000, 5_000, 500, 50, 5, 0.5 })
            {
                int p = RadiusHasher.GetPrecisionForRadius(radius);
                Assert.True(p >= previous, $"Precision dropped from {previous} to {p} at r={radius}.");
                previous = p;
            }
        }

        [Fact]
        public void GetPrecisionForRadius_TinyRadius_ClampsToMaxPrecision()
        {
            Assert.Equal(Geohasher.MaxPrecision, RadiusHasher.GetPrecisionForRadius(0.001));
        }

        [Fact]
        public void GetCellSizeMeters_MatchesDocumentedPrecisionTable()
        {
            // Precision 1: ~5000 x 5000 km; Precision 2: ~1250 x 625 km (at the equator).
            var (w1, h1) = RadiusHasher.GetCellSizeMeters(1);
            Assert.Equal(5_000_000, w1, tolerance: 50_000);
            Assert.Equal(5_000_000, h1, tolerance: 50_000);

            var (w2, h2) = RadiusHasher.GetCellSizeMeters(2);
            Assert.Equal(1_250_000, w2, tolerance: 12_500);
            Assert.Equal(625_000, h2, tolerance: 6_500);
        }

        [Fact]
        public void GetCellSizeMeters_WidthShrinksWithCosineOfLatitude()
        {
            var (wEquator, hEquator) = RadiusHasher.GetCellSizeMeters(6, latitude: 0);
            var (w60, h60) = RadiusHasher.GetCellSizeMeters(6, latitude: 60);

            Assert.Equal(wEquator * 0.5, w60, tolerance: 1e-6);  // cos(60°) = 0.5
            Assert.Equal(hEquator, h60, tolerance: 1e-6);        // height is latitude-invariant
        }

        // =====================================================================
        // GetDistanceMeters
        // =====================================================================

        [Fact]
        public void GetDistanceMeters_SamePoint_IsZero()
        {
            Assert.Equal(0, RadiusHasher.GetDistanceMeters(52.5, 13.4, 52.5, 13.4), tolerance: 1e-9);
        }

        [Fact]
        public void GetDistanceMeters_OneDegreeAtEquator_IsAbout111km()
        {
            double d = RadiusHasher.GetDistanceMeters(0, 0, 0, 1);
            Assert.Equal(111_195, d, tolerance: 120); // within ~0.1%
        }

        [Fact]
        public void GetDistanceMeters_ParisToLondon_IsAbout343km()
        {
            double d = RadiusHasher.GetDistanceMeters(48.8566, 2.3522, 51.5074, -0.1278);
            Assert.InRange(d, 340_000, 348_000);
        }

        [Fact]
        public void GetDistanceMeters_AcrossDateLine_TakesShortPath()
        {
            // (0, 179.5) and (0, -179.5) are 1° apart, not 359°.
            double d = RadiusHasher.GetDistanceMeters(0, 179.5, 0, -179.5);
            Assert.Equal(111_195, d, tolerance: 120);
        }

        [Fact]
        public void GetDistanceMeters_IsSymmetric()
        {
            double ab = RadiusHasher.GetDistanceMeters(10, 20, -30, 140);
            double ba = RadiusHasher.GetDistanceMeters(-30, 140, 10, 20);
            Assert.Equal(ab, ba, tolerance: 1e-6);
        }

        [Fact]
        public void GetDistanceMeters_Geohashes_SameHashIsZero_NeighborsAreCellSized()
        {
            Assert.Equal(0, RadiusHasher.GetDistanceMeters("u33db2m", "u33db2m"), tolerance: 1e-9);

            string hash = _geohasher.Encode(52.5, 13.4, 7);
            string east = _geohasher.GetNeighbor(hash, Direction.East);

            var (cellWidth, _) = RadiusHasher.GetCellSizeMeters(7, latitude: 52.5);
            double d = RadiusHasher.GetDistanceMeters(hash, east);

            Assert.Equal(cellWidth, d, tolerance: cellWidth * 0.05);
        }

        // =====================================================================
        // Test helpers (independent of production hot-loop math)
        // =====================================================================

        /// <summary>Haversine distance from a point to the nearest point of a cell.</summary>
        /// <summary>
        /// True nearest haversine distance from a point to a cell, found numerically
        /// (golden-section search along each edge — distance is unimodal per edge).
        /// Deliberately independent of the production analytic formula.
        /// </summary>
        private static double NearestDistanceToCell(double lat, double lng, BoundingBox box)
        {
            double qLng = lng;
            if (qLng < box.MinLng - 180) qLng += 360;
            else if (qLng > box.MaxLng + 180) qLng -= 360;

            // Query inside the cell?
            if (lat >= box.MinLat && lat <= box.MaxLat && qLng >= box.MinLng && qLng <= box.MaxLng)
                return 0;

            double best = double.MaxValue;
            best = Math.Min(best, GoldenMin(x => RadiusHasher.GetDistanceMeters(lat, qLng, box.MinLat, x), box.MinLng, box.MaxLng));
            best = Math.Min(best, GoldenMin(x => RadiusHasher.GetDistanceMeters(lat, qLng, box.MaxLat, x), box.MinLng, box.MaxLng));
            best = Math.Min(best, GoldenMin(y => RadiusHasher.GetDistanceMeters(lat, qLng, y, box.MinLng), box.MinLat, box.MaxLat));
            best = Math.Min(best, GoldenMin(y => RadiusHasher.GetDistanceMeters(lat, qLng, y, box.MaxLng), box.MinLat, box.MaxLat));
            return best;
        }

        private static double GoldenMin(Func<double, double> f, double a, double b)
        {
            const double inv = 0.6180339887498949; // 1/φ
            double x1 = b - inv * (b - a), x2 = a + inv * (b - a);
            double f1 = f(x1), f2 = f(x2);

            for (int i = 0; i < 100; i++)
            {
                if (f1 < f2) { b = x2; x2 = x1; f2 = f1; x1 = b - inv * (b - a); f1 = f(x1); }
                else { a = x1; x1 = x2; f1 = f2; x2 = a + inv * (b - a); f2 = f(x2); }
            }
            return Math.Min(f1, f2);
        }



        /// <summary>Spherical destination point: start + bearing + distance.</summary>
        private static (double lat, double lng) Destination(
            double lat, double lng, double bearingDeg, double distanceMeters)
        {
            double delta = distanceMeters / RadiusHasher.EarthRadiusMeters;
            double theta = bearingDeg * Rad;
            double phi1 = lat * Rad;
            double lambda1 = lng * Rad;

            double phi2 = Math.Asin(
                Math.Sin(phi1) * Math.Cos(delta) +
                Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));

            double lambda2 = lambda1 + Math.Atan2(
                Math.Sin(theta) * Math.Sin(delta) * Math.Cos(phi1),
                Math.Cos(delta) - Math.Sin(phi1) * Math.Sin(phi2));

            double outLng = ((lambda2 / Rad + 540.0) % 360.0) - 180.0;
            return (phi2 / Rad, outLng);
        }

        private static double Max(double a, double b, double c, double d) =>
            Math.Max(Math.Max(a, b), Math.Max(c, d));
    }
}
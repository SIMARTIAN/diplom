using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace HistorikasMap
{
    public partial class MainWindow : Window
    {
        DatabaseService _db = new DatabaseService();

        public MainWindow()
        {
            InitializeComponent();

            MainMap.MapProvider = GMapProviders.GoogleMap;
            MainMap.Position = new PointLatLng(49.4444, 32.0598); // Координати Черкас
            MainMap.MinZoom = 1;
            MainMap.MaxZoom = 18;
            MainMap.Zoom = 12;
            MainMap.ShowCenter = false;
            MainMap.CanDragMap = true;

            if (!_db.TestConnection())
            {
                MessageBox.Show("Не вдалося підключитися до бази даних!");
                return;
            }

            int streetCount = _db.GetStreetsCount();
            MessageBox.Show($"У базі знайдено вулиць: {streetCount}");

            if (streetCount > 0)
            {
                var streets = _db.GetStreets();
                foreach (var street in streets)
                {
                    AddStreetToMap(MainMap, street);
                }
            }
        }

        public void AddStreetToMap(GMapControl map, Street street)
        {
            if (street.Geometry is MultiLineString mls)
            {
                foreach (var line in mls.Geometries)
                {
                    var routePoints = new List<PointLatLng>();
                    foreach (var coord in line.Coordinates)
                    {
                        routePoints.Add(new PointLatLng(coord.Y, coord.X)); // Latitude, Longitude
                    }

                    var route = new GMapRoute(routePoints)
                    {
                        Shape = new System.Windows.Shapes.Path
                        {
                            Stroke = Brushes.Blue,
                            StrokeThickness = 2
                        }
                    };

                    map.RegenerateShape(route);
                    map.Markers.Add(route);
                }
            }
            else if (street.Geometry is LineString ls)
            {
                var routePoints = new List<PointLatLng>();
                foreach (var coord in ls.Coordinates)
                {
                    routePoints.Add(new PointLatLng(coord.Y, coord.X));
                }

                var route = new GMapRoute(routePoints)
                {
                    Shape = new System.Windows.Shapes.Path
                    {
                        Stroke = Brushes.Blue,
                        StrokeThickness = 2
                    }
                };

                map.RegenerateShape(route);
                map.Markers.Add(route);
            }
        }
    }

    public class Street
    {
        public string Name { get; set; }
        public NtsGeometry Geometry { get; set; }
    }

    public class DatabaseService
    {
        private string _connectionString = "Host=127.0.0.1;Username=postgres;Password=s1a2s3h4a5;Database=HistoricalMap";

        public bool TestConnection()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public int GetStreetsCount()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM streets", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<Street> GetStreets()
        {
            var streets = new List<Street>();
            var wkbReader = new WKBReader();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("SELECT name, ST_AsBinary(geom) FROM streets", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string name = reader.GetString(0);
                byte[] wkb = (byte[])reader[1];
                var geom = wkbReader.Read(wkb);

                streets.Add(new Street { Name = name, Geometry = geom });
            }

            return streets;
        }
        public string? GetStreetNameByOgcFid(int ogcFid)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("SELECT name FROM public.streets WHERE ogc_fid = @ogcFid", conn);
            cmd.Parameters.AddWithValue("ogcFid", ogcFid);

            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                return (string)result;

            return null;
        }

    }
}

using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace LoUAM
{
    internal class MapImage : System.Windows.Controls.Image
    {
        public string TileImagePath;
        public string TilePrefabPath;

        public const int DEFAULT_TILE_RESOLUTION = 512;
        public int Resolution = DEFAULT_TILE_RESOLUTION;

        static Dictionary<string, Point3D[]> TilesCoords = new Dictionary<string, Point3D[]>()
        {
            { "Grid_x0_z0", new Point3D[] { new Point3D()  {X= 384D, Y= 32D, Z= 384D}, new Point3D()  {X= 384D, Y= 32D, Z= 128D}, new Point3D()  {X= 128D, Y= 32D, Z= 384D}, new Point3D()  {X= 128D, Y= 32D, Z= 128D} } },
            { "Grid_x0_z1", new Point3D[] { new Point3D()  {X= 384D, Y= 32D, Z= 896D}, new Point3D()  {X= 384D, Y= 32D, Z= 640D}, new Point3D()  {X= 128D, Y= 32D, Z= 896D}, new Point3D()  {X= 128D, Y= 32D, Z= 640D} } },
            { "Grid_x0_z-1", new Point3D[] { new Point3D()  {X= 384D, Y= 32D, Z= -128D}, new Point3D()  {X= 384D, Y= 32D, Z= -384D}, new Point3D()  {X= 128D, Y= 32D, Z= -128D}, new Point3D()  {X= 128D, Y= 32D, Z= -384D} } },
            { "Grid_x0_z2", new Point3D[] { new Point3D()  {X= 384D, Y= 32D, Z= 1408D}, new Point3D()  {X= 384D, Y= 32D, Z= 1152D}, new Point3D()  {X= 128D, Y= 32D, Z= 1408D}, new Point3D()  {X= 128D, Y= 32D, Z= 1152D} } },
            { "Grid_x0_z-2", new Point3D[] { new Point3D()  {X= 384D, Y= 43D, Z= -640D}, new Point3D()  {X= 384D, Y= 43D, Z= -896D}, new Point3D()  {X= 128D, Y= 43D, Z= -640D}, new Point3D()  {X= 128D, Y= 43D, Z= -896D} } },
            { "Grid_x0_z3", new Point3D[] { new Point3D()  {X= 384D, Y= 24D, Z= 1920D}, new Point3D()  {X= 384D, Y= 24D, Z= 1664D}, new Point3D()  {X= 128D, Y= 24D, Z= 1920D}, new Point3D()  {X= 128D, Y= 24D, Z= 1664D} } },
            { "Grid_x0_z-3", new Point3D[] { new Point3D()  {X= 384D, Y= 43D, Z= -1152D}, new Point3D()  {X= 384D, Y= 43D, Z= -1408D}, new Point3D()  {X= 128D, Y= 43D, Z= -1152D}, new Point3D()  {X= 128D, Y= 43D, Z= -1408D} } },
            { "Grid_x0_z4", new Point3D[] { new Point3D()  {X= 384D, Y= 24D, Z= 2432D}, new Point3D()  {X= 384D, Y= 24D, Z= 2176D}, new Point3D()  {X= 128D, Y= 24D, Z= 2432D}, new Point3D()  {X= 128D, Y= 24D, Z= 2176D} } },
            { "Grid_x0_z-4", new Point3D[] { new Point3D()  {X= 384D, Y= 32D, Z= -1664D}, new Point3D()  {X= 384D, Y= 32D, Z= -1920D}, new Point3D()  {X= 128D, Y= 32D, Z= -1664D}, new Point3D()  {X= 128D, Y= 32D, Z= -1920D} } },
            { "Grid_x0_z-5", new Point3D[] { new Point3D()  {X= 384D, Y= 32D, Z= -2176D}, new Point3D()  {X= 384D, Y= 32D, Z= -2432D}, new Point3D()  {X= 128D, Y= 32D, Z= -2176D}, new Point3D()  {X= 128D, Y= 32D, Z= -2432D} } },
            { "Grid_x1_z0", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= 384D}, new Point3D()  {X= 896D, Y= 32D, Z= 128D}, new Point3D()  {X= 640D, Y= 32D, Z= 384D}, new Point3D()  {X= 640D, Y= 32D, Z= 128D} } },
            { "Grid_x-1_z0", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= 384D}, new Point3D()  {X= -128D, Y= 32D, Z= 128D}, new Point3D()  {X= -384D, Y= 32D, Z= 384D}, new Point3D()  {X= -384D, Y= 32D, Z= 128D} } },
            { "Grid_x1_z1", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= 896D}, new Point3D()  {X= 896D, Y= 32D, Z= 640D}, new Point3D()  {X= 640D, Y= 32D, Z= 896D}, new Point3D()  {X= 640D, Y= 32D, Z= 640D} } },
            { "Grid_x1_z-1", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= -128D}, new Point3D()  {X= 896D, Y= 32D, Z= -384D}, new Point3D()  {X= 640D, Y= 32D, Z= -128D}, new Point3D()  {X= 640D, Y= 32D, Z= -384D} } },
            { "Grid_x-1_z1", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= 896D}, new Point3D()  {X= -128D, Y= 32D, Z= 640D}, new Point3D()  {X= -384D, Y= 32D, Z= 896D}, new Point3D()  {X= -384D, Y= 32D, Z= 640D} } },
            { "Grid_x-1_z-1", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= -128D}, new Point3D()  {X= -128D, Y= 32D, Z= -384D}, new Point3D()  {X= -384D, Y= 32D, Z= -128D}, new Point3D()  {X= -384D, Y= 32D, Z= -384D} } },
            { "Grid_x1_z2", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= 1408D}, new Point3D()  {X= 896D, Y= 32D, Z= 1152D}, new Point3D()  {X= 640D, Y= 32D, Z= 1408D}, new Point3D()  {X= 640D, Y= 32D, Z= 1152D} } },
            { "Grid_x1_z-2", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= -640D}, new Point3D()  {X= 896D, Y= 32D, Z= -896D}, new Point3D()  {X= 640D, Y= 32D, Z= -640D}, new Point3D()  {X= 640D, Y= 32D, Z= -896D} } },
            { "Grid_x-1_z2", new Point3D[] { new Point3D()  {X= -128D, Y= 24D, Z= 1408D}, new Point3D()  {X= -128D, Y= 24D, Z= 1152D}, new Point3D()  {X= -384D, Y= 24D, Z= 1408D}, new Point3D()  {X= -384D, Y= 24D, Z= 1152D} } },
            { "Grid_x-1_z-2", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= -640D}, new Point3D()  {X= -128D, Y= 32D, Z= -896D}, new Point3D()  {X= -384D, Y= 32D, Z= -640D}, new Point3D()  {X= -384D, Y= 32D, Z= -896D} } },
            { "Grid_x1_z3", new Point3D[] { new Point3D()  {X= 896D, Y= 24D, Z= 1920D}, new Point3D()  {X= 896D, Y= 24D, Z= 1664D}, new Point3D()  {X= 640D, Y= 24D, Z= 1920D}, new Point3D()  {X= 640D, Y= 24D, Z= 1664D} } },
            { "Grid_x1_z-3", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= -1152D}, new Point3D()  {X= 896D, Y= 32D, Z= -1408D}, new Point3D()  {X= 640D, Y= 32D, Z= -1152D}, new Point3D()  {X= 640D, Y= 32D, Z= -1408D} } },
            { "Grid_x-1_z3", new Point3D[] { new Point3D()  {X= -128D, Y= 24D, Z= 1920D}, new Point3D()  {X= -128D, Y= 24D, Z= 1664D}, new Point3D()  {X= -384D, Y= 24D, Z= 1920D}, new Point3D()  {X= -384D, Y= 24D, Z= 1664D} } },
            { "Grid_x-1_z-3", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= -1152D}, new Point3D()  {X= -128D, Y= 32D, Z= -1408D}, new Point3D()  {X= -384D, Y= 32D, Z= -1152D}, new Point3D()  {X= -384D, Y= 32D, Z= -1408D} } },
            { "Grid_x1_z4", new Point3D[] { new Point3D()  {X= 896D, Y= 24D, Z= 2432D}, new Point3D()  {X= 896D, Y= 24D, Z= 2176D}, new Point3D()  {X= 640D, Y= 24D, Z= 2432D}, new Point3D()  {X= 640D, Y= 24D, Z= 2176D} } },
            { "Grid_x1_z-4", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= -1664D}, new Point3D()  {X= 896D, Y= 32D, Z= -1920D}, new Point3D()  {X= 640D, Y= 32D, Z= -1664D}, new Point3D()  {X= 640D, Y= 32D, Z= -1920D} } },
            { "Grid_x-1_z4", new Point3D[] { new Point3D()  {X= -128D, Y= 24D, Z= 2432D}, new Point3D()  {X= -128D, Y= 24D, Z= 2176D}, new Point3D()  {X= -384D, Y= 24D, Z= 2432D}, new Point3D()  {X= -384D, Y= 24D, Z= 2176D} } },
            { "Grid_x-1_z-4", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= -1664D}, new Point3D()  {X= -128D, Y= 32D, Z= -1920D}, new Point3D()  {X= -384D, Y= 32D, Z= -1664D}, new Point3D()  {X= -384D, Y= 32D, Z= -1920D} } },
            { "Grid_x1_z-5", new Point3D[] { new Point3D()  {X= 896D, Y= 32D, Z= -2176D}, new Point3D()  {X= 896D, Y= 32D, Z= -2432D}, new Point3D()  {X= 640D, Y= 32D, Z= -2176D}, new Point3D()  {X= 640D, Y= 32D, Z= -2432D} } },
            { "Grid_x-1_z-5", new Point3D[] { new Point3D()  {X= -128D, Y= 32D, Z= -2176D}, new Point3D()  {X= -128D, Y= 32D, Z= -2432D}, new Point3D()  {X= -384D, Y= 32D, Z= -2176D}, new Point3D()  {X= -384D, Y= 32D, Z= -2432D} } },
            { "Grid_x2_z0", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= 384D}, new Point3D()  {X= 1408D, Y= 32D, Z= 128D}, new Point3D()  {X= 1152D, Y= 32D, Z= 384D}, new Point3D()  {X= 1152D, Y= 32D, Z= 128D} } },
            { "Grid_x-2_z0", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= 384D}, new Point3D()  {X= -640D, Y= 32D, Z= 128D}, new Point3D()  {X= -896D, Y= 32D, Z= 384D}, new Point3D()  {X= -896D, Y= 32D, Z= 128D} } },
            { "Grid_x2_z1", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= 896D}, new Point3D()  {X= 1408D, Y= 32D, Z= 640D}, new Point3D()  {X= 1152D, Y= 32D, Z= 896D}, new Point3D()  {X= 1152D, Y= 32D, Z= 640D} } },
            { "Grid_x2_z-1", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= -128D}, new Point3D()  {X= 1408D, Y= 32D, Z= -384D}, new Point3D()  {X= 1152D, Y= 32D, Z= -128D}, new Point3D()  {X= 1152D, Y= 32D, Z= -384D} } },
            { "Grid_x-2_z1", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= 896D}, new Point3D()  {X= -640D, Y= 32D, Z= 640D}, new Point3D()  {X= -896D, Y= 32D, Z= 896D}, new Point3D()  {X= -896D, Y= 32D, Z= 640D} } },
            { "Grid_x-2_z-1", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= -128D}, new Point3D()  {X= -640D, Y= 32D, Z= -384D}, new Point3D()  {X= -896D, Y= 32D, Z= -128D}, new Point3D()  {X= -896D, Y= 32D, Z= -384D} } },
            { "Grid_x2_z2", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= 1408D}, new Point3D()  {X= 1408D, Y= 32D, Z= 1152D}, new Point3D()  {X= 1152D, Y= 32D, Z= 1408D}, new Point3D()  {X= 1152D, Y= 32D, Z= 1152D} } },
            { "Grid_x2_z-2", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= -640D}, new Point3D()  {X= 1408D, Y= 32D, Z= -896D}, new Point3D()  {X= 1152D, Y= 32D, Z= -640D}, new Point3D()  {X= 1152D, Y= 32D, Z= -896D} } },
            { "Grid_x-2_z2", new Point3D[] { new Point3D()  {X= -640D, Y= 24D, Z= 1408D}, new Point3D()  {X= -640D, Y= 24D, Z= 1152D}, new Point3D()  {X= -896D, Y= 24D, Z= 1408D}, new Point3D()  {X= -896D, Y= 24D, Z= 1152D} } },
            { "Grid_x-2_z-2", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= -640D}, new Point3D()  {X= -640D, Y= 32D, Z= -896D}, new Point3D()  {X= -896D, Y= 32D, Z= -640D}, new Point3D()  {X= -896D, Y= 32D, Z= -896D} } },
            { "Grid_x2_z3", new Point3D[] { new Point3D()  {X= 1408D, Y= 24D, Z= 1920D}, new Point3D()  {X= 1408D, Y= 24D, Z= 1664D}, new Point3D()  {X= 1152D, Y= 24D, Z= 1920D}, new Point3D()  {X= 1152D, Y= 24D, Z= 1664D} } },
            { "Grid_x2_z-3", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= -1152D}, new Point3D()  {X= 1408D, Y= 32D, Z= -1408D}, new Point3D()  {X= 1152D, Y= 32D, Z= -1152D}, new Point3D()  {X= 1152D, Y= 32D, Z= -1408D} } },
            { "Grid_x-2_z3", new Point3D[] { new Point3D()  {X= -640D, Y= 24D, Z= 1920D}, new Point3D()  {X= -640D, Y= 24D, Z= 1664D}, new Point3D()  {X= -896D, Y= 24D, Z= 1920D}, new Point3D()  {X= -896D, Y= 24D, Z= 1664D} } },
            { "Grid_x-2_z-3", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= -1152D}, new Point3D()  {X= -640D, Y= 32D, Z= -1408D}, new Point3D()  {X= -896D, Y= 32D, Z= -1152D}, new Point3D()  {X= -896D, Y= 32D, Z= -1408D} } },
            { "Grid_x2_z4", new Point3D[] { new Point3D()  {X= 1408D, Y= 24D, Z= 2432D}, new Point3D()  {X= 1408D, Y= 24D, Z= 2176D}, new Point3D()  {X= 1152D, Y= 24D, Z= 2432D}, new Point3D()  {X= 1152D, Y= 24D, Z= 2176D} } },
            { "Grid_x2_z-4", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= -1664D}, new Point3D()  {X= 1408D, Y= 32D, Z= -1920D}, new Point3D()  {X= 1152D, Y= 32D, Z= -1664D}, new Point3D()  {X= 1152D, Y= 32D, Z= -1920D} } },
            { "Grid_x-2_z4", new Point3D[] { new Point3D()  {X= -640D, Y= 24D, Z= 2432D}, new Point3D()  {X= -640D, Y= 24D, Z= 2176D}, new Point3D()  {X= -896D, Y= 24D, Z= 2432D}, new Point3D()  {X= -896D, Y= 24D, Z= 2176D} } },
            { "Grid_x-2_z-4", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= -1664D}, new Point3D()  {X= -640D, Y= 32D, Z= -1920D}, new Point3D()  {X= -896D, Y= 32D, Z= -1664D}, new Point3D()  {X= -896D, Y= 32D, Z= -1920D} } },
            { "Grid_x2_z-5", new Point3D[] { new Point3D()  {X= 1408D, Y= 32D, Z= -2176D}, new Point3D()  {X= 1408D, Y= 32D, Z= -2432D}, new Point3D()  {X= 1152D, Y= 32D, Z= -2176D}, new Point3D()  {X= 1152D, Y= 32D, Z= -2432D} } },
            { "Grid_x-2_z-5", new Point3D[] { new Point3D()  {X= -640D, Y= 32D, Z= -2176D}, new Point3D()  {X= -640D, Y= 32D, Z= -2432D}, new Point3D()  {X= -896D, Y= 32D, Z= -2176D}, new Point3D()  {X= -896D, Y= 32D, Z= -2432D} } },
            { "Grid_x3_z0", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= 384D}, new Point3D()  {X= 1920D, Y= 32D, Z= 128D}, new Point3D()  {X= 1664D, Y= 32D, Z= 384D}, new Point3D()  {X= 1664D, Y= 32D, Z= 128D} } },
            { "Grid_x-3_z0", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= 384D}, new Point3D()  {X= -1152D, Y= 32D, Z= 128D}, new Point3D()  {X= -1408D, Y= 32D, Z= 384D}, new Point3D()  {X= -1408D, Y= 32D, Z= 128D} } },
            { "Grid_x3_z1", new Point3D[] { new Point3D()  {X= 1920D, Y= 43D, Z= 896D}, new Point3D()  {X= 1920D, Y= 43D, Z= 640D}, new Point3D()  {X= 1664D, Y= 43D, Z= 896D}, new Point3D()  {X= 1664D, Y= 43D, Z= 640D} } },
            { "Grid_x3_z-1", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= -128D}, new Point3D()  {X= 1920D, Y= 32D, Z= -384D}, new Point3D()  {X= 1664D, Y= 32D, Z= -128D}, new Point3D()  {X= 1664D, Y= 32D, Z= -384D} } },
            { "Grid_x-3_z1", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= 896D}, new Point3D()  {X= -1152D, Y= 32D, Z= 640D}, new Point3D()  {X= -1408D, Y= 32D, Z= 896D}, new Point3D()  {X= -1408D, Y= 32D, Z= 640D} } },
            { "Grid_x-3_z-1", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= -128D}, new Point3D()  {X= -1152D, Y= 32D, Z= -384D}, new Point3D()  {X= -1408D, Y= 32D, Z= -128D}, new Point3D()  {X= -1408D, Y= 32D, Z= -384D} } },
            { "Grid_x3_z2", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= 1408D}, new Point3D()  {X= 1920D, Y= 32D, Z= 1152D}, new Point3D()  {X= 1664D, Y= 32D, Z= 1408D}, new Point3D()  {X= 1664D, Y= 32D, Z= 1152D} } },
            { "Grid_x3_z-2", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= -640D}, new Point3D()  {X= 1920D, Y= 32D, Z= -896D}, new Point3D()  {X= 1664D, Y= 32D, Z= -640D}, new Point3D()  {X= 1664D, Y= 32D, Z= -896D} } },
            { "Grid_x-3_z2", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= 1408D}, new Point3D()  {X= -1152D, Y= 32D, Z= 1152D}, new Point3D()  {X= -1408D, Y= 32D, Z= 1408D}, new Point3D()  {X= -1408D, Y= 32D, Z= 1152D} } },
            { "Grid_x-3_z-2", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= -640D}, new Point3D()  {X= -1152D, Y= 32D, Z= -896D}, new Point3D()  {X= -1408D, Y= 32D, Z= -640D}, new Point3D()  {X= -1408D, Y= 32D, Z= -896D} } },
            { "Grid_x3_z3", new Point3D[] { new Point3D()  {X= 1920D, Y= 24D, Z= 1920D}, new Point3D()  {X= 1920D, Y= 24D, Z= 1664D}, new Point3D()  {X= 1664D, Y= 24D, Z= 1920D}, new Point3D()  {X= 1664D, Y= 24D, Z= 1664D} } },
            { "Grid_x3_z-3", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= -1152D}, new Point3D()  {X= 1920D, Y= 32D, Z= -1408D}, new Point3D()  {X= 1664D, Y= 32D, Z= -1152D}, new Point3D()  {X= 1664D, Y= 32D, Z= -1408D} } },
            { "Grid_x-3_z-3", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= -1152D}, new Point3D()  {X= -1152D, Y= 32D, Z= -1408D}, new Point3D()  {X= -1408D, Y= 32D, Z= -1152D}, new Point3D()  {X= -1408D, Y= 32D, Z= -1408D} } },
            { "Grid_x3_z4", new Point3D[] { new Point3D()  {X= 1920D, Y= 24D, Z= 2432D}, new Point3D()  {X= 1920D, Y= 24D, Z= 2176D}, new Point3D()  {X= 1664D, Y= 24D, Z= 2432D}, new Point3D()  {X= 1664D, Y= 24D, Z= 2176D} } },
            { "Grid_x3_z-4", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= -1664D}, new Point3D()  {X= 1920D, Y= 32D, Z= -1920D}, new Point3D()  {X= 1664D, Y= 32D, Z= -1664D}, new Point3D()  {X= 1664D, Y= 32D, Z= -1920D} } },
            { "Grid_x-3_z-4", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= -1664D}, new Point3D()  {X= -1152D, Y= 32D, Z= -1920D}, new Point3D()  {X= -1408D, Y= 32D, Z= -1664D}, new Point3D()  {X= -1408D, Y= 32D, Z= -1920D} } },
            { "Grid_x3_z-5", new Point3D[] { new Point3D()  {X= 1920D, Y= 32D, Z= -2176D}, new Point3D()  {X= 1920D, Y= 32D, Z= -2432D}, new Point3D()  {X= 1664D, Y= 32D, Z= -2176D}, new Point3D()  {X= 1664D, Y= 32D, Z= -2432D} } },
            { "Grid_x-3_z-5", new Point3D[] { new Point3D()  {X= -1152D, Y= 32D, Z= -2176D}, new Point3D()  {X= -1152D, Y= 32D, Z= -2432D}, new Point3D()  {X= -1408D, Y= 32D, Z= -2176D}, new Point3D()  {X= -1408D, Y= 32D, Z= -2432D} } },
            { "Grid_x4_z0", new Point3D[] { new Point3D()  {X= 2432D, Y= 32D, Z= 384D}, new Point3D()  {X= 2432D, Y= 32D, Z= 128D}, new Point3D()  {X= 2176D, Y= 32D, Z= 384D}, new Point3D()  {X= 2176D, Y= 32D, Z= 128D} } },
            { "Grid_x-4_z0", new Point3D[] { new Point3D()  {X= -1664D, Y= 32D, Z= 384D}, new Point3D()  {X= -1664D, Y= 32D, Z= 128D}, new Point3D()  {X= -1920D, Y= 32D, Z= 384D}, new Point3D()  {X= -1920D, Y= 32D, Z= 128D} } },
            { "Grid_x4_z1", new Point3D[] { new Point3D()  {X= 2432D, Y= 32D, Z= 896D}, new Point3D()  {X= 2432D, Y= 32D, Z= 640D}, new Point3D()  {X= 2176D, Y= 32D, Z= 896D}, new Point3D()  {X= 2176D, Y= 32D, Z= 640D} } },
            { "Grid_x4_z-1", new Point3D[] { new Point3D()  {X= 2432D, Y= 32D, Z= -128D}, new Point3D()  {X= 2432D, Y= 32D, Z= -384D}, new Point3D()  {X= 2176D, Y= 32D, Z= -128D}, new Point3D()  {X= 2176D, Y= 32D, Z= -384D} } },
            { "Grid_x-4_z1", new Point3D[] { new Point3D()  {X= -1664D, Y= 32D, Z= 896D}, new Point3D()  {X= -1664D, Y= 32D, Z= 640D}, new Point3D()  {X= -1920D, Y= 32D, Z= 896D}, new Point3D()  {X= -1920D, Y= 32D, Z= 640D} } },
            { "Grid_x-4_z-1", new Point3D[] { new Point3D()  {X= -1664D, Y= 32D, Z= -128D}, new Point3D()  {X= -1664D, Y= 32D, Z= -384D}, new Point3D()  {X= -1920D, Y= 32D, Z= -128D}, new Point3D()  {X= -1920D, Y= 32D, Z= -384D} } },
            { "Grid_x4_z2", new Point3D[] { new Point3D()  {X= 2432D, Y= 32D, Z= 1408D}, new Point3D()  {X= 2432D, Y= 32D, Z= 1152D}, new Point3D()  {X= 2176D, Y= 32D, Z= 1408D}, new Point3D()  {X= 2176D, Y= 32D, Z= 1152D} } },
            { "Grid_x4_z-2", new Point3D[] { new Point3D()  {X= 2432D, Y= 32D, Z= -640D}, new Point3D()  {X= 2432D, Y= 32D, Z= -896D}, new Point3D()  {X= 2176D, Y= 32D, Z= -640D}, new Point3D()  {X= 2176D, Y= 32D, Z= -896D} } },
            { "Grid_x-4_z2", new Point3D[] { new Point3D()  {X= -1664D, Y= 32D, Z= 1408D}, new Point3D()  {X= -1664D, Y= 32D, Z= 1152D}, new Point3D()  {X= -1920D, Y= 32D, Z= 1408D}, new Point3D()  {X= -1920D, Y= 32D, Z= 1152D} } },
            { "Grid_x-4_z-2", new Point3D[] { new Point3D()  {X= -1664D, Y= 24D, Z= -640D}, new Point3D()  {X= -1664D, Y= 24D, Z= -896D}, new Point3D()  {X= -1920D, Y= 24D, Z= -640D}, new Point3D()  {X= -1920D, Y= 24D, Z= -896D} } },
            { "Grid_x4_z3", new Point3D[] { new Point3D()  {X= 2444.812D, Y= 32D, Z= 1921.317D}, new Point3D()  {X= 2444.812D, Y= 32D, Z= 1665.317D}, new Point3D()  {X= 2188.812D, Y= 32D, Z= 1921.317D}, new Point3D()  {X= 2188.812D, Y= 32D, Z= 1665.317D} } },
            { "Grid_x4_z-3", new Point3D[] { new Point3D()  {X= 2432D, Y= 32D, Z= -1152D}, new Point3D()  {X= 2432D, Y= 32D, Z= -1408D}, new Point3D()  {X= 2176D, Y= 32D, Z= -1152D}, new Point3D()  {X= 2176D, Y= 32D, Z= -1408D} } },
            { "Grid_x-4_z-3", new Point3D[] { new Point3D()  {X= -1664D, Y= 32D, Z= -1152D}, new Point3D()  {X= -1664D, Y= 32D, Z= -1408D}, new Point3D()  {X= -1920D, Y= 32D, Z= -1152D}, new Point3D()  {X= -1920D, Y= 32D, Z= -1408D} } },
            { "Grid_x4_z4", new Point3D[] { new Point3D()  {X= 2444.609D, Y= 32D, Z= 2432.991D}, new Point3D()  {X= 2444.609D, Y= 32D, Z= 2176.991D}, new Point3D()  {X= 2188.609D, Y= 32D, Z= 2432.991D}, new Point3D()  {X= 2188.609D, Y= 32D, Z= 2176.991D} } },
            { "Grid_x-4_z-4", new Point3D[] { new Point3D()  {X= -1664D, Y= 43D, Z= -1664D}, new Point3D()  {X= -1664D, Y= 43D, Z= -1920D}, new Point3D()  {X= -1920D, Y= 43D, Z= -1664D}, new Point3D()  {X= -1920D, Y= 43D, Z= -1920D} } },
            { "Grid_x-4_z-5", new Point3D[] { new Point3D()  {X= -1664D, Y= 43D, Z= -2176D}, new Point3D()  {X= -1664D, Y= 43D, Z= -2432D}, new Point3D()  {X= -1920D, Y= 43D, Z= -2176D}, new Point3D()  {X= -1920D, Y= 43D, Z= -2432D} } },
            { "Grid_x5_z0", new Point3D[] { new Point3D()  {X= 2944D, Y= 32D, Z= 384D}, new Point3D()  {X= 2944D, Y= 32D, Z= 128D}, new Point3D()  {X= 2688D, Y= 32D, Z= 384D}, new Point3D()  {X= 2688D, Y= 32D, Z= 128D} } },
            { "Grid_x-5_z0", new Point3D[] { new Point3D()  {X= -2176D, Y= 32D, Z= 384D}, new Point3D()  {X= -2176D, Y= 32D, Z= 128D}, new Point3D()  {X= -2432D, Y= 32D, Z= 384D}, new Point3D()  {X= -2432D, Y= 32D, Z= 128D} } },
            { "Grid_x5_z1", new Point3D[] { new Point3D()  {X= 2944D, Y= 32D, Z= 896D}, new Point3D()  {X= 2944D, Y= 32D, Z= 640D}, new Point3D()  {X= 2688D, Y= 32D, Z= 896D}, new Point3D()  {X= 2688D, Y= 32D, Z= 640D} } },
            { "Grid_x5_z-1", new Point3D[] { new Point3D()  {X= 2944D, Y= 32D, Z= -128D}, new Point3D()  {X= 2944D, Y= 32D, Z= -384D}, new Point3D()  {X= 2688D, Y= 32D, Z= -128D}, new Point3D()  {X= 2688D, Y= 32D, Z= -384D} } },
            { "Grid_x-5_z-1", new Point3D[] { new Point3D()  {X= -2176D, Y= 32D, Z= -128D}, new Point3D()  {X= -2176D, Y= 32D, Z= -384D}, new Point3D()  {X= -2432D, Y= 32D, Z= -128D}, new Point3D()  {X= -2432D, Y= 32D, Z= -384D} } },
            { "Grid_x5_z2", new Point3D[] { new Point3D()  {X= 2944D, Y= 32D, Z= 1408D}, new Point3D()  {X= 2944D, Y= 32D, Z= 1152D}, new Point3D()  {X= 2688D, Y= 32D, Z= 1408D}, new Point3D()  {X= 2688D, Y= 32D, Z= 1152D} } },
            { "Grid_x5_z-2", new Point3D[] { new Point3D()  {X= 2944D, Y= 32D, Z= -640D}, new Point3D()  {X= 2944D, Y= 32D, Z= -896D}, new Point3D()  {X= 2688D, Y= 32D, Z= -640D}, new Point3D()  {X= 2688D, Y= 32D, Z= -896D} } },
            { "Grid_x-5_z-2", new Point3D[] { new Point3D()  {X= -2176D, Y= 24D, Z= -640D}, new Point3D()  {X= -2176D, Y= 24D, Z= -896D}, new Point3D()  {X= -2432D, Y= 24D, Z= -640D}, new Point3D()  {X= -2432D, Y= 24D, Z= -896D} } },
            { "Grid_x5_z3", new Point3D[] { new Point3D()  {X= 2985.61D, Y= 32D, Z= 1920.991D}, new Point3D()  {X= 2985.61D, Y= 32D, Z= 1664.991D}, new Point3D()  {X= 2729.61D, Y= 32D, Z= 1920.991D}, new Point3D()  {X= 2729.61D, Y= 32D, Z= 1664.991D} } },
            { "Grid_x5_z-3", new Point3D[] { new Point3D()  {X= 2944D, Y= 32D, Z= -1152D}, new Point3D()  {X= 2944D, Y= 32D, Z= -1408D}, new Point3D()  {X= 2688D, Y= 32D, Z= -1152D}, new Point3D()  {X= 2688D, Y= 32D, Z= -1408D} } },
            { "Grid_x-5_z-3", new Point3D[] { new Point3D()  {X= -2176D, Y= 32D, Z= -1152D}, new Point3D()  {X= -2176D, Y= 32D, Z= -1408D}, new Point3D()  {X= -2432D, Y= 32D, Z= -1152D}, new Point3D()  {X= -2432D, Y= 32D, Z= -1408D} } },
            { "Grid_x5_z4", new Point3D[] { new Point3D()  {X= 2985.61D, Y= 32D, Z= 2432.991D}, new Point3D()  {X= 2985.61D, Y= 32D, Z= 2176.991D}, new Point3D()  {X= 2729.61D, Y= 32D, Z= 2432.991D}, new Point3D()  {X= 2729.61D, Y= 32D, Z= 2176.991D} } },
            { "Grid_x-5_z-4", new Point3D[] { new Point3D()  {X= -2176D, Y= 32D, Z= -1664D}, new Point3D()  {X= -2176D, Y= 32D, Z= -1920D}, new Point3D()  {X= -2432D, Y= 32D, Z= -1664D}, new Point3D()  {X= -2432D, Y= 32D, Z= -1920D} } },
            { "Grid_x-5_z-5", new Point3D[] { new Point3D()  {X= -2176D, Y= 32D, Z= -2176D}, new Point3D()  {X= -2176D, Y= 32D, Z= -2432D}, new Point3D()  {X= -2432D, Y= 32D, Z= -2176D}, new Point3D()  {X= -2432D, Y= 32D, Z= -2432D} } },
            { "Grid_x-6_z0", new Point3D[] { new Point3D()  {X= -2688D, Y= 32D, Z= 384D}, new Point3D()  {X= -2688D, Y= 32D, Z= 128D}, new Point3D()  {X= -2944D, Y= 32D, Z= 384D}, new Point3D()  {X= -2944D, Y= 32D, Z= 128D} } },
            { "Grid_x-6_z-1", new Point3D[] { new Point3D()  {X= -2688D, Y= 32D, Z= -128D}, new Point3D()  {X= -2688D, Y= 32D, Z= -384D}, new Point3D()  {X= -2944D, Y= 32D, Z= -128D}, new Point3D()  {X= -2944D, Y= 32D, Z= -384D} } },
            { "Grid_x-6_z-2", new Point3D[] { new Point3D()  {X= -2688D, Y= 32D, Z= -640D}, new Point3D()  {X= -2688D, Y= 32D, Z= -896D}, new Point3D()  {X= -2944D, Y= 32D, Z= -640D}, new Point3D()  {X= -2944D, Y= 32D, Z= -896D} } },
            { "Grid_x-6_z-3", new Point3D[] { new Point3D()  {X= -2688D, Y= 32D, Z= -1152D}, new Point3D()  {X= -2688D, Y= 32D, Z= -1408D}, new Point3D()  {X= -2944D, Y= 32D, Z= -1152D}, new Point3D()  {X= -2944D, Y= 32D, Z= -1408D} } },
            { "Grid_x-6_z-4", new Point3D[] { new Point3D()  {X= -2688D, Y= 32D, Z= -1664D}, new Point3D()  {X= -2688D, Y= 32D, Z= -1920D}, new Point3D()  {X= -2944D, Y= 32D, Z= -1664D}, new Point3D()  {X= -2944D, Y= 32D, Z= -1920D} } },
            { "Grid_x-6_z-5", new Point3D[] { new Point3D()  {X= -2688D, Y= 32D, Z= -2176D}, new Point3D()  {X= -2688D, Y= 32D, Z= -2432D}, new Point3D()  {X= -2944D, Y= 32D, Z= -2176D}, new Point3D()  {X= -2944D, Y= 32D, Z= -2432D} } },
        };
        // This method was used to dump from the prefabs the coords up there,
        // keeping it here in case we need it in the future.
        // We'll extract these prefabs son, but for now, it looks like we cannot do it with AssetStudio.
        //public static void DumpCoords()
        //{
        //    SortedDictionary<string, string> TilesCoordsCode = new SortedDictionary<string, string>();
        //    string[] files = Directory.GetFiles("./MapData/", "*.prefab");
        //    foreach (string file in files)
        //    {
        //        List<string> matchingLines = new List<string>();
        //        string[] lines = File.ReadAllLines(file);
        //        foreach (var line in lines)
        //        {
        //            if (line.Contains("m_LocalPosition"))
        //            {
        //                matchingLines.Add(line);
        //            }
        //        }
        //        if (matchingLines.Count > 0)
        //        {
        //            string TileName = Path.GetFileName(file).Replace("Minimap.prefab", "");
        //            string Coord1 = matchingLines[1].Replace(" m_LocalPosition: ", "").ToUpper().Replace(":", "=").Replace(",", "D,").Replace("}", "D}");
        //            string Coord2 = matchingLines[2].Replace(" m_LocalPosition: ", "").ToUpper().Replace(":", "=").Replace(",", "D,").Replace("}", "D}");
        //            string Coord3 = matchingLines[3].Replace(" m_LocalPosition: ", "").ToUpper().Replace(":", "=").Replace(",", "D,").Replace("}", "D}");
        //            string Coord4 = matchingLines[4].Replace(" m_LocalPosition: ", "").ToUpper().Replace(":", "=").Replace(",", "D,").Replace("}", "D}");
        //            string CodeLine = $"{{ \"{TileName}\", new Point3D[] {{ new Point3D() {Coord1}, new Point3D() {Coord2}, new Point3D() {Coord3}, new Point3D() {Coord4} }} }},";
        //            TilesCoordsCode.Add(TileName, CodeLine);
        //        }
        //    }
        //    foreach (var TileCoordsCode in TilesCoordsCode)
        //    {
        //        Debug.Print(TileCoordsCode.Value);
        //    }
        //}

        public MapImage(string TileImagePath, string TilePrefabPath)
        {
            this.TileImagePath = TileImagePath;
            this.SetTileImage();

            this.TilePrefabPath = TilePrefabPath;
            this.SetTilePositionFromPrefab();
        }

        private BitmapImage GetScaledImage(string uriSource, int Resolution)
        {
            Image img = new Image();

            var buffer = File.ReadAllBytes(uriSource);
            MemoryStream ms = new MemoryStream(buffer);
            BitmapImage src = new BitmapImage();
            src.BeginInit();
            src.StreamSource = ms;
            src.DecodePixelHeight = Resolution;
            src.DecodePixelWidth = Resolution;
            src.EndInit();

            return src;
        }

        public void UpdateResolution(int Resolution)
        {
            this.Resolution = Resolution;
            this.SetTileImage();
        }

        private void SetTileImage()
        {
            this.Source = GetScaledImage(this.TileImagePath, this.Resolution);
        }

        private void SetTilePositionFromPrefab()
        {
            if (TileImagePath == null || TileImagePath.Trim() == "")
            {
                throw new System.Exception("Tile has no name, cannot calculate its position.");
            }

            string TileName = Path.GetFileNameWithoutExtension(TileImagePath);
            int TileIndex = int.Parse(TileName.Substring(TileName.Length - 1, 1));
            TileName = TileName.Substring(0, TileName.Length - 2);

            if (MapImage.TilesCoords.ContainsKey(TileName))
            {
                Point3D TileCoordinates = MapImage.TilesCoords[TileName][TileIndex];
                this.SetValue(Canvas.LeftProperty, TileCoordinates.X);
                this.SetValue(Canvas.TopProperty, TileCoordinates.Z);
            }
        }
    }
}
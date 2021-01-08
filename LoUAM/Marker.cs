using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LoUAM
{
    public enum MarkerType
    {
        CurrentPlayer = 0,
        OtherPlayer = 1,
        MOB = 2,
        Place = 3
    }

    public enum MarkerIcon
    {
        blue,
        green,
        grey,
        orange,
        red
    }

    public struct Marker
    {
        public MarkerType Type { get; set; }
        public string Id { get; set; }
        public MarkerIcon Icon { get; set; }
        public string Label { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Marker(MarkerType type, string id, MarkerIcon Icon, string label, float x, float y, float z)
        {
            this.Type = type;
            this.Id = id;
            this.Icon = Icon;
            this.Label = label;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace UnityX.ValueMonitor
{
    public partial class GLGraphDrawer
    {
        public class Curve
        {
            public Curve() { }
            public Curve(Color color)
            {
                Color = color;
            }

            public List<Vector2> Positions { get; private set; } = new List<Vector2>();
            public Color Color { get; set; }
        }
    }
}
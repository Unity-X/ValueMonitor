using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityX.ValueMonitor
{
    public partial class GLGraphDrawer
    {
        public interface IGridInfo
        {
            double CellSizeX { get; }
            double CellSizeY { get; }
            double CellOffsetX { get; }
            double CellOffsetY { get; }
        }

        public Material Material { get; set; }

        public List<Curve> Curves = new List<Curve>();
        public List<Point> Points = new List<Point>();

        public Rect ValueDisplayRect;
        public Rect ScreenDisplayRect;
        public Color GridMainColor = new Color(0.5f, 0.5f, 0.5f);
        public Color GridAxisColor = new Color(1, 1, 1);
        public Color GridSubColor = new Color(0.25f, 0.25f, 0.25f);
        public float GridCellPerPixel = 1 / 50f;
        public bool AutoZoomHorizontal = true;
        public bool AutoZoomVertical = true;
        public Vector2 AutoZoomPadding = new Vector2(2f, 2f);
        public IGridInfo LastGridDrawInfo => _gridDrawInfo;
        private GridInfo _gridDrawInfo = new GridInfo();

        private class GridInfo : IGridInfo
        {
            public double CellSizeX;
            public double CellSizeY;
            public double CellOffsetX;
            public double CellOffsetY;
            public double SubCellSizeX;
            public double SubCellSizeY;
            public double SubCellOffsetX;
            public double SubCellOffsetY;
            double IGridInfo.CellSizeX => CellSizeX;
            double IGridInfo.CellSizeY => CellSizeY;
            double IGridInfo.CellOffsetX => CellOffsetX;
            double IGridInfo.CellOffsetY => CellOffsetY;
        }

        private bool AutoSizeAny => AutoZoomVertical | AutoZoomHorizontal;

        public const float MIN_DISPLAY_RANGE = 0.001f;

        public GLGraphDrawer(Material material)
        {
            Material = material ?? throw new ArgumentNullException(nameof(material));
            ScreenDisplayRect = new Rect(0, 0, Screen.width, Screen.height);
            ValueDisplayRect = new Rect(position: Vector2.one * -5, size: Vector2.one * 10);
        }

        public void Draw()
        {
            Material.SetPass(0);

            if (AutoSizeAny)
            {
                Rect dataValueRect = CalculateValueRectFromData();
                Vector2 valuePadding = ScreenToValueVector(AutoZoomPadding);
                if (AutoZoomHorizontal)
                {
                    ValueDisplayRect.xMin = dataValueRect.xMin - valuePadding.x;
                    ValueDisplayRect.xMax = dataValueRect.xMax + valuePadding.x;
                }
                if (AutoZoomVertical)
                {
                    ValueDisplayRect.yMin = dataValueRect.yMin - valuePadding.y;
                    ValueDisplayRect.yMax = dataValueRect.yMax + valuePadding.y;
                }
            }

            GL.PushMatrix();

            GL.Begin(GL.LINES);
            GL.Color(Color.white);
            GL.LoadPixelMatrix();

            // Draw grid
            DrawGrid();

            // Draw curves
            for (int i = 0; i < Curves.Count; i++)
            {
                GL.Color(Curves[i].Color);
                for (int j = 1; j < Curves[i].Positions.Count; j++)
                {
                    AddLine_ValueSpace(Curves[i].Positions[j], Curves[i].Positions[j - 1]);
                }
            }

            // Draw points
            for (int i = 0; i < Points.Count; i++)
            {
                GL.Color(Points[i].Color);
                AddCross_ValueSpace(Points[i].Position);
            }
            GL.End();

            GL.PopMatrix();
        }

        private void DrawGrid()
        {
            if (GridMainColor.a <= 0 || ScreenDisplayRect.width == 0 || ScreenDisplayRect.height == 0)
                return;

            UpdateGridDrawInfo(_gridDrawInfo);

            // Sub grid lines
            GL.Color(GridSubColor);
            {
                double p;
                int index = 0;
                while (true)
                {
                    p = _gridDrawInfo.SubCellOffsetX + (_gridDrawInfo.SubCellSizeX * index);
                    if (p > ValueDisplayRect.xMax)
                        break;
                    AddLine_ValueSpace(new Vector2((float)p, ValueDisplayRect.yMin), new Vector2((float)p, ValueDisplayRect.yMax));
                    index++;
                }

                index = 0;
                while (true)
                {
                    p = _gridDrawInfo.SubCellOffsetY + (_gridDrawInfo.SubCellSizeY * index);
                    if (p > ValueDisplayRect.yMax)
                        break;
                    AddLine_ValueSpace(new Vector2(ValueDisplayRect.xMin, (float)p), new Vector2(ValueDisplayRect.xMax, (float)p));
                    index++;
                }
            }

            // Main grid lines
            {
                double p;
                int index = 0;
                while (true)
                {
                    p = _gridDrawInfo.CellOffsetX + (_gridDrawInfo.CellSizeX * index);
                    if (p > ValueDisplayRect.xMax)
                        break;
                    GL.Color(Mathf.Approximately((float)p, 0) ? GridAxisColor : GridMainColor);
                    AddLine_ValueSpace(new Vector2((float)p, ValueDisplayRect.yMin), new Vector2((float)p, ValueDisplayRect.yMax));
                    index++;
                }

                index = 0;
                while (true)
                {
                    p = _gridDrawInfo.CellOffsetY + (_gridDrawInfo.CellSizeY * index);
                    if (p > ValueDisplayRect.yMax)
                        break;
                    GL.Color(Mathf.Approximately((float)p, 0) ? GridAxisColor : GridMainColor);
                    AddLine_ValueSpace(new Vector2(ValueDisplayRect.xMin, (float)p), new Vector2(ValueDisplayRect.xMax, (float)p));
                    index++;
                }
            }
        }

        void UpdateGridDrawInfo(GridInfo gridDrawInfo)
        {
            double gridLineCountX = Math.Abs(ScreenDisplayRect.size.x * GridCellPerPixel);
            double gridLineCountY = Math.Abs(ScreenDisplayRect.size.y * GridCellPerPixel);
            double cellSizeApproxX = ValueDisplayRect.size.x / gridLineCountX;
            double cellSizeApproxY = ValueDisplayRect.size.y / gridLineCountY;

            gridDrawInfo.CellSizeX = GetCellSizeFromApprox(cellSizeApproxX, twoLevelsUnder: false);
            gridDrawInfo.CellSizeY = GetCellSizeFromApprox(cellSizeApproxY, twoLevelsUnder: false);

            gridDrawInfo.CellOffsetX = Math.Ceiling(ValueDisplayRect.xMin / gridDrawInfo.CellSizeX) * gridDrawInfo.CellSizeX;
            gridDrawInfo.CellOffsetY = Math.Ceiling(ValueDisplayRect.yMin / gridDrawInfo.CellSizeY) * gridDrawInfo.CellSizeY;

            // "div by 3" will cause the sub grid to be 2 subdivision levels below the main grid
            gridDrawInfo.SubCellSizeX = GetCellSizeFromApprox(cellSizeApproxX, twoLevelsUnder: true);
            gridDrawInfo.SubCellSizeY = GetCellSizeFromApprox(cellSizeApproxY, twoLevelsUnder: true);

            gridDrawInfo.SubCellOffsetX = Math.Ceiling(ValueDisplayRect.xMin / gridDrawInfo.SubCellSizeX) * gridDrawInfo.SubCellSizeX;
            gridDrawInfo.SubCellOffsetY = Math.Ceiling(ValueDisplayRect.yMin / gridDrawInfo.SubCellSizeY) * gridDrawInfo.SubCellSizeY;
        }

        double GetCellSizeFromApprox(double cellSizeApprox, bool twoLevelsUnder)
        {
            int magnitudeOrder = (int)Math.Floor(Math.Log10(cellSizeApprox));
            double magnitude = Math.Pow(10, magnitudeOrder);
            int firstDigit = (int)(cellSizeApprox / magnitude);

            if (firstDigit >= 5)
            {
                if (twoLevelsUnder)
                {
                    firstDigit = 1;
                }
                else
                {
                    firstDigit = 5;
                }
            }
            else if (firstDigit >= 2)
            {
                if (twoLevelsUnder)
                {
                    firstDigit = 5;
                    magnitude /= 10;
                }
                else
                {
                    firstDigit = 2;
                }
            }
            else
            {
                if (twoLevelsUnder)
                {
                    firstDigit = 2;
                    magnitude /= 10;
                }
                else
                {
                    firstDigit = 1;
                }
            }

            return firstDigit * magnitude;
        }

        Rect CalculateValueRectFromData()
        {
            Vector2 min = new Vector2(int.MaxValue, int.MaxValue);
            Vector2 max = new Vector2(int.MinValue, int.MinValue);

            void considerPoint(Vector2 p)
            {
                if (p.x < min.x)
                    min.x = p.x;
                if (p.y < min.y)
                    min.y = p.y;

                if (p.x > max.x)
                    max.x = p.x;
                if (p.y > max.y)
                    max.y = p.y;
            }

            void ensureMinimalRange(float rangeMin, ref float rangeMax)
            {
                float d = rangeMax - rangeMin;
                float sign = Mathf.Sign(d);
                if (sign * d < MIN_DISPLAY_RANGE)
                    rangeMax = rangeMin + (MIN_DISPLAY_RANGE * sign);
            }

            // Check curves
            for (int i = 0; i < Curves.Count; i++)
                for (int j = 0; j < Curves[i].Positions.Count; j++)
                    considerPoint(Curves[i].Positions[j]);

            // Check points
            for (int i = 0; i < Points.Count; i++)
                considerPoint(Points[i].Position);

            // Si ya aucun point, on met des valeurs par défaut
            if (max.x == int.MinValue)
            {
                min = Vector2.zero;
                max = Vector2.one;
            }

            // ensure a minimum range of MIN_DISPLAY_RANGE
            ensureMinimalRange(min.x, ref max.x);
            ensureMinimalRange(min.y, ref max.y);

            return new Rect(position: min, size: max - min);
        }

        void AddLine_ValueSpace(Vector2 a, Vector2 b)
        {
            AddLine_ScreenSpace(ValueToScreenPos(a), ValueToScreenPos(b));
        }
        void AddLine_ScreenSpace(Vector2 a, Vector2 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        private const int CROSS_SIZE = 4;
        void AddCross_ValueSpace(Vector2 a)
        {
            AddCross_ScreenSpace(ValueToScreenPos(a));
        }
        void AddCross_ScreenSpace(Vector2 a)
        {
            AddLine_ScreenSpace(a + Vector2.left * CROSS_SIZE, a + Vector2.right * CROSS_SIZE);
            AddLine_ScreenSpace(a + Vector2.down * CROSS_SIZE, a + Vector2.up * CROSS_SIZE);
        }

        public Vector2 ValueToScreenPos(Vector2 point)
        {
            return new Vector2(
                (point.x - ValueDisplayRect.xMin) / ValueDisplayRect.width * ScreenDisplayRect.width + ScreenDisplayRect.x,
                (point.y - ValueDisplayRect.yMin) / ValueDisplayRect.height * ScreenDisplayRect.height + ScreenDisplayRect.y);
        }

        public Vector2 ValueToScreenVector(Vector2 vector)
        {
            return new Vector2(
                vector.x / ValueDisplayRect.width * ScreenDisplayRect.width,
                vector.y / ValueDisplayRect.height * ScreenDisplayRect.height);
        }

        public Vector2 ScreenToValuePos(Vector2 point)
        {
            return new Vector2(
                (point.x - ScreenDisplayRect.xMin) / ScreenDisplayRect.width * ValueDisplayRect.width + ValueDisplayRect.x,
                (point.y - ScreenDisplayRect.yMin) / ScreenDisplayRect.height * ValueDisplayRect.height + ValueDisplayRect.y);
        }

        public Vector2 ScreenToValueVector(Vector2 vector)
        {
            return new Vector2(
                vector.x / ScreenDisplayRect.width * ValueDisplayRect.width,
                vector.y / ScreenDisplayRect.height * ValueDisplayRect.height);
        }
    }

}
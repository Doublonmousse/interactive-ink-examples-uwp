// Copyright @ MyScript. All rights reserved.

using MyScript.IInk.Graphics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class Path : IPath
    {
        private CanvasPathBuilder _pathBuilder;
        private CanvasGeometry _cachedPath; // still valid after _pathBuilder is disposed
        private bool _isInFigure;
        private bool _isConfigured;

        public Path(CanvasDevice device)
        {
            _pathBuilder = new CanvasPathBuilder(device);
            _cachedPath = null;
            _isInFigure = false;
            _isConfigured = false;
        }

        public uint UnsupportedOperations
        {
          get { return (uint)PathOperation.ARC_OPS; }
        }

        public void MoveTo(float x, float y)
        {
            if (!_isConfigured)
            {
                // Settings must be set from Canvas properties and before BeginFigure
                _pathBuilder.SetSegmentOptions(CanvasFigureSegmentOptions.None);
                _pathBuilder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
                _isConfigured = true;
            }

            if (_isInFigure)
            {
                _pathBuilder.EndFigure(CanvasFigureLoop.Open);
            }

            _pathBuilder.BeginFigure(x, y);
            _isInFigure = true;
            _cachedPath = null;
        }

        public void LineTo(float x, float y)
        {
            _pathBuilder.AddLine(x, y);
            _cachedPath = null;
        }

        public void CurveTo(float x1, float y1, float x2, float y2, float x, float y)
        {
            var controlPoint1 = new System.Numerics.Vector2(x1, y1);
            var controlPoint2 = new System.Numerics.Vector2(x2, y2);
            var endPoint = new System.Numerics.Vector2(x, y);

            _pathBuilder.AddCubicBezier(controlPoint1, controlPoint2, endPoint);
            _cachedPath = null;
        }

        public void QuadTo(float x1, float y1, float x, float y)
        {
            var controlPoint = new System.Numerics.Vector2(x1, y1);
            var endPoint = new System.Numerics.Vector2(x, y);

            _pathBuilder.AddQuadraticBezier(controlPoint, endPoint);
            _cachedPath = null;
        }

        public void ArcTo(float rx, float ry, float phi, bool fA, bool fS, float x, float y)
        {
            // not supported, see unsupportedOperations
        }

        public void ClosePath()
        {
            _pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            _isInFigure = false;
            _cachedPath = null;
        }

        public CanvasGeometry CreateGeometry()
        {
            if (_isInFigure)
            {
                _pathBuilder.EndFigure(CanvasFigureLoop.Open);
                _isInFigure = false;
            }
            else if (_cachedPath != null)
            {
                return _cachedPath;
            }
            _cachedPath = CanvasGeometry.CreatePath(_pathBuilder);
            return _cachedPath;
        }
    };
}

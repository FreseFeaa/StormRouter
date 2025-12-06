using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StormRouterVisualization.Services
{
    public class GraphInteractionService
    {
        public TranslateTransform Translate { get; } = new TranslateTransform();
        public ScaleTransform ScaleTransform { get; } = new ScaleTransform(1.0, 1.0);

        private Point? _dragStart = null;
        private Point _initialOffset;

        public void BeginDrag(Point mousePos)
        {
            _dragStart = mousePos;
            _initialOffset = new Point(Translate.X, Translate.Y);
        }

        public void Drag(Point current)
        {
            if (_dragStart == null)
                return;

            double dx = current.X - _dragStart.Value.X;
            double dy = current.Y - _dragStart.Value.Y;

            Translate.X = _initialOffset.X + dx;
            Translate.Y = _initialOffset.Y + dy;
        }

        public void EndDrag()
        {
            _dragStart = null;
        }

        public void Zoom(double delta, Point center)
        {
            double zoomFactor = (delta > 0) ? 1.1 : 0.9;

            double newScale = ScaleTransform.ScaleX * zoomFactor;
            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 5.0) newScale = 5.0;

            // Смещение относительно точки зума
            double scaleRatio = newScale / ScaleTransform.ScaleX;

            Translate.X = center.X - (center.X - Translate.X) * scaleRatio;
            Translate.Y = center.Y - (center.Y - Translate.Y) * scaleRatio;

            ScaleTransform.ScaleX = newScale;
            ScaleTransform.ScaleY = newScale;
        }

        public void Reset()
        {
            ScaleTransform.ScaleX = ScaleTransform.ScaleY = 1.0;
            Translate.X = Translate.Y = 0.0;
        }
    }
}

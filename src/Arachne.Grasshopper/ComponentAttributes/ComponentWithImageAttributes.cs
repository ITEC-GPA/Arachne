using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Arachne.Grasshopper.ComponentAttributes
{
    public class ComponentWithImageAttributes : GH_ComponentAttributes
    {
        protected Bitmap _bitmap;
        protected Bitmap _overlappedBitmap;
        protected PointF _overlappedPosition;

        public Bitmap Bitmap
        {
            get => _bitmap;
            set => _bitmap = value;
        }

        public ComponentWithImageAttributes(GH_Component owner, Bitmap bitmap)
            : base (owner)
        {
            _bitmap = bitmap;
            _overlappedPosition = PointF.Empty;
        }

        public void AddOverlappedBitmap(PointF position, Bitmap bitmap)
        {
            _overlappedPosition = position;
            _overlappedBitmap = bitmap;
        }

        protected override void Layout()
        {
            base.Layout();

            if (_bitmap != null)
            {
                Rectangle rec = GH_Convert.ToRectangle(Bounds);
                rec.Height += _bitmap.Height + 5;
                if (_bitmap.Width > rec.Width)
                {
                    int delta = _bitmap.Width + 5 - rec.Width;
                    rec.Width = _bitmap.Width + 5;
                    // Moves the outer param: https://github.com/ksteinfe/dyear/blob/master/CSProj/Heatmap.cs                    
                    Rectangle rec_out = GH_Convert.ToRectangle(Owner.Params.Output[0].Attributes.Bounds);
                    rec_out.X += delta;
                    Owner.Params.Output[0].Attributes.Bounds = rec_out;
                }

                Bounds = rec;                
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // https://discourse.mcneel.com/t/creating-custom-diagrams-as-icons-within-components-like-the-trigonometry-one/74633/3
            // https://developer.rhino3d.com/api/grasshopper/html/8a7974ab-7b2b-4f48-84d0-6e81b184e6b0.htm
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects && _bitmap != null)
            {
                PointF iconLocation = new PointF(Bounds.Location.X + (Bounds.Width / 2 - _bitmap.Width / 2), 
                    Bounds.Location.Y + Bounds.Height - _bitmap.Height - 5);
                graphics.DrawImage(_bitmap, new RectangleF(iconLocation, new SizeF(_bitmap.Width, _bitmap.Height)));

                if (_overlappedPosition != PointF.Empty)
                {
                    float x = _overlappedPosition.X >= 0 ? _overlappedPosition.X : Bounds.Width + _overlappedPosition.X;
                    float y = _overlappedPosition.Y >= 0 ? _overlappedPosition.Y : Bounds.Height + _overlappedPosition.Y;
                    iconLocation = new PointF(Bounds.Location.X + x, Bounds.Location.Y + y);
                    graphics.DrawImage(_overlappedBitmap, new RectangleF(iconLocation,
                        new SizeF(_overlappedBitmap.Width, _overlappedBitmap.Height)));
                }
            }
        }
    }
}

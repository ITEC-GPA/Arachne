using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Arachne.Grasshopper.ComponentAttributes
{
    public class ComponentNButtonsAttributes : GH_ComponentAttributes
    {
        private const int BTN_HEIGHT = 22;
        private const int PADDING = 2;

        public class ButtonBase
        {
            public enum ButtonType
            {
                BUTTON,
                TOGGLE
            }

            public ButtonType Type { get; }
            public bool Active { get; set; }
            public Rectangle Bounds { get; set; }
            public string Text { get; set; }

            public ButtonBase(string text, ButtonType type = 0, bool active = false) {
                Text = text;
                Type = type;
                Active = active;
            }
        }

        public List<ButtonBase> Buttons { get; set; }
        public delegate void ButtonPressedHandler(int i);
        public event ButtonPressedHandler ButtonPressed;


        public ComponentNButtonsAttributes(GH_Component owner, List<ButtonBase> buttons)
            : base(owner)
        {
            Buttons = buttons;
        }

        protected override void Layout()
        {
            base.Layout();

            Rectangle rec0 = GH_Convert.ToRectangle(Bounds);
            Rectangle rec = rec0;
            rec.Y += PADDING + rec.Height;
            rec.Height = BTN_HEIGHT;

            foreach (ButtonBase btn in Buttons) {
                btn.Bounds = rec;
                rec.Y += PADDING + BTN_HEIGHT;
            }

            Rectangle rec1 = Buttons[Buttons.Count - 1].Bounds;
            rec0.Height = rec1.Bottom - rec0.Y;
            rec0.Inflate(2, 2);

            Bounds = rec0;
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                foreach (ButtonBase btn in Buttons)
                {
                    switch (btn.Type) {
                        case ButtonBase.ButtonType.BUTTON:
                            GH_Capsule button = GH_Capsule.CreateTextCapsule(btn.Bounds, btn.Bounds, GH_Palette.Black, btn.Text, 2, 0);
                            button.Render(graphics, Selected, Owner.Locked, false);
                            button.Dispose();
                            break;
                        case ButtonBase.ButtonType.TOGGLE:
                            var checkBounds = btn.Bounds;
                            checkBounds.Width = checkBounds.Height;

                            var textBounds = btn.Bounds;
                            var shiftX = checkBounds.Height + PADDING;
                            textBounds.Width -= shiftX;
                            textBounds.X += shiftX;


                            GH_Capsule toggle = GH_Capsule.CreateTextCapsule(btn.Bounds, textBounds, GH_Palette.Black, btn.Text, 2, 0);
                            toggle.Render(graphics, Selected, Owner.Locked, false);
                            toggle.Dispose();

                            checkBounds.Inflate(-4, -4);
                            GH_Capsule checkOut = GH_Capsule.CreateCapsule(checkBounds, GH_Palette.White, 100, 0);
                            checkOut.Render(graphics, Selected, Owner.Locked, false);
                            checkOut.Dispose();

                            if (btn.Active) {
                                var innerBounds = checkBounds;
                                innerBounds.Inflate(-2, -2);
                                GH_Capsule checkIn = GH_Capsule.CreateCapsule(innerBounds, GH_Palette.Black, 100, 0);
                                checkIn.Render(graphics, Selected, Owner.Locked, false);
                                checkIn.Dispose();
                            }
                            break;

                    }

                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                for(int i = 0; i < Buttons.Count; ++i)
                {
                    var btn = Buttons[i];
                    RectangleF rec = btn.Bounds;
                    if (rec.Contains(e.CanvasLocation))
                    {
                        if (ButtonPressed != null)
                        {
                            ButtonPressed(i);
                        }

                        return GH_ObjectResponse.Handled;
                    }
                }
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}

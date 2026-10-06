using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Forms;
using Arachne.Grasshopper.Helpers;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Arachne.Grasshopper.Components.Parameters
{
    public class LoadCombinationsParam : GH_PersistentParam<GH_LoadCombination>
    {
        protected List<LoadCaseModel> _loadCases;

        public List<LoadCaseModel> LoadCaseList => _loadCases;

        public LoadCombinationsParam()
            : base("Load Combinations", "LCS", "The load combinations definition", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PARAMETERS)
        {
            _loadCases = new List<LoadCaseModel>();
        }

        public void UpdateLoadCases(IEnumerable<LoadCaseModel> loadCases)
        {
            // Recreate the LoadCase names list
            _loadCases.Clear();
            foreach (LoadCaseModel loadCase in loadCases)
            {
                if (_loadCases.Count(lc => lc.Name == loadCase.Name) == 0)
                    _loadCases.Add(loadCase);
            }

            if (VolatileDataCount == 0)
                return;

            // Update the exiting combinations
            bool changed = false;
            List<GH_LoadCombination> combinations = (List<GH_LoadCombination>)VolatileData.get_Branch(0);

            foreach (GH_LoadCombination combo in combinations)
            {
                // Remove the non existing values
                LoadCaseModel[] to_remove = combo.Value.Values.
                    Where(v => !loadCases.Contains(v.Key, new Common.Helpers.LoadCaseModelsEqualityComparer())).
                    Select(c => c.Key).ToArray();
                foreach (LoadCaseModel lc in to_remove)
                    combo.Value.Values.Remove(lc);
                if (to_remove.Length > 0)
                    changed = true;

                foreach (LoadCaseModel loadCase in loadCases)
                {
                    if (!combo.Value.Values.Keys.Contains(loadCase, new Common.Helpers.LoadCaseModelsEqualityComparer()))
                        combo.Value.Values.Add(loadCase, 1);
                }
            }

            if (changed)
            {
                PersistentData.Clear();
                SetPersistentData(combinations);
                ExpireSolution(true);
            }
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_LoadCombination> values)
        {
            return GH_GetterResult.cancel;
        }

        protected override GH_GetterResult Prompt_Singular(ref GH_LoadCombination value)
        {
            return GH_GetterResult.success;
        }

        public override void CreateAttributes()
        {
            m_attributes = new LoadCombinationsAttributes(this);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override Guid ComponentGuid => new Guid("12666a01-d9d7-4ed6-ac5a-010e24c7fe34");

        protected override Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);
    }

    internal class LoadCombinationsAttributes : GH_ResizableAttributes<LoadCombinationsParam>
    {
        private Rectangle _internalRect;

        protected override Size MinimumSize => new Size(120, 60);

        protected override Size MaximumSize { get { return new Size(730, 240); } }

        protected override Padding SizingBorders { get { return new Padding(10); } }

        public LoadCombinationsAttributes(LoadCombinationsParam owner)
            : base(owner)
        {
            Bounds = new RectangleF(Pivot, new Size(150, 80));
        }

        protected override void Layout()
        {
            base.Layout();
            _internalRect = GH_Convert.ToRectangle(Bounds);
            _internalRect.Inflate(-5, -5);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Objects)
            {
                GH_Palette palette = GH_Palette.Normal;

                // Adjust palette based on the Owner's worst case messaging level.
                switch (Owner.RuntimeMessageLevel)
                {
                    case GH_RuntimeMessageLevel.Warning:
                        palette = GH_Palette.Warning;
                        break;

                    case GH_RuntimeMessageLevel.Error:
                        palette = GH_Palette.Error;
                        break;
                }

                RectangleF capsuleBox = new RectangleF(Bounds.Location, Bounds.Size);

                GH_Capsule capsule = GH_Capsule.CreateCapsule(capsuleBox, palette);
                capsule.AddOutputGrip(OutputGrip.Y);
                capsule.Render(graphics, Selected, Owner.Locked, false);
                capsule.Dispose();

                using (Pen pen = new Pen(Color.Black))
                    graphics.DrawRectangle(pen, _internalRect);

                StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                RectangleF textRectangle = Bounds;
                textRectangle.Y += 2;
                textRectangle.Height = 20;
                textRectangle.Inflate(-4, 0);
                graphics.DrawString("Combinations", GH_FontServer.Standard, Brushes.Black, textRectangle, format);

                if (!Owner.VolatileData.IsEmpty)
                {
                    format.Alignment = StringAlignment.Near;
                    textRectangle.X += 2;
                    IList<GH_LoadCombination> list = (IList<GH_LoadCombination>)Owner.VolatileData.get_Branch(0);
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == null)
                            continue;
                        textRectangle.Y += 20;
                        if (textRectangle.Y + textRectangle.Height + 20 > Bounds.Y + Bounds.Height)
                        {
                            graphics.DrawString("etc.", GH_FontServer.Standard, Brushes.Black, textRectangle,
                            format);
                            break;
                        }
                        graphics.DrawString(list[i].ToString(), GH_FontServer.Standard, Brushes.Black, textRectangle,
                            format);
                    }
                }
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                List<GH_LoadCombination> list = null;
                if (!Owner.VolatileData.IsEmpty)
                    list = (List<GH_LoadCombination>)Owner.VolatileData.get_Branch(0);
                LoadCombinationsForm form = new LoadCombinationsForm(Owner.LoadCaseList.ToArray(), list);
                GH_WindowsFormUtil.CenterFormOnCursor(form, true);
                if (form.ShowDialog(Instances.DocumentEditor) == DialogResult.OK)
                {
                    Owner.PersistentData.Clear();
                    Owner.SetPersistentData(form.Values);
                    Owner.ExpireSolution(true);
                }
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDoubleClick(sender, e);
        }
    }
}

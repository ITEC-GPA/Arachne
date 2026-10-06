using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Arachne.Grasshopper.Components.Parameters
{
    public class CoordinateSystemParam : GH_PersistentParam<GH_CoordinateSystem>
    {
        public CoordinateSystemParam()
            : base("CoordinateSystem", "CS", "The model coordinate system", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PARAMETERS)
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_CoordinateSystem> values)
        {
            return GH_GetterResult.cancel;
        }

        protected override GH_GetterResult Prompt_Singular(ref GH_CoordinateSystem value)
        {
            return GH_GetterResult.success;
        }

        public override bool AppendMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendObjectNameEx(menu);
            Menu_AppendWireDisplay(menu);
            Menu_AppendDisconnectWires(menu);
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Local", (s, e) => { PersistentData.Clear(); ExpireSolution(true); }, true,
                PersistentData.DataCount == 0 ? true : false);
            Menu_AppendItem(menu, "Global", (s, e) => { SetPersistentData(new GH_CoordinateSystem()); ExpireSolution(true); }, true,
                PersistentData.DataCount == 0 ? false : true);

            Menu_AppendSeparator(menu);
            Menu_AppendDestroyPersistent(menu);
            Menu_AppendInternaliseData(menu);

            return true;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        public override Guid ComponentGuid => new Guid("c1a59f62-2d20-4f66-aeb0-38999eb77b7a");

    }
}

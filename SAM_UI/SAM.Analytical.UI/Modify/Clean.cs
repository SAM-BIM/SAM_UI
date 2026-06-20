using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void Clean(this UIAnalyticalModel uIAnalyticalModel, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            analyticalModel = Query.Clean(analyticalModel);
            if(analyticalModel == null)
            {
                return;
            }

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }
    }
}
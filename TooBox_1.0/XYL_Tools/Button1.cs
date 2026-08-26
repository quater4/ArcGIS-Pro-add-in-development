using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XYL_Tools
{
    internal class Button1 : Button
    {
        protected override void OnClick()
        {
            var dockPane = FrameworkApplication.DockPaneManager.Find("XYL_Tools_DataCheckDockPane");
            dockPane?.Activate();
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;

namespace KMep.PlumbingPro
{
    public sealed class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            const string tabName = "KMep";
            try { application.CreateRibbonTab(tabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { /* Already exists */ }

            // Re-use only a panel belonging to KMep; do not touch KStruct.
            RibbonPanel panel = application.GetRibbonPanels(tabName)
                .FirstOrDefault(p => p.Name == "PlumbingPro")
                ?? application.CreateRibbonPanel(tabName, "PlumbingPro");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (!panel.GetItems().Any(x => x.Name == "KMep.RouteColdWater"))
            {
                var data = new PushButtonData("KMep.RouteColdWater", "Cấp lạnh\nỐng nhánh", assemblyPath,
                    typeof(RouteColdWaterCommand).FullName);
                data.ToolTip = "Chọn ống chính, rồi chọn thiết bị có đầu nối nước lạnh để tạo đoạn ống nhánh thử nghiệm.";
                panel.AddItem(data);
            }
            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication application) { return Result.Succeeded; }
    }
}

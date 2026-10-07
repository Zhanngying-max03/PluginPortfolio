using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace PluginPortfolio
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command02 : IExternalCommand
    {
        private const string MODULE = "P02_Analysis";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                if (commandData == null) { message = "未获取到 Revit 上下文"; return Result.Failed; }
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = (uidoc == null) ? null : uidoc.Document;
                if (doc == null) { message = "未找到活动文档"; return Result.Failed; }

                IntPtr revitHandle = IntPtr.Zero;
                try { revitHandle = uiapp.MainWindowHandle; } catch { }
                if (revitHandle == IntPtr.Zero)
                {
                    try { revitHandle = Process.GetCurrentProcess().MainWindowHandle; }
                    catch { }
                }

                AppLogger.Info(MODULE, "启动多维度工程量分析窗口");
                // 宿主(Revit/其他插件)可能已设过 ResourceAssembly，二次赋值会抛异常，安全跳过
                try
                {
                    if (System.Windows.Application.ResourceAssembly == null)
                    {
                        System.Windows.Application.ResourceAssembly = typeof(MainWindow).Assembly;
                    }
                }
                catch { /* 已设过，忽略 */ }
                MainWindow window = new MainWindow(doc, uidoc);

                if (revitHandle != IntPtr.Zero
                    && new WindowInteropHelper(window).Owner == IntPtr.Zero)
                {
                    new WindowInteropHelper(window).Owner = revitHandle;
                }

                bool? r = window.ShowDialog();
                window.ClearHighlightOnClose();

                return r == true ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                AppLogger.Error(MODULE, "启动失败", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                try
                {
                    TaskDialog td = new TaskDialog("插件错误");
                    td.MainInstruction = "多维度工程量分析启动失败";
                    td.MainContent = ex.Message;
                    td.ExpandedContent = ex.StackTrace + "\n\n日志：" + AppLogger.CurrentLogFile;
                    td.Show();
                }
                catch { }
                return Result.Failed;
            }
        }
    }
}

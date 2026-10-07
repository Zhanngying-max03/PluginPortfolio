using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace PluginPortfolio
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command03 : IExternalCommand
    {
        private const string MODULE = "P03_Semantic";

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

                string dllDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string cfgPath = JsonConfigLoader.SuggestConfigPath(dllDir,
                    "semanticfilter.appsettings.json");
                JsonConfigLoader.SemanticFilterConfig cfg =
                    JsonConfigLoader.LoadSemanticFilterConfig(cfgPath);
                AppLogger.Info(MODULE, string.Format(
                    "读取配置：model={0}, timeoutMs={1}, apiKey空={2}",
                    cfg.ModelName, cfg.TimeoutMs,
                    string.IsNullOrEmpty(cfg.DeepSeekApiKey) ? "是" : "否(后4位="
                        + (cfg.DeepSeekApiKey.Length > 4
                            ? cfg.DeepSeekApiKey.Substring(cfg.DeepSeekApiKey.Length - 4)
                            : cfg.DeepSeekApiKey) + ")"));

                // 宿主(Revit/其他插件)可能已设过 ResourceAssembly，二次赋值会抛异常，安全跳过
                try
                {
                    if (System.Windows.Application.ResourceAssembly == null)
                    {
                        System.Windows.Application.ResourceAssembly = typeof(MainWindows).Assembly;
                    }
                }
                catch { /* 已设过，忽略 */ }
                MainWindows window = new MainWindows(doc, uidoc, revitHandle, cfg);
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
                    td.MainInstruction = "语义筛选器启动失败";
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

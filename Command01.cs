using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PluginPortfolio
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command01 : IExternalCommand
    {
        private const string MODULE = "P01_Counter";

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

                AppLogger.Info(MODULE, "开始执行：墙门窗批量统计");

                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(l => l.Elevation).ToList();
                Dictionary<ElementId, string> lvDict = levels.ToDictionary(l => l.Id, l => l.Name);

                var cats = new Dictionary<string, BuiltInCategory>
                {
                    { "墙", BuiltInCategory.OST_Walls },
                    { "门", BuiltInCategory.OST_Doors },
                    { "窗", BuiltInCategory.OST_Windows },
                };

                var headers = new List<string> { "类别", "数量", "总面积 ㎡", "总体积 m³", "标高明细" };
                var rows = new List<List<string>>();

                double totalAreaM2 = 0.0, totalVolM3 = 0.0;
                int totalCount = 0;

                foreach (var cat in cats)
                {
                    List<Element> list = new FilteredElementCollector(doc)
                        .OfCategory(cat.Value)
                        .WhereElementIsNotElementType()
                        .ToList();

                    bool isHost = (cat.Value == BuiltInCategory.OST_Walls);

                    int count = list.Count;
                    double area = list.Sum(x => RevitElementUtils.GetHostAreaRaw(x));
                    double vol = list.Sum(x => RevitElementUtils.GetHostVolumeRaw(x));
                    double areaM2 = area * UnitConverter.SQ_FT_TO_SQ_M;
                    double volM3 = vol * UnitConverter.CUBIC_FT_TO_CUBIC_M;

                    totalCount += count;
                    if (isHost) { totalAreaM2 += areaM2; totalVolM3 += volM3; }

                    var byLv = list.GroupBy(x =>
                    {
                        ElementId id = RevitElementUtils.GetLevelId(x);
                        return (id != null && lvDict.ContainsKey(id)) ? lvDict[id] : "未关联标高";
                    }).OrderBy(g => g.Key).ToList();

                    StringBuilder detail = new StringBuilder();
                    foreach (var g in byLv)
                        detail.Append((string)g.Key).Append(" x").Append(g.Count().ToString()).Append(" | ");
                    if (detail.Length >= 3) detail.Length -= 3;

                    rows.Add(new List<string>
                    {
                        cat.Key,
                        count.ToString(),
                        isHost ? areaM2.ToString("F2") : UnitConverter.N_A,
                        isHost ? volM3.ToString("F2") : UnitConverter.N_A,
                        detail.ToString(),
                    });
                }

                rows.Add(new List<string>
                {
                    "合计",
                    totalCount.ToString(),
                    totalAreaM2.ToString("F2"),
                    totalVolM3.ToString("F2"),
                    "文档: " + doc.Title
                });

                string info = string.Format("统计完成：合计 {0} 项，面积 {1} ㎡，体积 {2} m³",
                    totalCount, totalAreaM2.ToString("F2"), totalVolM3.ToString("F2"));
                AppLogger.Info(MODULE, info);

                StringBuilder sb = new StringBuilder();
                foreach (var r in rows)
                {
                    string line = string.Join("   |   ",
                        ((string)r[0]).PadRight(8),
                        ((string)r[1]).PadLeft(6),
                        ((string)r[2]).PadLeft(12),
                        ((string)r[3]).PadLeft(12),
                        (string)r[4]);
                    sb.AppendLine(line);
                }

                string csvPath = CsvExport.BuildDesktopFileName("P01_Counter", "Summary");
                using (StreamWriter sw = CsvExport.OpenWriter(csvPath))
                {
                    CsvExport.WriteRow(sw, headers.ToArray());
                    foreach (var r in rows)
                        CsvExport.WriteRow(sw, r.ToArray());
                }

                TaskDialog td = new TaskDialog("插件一 · 墙门窗批量统计");
                td.MainInstruction = info;
                td.MainContent = sb.ToString();
                td.ExpandedContent = "CSV 已导出到：\n" + csvPath
                    + "\n\n日志：%Temp%\\RevitPortfolio\\RevitPortfolio_YYYYMMDD.log";
                td.CommonButtons = TaskDialogCommonButtons.Ok;
                td.Show();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException rex)
            {
                AppLogger.Error(MODULE, "Revit API 异常", rex);
                message = "Revit 内部错误：" + rex.Message;
                return Result.Failed;
            }
            catch (IOException iox)
            {
                AppLogger.Error(MODULE, "CSV 写入失败（可能被 Excel 占用）", iox);
                TaskDialog.Show("CSV 写入失败",
                    "文件被占用，请先关闭占用该 CSV 的 Excel：\n" + iox.Message);
                return Result.Failed;
            }
            catch (InvalidOperationException ioex)
            {
                AppLogger.Error(MODULE, "集合遍历异常", ioex);
                message = "数据遍历出错：" + ioex.Message;
                return Result.Failed;
            }
            catch (Exception ex)
            {
                AppLogger.Error(MODULE, "未预期异常", ex);
                message = "发生未知错误：" + ex.Message;
                return Result.Failed;
            }
        }
    }
}

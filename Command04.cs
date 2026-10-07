using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PluginPortfolio
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command04 : IExternalCommand
    {
        private const string MODULE = "P04_NameCheck";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = (uidoc == null) ? null : uidoc.Document;
                if (doc == null) { message = "未找到活动文档"; return Result.Failed; }

                AppLogger.Info(MODULE, "开始：族/类型命名规范检查");

                string dllDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string cfgPath = JsonConfigLoader.SuggestConfigPath(dllDir,
                    "namechecker.rules.json");
                JsonConfigLoader.NameCheckerConfig cfg =
                    JsonConfigLoader.LoadNameCheckerConfig(cfgPath);

                Regex badRegex;
                try { badRegex = new Regex(cfg.ForbiddenRegexChars); }
                catch (ArgumentException rex)
                {
                    AppLogger.Error(MODULE, "rules.json 中 ForbiddenRegexChars 非法，回退默认", rex);
                    badRegex = new Regex(
                        @"[*?/\\:""<>\|\s]+|[，。！？、；：""''（）【】《》￥…—]");
                }
                int maxLen = cfg.MaxNameLength <= 0 ? 50 : cfg.MaxNameLength;

                Dictionary<BuiltInCategory, List<string>> prefixRules =
                    BuildPrefixRules(cfg.CategoryPrefixRules);

                AppLogger.Info(MODULE, string.Format(
                    "规则加载完成：禁止字符正则长度={0}，最大长度={1}，前缀规则条数={2}",
                    cfg.ForbiddenRegexChars == null ? 0 : cfg.ForbiddenRegexChars.Length,
                    maxLen, prefixRules.Count));

                List<ViolationItem> violations = new List<ViolationItem>();

                FilteredElementCollector families =
                    new FilteredElementCollector(doc).OfClass(typeof(Family));
                foreach (Element f in families)
                {
                    Family fam = f as Family;
                    if (fam == null) continue;
                    Category cat = fam.FamilyCategory;
                    BuiltInCategory? bic = (cat == null)
                        ? (BuiltInCategory?)null
                        : (BuiltInCategory)cat.Id.IntegerValue;

                    List<string> reasons = CheckName(fam.Name, true, bic, badRegex,
                        maxLen, prefixRules);
                    if (reasons.Count > 0)
                        violations.Add(new ViolationItem(
                            (cat == null || cat.Name == null) ? "—" : cat.Name,
                            "族", fam.Name == null ? "(空)" : fam.Name,
                            SuggestName(fam.Name, true, bic, badRegex, maxLen, prefixRules),
                            string.Join("；", reasons),
                            fam.Id.IntegerValue.ToString(), fam.Id));
                }

                FilteredElementCollector symbols =
                    new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));
                foreach (Element s in symbols)
                {
                    FamilySymbol fs = s as FamilySymbol;
                    if (fs == null) continue;
                    Category cat = fs.Category;
                    BuiltInCategory? bic = (cat == null)
                        ? (BuiltInCategory?)null
                        : (BuiltInCategory)cat.Id.IntegerValue;
                    List<string> reasons = CheckName(fs.Name, false, bic, badRegex,
                        maxLen, prefixRules);
                    if (reasons.Count > 0)
                        violations.Add(new ViolationItem(
                            (cat == null || cat.Name == null) ? "—" : cat.Name,
                            "类型", fs.Name == null ? "(空)" : fs.Name,
                            SuggestName(fs.Name, false, bic, badRegex, maxLen, prefixRules),
                            string.Join("；", reasons),
                            fs.Id.IntegerValue.ToString(), fs.Id));
                }

                AppLogger.Info(MODULE, string.Format(
                    "检查完成，违规数：{0}", violations.Count));

                int famCount = 0, typeCount = 0;
                Dictionary<string, int> byCat = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                List<ElementId> related = new List<ElementId>();
                foreach (ViolationItem v in violations)
                {
                    if (v.Kind == "族") famCount++;
                    else typeCount++;
                    if (!byCat.ContainsKey(v.CategoryName))
                        byCat[v.CategoryName] = 0;
                    byCat[v.CategoryName]++;
                    if (v.RelatedId != null && v.RelatedId != ElementId.InvalidElementId
                        && !related.Contains(v.RelatedId))
                        related.Add(v.RelatedId);
                }

                if (uidoc != null && related.Count > 0)
                {
                    try { uidoc.Selection.SetElementIds(related); }
                    catch (Exception ex)
                    { AppLogger.Warn(MODULE, "同步选中集失败：" + ex.Message); }
                }

                string csvPath = CsvExport.BuildDesktopFileName("命名规范", "违规清单");
                using (StreamWriter sw = CsvExport.OpenWriter(csvPath))
                {
                    CsvExport.WriteRow(sw, "类别", "对象种类", "原名称", "建议修正名",
                        "违规原因", "关联 ElementId");
                    foreach (ViolationItem v in violations)
                    {
                        CsvExport.WriteRow(sw,
                            CsvExport.Escape(v.CategoryName),
                            CsvExport.Escape(v.Kind),
                            CsvExport.Escape(v.Original),
                            CsvExport.Escape(v.Suggestion),
                            CsvExport.Escape(v.Reason),
                            v.ElemId);
                    }
                }
                AppLogger.Info(MODULE, "CSV 导出：" + csvPath + "（" + violations.Count + "行）");

                StringBuilder sb = new StringBuilder();
                sb.Append(string.Format("✅ 命名规范检查完成，共 {0} 条违规\n",
                    violations.Count));
                sb.Append(string.Format("  · 族名违规：{0} 条\n", famCount));
                sb.Append(string.Format("  · 类型名违规：{0} 条\n\n", typeCount));
                sb.Append("—— 按类别拆分 ——\n");
                List<KeyValuePair<string, int>> sortedCat = byCat
                    .OrderByDescending(pair => pair.Value).ToList();
                foreach (KeyValuePair<string, int> pair in sortedCat)
                    sb.Append(string.Format("  · {0}：{1} 条\n", pair.Key, pair.Value));
                sb.Append("\n—— 前 20 条明细 ——\n");
                for (int i = 0; i < Math.Min(20, violations.Count); i++)
                {
                    ViolationItem v = violations[i];
                    sb.Append(string.Format(
                        "{0}. [{1}/{2}] {3}\n     原因：{4}\n     建议：{5}\n",
                        i + 1, v.CategoryName, v.Kind,
                        Truncate(v.Original, 60), v.Reason, v.Suggestion));
                }
                if (violations.Count > 20)
                    sb.Append(string.Format("\n（其余 {0} 条见导出的 CSV）\n",
                        violations.Count - 20));
                sb.Append("\n📁 完整违规清单已导出到桌面：\n").Append(csvPath);
                sb.Append("\n📝 日志：").Append(AppLogger.CurrentLogFile);
                if (related.Count > 0)
                    sb.Append("\n\n🎯 已在 Revit 中选中涉及违规的 Family/ FamilySymbol，便于定位修改");

                TaskDialog tdDlg = new TaskDialog("命名规范批量检查 v1.0");
                tdDlg.MainInstruction = string.Format("发现 {0} 条命名违规",
                    violations.Count);
                tdDlg.MainContent = sb.ToString();
                tdDlg.CommonButtons = TaskDialogCommonButtons.Ok;
                tdDlg.Show();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException rvtex)
            {
                AppLogger.Error(MODULE, "Revit API 状态异常", rvtex);
                message = rvtex.Message;
                return Result.Failed;
            }
            catch (IOException ioex)
            {
                AppLogger.Error(MODULE, "CSV 写出 IO 异常", ioex);
                message = "导出 CSV 失败：" + ioex.Message;
                try
                {
                    TaskDialog.Show("导出错误",
                        "无法写出 CSV，通常是目标文件被 Excel 占用。\n请关闭后重试。\n\n"
                        + ioex.Message);
                }
                catch { }
                return Result.Failed;
            }
            catch (ArgumentException aex)
            {
                AppLogger.Error(MODULE, "配置文件参数异常（正则/字典 key）", aex);
                message = aex.Message;
                try
                {
                    TaskDialog.Show("配置错误",
                        "namechecker.rules.json 格式或正则有错误，已回退默认规则。\n\n"
                        + aex.Message + "\n\n日志：" + AppLogger.CurrentLogFile);
                }
                catch { }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                AppLogger.Error(MODULE, "未分类异常", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                try
                {
                    TaskDialog td = new TaskDialog("插件错误");
                    td.MainInstruction = "命名规范检查失败";
                    td.MainContent = ex.Message;
                    td.ExpandedContent = ex.StackTrace + "\n\n日志：" + AppLogger.CurrentLogFile;
                    td.Show();
                }
                catch { }
                return Result.Failed;
            }
        }

        private static Dictionary<BuiltInCategory, List<string>> BuildPrefixRules(
            Dictionary<string, List<string>> cfgRules)
        {
            Dictionary<BuiltInCategory, List<string>> r =
                new Dictionary<BuiltInCategory, List<string>>();
            if (cfgRules == null || cfgRules.Count == 0)
            {
                AddDefaultRule(r, BuiltInCategory.OST_Walls,
                    new string[] { "墙", "W_", "W -" });
                AddDefaultRule(r, BuiltInCategory.OST_Doors, new string[] { "门", "M" });
                AddDefaultRule(r, BuiltInCategory.OST_Windows, new string[] { "窗", "C" });
                AddDefaultRule(r, BuiltInCategory.OST_Floors,
                    new string[] { "楼板", "FL_", "FL -" });
                AddDefaultRule(r, BuiltInCategory.OST_Columns,
                    new string[] { "柱", "Z_", "Z-" });
                AddDefaultRule(r, BuiltInCategory.OST_StructuralFraming,
                    new string[] { "梁", "KL", "L" });
                AddDefaultRule(r, BuiltInCategory.OST_Furniture,
                    new string[] { "家具", "F_", "F-" });
                AddDefaultRule(r, BuiltInCategory.OST_Ceilings,
                    new string[] { "天花", "吊顶", "CL_" });
                AddDefaultRule(r, BuiltInCategory.OST_Roofs,
                    new string[] { "屋顶", "RF_", "RF-" });
                AddDefaultRule(r, BuiltInCategory.OST_Stairs,
                    new string[] { "楼梯", "ST_", "ST-" });
                return r;
            }

            foreach (KeyValuePair<string, List<string>> kv in cfgRules)
            {
                try
                {
                    BuiltInCategory bic = (BuiltInCategory)Enum.Parse(
                        typeof(BuiltInCategory), kv.Key, true);
                    List<string> prefixes = kv.Value ?? new List<string>();
                    if (r.ContainsKey(bic))
                    {
                        foreach (string p in prefixes)
                            if (!r[bic].Contains(p)) r[bic].Add(p);
                    }
                    else
                        r[bic] = prefixes;
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(MODULE, "无法识别前缀规则 key："
                        + kv.Key + "：" + ex.Message);
                }
            }
            return r;
        }

        private static void AddDefaultRule(
            Dictionary<BuiltInCategory, List<string>> r,
            BuiltInCategory bic, string[] prefixes)
        {
            r[bic] = new List<string>(prefixes);
        }

        private static List<string> CheckName(
            string name,
            bool isFamily,
            BuiltInCategory? bic,
            Regex badRegex,
            int maxLen,
            Dictionary<BuiltInCategory, List<string>> prefixRules)
        {
            List<string> reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                reasons.Add("名称为空或只有空白字符");
                return reasons;
            }
            if (name.Length > maxLen)
                reasons.Add("名称过长（> " + maxLen + " 字符，" + name.Length + "）");

            MatchCollection badMatches = badRegex.Matches(name);
            if (badMatches.Count > 0)
            {
                HashSet<char> chars = new HashSet<char>();
                foreach (Match m in badMatches)
                {
                    string t = m.Value;
                    for (int i = 0; i < t.Length; i++) chars.Add(t[i]);
                }
                List<string> arr = new List<string>();
                foreach (char c in chars) arr.Add("'" + c + "'");
                reasons.Add("含禁止字符：" + string.Join("、", arr));
            }

            if (bic.HasValue && prefixRules.ContainsKey(bic.Value))
            {
                List<string> allowed = prefixRules[bic.Value];
                bool ok = false;
                if (allowed != null)
                {
                    foreach (string p in allowed)
                    {
                        if (string.IsNullOrEmpty(p)) continue;
                        if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        { ok = true; break; }
                    }
                }
                if (!ok)
                {
                    string list = (allowed == null || allowed.Count == 0)
                        ? "(未配置)"
                        : string.Join("/", allowed);
                    reasons.Add((isFamily ? "族名" : "类型名")
                        + "未使用类别合规前缀（允许：" + list + "）");
                }
            }

            return reasons;
        }

        private static string SuggestName(
            string name,
            bool isFamily,
            BuiltInCategory? bic,
            Regex badRegex,
            int maxLen,
            Dictionary<BuiltInCategory, List<string>> prefixRules)
        {
            if (string.IsNullOrEmpty(name)) return "(请重新命名)";
            string s = name;
            s = badRegex.Replace(s, "_");
            while (s.IndexOf("__", StringComparison.Ordinal) >= 0)
                s = s.Replace("__", "_");
            s = s.Trim('_', ' ', '\t');
            if (s.Length > maxLen) s = s.Substring(0, maxLen);

            if (bic.HasValue && prefixRules.ContainsKey(bic.Value))
            {
                List<string> allowed = prefixRules[bic.Value];
                if (allowed != null && allowed.Count > 0)
                {
                    bool hasPrefix = false;
                    foreach (string p in allowed)
                    {
                        if (string.IsNullOrEmpty(p)) continue;
                        if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        { hasPrefix = true; break; }
                    }
                    if (!hasPrefix) s = allowed[0] + s;
                }
            }
            return s;
        }

        private static string Truncate(string s, int len)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= len) return s;
            return s.Substring(0, len) + "...";
        }
    }

    public class ViolationItem
    {
        public string CategoryName;
        public string Kind;
        public string Original;
        public string Suggestion;
        public string Reason;
        public string ElemId;
        public ElementId RelatedId;

        public ViolationItem(string cat, string kind, string orig,
            string sug, string reason, string id, ElementId rid)
        {
            CategoryName = cat; Kind = kind; Original = orig;
            Suggestion = sug; Reason = reason; ElemId = id; RelatedId = rid;
        }
    }
}

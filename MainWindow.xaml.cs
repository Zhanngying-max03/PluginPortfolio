using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PluginPortfolio
{
    public partial class MainWindow : Window
    {
        private const string MODULE = "P02_Analysis";

        private Document _doc;
        private UIDocument _uiDoc;
        private List<ElementInfoRow> _rows;

        public MainWindow(Document doc, UIDocument uiDoc)
        {
            InitializeComponent();
            _doc = doc;
            _uiDoc = uiDoc;
            _rows = new List<ElementInfoRow>();
            BuildCategoryCheckBoxes();
            LoadLevelCheckboxes();
        }

        private void BuildCategoryCheckBoxes()
        {
            CategoriesPanel.Children.Clear();
            TextBlock title = new TextBlock();
            title.Text = "1. 选择分析类别";
            title.FontSize = 14; title.FontWeight = FontWeights.Bold;
            title.Foreground = (Brush)(new BrushConverter().ConvertFrom("#2196F3"));
            title.Margin = new Thickness(0, 0, 0, 6);
            CategoriesPanel.Children.Add(title);

            Dictionary<string, List<Tuple<string, BuiltInCategory, bool>>> groups =
                new Dictionary<string, List<Tuple<string, BuiltInCategory, bool>>>(StringComparer.Ordinal);
            groups["（建筑类）"] = new List<Tuple<string, BuiltInCategory, bool>>();
            groups["（结构类）"] = new List<Tuple<string, BuiltInCategory, bool>>();
            groups["（机电类）"] = new List<Tuple<string, BuiltInCategory, bool>>();
            string[] order = new string[] { "（建筑类）", "（结构类）", "（机电类）" };

            foreach (CategoryEntry entry in CategoryMapper.All)
            {
                BuiltInCategory b = entry.BIC;
                string bucket = order[0];
                if (b == BuiltInCategory.OST_Columns
                    || b == BuiltInCategory.OST_StructuralFraming
                    || b == BuiltInCategory.OST_StructuralFoundation) bucket = order[1];
                else if (b == BuiltInCategory.OST_PlumbingFixtures
                         || b == BuiltInCategory.OST_MechanicalEquipment
                         || b == BuiltInCategory.OST_ElectricalFixtures) bucket = order[2];
                groups[bucket].Add(Tuple.Create(entry.DisplayCn, b, entry.SupportAreaVolume));
            }

            string[] defaultCheckedCn = new string[] { "墙", "门", "窗" };

            foreach (string g in order)
            {
                TextBlock head = new TextBlock();
                head.Text = g; head.Foreground = (Brush)(new BrushConverter().ConvertFrom("#888"));
                head.FontSize = 11; head.Margin = new Thickness(0, 6, 0, 2);
                CategoriesPanel.Children.Add(head);

                foreach (Tuple<string, BuiltInCategory, bool> item in groups[g])
                {
                    CheckBox cb = new CheckBox();
                    cb.Content = item.Item1;
                    cb.Tag = item;
                    cb.Margin = new Thickness(0, 2, 0, 2);
                    cb.IsChecked = defaultCheckedCn.Contains(item.Item1);
                    CategoriesPanel.Children.Add(cb);
                }
            }
        }

        private void LoadLevelCheckboxes()
        {
            LevelList.Items.Clear();
            List<Level> levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
            foreach (Level l in levels)
            {
                CheckBox cb = new CheckBox();
                cb.Content = l.Name;
                cb.Tag = l.Id;
                cb.IsChecked = true;
                cb.Margin = new Thickness(0, 2, 0, 2);
                cb.FontSize = 12;
                LevelList.Items.Add(cb);
            }
            LevelAll.IsChecked = true;
        }

        private void LevelAllChanged(object sender, RoutedEventArgs e)
        {
            // InitializeComponent 期间 LevelList 还没构造好，事件先被触发，需 null 守卫
            if (LevelAll == null || LevelList == null) return;
            bool all = LevelAll.IsChecked == true;
            foreach (object o in LevelList.Items)
            {
                CheckBox c = o as CheckBox;
                if (c != null) c.IsChecked = all;
            }
        }

        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _rows.Clear();
                StatusBar.Text = "分析中...";
                ExportButton.IsEnabled = false;

                List<BuiltInCategory> cats = new List<BuiltInCategory>();
                List<string> catDisplay = new List<string>();
                Dictionary<BuiltInCategory, bool> supportAvMap = new Dictionary<BuiltInCategory, bool>();
                foreach (object o in CategoriesPanel.Children)
                {
                    CheckBox cb = o as CheckBox;
                    if (cb == null || cb.IsChecked != true) continue;
                    Tuple<string, BuiltInCategory, bool> tag = cb.Tag as Tuple<string, BuiltInCategory, bool>;
                    if (tag == null) continue;
                    cats.Add(tag.Item2);
                    catDisplay.Add(tag.Item1);
                    supportAvMap[tag.Item2] = tag.Item3;
                }
                if (cats.Count == 0)
                {
                    MessageBox.Show("请至少勾选一个构件类别", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusBar.Text = "请选择类别";
                    return;
                }

                HashSet<ElementId> levelIds = new HashSet<ElementId>();
                foreach (object o in LevelList.Items)
                {
                    CheckBox c = o as CheckBox;
                    if (c != null && c.IsChecked == true)
                        levelIds.Add((ElementId)c.Tag);
                }

                string typeKeyword = (TypeKeywordBox.Text ?? "").Trim();

                AppLogger.Info(MODULE, string.Format(
                    "开始分析：类别={0}，选中标高数={1}，关键字={2}",
                    string.Join("/", catDisplay), levelIds.Count,
                    string.IsNullOrEmpty(typeKeyword) ? "(无)" : typeKeyword));

                ElementMulticategoryFilter multi = new ElementMulticategoryFilter(cats);
                FilteredElementCollector collector =
                    new FilteredElementCollector(_doc).WherePasses(multi)
                        .WhereElementIsNotElementType();

                double totalArea = 0, totalVolume = 0;
                List<ElementId> selected = new List<ElementId>();
                int idx = 1;

                foreach (Element elem in collector)
                {
                    try
                    {
                        Category cat = elem.Category;
                        if (cat == null) continue;
                        CategoryEntry entry = CategoryMapper.FindByCategoryId(
                            (int)cat.Id.IntegerValue);
                        bool supportAv = (entry != null) ? entry.SupportAreaVolume : false;
                        if (supportAvMap.ContainsKey(entry == null
                            ? BuiltInCategory.INVALID
                            : entry.BIC))
                            supportAv = supportAvMap[entry == null
                                ? BuiltInCategory.INVALID
                                : entry.BIC];

                        string displayCn = (entry != null) ? entry.DisplayCn
                            : ((cat.Name != null) ? cat.Name : "—");

                        ElementId lid = RevitElementUtils.GetLevelId(elem);
                        if (levelIds.Count > 0 && (lid == null
                            || lid == ElementId.InvalidElementId
                            || !levelIds.Contains(lid))) continue;

                        string typeName = RevitElementUtils.GetTypeName(_doc, elem);
                        if (typeKeyword.Length > 0
                            && typeName.IndexOf(typeKeyword,
                                StringComparison.OrdinalIgnoreCase) < 0) continue;

                        double areaM2, volumeM3;
                        ElementInfoRow row = RevitElementUtils.BuildInfoRow(
                            _doc, elem, idx++, displayCn, supportAv,
                            out areaM2, out volumeM3);
                        _rows.Add(row);
                        selected.Add(row.RevitId);
                        totalArea += areaM2;
                        totalVolume += volumeM3;
                    }
                    catch (Exception inner)
                    {
                        AppLogger.Warn(MODULE,
                            "跳过图元 ID=" + elem.Id.IntegerValue + "，原因：" + inner.Message);
                    }
                }

                ResultGrid.ItemsSource = null;
                ResultGrid.ItemsSource = _rows;
                BuildSummary(totalArea, totalVolume);
                ExportButton.IsEnabled = _rows.Count > 0;

                if (_uiDoc != null && selected.Count > 0)
                {
                    try { _uiDoc.Selection.SetElementIds(selected); }
                    catch (Exception selEx)
                    {
                        AppLogger.Warn(MODULE, "同步选中集到 Revit 失败：" + selEx.Message);
                    }
                }

                AppLogger.Info(MODULE, string.Format(
                    "分析完成：匹配图元 {0} 个，面积合计 {1:F2}㎡，体积合计 {2:F2}m³",
                    _rows.Count, totalArea, totalVolume));
                StatusBar.Text = string.Format(
                    "分析完成：共找到 {0} 个图元，总面积 {1:F2} ㎡，总体积 {2:F2} m³",
                    _rows.Count, totalArea, totalVolume);
            }
            catch (Exception ex)
            {
                AppLogger.Error(MODULE, "分析失败", ex);
                MessageBox.Show("分析失败：" + ex.Message + "\n\n日志："
                    + AppLogger.CurrentLogFile, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusBar.Text = "失败：" + ex.Message;
            }
        }

        private void BuildSummary(double totalArea, double totalVolume)
        {
            Dictionary<string, CatStat> byCat = new Dictionary<string, CatStat>(
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> byType = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            foreach (ElementInfoRow r in _rows)
            {
                if (!byCat.ContainsKey(r.Category))
                    byCat[r.Category] = new CatStat(r.Category);
                byCat[r.Category].Count++;
                double a = 0, v = 0;
                if (r.AreaM2 != UnitConverter.N_A) double.TryParse(r.AreaM2, out a);
                if (r.VolumeM3 != UnitConverter.N_A) double.TryParse(r.VolumeM3, out v);
                byCat[r.Category].AreaM2 += a;
                byCat[r.Category].VolumeM3 += v;
                string k = r.Category + " / " + r.FamilyName + " : " + r.TypeName;
                if (!byType.ContainsKey(k)) byType[k] = 0;
                byType[k]++;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format(
                "共找到 {0} 个图元 | 面积合计 {1:F2} ㎡ | 体积合计 {2:F2} m³",
                _rows.Count, totalArea, totalVolume));
            sb.Append("\n—— 按类别汇总 ——\n");
            foreach (KeyValuePair<string, CatStat> kv in byCat)
            {
                sb.Append(string.Format("  · {0}：{1} 个 · 面积 {2:F2} ㎡ · 体积 {3:F2} m³\n",
                    kv.Value.CategoryName, kv.Value.Count,
                    kv.Value.AreaM2, kv.Value.VolumeM3));
            }
            CatSummary.Text = sb.ToString();

            List<string> lines = new List<string>();
            List<KeyValuePair<string, int>> sorted = byType
                .OrderByDescending(pair => pair.Value).ToList();
            int i = 0;
            foreach (KeyValuePair<string, int> pair in sorted)
            {
                lines.Add(string.Format("{0}. {1} × {2}", ++i, pair.Key, pair.Value));
                if (i >= 50) break;
            }
            TypeSummary.ItemsSource = lines;
        }

        private void ResultGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ElementInfoRow info = ResultGrid.SelectedItem as ElementInfoRow;
            if (info == null || _uiDoc == null) return;
            try
            {
                ElementId[] one = new ElementId[] { info.RevitId };
                _uiDoc.Selection.SetElementIds(one);
                _uiDoc.ShowElements(info.RevitId);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(MODULE, "双击跳转失败：" + ex.Message);
                MessageBox.Show("跳转失败：" + ex.Message, "错误");
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0) return;
            try
            {
                string[] headers = new string[] {
                    "序号","ID","类别","族","类型","标高","面积(㎡)","体积(m³)"
                };
                Func<ElementInfoRow, string[]> extractor = delegate(ElementInfoRow r)
                {
                    return new string[] {
                        r.Index.ToString(), r.ElementId.ToString(),
                        CsvExport.Escape(r.Category),
                        CsvExport.Escape(r.FamilyName),
                        CsvExport.Escape(r.TypeName),
                        CsvExport.Escape(r.LevelName),
                        r.AreaM2, r.VolumeM3
                    };
                };

                string f1 = CsvExport.BuildDesktopFileName("多维度分析", "明细");
                int c1 = CsvExport.WriteDetailSheet(f1, headers, _rows, extractor);
                AppLogger.Info(MODULE, "明细 CSV 写出：" + f1 + "（" + c1 + "行）");

                Dictionary<string, CatStat> byCat = new Dictionary<string, CatStat>(
                    StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> byType = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (ElementInfoRow r in _rows)
                {
                    if (!byCat.ContainsKey(r.Category))
                        byCat[r.Category] = new CatStat(r.Category);
                    byCat[r.Category].Count++;
                    double a = 0, v = 0;
                    if (r.AreaM2 != UnitConverter.N_A) double.TryParse(r.AreaM2, out a);
                    if (r.VolumeM3 != UnitConverter.N_A) double.TryParse(r.VolumeM3, out v);
                    byCat[r.Category].AreaM2 += a;
                    byCat[r.Category].VolumeM3 += v;
                    string k = r.Category + " / " + r.FamilyName + " : " + r.TypeName;
                    if (!byType.ContainsKey(k)) byType[k] = 0;
                    byType[k]++;
                }

                string f2 = CsvExport.BuildDesktopFileName("多维度分析", "类别汇总");
                using (StreamWriter sw = CsvExport.OpenWriter(f2))
                {
                    CsvExport.WriteRow(sw, "类别", "数量", "面积(㎡)", "体积(m³)");
                    foreach (KeyValuePair<string, CatStat> kv in byCat)
                    {
                        CsvExport.WriteRow(sw,
                            CsvExport.Escape(kv.Value.CategoryName),
                            kv.Value.Count.ToString(),
                            string.Format(UnitConverter.FMT_2_DEC, kv.Value.AreaM2),
                            string.Format(UnitConverter.FMT_2_DEC, kv.Value.VolumeM3));
                    }
                }

                string f3 = CsvExport.BuildDesktopFileName("多维度分析", "类型分组");
                using (StreamWriter sw = CsvExport.OpenWriter(f3))
                {
                    CsvExport.WriteRow(sw, "类别/族:类型", "数量");
                    List<KeyValuePair<string, int>> sorted = byType
                        .OrderByDescending(pair => pair.Value).ToList();
                    foreach (KeyValuePair<string, int> pair in sorted)
                        CsvExport.WriteRow(sw, CsvExport.Escape(pair.Key),
                            pair.Value.ToString());
                }

                MessageBox.Show("✅ 已导出 3 份 CSV 到桌面：\n· "
                    + Path.GetFileName(f1) + "（" + c1 + "行）\n· "
                    + Path.GetFileName(f2) + "\n· "
                    + Path.GetFileName(f3),
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusBar.Text = "CSV 导出完成";
                AppLogger.Info(MODULE, "3 张表导出完成：" + f2 + "；" + f3);
            }
            catch (IOException ioex)
            {
                AppLogger.Error(MODULE, "CSV 导出 IO 异常", ioex);
                MessageBox.Show("导出失败：CSV 可能被 Excel 占用，请关闭后重试。\n\n"
                    + ioex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(MODULE, "CSV 导出失败", ex);
                MessageBox.Show("导出失败：" + ex.Message + "\n\n日志："
                    + AppLogger.CurrentLogFile, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _rows.Clear();
            ResultGrid.ItemsSource = null;
            CatSummary.Text = "（点击\"开始分析\"查看汇总）";
            TypeSummary.ItemsSource = null;
            ExportButton.IsEnabled = false;
            StatusBar.Text = "已清除结果";
        }

        public void ClearHighlightOnClose()
        {
            try
            {
                if (_uiDoc != null)
                    _uiDoc.Selection.SetElementIds(new ElementId[0]);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(MODULE, "清空选中集失败：" + ex.Message);
            }
        }
    }

    public class CatStat
    {
        public string CategoryName;
        public int Count;
        public double AreaM2;
        public double VolumeM3;
        public CatStat(string n) { CategoryName = n; Count = 0; AreaM2 = 0; VolumeM3 = 0; }
    }
}

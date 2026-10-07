using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PluginPortfolio
{
    public partial class MainWindows : Window
    {
        private const string MODULE = "P03_Semantic";
        private const string DeepSeekUrl = "https://api.deepseek.com/chat/completions";

        private Document _doc;
        private UIDocument _uiDoc;
        private IntPtr _revitHandle;
        private JsonConfigLoader.SemanticFilterConfig _cfg;
        private string _realApiKey;
        private List<ElementInfoRow> _rows;

        public MainWindows(Document doc, UIDocument uiDoc,
            IntPtr revitHandle, JsonConfigLoader.SemanticFilterConfig cfg)
        {
            InitializeComponent();
            _doc = doc;
            _uiDoc = uiDoc;
            _revitHandle = revitHandle;
            _cfg = cfg ?? new JsonConfigLoader.SemanticFilterConfig();
            _rows = new List<ElementInfoRow>();
            _realApiKey = "";

            if (!string.IsNullOrEmpty(_cfg.DeepSeekApiKey))
            {
                _realApiKey = _cfg.DeepSeekApiKey;
                ApiKeyBox.Text = ToMask(_realApiKey);
                ShowKeyCheckBox.IsChecked = false;
                StatusBar.Text = "已从 DLL 旁 JSON 配置填入 API Key";
            }
            else
            {
                string saved = LoadSavedApiKey();
                if (saved.Length > 0)
                {
                    _realApiKey = saved;
                    ApiKeyBox.Text = ToMask(saved);
                    ShowKeyCheckBox.IsChecked = false;
                    StatusBar.Text = "已自动填入上次记住的 API Key（掩码）";
                }
                else
                {
                    UpdateApiKeyMasking();
                }
            }
        }

        private void UpdateApiKeyMasking()
        {
            // InitializeComponent 期间控件可能还没构造好，需 null 守卫
            if (ShowKeyCheckBox == null || ApiKeyBox == null) return;
            bool show = ShowKeyCheckBox.IsChecked == true;
            if (show)
            {
                if (ApiKeyBox.Text == ToMask(_realApiKey))
                    ApiKeyBox.Text = _realApiKey;
            }
            else
            {
                if (!string.IsNullOrEmpty(ApiKeyBox.Text)
                    && !AllMaskingChars(ApiKeyBox.Text))
                    _realApiKey = ApiKeyBox.Text;
                ApiKeyBox.Text = ToMask(_realApiKey);
            }
        }
        private static bool AllMaskingChars(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            for (int i = 0; i < s.Length; i++)
                if (s[i] != '*') return false;
            return true;
        }
        private static string ToMask(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int len = s.Length;
            if (len <= 4) return new string('*', len);
            return new string('*', len - 4) + s.Substring(len - 4);
        }
        private string GetRealApiKey()
        {
            bool show = ShowKeyCheckBox.IsChecked == true;
            if (show) return (ApiKeyBox.Text ?? "").Trim();
            string t = ApiKeyBox.Text ?? "";
            if (AllMaskingChars(t)) return _realApiKey;
            return t.Trim();
        }
        private void ShowKeyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateApiKeyMasking();
        }
        private void ForgetKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try { if (File.Exists(ApiKeyStoragePath)) File.Delete(ApiKeyStoragePath); }
            catch (Exception ex) { AppLogger.Warn(MODULE, "删除记住的Key失败：" + ex.Message); }
            _realApiKey = "";
            ApiKeyBox.Text = "";
            StatusBar.Text = "已清除本地记住的 API Key";
        }
        private static string ApiKeyStoragePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RevitPluginDemo");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "apikey.txt");
            }
        }
        private static string LoadSavedApiKey()
        {
            try { if (File.Exists(ApiKeyStoragePath))
                    return (File.ReadAllText(ApiKeyStoragePath) ?? "").Trim(); }
            catch { }
            return "";
        }
        private static void SaveApiKey(string key)
        {
            try { File.WriteAllText(ApiKeyStoragePath, key ?? ""); }
            catch (Exception ex)
            { AppLogger.Warn(MODULE, "持久化 API Key 失败：" + ex.Message); }
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) DoSearchAsync();
        }
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DoSearchAsync();
        }

        private async void DoSearchAsync()
        {
            try
            {
                string prompt = (InputBox.Text ?? "").Trim();
                if (prompt.Length == 0)
                {
                    MessageBox.Show("请输入筛选指令", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string apiKey = GetRealApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    MessageBox.Show("请先填写 DeepSeek API Key", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string modelName = string.IsNullOrEmpty(_cfg.ModelName)
                    ? "deepseek-chat" : _cfg.ModelName;
                int timeoutMs = (_cfg.TimeoutMs <= 0) ? 30000 : _cfg.TimeoutMs;

                StatusBar.Text = "请求 DeepSeek AI 中（模型：" + modelName + "）...";
                LoadingPanel.Visibility = System.Windows.Visibility.Visible;
                EmptyHint.Visibility = System.Windows.Visibility.Collapsed;
                ResultGrid.Visibility = System.Windows.Visibility.Collapsed;
                ExportButton.IsEnabled = false;
                SearchButton.IsEnabled = false;

                AppLogger.Info(MODULE, string.Format(
                    "发起筛选请求：prompt=\"{0}\"，模型={1}, timeout={2}ms",
                    prompt.Length > 80 ? prompt.Substring(0, 80) + "..." : prompt,
                    modelName, timeoutMs));

                List<string> categories = await CallDeepSeek(apiKey, modelName, timeoutMs, prompt);
                if (categories == null || categories.Count == 0)
                {
                    LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
                    EmptyHint.Visibility = System.Windows.Visibility.Visible;
                    EmptyHint.Text = "AI 未识别出任何构件类别，请换说法重试";
                    SearchButton.IsEnabled = true;
                    StatusBar.Text = "未识别出类别";
                    AppLogger.Warn(MODULE, "AI 返回空类别数组");
                    return;
                }

                AppLogger.Info(MODULE, "AI 解析到类别：" + string.Join("、", categories));
                FilterElementsByCategories(categories);
                SaveApiKey(apiKey);
                _realApiKey = apiKey;
                if (ShowKeyCheckBox.IsChecked != true) ApiKeyBox.Text = ToMask(apiKey);

                LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
                if (_rows.Count > 0)
                {
                    ResultGrid.Visibility = System.Windows.Visibility.Visible;
                    EmptyHint.Visibility = System.Windows.Visibility.Collapsed;
                    ExportButton.IsEnabled = true;
                    StatusBar.Text = string.Format(
                        "找到 {0} 个图元（已在模型中选中），类别：{1}",
                        _rows.Count, string.Join("、", categories));
                }
                else
                {
                    EmptyHint.Visibility = System.Windows.Visibility.Visible;
                    EmptyHint.Text = "模型中没有类别为 "
                        + string.Join("、", categories) + " 的图元";
                    ResultGrid.Visibility = System.Windows.Visibility.Collapsed;
                    StatusBar.Text = "模型内无匹配图元";
                }
                SearchButton.IsEnabled = true;
            }
            catch (TaskCanceledException tcex)
            {
                HandleError("调用 DeepSeek 超时（网络慢或 Key 限流）", tcex);
            }
            catch (HttpRequestException hex)
            {
                HandleError("网络请求异常：通常是代理/防火墙/DNS 问题", hex);
            }
            catch (JsonReaderException jex)
            {
                HandleError("AI 返回格式无法解析", jex);
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException rvtex)
            {
                HandleError("Revit API 状态异常", rvtex);
            }
            catch (IOException ioex)
            {
                HandleError("本地文件 IO 异常", ioex);
            }
            catch (Exception ex)
            {
                HandleError("未分类异常", ex);
            }
        }

        private void HandleError(string brief, Exception ex)
        {
            LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
            SearchButton.IsEnabled = true;
            StatusBar.Text = "失败：" + brief;
            AppLogger.Error(MODULE, brief, ex);
            MessageBox.Show(brief + "\n\n详细信息：" + ex.Message
                + "\n\n日志文件：" + AppLogger.CurrentLogFile,
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static async Task<List<string>> CallDeepSeek(
            string apiKey, string modelName, int timeoutMs, string userPrompt)
        {
            StringBuilder aiKeysList = new StringBuilder();
            IReadOnlyList<CategoryEntry> all = CategoryMapper.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0) aiKeysList.Append(", ");
                aiKeysList.Append(all[i].AiKey);
            }
            string systemPrompt =
                "你是一个BIM助手。用户会用自然语言说他想筛选Revit中的哪些构件。" +
                "请只返回一个JSON对象：{\"categories\": [\"CategoryName1\"]}。" +
                "可选的类别关键词（必须从中选）：" + aiKeysList.ToString() + "。" +
                "规则：1) 如果用户说的没有具体类别，返回空数组；" +
                "2) 不要返回任何解释文字或 Markdown 包裹，只返回这一个 JSON。" +
                "用户需求：" + userPrompt;

            var body = new
            {
                model = modelName,
                messages = new object[]
                { new { role = "user", content = systemPrompt } },
                temperature = 0.2,
                max_tokens = 200
            };
            string jsonBody = JsonConvert.SerializeObject(body);

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
                StringContent content = new StringContent(jsonBody, Encoding.UTF8,
                    "application/json");

                HttpResponseMessage resp = await client.PostAsync(DeepSeekUrl, content);
                string raw = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    AppLogger.Error(MODULE, "DeepSeek HTTP " + resp.StatusCode,
                        new HttpRequestException(raw));
                    MessageBox.Show(
                        "DeepSeek 返回错误（" + resp.StatusCode + "）\n\n" + raw,
                        "AI 调用失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                JObject root = JObject.Parse(raw);
                string reply = "";
                JToken choices = root["choices"];
                if (choices != null && choices.HasValues
                    && choices.First != null
                    && choices.First["message"] != null
                    && choices.First["message"]["content"] != null)
                    reply = choices.First["message"]["content"].ToString();

                int start = reply.IndexOf('{');
                int end = reply.LastIndexOf('}');
                if (start >= 0 && end > start)
                    reply = reply.Substring(start, end - start + 1);

                JObject parsed = JObject.Parse(reply);
                JArray arr = parsed["categories"] as JArray;
                List<string> result = new List<string>();
                if (arr != null)
                {
                    foreach (JToken t in arr)
                    {
                        string s = (t == null) ? "" : t.ToString();
                        if (!string.IsNullOrEmpty(s)) result.Add(s.Trim());
                    }
                }
                AppLogger.Info(MODULE, "解析完成，有效类别数：" + result.Count);
                return result;
            }
        }

        private void FilterElementsByCategories(List<string> categories)
        {
            _rows.Clear();
            List<BuiltInCategory> bics = new List<BuiltInCategory>();
            foreach (string c in categories)
            {
                CategoryEntry e = CategoryMapper.FindByAiKey(c);
                if (e != null) bics.Add(e.BIC);
                else
                    AppLogger.Warn(MODULE,
                        "类别名 \"" + c + "\" 不在 CategoryMapper 中，跳过");
            }
            if (bics.Count == 0)
            {
                ResultGrid.ItemsSource = null;
                BuildSummary(0, 0);
                return;
            }

            ElementMulticategoryFilter multi = new ElementMulticategoryFilter(bics);
            FilteredElementCollector collector =
                new FilteredElementCollector(_doc).WherePasses(multi)
                    .WhereElementIsNotElementType();

            int idx = 1;
            double totalA = 0, totalV = 0;
            List<ElementId> selected = new List<ElementId>();

            foreach (Element elem in collector)
            {
                try
                {
                    Category cat = elem.Category;
                    if (cat == null) continue;
                    CategoryEntry entry = CategoryMapper.FindByCategoryId(
                        (int)cat.Id.IntegerValue);
                    bool supportAv = (entry != null) && entry.SupportAreaVolume;
                    string displayCn = (entry != null) ? entry.DisplayCn
                        : ((cat.Name != null) ? cat.Name : "—");

                    double a, v;
                    ElementInfoRow row = RevitElementUtils.BuildInfoRow(
                        _doc, elem, idx++, displayCn, supportAv, out a, out v);
                    _rows.Add(row);
                    selected.Add(row.RevitId);
                    totalA += a;
                    totalV += v;
                }
                catch (Exception inner)
                {
                    AppLogger.Warn(MODULE, "跳过图元 ID="
                        + (elem == null ? "?" : elem.Id.IntegerValue.ToString())
                        + "：" + inner.Message);
                }
            }

            ResultGrid.ItemsSource = null;
            ResultGrid.ItemsSource = _rows;
            BuildSummary(totalA, totalV);

            if (_uiDoc != null && selected.Count > 0)
            {
                try { _uiDoc.Selection.SetElementIds(selected); }
                catch (Exception ex)
                { AppLogger.Warn(MODULE, "同步选中集失败：" + ex.Message); }
            }
        }

        private void BuildSummary(double totalA, double totalV)
        {
            StatusBar.Text = string.Format(
                "找到 {0} 个图元 · 总面积 {1:F2} ㎡ · 总体积 {2:F2} m³",
                _rows.Count, totalA, totalV);
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
                Func<ElementInfoRow, string[]> ext = delegate(ElementInfoRow r)
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
                string f1 = CsvExport.BuildDesktopFileName("语义筛选", "明细");
                int c1 = CsvExport.WriteDetailSheet(f1, headers, _rows, ext);

                AppLogger.Info(MODULE, "导出明细：" + f1 + "（" + c1 + "行）");
                MessageBox.Show("✅ 已导出 CSV 到桌面：\n" + Path.GetFileName(f1)
                    + "（" + c1 + "行）",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusBar.Text = "CSV 导出完成";
            }
            catch (IOException ioex)
            {
                AppLogger.Error(MODULE, "CSV IO 异常", ioex);
                MessageBox.Show("导出失败：CSV 可能被 Excel 占用。\n\n" + ioex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(MODULE, "CSV 导出失败", ex);
                MessageBox.Show("导出失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _rows.Clear();
            ResultGrid.ItemsSource = null;
            ResultGrid.Visibility = System.Windows.Visibility.Collapsed;
            LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
            EmptyHint.Visibility = System.Windows.Visibility.Visible;
            EmptyHint.Text = "输入指令后点击筛选";
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
            { AppLogger.Warn(MODULE, "清空选中集失败：" + ex.Message); }
        }
    }
}

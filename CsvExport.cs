using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PluginPortfolio
{
    /// <summary>
    /// CSV 导出统一工具：BOM UTF-8 + 转义 + 时间戳文件名
    /// </summary>
    public static class CsvExport
    {
        public static string BuildDesktopFileName(string filePrefix, string sheetName)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string name = string.Format("{0}_{1}_{2}.csv", filePrefix, sheetName, ts);
            return Path.Combine(desktop, name);
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static void WriteRow(StreamWriter sw, params string[] cells)
        {
            if (sw == null) return;
            if (cells == null || cells.Length == 0) { sw.WriteLine(); return; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(cells[i]));
            }
            sw.WriteLine(sb.ToString());
        }

        public static StreamWriter OpenWriter(string fullPath)
        {
            return new StreamWriter(fullPath, false, new UTF8Encoding(true));
        }

        public static int WriteDetailSheet<T>(string fullPath,
            string[] headers,
            IList<T> rows,
            Func<T, string[]> rowExtractor)
        {
            int count = 0;
            using (StreamWriter sw = OpenWriter(fullPath))
            {
                WriteRow(sw, headers);
                if (rows != null)
                {
                    foreach (T r in rows)
                    {
                        string[] cells = (rowExtractor == null) ? null : rowExtractor(r);
                        if (cells != null) { WriteRow(sw, cells); count++; }
                    }
                }
            }
            return count;
        }
    }
}

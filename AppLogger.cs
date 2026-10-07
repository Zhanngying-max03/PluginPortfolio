using System;
using System.IO;
using System.Text;

namespace PluginPortfolio
{
    /// <summary>
    /// 轻量应用日志类。不需要引用 NLog / Serilog 这类重型 DLL（避免 Revit 加载
    /// 插件时加载一堆依赖）。纯手写 File.AppendAllText 写日志文件到系统 %Temp%。
    /// 三个等级：Info / Warn / Error，不同等级前缀不同，后期 grep 检索方便。
    /// </summary>
    public static class AppLogger
    {
        private static readonly object _lock = new object();
        private static string _currentFile;

        private static string EnsureFile()
        {
            if (!string.IsNullOrEmpty(_currentFile) && File.Exists(_currentFile))
                return _currentFile;
            string temp = Path.GetTempPath();
            string dir = Path.Combine(temp, "RevitPortfolio");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string day = DateTime.Now.ToString("yyyyMMdd");
            _currentFile = Path.Combine(dir, "RevitPortfolio_" + day + ".log");
            return _currentFile;
        }

        private static void Write(string level, string module, string message, Exception ex)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss}] [{1,-5}] [{2}] ",
                    DateTime.Now, level, module ?? "?");
                sb.AppendLine(message);
                if (ex != null)
                {
                    sb.Append("  异常类型：").AppendLine(ex.GetType().FullName);
                    sb.Append("  异常消息：").AppendLine(ex.Message);
                    if (ex.StackTrace != null)
                    {
                        using (StringReader sr = new StringReader(ex.StackTrace))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                                sb.Append("    ").AppendLine(line);
                        }
                    }
                }
                lock (_lock)
                {
                    File.AppendAllText(EnsureFile(), sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // 日志本身出错不能影响业务
            }
        }

        public static void Info(string module, string message)
        {
            Write("INFO", module, message, null);
        }

        public static void Warn(string module, string message)
        {
            Write("WARN", module, message, null);
        }

        public static void Error(string module, string message, Exception ex)
        {
            Write("ERROR", module, message, ex);
        }

        public static string CurrentLogFile
        {
            get { return EnsureFile(); }
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PluginPortfolio
{
    /// <summary>
    /// 外部配置文件加载器。
    /// </summary>
    public static class JsonConfigLoader
    {
        private static T DeserializeFromFile<T>(string fullPath) where T : new()
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return new T();
            try
            {
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                T obj = JsonConvert.DeserializeObject<T>(json);
                return (obj == null) ? new T() : obj;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Config", "加载配置文件失败：" + fullPath, ex);
                return new T();
            }
        }

        public class SemanticFilterConfig
        {
            public string DeepSeekApiKey { get; set; }
            public string ModelName { get; set; }
            public int TimeoutMs { get; set; }

            public SemanticFilterConfig()
            {
                DeepSeekApiKey = "";
                ModelName = "deepseek-chat";
                TimeoutMs = 30000;
            }
        }

        public static SemanticFilterConfig LoadSemanticFilterConfig(string fullPath)
        {
            return DeserializeFromFile<SemanticFilterConfig>(fullPath);
        }

        public class NameCheckerConfig
        {
            public string ForbiddenRegexChars { get; set; }
            public int MaxNameLength { get; set; }
            public Dictionary<string, List<string>> CategoryPrefixRules { get; set; }

            public NameCheckerConfig()
            {
                ForbiddenRegexChars =
                    @"[*?/\\:""<>\|\s]+|[，。！？、；：""''（）【】《》￥…—]";
                MaxNameLength = 50;
                CategoryPrefixRules = new Dictionary<string, List<string>>
                (StringComparer.OrdinalIgnoreCase);
            }
        }

        public static NameCheckerConfig LoadNameCheckerConfig(string fullPath)
        {
            return DeserializeFromFile<NameCheckerConfig>(fullPath);
        }

        public static string SuggestConfigPath(string pluginAssemblyDir,
            string configFileName)
        {
            if (string.IsNullOrEmpty(pluginAssemblyDir))
                pluginAssemblyDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(pluginAssemblyDir, configFileName);
        }
    }
}

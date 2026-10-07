using System;

namespace PluginPortfolio
{
    /// <summary>
    /// Revit API 默认用英制单位（英尺/平方英尺/立方英尺）。
    /// 施工图/汇报需要公制单位（米/㎡/m³）。
    /// 本类提供统一的换算常量和静态方法。
    /// </summary>
    public static class UnitConverter
    {
        public const double FT_TO_M = 0.3048;
        public const double SQ_FT_TO_SQ_M = 0.09290304;
        public const double CUBIC_FT_TO_CUBIC_M = 0.0283168466;

        public const string FMT_2_DEC = "{0:F2}";
        public const string N_A = "N/A";

        public static string FormatAreaM2(double sqftValueRaw, bool supportArea)
        {
            if (!supportArea) return N_A;
            double m2 = sqftValueRaw * SQ_FT_TO_SQ_M;
            return string.Format(FMT_2_DEC, m2);
        }

        public static string FormatVolumeM3(double cuftValueRaw, bool supportVolume)
        {
            if (!supportVolume) return N_A;
            double m3 = cuftValueRaw * CUBIC_FT_TO_CUBIC_M;
            return string.Format(FMT_2_DEC, m3);
        }

        public static double SafeAreaRawToM2(double sqftValueRaw)
        {
            if (sqftValueRaw <= 0) return 0;
            return sqftValueRaw * SQ_FT_TO_SQ_M;
        }

        public static double SafeVolumeRawToM3(double cuftValueRaw)
        {
            if (cuftValueRaw <= 0) return 0;
            return cuftValueRaw * CUBIC_FT_TO_CUBIC_M;
        }
    }
}

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace PluginPortfolio
{
    /// <summary>
    /// 从 Element 上提取常见字段的静态工具函数。
    /// </summary>
    public static class RevitElementUtils
    {
        public static ElementId GetLevelId(Element e)
        {
            if (e == null) return ElementId.InvalidElementId;
            ElementId id = e.LevelId;
            if (id != null && id != ElementId.InvalidElementId) return id;

            Parameter lp = e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (lp != null)
            {
                ElementId pid = lp.AsElementId();
                if (pid != null) return pid;
            }
            return ElementId.InvalidElementId;
        }

        public static string GetLevelName(Document doc, Element e)
        {
            if (doc == null || e == null) return "—";
            ElementId lid = GetLevelId(e);
            if (lid == null || lid == ElementId.InvalidElementId) return "—";
            Level lvl = doc.GetElement(lid) as Level;
            return (lvl != null && lvl.Name != null) ? lvl.Name : "—";
        }

        public static string GetFamilyName(Element e)
        {
            if (e == null) return "—";
            FamilyInstance fi = e as FamilyInstance;
            if (fi != null && fi.Symbol != null && fi.Symbol.Family != null
                && fi.Symbol.Family.Name != null)
                return fi.Symbol.Family.Name;
            return "—";
        }

        public static string GetTypeName(Document doc, Element e)
        {
            if (doc == null || e == null) return "—";
            FamilyInstance fi = e as FamilyInstance;
            if (fi != null && fi.Symbol != null && fi.Symbol.Name != null)
                return fi.Symbol.Name;
            ElementId tid = e.GetTypeId();
            if (tid == null || tid == ElementId.InvalidElementId) return "—";
            Element t = doc.GetElement(tid);
            if (t != null && t.Name != null) return t.Name;
            return "—";
        }

        public static double GetHostAreaRaw(Element e)
        {
            if (e == null) return 0;
            Parameter p = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            return (p == null) ? 0 : p.AsDouble();
        }

        public static double GetHostVolumeRaw(Element e)
        {
            if (e == null) return 0;
            Parameter p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
            return (p == null) ? 0 : p.AsDouble();
        }

        public static ElementInfoRow BuildInfoRow(Document doc, Element e,
            int index,
            string categoryDisplayName,
            bool supportAreaVolume,
            out double areaM2,
            out double volumeM3)
        {
            areaM2 = 0;
            volumeM3 = 0;
            ElementInfoRow row = new ElementInfoRow();
            row.Index = index;
            row.ElementId = (e == null || e.Id == null) ? -1 : e.Id.IntegerValue;
            row.Category = string.IsNullOrEmpty(categoryDisplayName) ? "—" : categoryDisplayName;
            row.FamilyName = GetFamilyName(e);
            row.TypeName = GetTypeName(doc, e);
            row.LevelName = GetLevelName(doc, e);

            if (supportAreaVolume)
            {
                double rawArea = GetHostAreaRaw(e);
                double rawVol = GetHostVolumeRaw(e);
                areaM2 = UnitConverter.SafeAreaRawToM2(rawArea);
                volumeM3 = UnitConverter.SafeVolumeRawToM3(rawVol);
                row.AreaM2 = string.Format(UnitConverter.FMT_2_DEC, areaM2);
                row.VolumeM3 = string.Format(UnitConverter.FMT_2_DEC, volumeM3);
            }
            else
            {
                row.AreaM2 = UnitConverter.N_A;
                row.VolumeM3 = UnitConverter.N_A;
            }
            row.RevitId = (e == null) ? ElementId.InvalidElementId : e.Id;
            return row;
        }
    }

    public class ElementInfoRow
    {
        public int Index { get; set; }
        public int ElementId { get; set; }
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string LevelName { get; set; }
        public string AreaM2 { get; set; }
        public string VolumeM3 { get; set; }

        public ElementId RevitId { get; set; }
    }
}

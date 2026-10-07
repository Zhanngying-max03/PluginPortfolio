using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace PluginPortfolio
{
    public class CategoryEntry
    {
        public string AiKey;
        public string DisplayCn;
        public BuiltInCategory BIC;
        public bool SupportAreaVolume;

        public CategoryEntry(string aiKey, string cn, BuiltInCategory b, bool s)
        { AiKey = aiKey; DisplayCn = cn; BIC = b; SupportAreaVolume = s; }
    }

    public static class CategoryMapper
    {
        private static readonly List<CategoryEntry> _all = new List<CategoryEntry>()
        {
            new CategoryEntry("Wall",         "墙",        BuiltInCategory.OST_Walls,                    true),
            new CategoryEntry("Door",         "门",        BuiltInCategory.OST_Doors,                    false),
            new CategoryEntry("Window",       "窗",        BuiltInCategory.OST_Windows,                  false),
            new CategoryEntry("Floor",        "楼板",      BuiltInCategory.OST_Floors,                   true),
            new CategoryEntry("Ceiling",      "天花板",    BuiltInCategory.OST_Ceilings,                  true),
            new CategoryEntry("Roof",         "屋顶",      BuiltInCategory.OST_Roofs,                     true),
            new CategoryEntry("Furniture",    "家具",      BuiltInCategory.OST_Furniture,                 false),
            new CategoryEntry("Stairs",       "楼梯",      BuiltInCategory.OST_Stairs,                    false),
            new CategoryEntry("Ramp",         "坡道",      BuiltInCategory.OST_Ramps,                     false),
            new CategoryEntry("Column",       "柱",        BuiltInCategory.OST_Columns,                   false),
            new CategoryEntry("Beam",         "梁",        BuiltInCategory.OST_StructuralFraming,         false),
            new CategoryEntry("Foundation",   "结构基础",  BuiltInCategory.OST_StructuralFoundation,      true),
            new CategoryEntry("PlumbingFixture",        "卫浴装置",  BuiltInCategory.OST_PlumbingFixtures,       false),
            new CategoryEntry("MechanicalEquipment",    "机械设备",  BuiltInCategory.OST_MechanicalEquipment,    false),
            new CategoryEntry("ElectricalFixture",      "电气装置",  BuiltInCategory.OST_ElectricalFixtures,     false)
        };

        public static IReadOnlyList<CategoryEntry> All { get { return _all.AsReadOnly(); } }

        public static int Count { get { return _all.Count; } }

        public static CategoryEntry FindByAiKey(string aiKey)
        {
            if (string.IsNullOrEmpty(aiKey)) return null;
            foreach (CategoryEntry e in _all)
            {
                if (e.AiKey.Equals(aiKey, StringComparison.OrdinalIgnoreCase)) return e;
            }
            return null;
        }

        public static CategoryEntry FindByCategoryId(int categoryIntegerValue)
        {
            foreach (CategoryEntry e in _all)
            {
                if ((int)e.BIC == categoryIntegerValue) return e;
            }
            return null;
        }

        public static CategoryEntry FindByBuiltInCategory(BuiltInCategory b)
        {
            foreach (CategoryEntry e in _all)
            {
                if (e.BIC == b) return e;
            }
            return null;
        }

        public static List<Tuple<string, BuiltInCategory, bool>> ForCategoryCheckBox()
        {
            List<Tuple<string, BuiltInCategory, bool>> r =
                new List<Tuple<string, BuiltInCategory, bool>>();
            foreach (CategoryEntry e in _all)
                r.Add(Tuple.Create(e.DisplayCn, e.BIC, e.SupportAreaVolume));
            return r;
        }
    }
}

# Revit 二次开发插件集

> 4 个独立插件，覆盖 BIM 工程量统计 / 交互分析 / AI 语义筛选 / 命名规范检查  
> 技术栈：C# + Revit API 2022 + WPF + DeepSeek API

---

## 🎯 插件列表

### 插件 01｜墙门窗批量统计
- **功能**：自动统计墙/门/窗的数量、总面积、总体积，按标高分组
- **技术**：FilteredElementCollector + LINQ 聚合 + 单位换算
- **输出**：CSV 工程量表（桌面）

### 插件 02｜多维度工程量分析
- **功能**：WPF 交互界面，支持按类别/标高筛选，3 表联动导出
- **技术**：WPF + DataGrid 绑定 + ObservableCollection
- **输出**：汇总表 + 明细表 + 标高分组表（CSV）

### 插件 03｜AI 语义筛选器 ⭐
- **功能**：用自然语言（如「找出所有外墙和门」）筛选 BIM 构件
- **技术**：DeepSeek API 异步调用 + Prompt 工程 + JSON 解析
- **亮点**：支持 15 种构件类别识别，AI + BIM 前沿方向

### 插件 04｜命名规范自动检查
- **功能**：正则 + JSON 配置驱动，自动找出不符合规范的族并选中
- **技术**：正则匹配 + 配置驱动 + 违规图元自动选中
- **输出**：违规清单 CSV（桌面）

---

## 🏗️ 架构

```
PluginPortfolio（单项目 .NET Framework 4.8）
├── Shared 工具类
│   ├── AppLogger          日志系统（%Temp%\RevitPortfolio\）
│   ├── UnitConverter      英制→公制换算
│   ├── CsvExport          CSV 导出（UTF-8 BOM）
│   ├── CategoryMapper     类别映射（15 种 Revit BuiltInCategory）
│   ├── RevitElementUtils  图元工具 + ElementInfoRow
│   └── JsonConfigLoader   配置加载
├── Command01  墙门窗统计
├── Command02  多维度分析（WPF）
├── Command03  AI 语义筛选（WPF + DeepSeek）
├── Command04  命名规范检查
├── MainWindow / MainWindows  WPF 窗口
├── config/    JSON 配置样例
└── deploy/    .addin 注册文件
```

---

## 🚀 安装使用

### 环境要求
- Revit 2022（其他版本需修改 RevitAPI.dll 路径 + 目标框架）
- .NET Framework 4.8
- Visual Studio 2019 / 2022

### 编译
1. 用 VS 新建 **类库 (.NET Framework 4.8)** 项目，名称 `PluginPortfolio`
2. 把仓库所有 `.cs` + `.xaml` 文件加入项目（删 VS 自动生成的 `Class1.cs`）
3. **引用**：RevitAPI.dll + RevitAPIUI.dll（`C:\Program Files\Autodesk\Revit 2022\`）
4. **NuGet**：Newtonsoft.Json 13.0.3
5. **WPF 引用**：PresentationCore / PresentationFramework / WindowsBase / System.Xaml / System.Net.Http
6. **XAML 生成操作**：.xaml 设为 `Page`，.xaml.cs 设为 `Compile`
7. 右键项目 → 属性 → 生成 → 目标平台设为 **x64**
8. Ctrl+Shift+B → DLL 输出：`bin\Debug\PluginPortfolio.dll`

### 在 Revit 中加载

**方式 A（推荐·调试用）：Add-In Manager**
1. 安装 [Revit Add-In Manager](https://github.com/Autodesk-Forge/RevitAddInManager)
2. 打开 Revit → 附加模块 → Revit Add-In Manager
3. Load Assembly → 选 `PluginPortfolio.dll`
4. 选中 Command01 / 02 / 03 / 04 → Run

**方式 B（一劳永逸·发布用）：.addin 注册**
把 `deploy/PluginPortfolio.addin` 复制到：
```
C:\ProgramData\Autodesk\Revit\Addins\2022\
```
⚠️ 打开 .addin 文件，把 `<Assembly>` 路径改成你实际的 DLL 路径。

开 Revit → 附加模块 → 外部工具 → 看到 4 个插件按钮。

### 插件 03 额外配置
首次使用需输入 DeepSeek API Key（sk- 开头），会自动保存在本地。
注册地址：https://platform.deepseek.com/ （充 1 块钱即可调用数百次）

---

## 📊 支持的 Revit 类别

| AiKey | 中文 | BuiltInCategory |
|---|---|---|
| Wall | 墙 | OST_Walls |
| Door | 门 | OST_Doors |
| Window | 窗 | OST_Windows |
| Floor | 楼板 | OST_Floors |
| Ceiling | 天花板 | OST_Ceilings |
| Roof | 屋顶 | OST_Roofs |
| Furniture | 家具 | OST_Furniture |
| Stairs | 楼梯 | OST_Stairs |
| Ramp | 坡道 | OST_Ramps |
| Column | 柱 | OST_Columns |
| Beam | 梁 | OST_StructuralFraming |
| Foundation | 结构基础 | OST_StructuralFoundation |
| PlumbingFixture | 卫浴装置 | OST_PlumbingFixtures |
| MechanicalEquipment | 机械设备 | OST_MechanicalEquipment |
| ElectricalFixture | 电气装置 | OST_ElectricalFixtures |

---

## 🛠️ 配置说明

### 插件 03 语义筛选（可选）
把 `config/semanticfilter.appsettings.json.sample` 复制到 DLL 同目录，重命名为 `semanticfilter.appsettings.json`：
```json
{
  "DeepSeekApiKey": "sk-你的key（可留空，运行时输入）",
  "ModelName": "deepseek-chat",
  "TimeoutMs": 15000
}
```

### 插件 04 命名规范（可选）
把 `config/namechecker.rules.json.sample` 复制到 DLL 同目录，重命名为 `namechecker.rules.json`：
```json
{
  "DefaultPrefix": "",
  "DefaultMaxLength": 64,
  "BadCharacters": "[\\\\/:*?\"<>|]",
  "CategoryPrefixes": {
    "OST_Walls": "W-",
    "OST_Doors": "D-",
    "OST_Windows": "WN-",
    "OST_Columns": "C-",
    "OST_StructuralFraming": "B-"
  }
}
```

---

## 📝 日志

所有插件运行日志：`%Temp%\RevitPortfolio\RevitPortfolio_yyyyMMdd.log`

---

## 📺 演示

（录屏链接 / GIF / 截图）

---

## 📄 License

MIT

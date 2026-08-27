# XYL_Tools (GeoTools Pro)

一个基于 ArcGIS Pro SDK 开发的 **数据质量检查** 插件（Add-In）。通过 DockPane 面板对地图中的要素图层进行批量检查，帮助快速发现并定位数据质量问题。

## 功能特性

- **空值检查**：检查属性字段中是否存在 NULL 值
- **几何检查**：检测几何为空 / 无效，以及自相交、未闭合等几何错误
- **属性重复检查**：检查记录之间是否存在完全相同的重复要素
- **空间参考检查**：检测图层是否缺少空间参考或空间参考未知
- **检查范围灵活**：可选择检查地图中 **所有图层** 或仅 **选中的图层**
- **问题定位**：在错误明细中选中问题后，可一键缩放并选中到对应的错误要素
- **报告导出**：将图层检查结果导出为 CSV 报告

## 界面预览

DockPane 面板主要包含以下区域：

1. **检查范围与规则**：选择检查范围（所有/选中图层）与启用的检查规则
2. **图层汇总表**：展示每个图层的检查状态（正常 / 警告 / 错误）及各项统计数据
3. **错误明细表**：列出每个错误要素的图层、OID、问题类型、字段及描述
4. **操作按钮**：开始检查、定位选中问题、导出报告

## 使用说明

### 环境要求

- ArcGIS Pro 3.x（开发使用 3.7）
- .NET SDK（项目目标框架为 `net10.0-windows`）
- Visual Studio 2022+（安装 ArcGIS Pro SDK for .NET）

### 构建

1. 克隆仓库并打开 `XYL_Tools.slnx` 解决方案
2. 确保本机已安装 ArcGIS Pro（构建依赖 `C:\Program Files\ArcGIS\Pro\bin` 下的程序集）
3. 使用 Visual Studio 构建项目，ArcGIS Pro 会自动加载生成的 `.esriAddinX` 文件

### 使用步骤

1. 在 ArcGIS Pro 中打开一个包含要素图层的地图
2. 点击功能区「测试」选项卡下的 **图层检查按钮**，打开「GeoTools Pro - 数据质量检查」面板
3. 选择检查范围和检查规则，点击 **开始检查**
4. 检查完成后，可在图层汇总表中查看每个图层的状态与统计
5. 在错误明细表中选择一条错误，点击 **定位选中问题** 可跳转到对应要素
6. 点击 **导出报告** 将检查结果保存为 CSV

## 项目结构

```
XYL_Tools/
├── Config.daml            # Add-In 配置（按钮、DockPane 注册）
├── Button1.cs             # 打开检查面板的按钮
├── Module1.cs             # Add-In 模块入口
├── DataCheckDockPaneView.xaml / .cs     # 检查面板视图
├── DataCheckDockPaneViewModel.cs        # 检查面板 ViewModel（命令、数据绑定）
├── Models/                # 数据模型
│   ├── CheckOptions.cs    # 检查选项（规则开关、检查范围）
│   ├── CheckStatus.cs     # 检查状态枚举（正常/警告/错误）
│   ├── FeatureIssue.cs    # 要素问题明细
│   └── LayerCheckResult.cs# 图层检查结果汇总
└── Services/              # 业务逻辑层
    ├── DataQualityChecker.cs  # 检查调度器，统一遍历游标
    ├── NullValueChecker.cs    # 空值检查
    ├── GeometryChecker.cs     # 几何检查
    └── DuplicateChecker.cs    # 属性重复检查
```

## 架构说明

项目采用 **MVVM** 架构，业务逻辑与 UI 分离：

- **Model**：`Models/` 目录存放数据模型，`CheckOptions` 实现 `INotifyPropertyChanged` 用于双向绑定
- **Service**：`Services/` 目录存放具体检查逻辑，所有检查通过一次游标遍历完成，避免重复读取数据
- **View / ViewModel**：DockPane 面板，ViewModel 仅负责命令与 UI 状态，不包含检查业务逻辑

## 许可证

待补充

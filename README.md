# 西门子STEP7趋势监控工具 (S7TrendMonitor)

基于 .NET 8 WinForms 的西门子 S7 PLC 趋势监控工具，支持实时数据采集、多游标分析、归一化显示。解决西门子step7软件没有趋势曲线的问题，方便日常调试，故障维修时数据监控

## 功能特性

- **多通信方式**：支持以太网 (TCP/IP) 和 MPI (PC Adapter USB / CP5611) 通信
- **实时趋势图**：基于 ScottPlot 5 的高性能数据可视化
- **多游标分析**：支持最多 6 条彩色游标线，实时显示各变量在游标位置的值
- **独立量程模式**：归一化显示，解决多变量量程差异过大的问题
- **变量管理**：支持添加、编辑、删除监控变量，支持多种 S7 数据类型
- **数据存储**：SQLite 数据库存储历史数据，支持数据保留策略
- **单文件部署**：编译为自包含单文件 EXE，无需安装运行时

## 支持的 S7 数据类型

| 类型 | 说明 | 字节长度 |
|------|------|----------|
| Bit | 位 (如 M0.0) | 1 |
| Byte | 字节 (如 MB0) | 1 |
| Word | 字 (如 MW0) | 2 |
| Int | 整数 (如 MW0) | 2 |
| DWord | 双字 (如 MD0) | 4 |
| DInt | 双整数 (如 MD0) | 4 |
| Real | 浮点数 (如 MD0) | 4 |

## 地址格式

- DB 块：`DB105.DBD2` (DB号.类型偏移)
- M 区：`MD0`, `MW10`, `M0.0`
- I 区：`IW0`, `IB0`
- Q 区：`QW0`, `QB0`

> 注：S7 地址不需要对齐，如 `DB105.DBD2` 是有效的浮点地址。

## 技术栈

- .NET 8 (net8.0-windows)
- ScottPlot 5 (数据可视化)
- S7NetPlus (以太网 S7 通信)
- libnodave (MPI 通信)
- Microsoft.Data.Sqlite (数据存储)

## 项目结构

```
S7TrendMonitor/
├── Chart/              # 图表服务 (趋势图、游标、归一化)
│   ├── TrendChartService.cs
│   └── VariableSeries.cs
├── Communication/      # PLC 通信
│   ├── IPlcCommunication.cs
│   ├── EthernetPlcService.cs   # 以太网通信
│   ├── MpiPlcService.cs        # MPI 通信
│   ├── PlcServiceFactory.cs    # 通信工厂
│   ├── S7AddressParser.cs      # 地址解析
│   └── LibnodaveNative.cs      # libnodave P/Invoke
├── Config/             # 配置管理
├── DataAcquisition/    # 数据采集服务
├── Forms/              # UI 窗体
│   ├── MainForm.cs             # 主窗体
│   ├── ConnectionForm.cs       # 连接设置
│   ├── SettingsForm.cs         # 参数设置
│   └── VariableEditForm.cs     # 变量编辑
├── Models/             # 数据模型
├── Storage/            # 数据存储 (SQLite)
├── Utils/              # 工具类
├── libs/               # 原生库
│   └── libnodave.dll
├── Program.cs          # 程序入口
├── S7TrendMonitor.csproj
└── app.ico             # 应用图标
```

## 构建方式

```bash
# 需要安装 .NET SDK 8.0
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

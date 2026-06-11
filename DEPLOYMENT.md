# 班组管理系统 - 部署指南

## 📋 项目概述

班组管理系统是一个基于 Flask 的企业级班组管理平台，包含备件管理、设备型号搜索、数据采集、电机维修、文档管理等功能。

---

## 📦 环境要求

| 项目 | 要求 | 说明 |
|------|------|------|
| 操作系统 | Windows 10/11 | 推荐使用 Windows |
| Python | 3.8 或更高版本 | 需要安装 Python |
| 端口 | 3000 | 确保端口未被占用 |

---

## 🚀 快速安装

### 方法一：一键安装（推荐）

1. **复制项目**：将整个 `team` 文件夹复制到目标电脑任意位置
2. **运行安装脚本**：双击 `install.bat`
3. **等待完成**：脚本会自动完成所有安装步骤

### 方法二：手动安装

#### 1. 安装 Python
下载并安装 Python 3.8+：https://www.python.org/downloads/

#### 2. 创建虚拟环境
```cmd
cd team
python -m venv venv
```

#### 3. 激活虚拟环境
```cmd
venv\Scripts\activate.bat
```

#### 4. 安装依赖
```cmd
pip install -r requirements.txt
```

#### 5. 初始化数据库
```cmd
python -c "from app import create_app; app = create_app(); app.app_context().push(); from models import db; db.create_all()"
```

#### 6. 创建目录
```cmd
mkdir static\uploads\BOM_清单
mkdir static\uploads\MCS图纸
mkdir backups
```

---

## 🔧 配置说明

### 主要配置文件

**config.py** - 系统配置文件

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| SECRET_KEY | 密钥，用于加密会话 | 自动生成 |
| DATABASE_URI | 数据库路径 | sqlite:///app.db |
| UPLOAD_FOLDER | 上传文件目录 | static/uploads |
| MAX_CONTENT_LENGTH | 最大上传文件大小 | 500MB |

### 修改配置

使用文本编辑器打开 `config.py` 修改配置。

---

## 🎯 启动方式

### 方式一：双击快捷方式
双击桌面上的 `启动班组系统.bat`

### 方式二：命令行启动
```cmd
cd team
venv\Scripts\activate.bat
python start_server.py
```

### 方式三：生产环境启动
```cmd
cd team
venv\Scripts\activate.bat
python -c "from waitress import serve; from app import create_app; serve(create_app(), host='0.0.0.0', port=3000, threads=16)"
```

---

## 🌐 访问方式

启动服务后，在浏览器中访问：
- **本地访问**: http://localhost:3000
- **局域网访问**: http://[本机IP]:3000

---

## 👤 默认账号

| 角色 | 用户名 | 密码 |
|------|--------|------|
| 管理员 | admin | 123456 |

---

## 📁 项目结构

```
team/
├── blueprints/          # 功能模块
│   ├── spare_parts.py   # 备件管理
│   ├── documents.py     # 文档管理
│   ├── data_search.py   # 数据采集
│   └── ...
├── static/              # 静态资源
│   ├── css/             # 样式文件
│   ├── js/              # JavaScript
│   └── uploads/         # 上传文件
├── templates/           # HTML模板
├── app.py               # 应用入口
├── config.py            # 配置文件
├── models.py            # 数据库模型
├── requirements.txt     # 依赖列表
├── install.bat          # 一键安装脚本
└── start_server.py      # 启动脚本
```

---

## 💾 数据迁移

### 备份数据
```cmd
python backup.py
```

### 恢复数据
将备份文件复制到 `backups/` 目录，然后运行：
```cmd
python backup.py --restore backups/[备份文件名]
```

---

## ❓ 常见问题

### Q1: 端口被占用
```cmd
netstat -ano | findstr ":3000"
taskkill /F /PID [进程ID]
```

### Q2: Python命令找不到
确保Python已添加到系统环境变量PATH

### Q3: 依赖安装失败
```cmd
pip install --upgrade pip
pip install -r requirements.txt --timeout=120
```

### Q4: 首次启动慢
首次启动需要加载BOM缓存，耐心等待几分钟

---

## 📞 技术支持

如有问题，请联系开发人员。
# bilibili

本仓库包含两个项目：

---

## 1. BilibiliApp — Blazor Web App

基于 .NET 10 的 Blazor Web App 项目，位于 `BilibiliApp/` 目录。

### 运行方法

```bash
cd BilibiliApp
dotnet run
```

启动后在浏览器打开 `https://localhost:5001`（或终端提示的端口）即可。

### 构建方法

```bash
cd BilibiliApp
dotnet build
```

---

## 2. bilibili.py — B站视频自动点赞 & 后台音频播放工具

功能：
1. 输入 BV 号（不含开头的 `BV`）
2. 自动点赞（需 Chrome 已登录 B 站）
3. 爬取并显示视频时长
4. 用户输入开始时间，后台播放音频

### 依赖安装

```bash
pip install -r requirements.txt
# 另需系统安装 ffmpeg 和 yt-dlp
```

### 使用方法

```bash
python bilibili.py
```

---
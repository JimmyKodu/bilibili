# bilibili

B站视频自动点赞 & 后台音频播放工具。

## 功能

1. **输入 BV 号**（不含开头的 `BV`）
2. **自动点赞**（调用已登录 B 站账号的 Chrome 浏览器）
3. **爬取视频时长**（通过 Bilibili 公开 API）
4. **用户输入开始时间**，在后台播放音频

## 依赖

### Python 包

```bash
pip install -r requirements.txt
```

### 系统工具

| 工具 | 用途 | 安装方法 |
|------|------|----------|
| Google Chrome | 浏览器自动化（点赞） | [官网下载](https://www.google.com/chrome/) |
| ChromeDriver | Selenium 驱动（版本需与 Chrome 匹配） | `pip install selenium` 会自动管理 |
| `ffmpeg` / `ffplay` | 后台音频播放 | `sudo apt install ffmpeg`（Linux）/ [官网](https://ffmpeg.org/) |
| `yt-dlp` | 解析 B 站音频流地址 | `pip install yt-dlp` 或 [官网](https://github.com/yt-dlp/yt-dlp) |

## 使用方法

```bash
python bilibili.py
```

运行后按提示操作：

```
请输入 BV号（不含开头的 BV，例如 1GJ411n7U）: 1GJ411n7U
```

- 程序会自动打开 Chrome，使用已登录的 B 站账号点赞视频
- 显示视频标题和时长
- 输入开始播放时间（支持 `90`、`1:30`、`0:01:30` 等格式）
- 音频在后台播放，按 **Enter** 停止

## 注意事项

- 自动点赞功能需要 Chrome 浏览器已登录 B 站账号
- 程序会使用系统默认的 Chrome 用户数据目录以复用登录状态
- 若 Chrome 正在运行，可能出现配置文件冲突，请先关闭 Chrome 再运行
#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
B站视频自动点赞与后台音频播放工具

功能：
  1. 输入BV号（不含开头的BV）
  2. 自动点赞（需当前浏览器已登录B站账号）
  3. 爬取并显示视频时长
  4. 用户输入开始时间
  5. 后台播放音频

依赖安装：
  pip install -r requirements.txt
  # 同时需要在系统中安装 yt-dlp 和 ffplay（ffmpeg 包含 ffplay）

使用方法：
  python bilibili.py
"""

import os
import sys
import time
import shutil
import platform
import subprocess

import requests
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import (
    TimeoutException,
    NoSuchElementException,
    WebDriverException,
)

# ---------------------------------------------------------------------------
# 常量
# ---------------------------------------------------------------------------

BILIBILI_API_BASE = "https://api.bilibili.com"
BILIBILI_VIDEO_BASE = "https://www.bilibili.com/video"

REQUEST_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/124.0.0.0 Safari/537.36"
    ),
    "Referer": "https://www.bilibili.com",
}

# 可能的点赞按钮 CSS 选择器（按优先级排列）
LIKE_BTN_SELECTORS = [
    ".video-like",
    "[class*='like-btn']",
    ".like",
    "[data-report*='like']",
    "//span[contains(@class,'like')]",  # XPath 备用（前缀 // 标识 XPath）
]

# ---------------------------------------------------------------------------
# 工具函数
# ---------------------------------------------------------------------------


def format_duration(seconds: int) -> str:
    """将秒数格式化为可读字符串（HH:MM:SS 或 MM:SS）。"""
    seconds = max(0, int(seconds))
    hours = seconds // 3600
    minutes = (seconds % 3600) // 60
    secs = seconds % 60
    if hours > 0:
        return f"{hours:02d}:{minutes:02d}:{secs:02d}"
    return f"{minutes:02d}:{secs:02d}"


def parse_time(time_str: str) -> int:
    """
    解析时间字符串为秒数。
    支持格式：
      - 纯秒数：  "90"
      - MM:SS：   "1:30"
      - HH:MM:SS："0:01:30"
    解析失败返回 0。
    """
    time_str = time_str.strip()
    if not time_str:
        return 0
    parts = time_str.split(":")
    try:
        if len(parts) == 3:
            return int(parts[0]) * 3600 + int(parts[1]) * 60 + int(parts[2])
        if len(parts) == 2:
            return int(parts[0]) * 60 + int(parts[1])
        return int(time_str)
    except ValueError:
        return 0


def check_tool(name: str) -> bool:
    """检查外部命令行工具是否存在（跨平台）。"""
    return shutil.which(name) is not None


# ---------------------------------------------------------------------------
# Bilibili API
# ---------------------------------------------------------------------------


def get_video_info(bvid: str) -> dict:
    """
    通过 Bilibili 公开 API 获取视频信息。

    返回包含 title、duration 等字段的字典，失败时返回空字典。
    """
    url = f"{BILIBILI_API_BASE}/x/web-interface/view?bvid=BV{bvid}"
    try:
        resp = requests.get(url, headers=REQUEST_HEADERS, timeout=10)
        resp.raise_for_status()
        data = resp.json()
        if data.get("code") == 0:
            return data["data"]
        print(f"[API] 错误 {data.get('code')}: {data.get('message', '未知错误')}")
    except requests.exceptions.RequestException as exc:
        print(f"[网络] 请求失败: {exc}")
    return {}


# ---------------------------------------------------------------------------
# Selenium 浏览器操作
# ---------------------------------------------------------------------------


def _get_chrome_user_data_dir() -> str:
    """根据当前操作系统返回 Chrome 用户数据目录的默认路径。"""
    system = platform.system()
    if system == "Windows":
        return os.path.join(
            os.environ.get("LOCALAPPDATA", ""),
            "Google", "Chrome", "User Data",
        )
    if system == "Darwin":
        return os.path.expanduser(
            "~/Library/Application Support/Google/Chrome"
        )
    # Linux / 其他
    return os.path.expanduser("~/.config/google-chrome")


def create_driver(headless: bool = False) -> webdriver.Chrome:
    """
    创建 Chrome WebDriver。

    优先使用现有 Chrome 用户配置文件（以保留登录状态）；
    如未找到，则使用临时配置文件（需手动登录）。
    """
    options = Options()
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("--disable-blink-features=AutomationControlled")
    options.add_experimental_option("excludeSwitches", ["enable-automation"])
    options.add_experimental_option("useAutomationExtension", False)

    if headless:
        options.add_argument("--headless=new")

    profile_dir = _get_chrome_user_data_dir()
    if os.path.isdir(profile_dir):
        options.add_argument(f"--user-data-dir={profile_dir}")
        print(f"[浏览器] 使用 Chrome 配置: {profile_dir}")
    else:
        print("[浏览器] 未找到 Chrome 配置文件，将使用新实例（需手动登录 B站）")

    return webdriver.Chrome(options=options)


def auto_like(bvid: str, driver: webdriver.Chrome) -> bool:
    """
    导航至指定视频并点击点赞按钮。

    如果已登录且点赞成功返回 True，否则返回 False。
    """
    video_url = f"{BILIBILI_VIDEO_BASE}/BV{bvid}"
    print(f"[浏览器] 正在打开: {video_url}")
    driver.get(video_url)

    wait = WebDriverWait(driver, 20)

    # 等待页面主体加载
    try:
        wait.until(EC.presence_of_element_located((By.TAG_NAME, "video")))
    except TimeoutException:
        print("[浏览器] 视频元素加载超时，尝试继续...")

    time.sleep(2)  # 等待 JS 渲染完成

    # 尝试多种选择器定位点赞按钮
    like_btn = None
    for selector in LIKE_BTN_SELECTORS:
        try:
            if selector.startswith("//"):
                like_btn = driver.find_element(By.XPATH, selector)
            else:
                like_btn = driver.find_element(By.CSS_SELECTOR, selector)
            break
        except NoSuchElementException:
            continue

    if like_btn is None:
        print("[浏览器] 未找到点赞按钮（可能未登录或页面结构已变化）")
        return False

    # 检查是否已经点赞
    btn_class = like_btn.get_attribute("class") or ""
    aria_pressed = like_btn.get_attribute("aria-pressed") or ""
    if "on" in btn_class or "liked" in btn_class or aria_pressed == "true":
        print("[浏览器] 该视频已点赞过，无需重复点赞")
        return True

    try:
        driver.execute_script("arguments[0].click();", like_btn)
        time.sleep(1)
        # 重新获取属性验证点赞结果
        btn_class = like_btn.get_attribute("class") or ""
        if "on" in btn_class or "liked" in btn_class:
            print("[浏览器] ✓ 点赞成功！")
        else:
            print("[浏览器] 点击完成（请确认浏览器是否已登录 B站）")
        return True
    except WebDriverException as exc:
        print(f"[浏览器] 点赞操作失败: {exc}")
        return False


# ---------------------------------------------------------------------------
# 音频播放
# ---------------------------------------------------------------------------


def _get_audio_stream_url(video_url: str) -> str:
    """使用 yt-dlp 获取视频的最佳音频流地址。"""
    result = subprocess.run(
        ["yt-dlp", "-g", "-f", "bestaudio/best", video_url],
        capture_output=True,
        text=True,
        timeout=30,
    )
    urls = result.stdout.strip().splitlines()
    # yt-dlp 分离流时返回多行（视频流在前，音频流在后），取最后一行作为音频地址
    return urls[-1] if urls else ""


def play_audio_background(bvid: str, start_seconds: int) -> "subprocess.Popen | None":
    """
    在后台播放指定 BV 视频的音频，从 start_seconds 秒处开始。

    依赖 yt-dlp 和 ffplay（来自 ffmpeg）。
    返回后台进程对象，失败时返回 None。
    """
    if not check_tool("yt-dlp"):
        print("[音频] 错误: 未找到 yt-dlp，请先安装: pip install yt-dlp")
        return None
    if not check_tool("ffplay"):
        print("[音频] 错误: 未找到 ffplay，请先安装 ffmpeg")
        return None

    video_url = f"{BILIBILI_VIDEO_BASE}/BV{bvid}"
    print("[音频] 正在解析音频流...")

    try:
        audio_url = _get_audio_stream_url(video_url)
    except subprocess.TimeoutExpired:
        print("[音频] 解析音频流超时")
        return None
    except FileNotFoundError:
        print("[音频] 错误: 未找到 yt-dlp")
        return None

    if not audio_url:
        print("[音频] 无法获取音频流地址（可能需要登录或该视频受限）")
        return None

    start_str = format_duration(start_seconds)
    print(f"[音频] 开始后台播放，起始位置: {start_str}")

    cmd = [
        "ffplay",
        "-nodisp",       # 不显示视频窗口
        "-autoexit",     # 播放结束后自动退出
        "-vn",           # 仅播放音频
        "-ss", str(start_seconds),
        audio_url,
    ]

    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    print(f"[音频] 后台播放中（进程 PID: {proc.pid}）")
    print(f"[音频] 提示: 使用 kill {proc.pid} 可强制停止播放")
    return proc


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------


def main() -> None:
    print("=" * 55)
    print("   B站视频自动点赞 & 后台音频播放工具")
    print("=" * 55)

    # 1. 输入 BV 号
    raw = input("\n请输入 BV号（不含开头的 BV，例如 1GJ411n7U）: ").strip()
    if not raw:
        print("错误: BV号不能为空")
        sys.exit(1)

    # 兼容用户误输入了 BV 前缀
    bvid = raw[2:] if raw.upper().startswith("BV") else raw

    # 2. 获取视频信息
    print(f"\n[API] 正在获取视频信息 (BV{bvid})...")
    info = get_video_info(bvid)
    if not info:
        print("错误: 无法获取视频信息，请检查 BV 号是否正确。")
        sys.exit(1)

    title = info.get("title", "（未知标题）")
    duration: int = info.get("duration", 0)
    duration_str = format_duration(duration)

    print(f"  标题: {title}")
    print(f"  时长: {duration_str}（共 {duration} 秒）")

    # 3. 自动点赞
    print("\n[浏览器] 正在启动 Chrome 进行自动点赞...")
    driver = None
    try:
        driver = create_driver()
        auto_like(bvid, driver)
    except WebDriverException as exc:
        print(f"[浏览器] 启动失败: {exc}")
        print("[浏览器] 跳过点赞，继续后续流程...")
    finally:
        if driver:
            driver.quit()
            print("[浏览器] 浏览器已关闭")

    # 4. 询问开始时间
    print(f"\n视频时长: {duration_str}")
    time_input = input(
        "请输入开始播放时间（格式: 秒数 / MM:SS / HH:MM:SS，直接回车从头播放）: "
    ).strip()

    start_seconds = parse_time(time_input) if time_input else 0

    if start_seconds >= duration > 0:
        print(
            f"警告: 开始时间 {format_duration(start_seconds)} "
            f"超过视频时长 {duration_str}，将从头播放"
        )
        start_seconds = 0

    # 5. 后台播放音频
    print(f"\n[音频] 起始位置: {format_duration(start_seconds)}")
    proc = play_audio_background(bvid, start_seconds)

    if proc:
        print("\n音频正在后台播放中，按 Enter 键停止并退出...")
        try:
            input()
        except KeyboardInterrupt:
            pass
        finally:
            proc.terminate()
            proc.wait()
            print("[音频] 播放已停止")
    else:
        print("\n提示: 请确保已安装 yt-dlp（pip install yt-dlp）和 ffmpeg。")

    print("\n程序结束。")


if __name__ == "__main__":
    main()

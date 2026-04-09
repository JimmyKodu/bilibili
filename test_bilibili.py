"""
单元测试 - bilibili.py 中的工具函数
"""
import unittest
from bilibili import format_duration, parse_time


class TestFormatDuration(unittest.TestCase):
    def test_zero_seconds(self):
        self.assertEqual(format_duration(0), "00:00")

    def test_negative_seconds(self):
        # 负数应当被截为 0
        self.assertEqual(format_duration(-10), "00:00")

    def test_seconds_only(self):
        self.assertEqual(format_duration(45), "00:45")

    def test_minutes_and_seconds(self):
        self.assertEqual(format_duration(90), "01:30")

    def test_exact_minute(self):
        self.assertEqual(format_duration(60), "01:00")

    def test_hours_minutes_seconds(self):
        self.assertEqual(format_duration(3661), "01:01:01")

    def test_exact_hour(self):
        self.assertEqual(format_duration(3600), "01:00:00")

    def test_large_value(self):
        # 2 小时 5 分 3 秒
        self.assertEqual(format_duration(7503), "02:05:03")


class TestParseTime(unittest.TestCase):
    def test_plain_seconds(self):
        self.assertEqual(parse_time("90"), 90)

    def test_mm_ss(self):
        self.assertEqual(parse_time("1:30"), 90)

    def test_hh_mm_ss(self):
        self.assertEqual(parse_time("0:01:30"), 90)

    def test_exact_hour(self):
        self.assertEqual(parse_time("1:00:00"), 3600)

    def test_empty_string(self):
        self.assertEqual(parse_time(""), 0)

    def test_whitespace(self):
        self.assertEqual(parse_time("  2:05  "), 125)

    def test_invalid_string(self):
        self.assertEqual(parse_time("abc"), 0)

    def test_zero(self):
        self.assertEqual(parse_time("0"), 0)

    def test_mm_ss_leading_zero(self):
        self.assertEqual(parse_time("00:45"), 45)


if __name__ == "__main__":
    unittest.main()

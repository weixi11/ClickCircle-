using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClickCircle
{
    /// <summary>
    /// 把 PDF 标注的 /Contents 还原成“屏幕上看到的标签文字”。
    ///
    /// PDF-XChange 的测量标注（如 PolyLineDimension）把标签按行写进 Contents，行之间用反斜杠
    /// 分隔，最后一行是测量值；渲染时标签行顺序与存储顺序相反、行之间用空格连接。例如
    /// <c>"EAD\300X200\\\n50.89 m "</c> 在图纸上显示为 <c>300X200 EAD</c>。
    /// 没有反斜杠的标注（普通文字批注、早期用斜杠写的标签）按原顺序取文字。
    /// </summary>
    public static class AnnotationLabel
    {
        private const string MeasurementValuePattern = @"^[+-]?\d[\d.,]*(?:\s*[A-Za-z\u00B0]{1,3}[\u00B2\u00B3]?)?$";

        /// <summary>这一行是不是测量值（数字，可带单位），如 “50.89 m ”、“0.6 m²”。</summary>
        public static bool IsMeasurementValue(string line)
        {
            if (line == null) return false;
            string value = line.Trim();
            if (value.Length == 0) return false;
            return Regex.IsMatch(value, MeasurementValuePattern, RegexOptions.CultureInvariant);
        }

        /// <summary>从 Contents 取可见标签；取不到返回 null。</summary>
        public static string FromContents(string contents)
        {
            if (String.IsNullOrWhiteSpace(contents)) return null;
            string normalized = contents.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            int count = lines.Length;
            while (count > 0 && (String.IsNullOrWhiteSpace(lines[count - 1]) ||
                                 IsMeasurementValue(lines[count - 1])))
                --count;

            var parts = new List<string>();
            bool reversed = false;
            for (int i = 0; i < count; ++i)
            {
                string line = lines[i];
                if (line.IndexOf('\\') >= 0)
                {
                    reversed = true;
                    foreach (string segment in line.Split('\\'))
                        AddPart(parts, segment);
                }
                else
                {
                    AddPart(parts, line);
                }
            }
            if (parts.Count == 0) return null;
            if (reversed) parts.Reverse();
            return String.Join(" ", parts);
        }

        private static void AddPart(List<string> parts, string value)
        {
            if (value == null) return;
            string trimmed = value.Trim();
            if (trimmed.Length > 0) parts.Add(trimmed);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace ClickCircle
{
    /// <summary>
    /// 可序列化的快捷键组合，面板显示为 "Ctrl+Alt+P"，存盘为 "None" 或同样的显示串。
    /// 只支持 Ctrl / Alt / Shift 三种修饰键加一个主键。
    /// </summary>
    public sealed class HotkeyBinding : IEquatable<HotkeyBinding>
    {
        private const Keys ModifierMask = Keys.Control | Keys.Alt | Keys.Shift;
        private const string StorageNone = "None";

        private static readonly HotkeyBinding emptyBinding = new HotkeyBinding(Keys.None, Keys.None);
        private static readonly HotkeyBinding defaultBinding = new HotkeyBinding(Keys.P, Keys.Control | Keys.Alt);
        private static readonly HashSet<string> KeyNames =
            new HashSet<string>(Enum.GetNames(typeof(Keys)), StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Keys> SymbolKeys =
            new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
            {
                { "`", Keys.Oemtilde },
                { "~", Keys.Oemtilde },
                { "-", Keys.OemMinus },
                { "_", Keys.OemMinus },
                { "=", Keys.Oemplus },
                { "[", Keys.OemOpenBrackets },
                { "{", Keys.OemOpenBrackets },
                { "]", Keys.OemCloseBrackets },
                { "}", Keys.OemCloseBrackets },
                { "\\", Keys.OemPipe },
                { ";", Keys.OemSemicolon },
                { ":", Keys.OemSemicolon },
                { "'", Keys.OemQuotes },
                { ",", Keys.Oemcomma },
                { "<", Keys.Oemcomma },
                { ".", Keys.OemPeriod },
                { ">", Keys.OemPeriod },
                { "/", Keys.OemQuestion },
                { "?", Keys.OemQuestion }
            };

        public HotkeyBinding(Keys key, Keys modifiers)
        {
            Key = key & Keys.KeyCode;
            Modifiers = modifiers & ModifierMask;
        }

        /// <summary>未设置：不注册任何快捷键。</summary>
        public static HotkeyBinding Empty { get { return emptyBinding; } }

        /// <summary>出厂默认：Ctrl+Alt+P。</summary>
        public static HotkeyBinding Default { get { return defaultBinding; } }

        public Keys Key { get; private set; }
        public Keys Modifiers { get; private set; }

        public bool IsEmpty { get { return Key == Keys.None; } }

        public string ToDisplay()
        {
            if (IsEmpty)
                return String.Empty;
            StringBuilder builder = new StringBuilder();
            if ((Modifiers & Keys.Control) != 0) builder.Append("Ctrl+");
            if ((Modifiers & Keys.Alt) != 0) builder.Append("Alt+");
            if ((Modifiers & Keys.Shift) != 0) builder.Append("Shift+");
            builder.Append(KeyName(Key));
            return builder.ToString();
        }

        public string ToStorage()
        {
            return IsEmpty ? StorageNone : ToDisplay();
        }

        public override string ToString()
        {
            return ToDisplay();
        }

        public bool Equals(HotkeyBinding other)
        {
            if (ReferenceEquals(other, null))
                return false;
            return Key == other.Key && Modifiers == other.Modifiers;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HotkeyBinding);
        }

        public override int GetHashCode()
        {
            return ((int)Key * 397) ^ (int)Modifiers;
        }

        public static bool IsModifierKey(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            return code == Keys.ControlKey || code == Keys.LControlKey || code == Keys.RControlKey ||
                   code == Keys.ShiftKey || code == Keys.LShiftKey || code == Keys.RShiftKey ||
                   code == Keys.Menu || code == Keys.LMenu || code == Keys.RMenu ||
                   code == Keys.LWin || code == Keys.RWin;
        }

        /// <summary>宽松解析；空串与非法值都算失败，交由调用方决定回退。</summary>
        public static bool TryParse(string text, out HotkeyBinding binding)
        {
            binding = emptyBinding;
            if (text == null)
                return false;
            string trimmed = text.Trim();
            if (trimmed.Length == 0)
                return false;
            if (trimmed.Equals(StorageNone, StringComparison.OrdinalIgnoreCase) || trimmed == "无")
            {
                binding = emptyBinding;
                return true;
            }

            string[] parts = trimmed.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            Keys modifiers = Keys.None;
            Keys key = Keys.None;
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index].Trim();
                if (part.Length == 0)
                    continue;

                Keys modifier;
                if (TryGetModifierName(part, out modifier))
                {
                    modifiers |= modifier;
                    continue;
                }

                Keys symbol;
                if (SymbolKeys.TryGetValue(part, out symbol))
                {
                    if (key != Keys.None)
                        return false;
                    key = symbol;
                    continue;
                }

                Keys parsed;
                // Enum.TryParse also accepts numbers and comma-separated flag combinations.
                if (!KeyNames.Contains(part) || !Enum.TryParse<Keys>(part, true, out parsed))
                    return false;
                if (IsModifierKey(parsed))
                {
                    modifier = ModifierOf(parsed);
                    if (modifier == Keys.None)
                        return false;
                    modifiers |= modifier;
                    continue;
                }
                if ((int)parsed <= 0 || (int)parsed > 0xFE ||
                    parsed == Keys.LButton || parsed == Keys.RButton || parsed == Keys.MButton ||
                    parsed == Keys.XButton1 || parsed == Keys.XButton2 || key != Keys.None)
                    return false;
                key = parsed;
            }

            if (key == Keys.None)
                return false;
            binding = new HotkeyBinding(key, modifiers);
            return true;
        }

        /// <summary>空值或非法值回退到默认快捷键。</summary>
        public static HotkeyBinding ParseOrDefault(string text)
        {
            HotkeyBinding parsed;
            if (TryParse(text, out parsed))
                return parsed;
            return defaultBinding;
        }

        private static bool TryGetModifierName(string text, out Keys modifier)
        {
            modifier = Keys.None;
            switch (text.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifier = Keys.Control;
                    return true;
                case "alt":
                case "menu":
                    modifier = Keys.Alt;
                    return true;
                case "shift":
                    modifier = Keys.Shift;
                    return true;
                default:
                    return false;
            }
        }

        private static Keys ModifierOf(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            if (code == Keys.ControlKey || code == Keys.LControlKey || code == Keys.RControlKey)
                return Keys.Control;
            if (code == Keys.ShiftKey || code == Keys.LShiftKey || code == Keys.RShiftKey)
                return Keys.Shift;
            if (code == Keys.Menu || code == Keys.LMenu || code == Keys.RMenu)
                return Keys.Alt;
            return Keys.None;
        }

        private static string KeyName(Keys key)
        {
            switch (key & Keys.KeyCode)
            {
                case Keys.Oemtilde: return "`";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                default:
                    string name = Enum.GetName(typeof(Keys), key & Keys.KeyCode);
                    return name ?? key.ToString();
            }
        }
    }
}

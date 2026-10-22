using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DeepSeekWhale
{
    public class AppConfig
    {
        public string KeyProtected = "";
        public string KeyHint = "";
        public int IntervalMin = 5;
        public double LowBalance = 10.0;
        public string Currency = "auto";
        public double MonthlyBudget = 0;
        public bool FollowDsh = true;
        public bool HideWithDsh = true;
        public bool Locked = false;
        public bool Mini = false;
        public string Skin = "blue";
        public bool WaterReminder = true;
        public int WaterIntervalMin = 60;
        public bool Wander = true;
        public int ReminderStartHour = 8;
        public int ReminderEndHour = 22;
        public int DwellSeconds = 20;
        public string ReminderText = "该喝水啦～\n起来走两步吧～\n看看远处，放松一下眼睛\n伸个懒腰吧～";
        public bool Dnd = false;
        public long DndUntil = 0;
        public bool DndAutoFullscreen = true;
        public bool DndAllowAlerts = true;
        public long LastPeakNotify = 0;
        public bool UseSystemProxy = true;
        public int X = -1;
        public int Y = -1;
        public long LastLowNotify = 0;
        public long LastTopupNotify = 0;
        public long LastSpikeNotify = 0;
        public long LastSetupPrompt = 0;

        public static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DeepSeekWhale.config.v1");

        public bool HasKey { get { return !string.IsNullOrEmpty(KeyProtected) && GetKey() != null; } }

        public void SetKey(string key)
        {
            key = (key ?? "").Trim();
            if (key.Length == 0)
            {
                KeyProtected = "";
                KeyHint = "";
                return;
            }
            byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), Entropy, DataProtectionScope.CurrentUser);
            KeyProtected = Convert.ToBase64String(enc);
            KeyHint = key.Length >= 4 ? key.Substring(key.Length - 4) : "****";
        }

        public string GetKey()
        {
            if (string.IsNullOrEmpty(KeyProtected)) return null;
            try
            {
                byte[] raw = ProtectedData.Unprotect(Convert.FromBase64String(KeyProtected), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch { return null; }
        }

        public AppConfig Clone()
        {
            var c = new AppConfig();
            c.KeyProtected = KeyProtected;
            c.KeyHint = KeyHint;
            c.IntervalMin = IntervalMin;
            c.LowBalance = LowBalance;
            c.Currency = Currency;
            c.MonthlyBudget = MonthlyBudget;
            c.FollowDsh = FollowDsh;
            c.HideWithDsh = HideWithDsh;
            c.Locked = Locked;
            c.Mini = Mini;
            c.Skin = Skin;
            c.WaterReminder = WaterReminder;
            c.WaterIntervalMin = WaterIntervalMin;
            c.Wander = Wander;
            c.ReminderStartHour = ReminderStartHour;
            c.ReminderEndHour = ReminderEndHour;
            c.DwellSeconds = DwellSeconds;
            c.ReminderText = ReminderText;
            c.Dnd = Dnd;
            c.DndUntil = DndUntil;
            c.DndAutoFullscreen = DndAutoFullscreen;
            c.DndAllowAlerts = DndAllowAlerts;
            c.LastPeakNotify = LastPeakNotify;
            c.UseSystemProxy = UseSystemProxy;
            c.X = X;
            c.Y = Y;
            c.LastLowNotify = LastLowNotify;
            c.LastTopupNotify = LastTopupNotify;
            c.LastSpikeNotify = LastSpikeNotify;
            c.LastSetupPrompt = LastSetupPrompt;
            return c;
        }

        public string ToJson()
        {
            var d = new Dictionary<string, object>();
            d["keyProtected"] = KeyProtected;
            d["keyHint"] = KeyHint;
            d["intervalMin"] = IntervalMin;
            d["lowBalance"] = LowBalance;
            d["currency"] = Currency;
            d["monthlyBudget"] = MonthlyBudget;
            d["followDsh"] = FollowDsh;
            d["hideWithDsh"] = HideWithDsh;
            d["locked"] = Locked;
            d["mini"] = Mini;
            d["skin"] = Skin;
            d["waterReminder"] = WaterReminder;
            d["waterIntervalMin"] = WaterIntervalMin;
            d["wander"] = Wander;
            d["reminderStartHour"] = ReminderStartHour;
            d["reminderEndHour"] = ReminderEndHour;
            d["dwellSeconds"] = DwellSeconds;
            d["reminderText"] = ReminderText;
            d["dnd"] = Dnd;
            d["dndUntil"] = DndUntil;
            d["dndAutoFullscreen"] = DndAutoFullscreen;
            d["dndAllowAlerts"] = DndAllowAlerts;
            d["lastPeakNotify"] = LastPeakNotify;
            d["useSystemProxy"] = UseSystemProxy;
            d["x"] = X;
            d["y"] = Y;
            d["lastLowNotify"] = LastLowNotify;
            d["lastTopupNotify"] = LastTopupNotify;
            d["lastSpikeNotify"] = LastSpikeNotify;
            d["lastSetupPrompt"] = LastSetupPrompt;
            return JsonUtil.Ser.Serialize(d);
        }

        public static AppConfig Load(string path)
        {
            var c = new AppConfig();
            try
            {
                if (!File.Exists(path)) return c;
                var d = JsonUtil.Parse(File.ReadAllText(path, Encoding.UTF8));
                c.KeyProtected = JsonUtil.Str(d, "keyProtected", "");
                c.KeyHint = JsonUtil.Str(d, "keyHint", "");
                c.IntervalMin = Math.Max(1, Math.Min(60, JsonUtil.Int(d, "intervalMin", 5)));
                c.LowBalance = Math.Max(0, JsonUtil.Dbl(d, "lowBalance", 10.0));
                c.Currency = JsonUtil.Str(d, "currency", "auto");
                c.MonthlyBudget = Math.Max(0, JsonUtil.Dbl(d, "monthlyBudget", 0));
                c.FollowDsh = JsonUtil.Bool(d, "followDsh", true);
                c.HideWithDsh = JsonUtil.Bool(d, "hideWithDsh", true);
                c.Locked = JsonUtil.Bool(d, "locked", false);
                c.Mini = JsonUtil.Bool(d, "mini", false);
                c.Skin = JsonUtil.Str(d, "skin", "blue");
                if (c.Skin != "sakura" && c.Skin != "mint") c.Skin = "blue";
                c.WaterReminder = JsonUtil.Bool(d, "waterReminder", true);
                c.WaterIntervalMin = Math.Max(15, Math.Min(240, JsonUtil.Int(d, "waterIntervalMin", 60)));
                c.Wander = JsonUtil.Bool(d, "wander", true);
                c.ReminderStartHour = Math.Max(0, Math.Min(23, JsonUtil.Int(d, "reminderStartHour", 8)));
                c.ReminderEndHour = Math.Max(0, Math.Min(23, JsonUtil.Int(d, "reminderEndHour", 22)));
                c.DwellSeconds = Math.Max(5, Math.Min(120, JsonUtil.Int(d, "dwellSeconds", 20)));
                c.ReminderText = JsonUtil.Str(d, "reminderText", c.ReminderText);
                c.Dnd = JsonUtil.Bool(d, "dnd", false);
                c.DndUntil = JsonUtil.Long(d, "dndUntil", 0);
                c.DndAutoFullscreen = JsonUtil.Bool(d, "dndAutoFullscreen", true);
                c.DndAllowAlerts = JsonUtil.Bool(d, "dndAllowAlerts", true);
                c.LastPeakNotify = JsonUtil.Long(d, "lastPeakNotify", 0);
                c.UseSystemProxy = JsonUtil.Bool(d, "useSystemProxy", true);
                c.X = JsonUtil.Int(d, "x", -1);
                c.Y = JsonUtil.Int(d, "y", -1);
                c.LastLowNotify = JsonUtil.Long(d, "lastLowNotify", 0);
                c.LastTopupNotify = JsonUtil.Long(d, "lastTopupNotify", 0);
                c.LastSpikeNotify = JsonUtil.Long(d, "lastSpikeNotify", 0);
                c.LastSetupPrompt = JsonUtil.Long(d, "lastSetupPrompt", 0);
                if (!string.Equals(c.Currency, "CNY") && !string.Equals(c.Currency, "USD")) c.Currency = "auto";
            }
            catch (Exception ex)
            {
                Log.Warn("config load failed: " + ex.Message);
            }
            return c;
        }

        public void Save(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, ToJson(), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Log.Error("config save failed: " + ex.Message);
            }
        }
    }

    public static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "DeepSeekWhale";
        const string LinkName = "DeepSeek 小鲸鱼.lnk";

        public static string LinkPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), LinkName); }
        }

        public static bool IsEnabled(string exePath)
        {
            try
            {
                if (File.Exists(LinkPath)) return true;
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    string v = k.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(v);
                }
            }
            catch { return false; }
        }

        public static bool Enable(string exePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(LinkPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { LinkPath });
                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                st.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { "--autostart" });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exePath) });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exePath + ",0" });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "DeepSeek 余额小鲸鱼" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return File.Exists(LinkPath);
            }
            catch (Exception ex) { Log.Warn("autostart enable failed: " + ex.Message); return false; }
        }

        public static bool Disable()
        {
            try
            {
                if (File.Exists(LinkPath)) File.Delete(LinkPath);
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k != null && k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex) { Log.Warn("autostart disable failed: " + ex.Message); return false; }
        }
    }
}

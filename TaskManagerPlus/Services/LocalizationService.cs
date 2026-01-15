using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TaskManagerPlus.Services
{
    public enum AppLanguage
    {
        VI,
        EN
    }

    public static class LocalizationService
    {
        private static Dictionary<string, string> _dict = new Dictionary<string, string>();

        public static AppLanguage CurrentLanguage = AppLanguage.VI;

        public static void LoadLanguage(AppLanguage lang)
        {
            CurrentLanguage = lang;

            string fileName = (lang == AppLanguage.VI) ? "vi.json" : "en.json";
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Localization");
            string path = Path.Combine(dir, fileName);

            // đảm bảo folder tồn tại
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // nếu thiếu file -> không crash, báo path + fallback
            if (!File.Exists(path))
            {
                _dict = new Dictionary<string, string>(); // fallback rỗng
                System.Windows.Forms.MessageBox.Show(
                    "Không tìm thấy file ngôn ngữ:\n\n" + path +
                    "\n\nHãy kiểm tra:\n- File vi.json/en.json có Build Action=Content\n- Copy to Output Directory=Copy if newer\n- Folder Localization có được copy vào bin hay chưa",
                    "Language file not found",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning
                );
                return;
            }

            string json = File.ReadAllText(path);
            _dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string val;
            if (_dict.TryGetValue(key, out val)) return val;
            return "[" + key + "]";
        }
    }
}

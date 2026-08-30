using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TT
{
    /// <summary>
    /// 常用字統計。刻意用一個獨立的 db (files/user_freq.db)，
    /// 令使用者可以隨時換走／更新 files/dataset.db 而不會失去自己的習慣，
    /// 反過來刪掉這個檔案也只是把統計清零，不會影響碼表。
    ///
    /// word_freq : 打過的單字 + 次數
    /// pair_freq : 連續打過的兩個字 (prev -> word) + 次數
    /// </summary>
    class TTFreq
    {
        public const string DbName = "files/user_freq.db";

        /// <summary>要「超過兩次」先算常用，即 count &gt; 2。</summary>
        public const int MinCount = 2;

        private SQLiteConnection con;
        private bool ok = false;

        public TTFreq()
        {
            try
            {
                // 跟 tt_settings.ini 一樣用 exe 隔籬，唔跟 working directory，
                // 咁樣就算用捷徑／開機自動啟動都唔會周圍散落 db 檔。
                string baseDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string dbPath = string.IsNullOrEmpty(baseDir)
                    ? DbName
                    : Path.Combine(baseDir, DbName.Replace('/', Path.DirectorySeparatorChar));

                string dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                con = new SQLiteConnection($"Data Source={dbPath}");
                con.Open();
                exec(@"CREATE TABLE IF NOT EXISTS word_freq (
                            word  TEXT NOT NULL PRIMARY KEY,
                            count INTEGER NOT NULL DEFAULT 0
                       );");
                exec(@"CREATE TABLE IF NOT EXISTS pair_freq (
                            prev  TEXT NOT NULL,
                            word  TEXT NOT NULL,
                            count INTEGER NOT NULL DEFAULT 0,
                            PRIMARY KEY(prev, word)
                       );");
                exec("CREATE INDEX IF NOT EXISTS idx_pair_prev ON pair_freq(prev, count DESC);");
                ok = true;
            }
            catch (Exception e)
            {
                // 統計壞了都唔應該打唔到字，靜靜地熄咗佢
                Debug.WriteLine($"TTFreq init failed: {e.Message}");
                ok = false;
            }
        }

        private void exec(string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>只計一個字元 (text element，emoji / 增補字都算一個)。</summary>
        public static bool isSingleWord(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return new StringInfo(s).LengthInTextElements == 1;
        }

        /// <summary>
        /// 記低啱啱打出的字。prev 係上一個打出的單字 (無就傳 "")。
        /// 只記單字；詞／標點對／速選裡的多字項目一律不記。
        /// </summary>
        public void record(string prev, string word)
        {
            if (!ok || !isSingleWord(word)) return;
            try
            {
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO word_freq(word, count) VALUES(@w, 1) " +
                    "ON CONFLICT(word) DO UPDATE SET count = count + 1;", con))
                {
                    cmd.Parameters.AddWithValue("@w", word);
                    cmd.ExecuteNonQuery();
                }

                if (isSingleWord(prev))
                {
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO pair_freq(prev, word, count) VALUES(@p, @w, 1) " +
                        "ON CONFLICT(prev, word) DO UPDATE SET count = count + 1;", con))
                    {
                        cmd.Parameters.AddWithValue("@p", prev);
                        cmd.Parameters.AddWithValue("@w", word);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"TTFreq.record failed: {e.Message}");
            }
        }

        /// <summary>
        /// 取 words 之中「打過超過兩次」的字同其次數。無份的字唔會出現喺結果入面。
        /// </summary>
        public Dictionary<string, int> getWordCounts(ICollection<string> words)
        {
            var result = new Dictionary<string, int>();
            if (!ok || words == null || words.Count == 0) return result;

            var distinct = words.Where(w => isSingleWord(w)).Distinct().ToList();
            if (distinct.Count == 0) return result;

            try
            {
                var names = new List<string>();
                using (var cmd = new SQLiteCommand(con))
                {
                    for (int i = 0; i < distinct.Count; i++)
                    {
                        string p = "@w" + i;
                        names.Add(p);
                        cmd.Parameters.AddWithValue(p, distinct[i]);
                    }
                    cmd.Parameters.AddWithValue("@min", MinCount);
                    cmd.CommandText =
                        $"SELECT word, count FROM word_freq WHERE count > @min AND word IN ({string.Join(",", names)});";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"TTFreq.getWordCounts failed: {e.Message}");
            }
            return result;
        }

        /// <summary>
        /// prev 之後連續打過超過兩次的字，次數多排先。
        /// </summary>
        public List<string> getFollowers(string prev)
        {
            var result = new List<string>();
            if (!ok || !isSingleWord(prev)) return result;

            try
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT word FROM pair_freq WHERE prev = @p AND count > @min ORDER BY count DESC, word ASC;", con))
                {
                    cmd.Parameters.AddWithValue("@p", prev);
                    cmd.Parameters.AddWithValue("@min", MinCount);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"TTFreq.getFollowers failed: {e.Message}");
            }
            return result;
        }

        /// <summary>tray menu「清除常用字記錄」用。</summary>
        public void clear()
        {
            if (!ok) return;
            try
            {
                exec("DELETE FROM word_freq;");
                exec("DELETE FROM pair_freq;");
                exec("VACUUM;");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"TTFreq.clear failed: {e.Message}");
            }
        }
    }
}

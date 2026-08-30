using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Text;
/*
 * mode:
 * same
 */
namespace TT
{

    class TTCore
    {
        private SQLiteConnection con;

        public TTCore()
        {
            string cs = "Data Source=files/dataset.db";
            con = new SQLiteConnection(cs);
            con.Open();
        }

        /// <summary>
        /// 這裡的 query 全部係字串拼接，打到 ' (例如標點表入面的單引號) 就會爆 SQL。
        /// 過一次 esc 就唔會因為打咗個標點而死機。
        /// </summary>
        private static string esc(string s)
        {
            return s == null ? "" : s.Replace("'", "''");
        }

        private string[] sql2strs(string stm, string splitStr = "")
        {
            SQLiteCommand cmd = new SQLiteCommand(stm, con);
            object result = cmd.ExecuteScalar();
            if (result != null)
            {
                string str = result.ToString();
                StringInfo stru8 = new System.Globalization.StringInfo(str);

                //char[] charArr = str.ToCharArray();

                if (splitStr == "")
                {
                    string[] strs = new string[stru8.LengthInTextElements];
                    for (int i = 0, t = stru8.LengthInTextElements; i < t; i++)
                    {
                        strs[i] = stru8.SubstringByTextElements(i, 1);
                    }
                    return strs;
                }
                else
                {
                    //*
                    return str.Split(splitStr.ToCharArray()[0]); ;
                }

                //Debug.WriteLine($"{key} : {str} : {str.Length} : {str[2]} ");
                //Debug.WriteLine($"{charArr[1]} :{charArr[2]} : {charArr[3]} : {charArr[4]} ");
                //Debug.WriteLine($"{strs.Length} {strs}");

                //string[] rels = processRelate("人 乜 得 左 限 睇 咁 用𦾟 𦾟 可 論 錯 理 意 奈 謂 啦啦 關 聊 比 數 力 辜 心 線 論如何 端端 反");

                //Debug.WriteLine($"{rels.Length} {rels[7]} {rels[7].Length} {new System.Globalization.StringInfo(rels[7]).LengthInTextElements}");

            }
            else
            {
                //Debug.WriteLine($"{key} : no result");
                return null;
            }

        }

        public string[] keyInput(int key)
        {
            //string stm = $"SELECT characters FROM `mapped_table` WHERE id='{key}'";
            return sql2strs($"SELECT characters FROM `mapped_table` WHERE id='{key}'");
        }

        public string[] getRelate(string word)
        {
            return sql2strs($"SELECT candidates FROM `related_candidates_table` WHERE character='{esc(word)}'", " ");
        }

        /*
        public string[] getHomo2(string word)
        {
            string stm = $"SELECT candidates FROM `homophone_candidates_table` WHERE character='{word}'";
            return sql2strs(stm);
        }
        */

        public int[] getCode(string word) {
            string stm = $"SELECT `id` FROM `mapped_table` WHERE INSTR(`characters`,'{esc(word)}');";
            var nums = new List<int>();
            using (var command = new SQLiteCommand(stm, con))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int num = reader.GetInt32(0);
                        nums.Add(num);
                    }
                }
            }
            return nums.ToArray();
        }

        public string[] getHomo(string word)
        {

            var words = new List<string>();
            if (word.Length > 1) return words.ToArray();

            string stm = $"SELECT w1.char FROM word_meta w1 INNER JOIN word_meta w2 ON w1.ping = w2.ping WHERE w2.char = '{esc(word)}' ORDER BY CASE WHEN w1.ping2 = w2.ping2 THEN 0 ELSE 1 END ASC;";//, w1.freq


            using (var command = new SQLiteCommand(stm, con))
            {
                // Use parameters to prevent SQL injection
               // command.Parameters.AddWithValue("@InputWord", inputWord);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string chr= reader["char"].ToString();
                        //if (chr == word) continue;
                        words.Add(chr);
                    }
                }
            }
            /*
            string[] homo2=getHomo2(word);
            foreach(string s in homo2)
            {
                if (!words.Contains(s)) { 
                    words.Add(s);
                }
            }
            */

            return words.ToArray();
        }


        public string tcsc(string input)
        {
            StringBuilder output = new StringBuilder();
            foreach (char c in input)
            {
                string stm = $"SELECT `simplified` FROM `ts_chinese_table` WHERE `traditional`='{esc(c.ToString())}' LIMIT 1";
                SQLiteCommand cmd = new SQLiteCommand(stm, con);
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    string str = result.ToString();
                    output.Append(str);
                }
                else
                {
                    output.Append(c);
                }
            }
            return output.ToString();
        }

    }
}

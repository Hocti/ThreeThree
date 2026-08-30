# CLAUDE.md

三三正體中文輸入法 (TT) — 一個 portable 的粵語/中文輸入法，用一個**已過期**的專利 HK1035043 做字碼邏輯。
以 numpad 打 2~3 個數字碼開始選字。

## 一句話架構

一個 WinForms 全域鍵盤 hook 程式：`InterceptKeys` 攔截所有按鍵 → `TTForm.handleKey()` 判斷 →
改 3×4 個 `Button` 的圖／字 → 揀好字之後用 `SendKeys.Send()` 打入前景視窗。
字碼、關聯字、同音、簡繁全部住喺 `files/dataset.db` (SQLite)。

**唔係** TSF/IMM32 輸入法，只係一個永遠 TopMost 的小視窗 + keyboard hook，所以任何程式都用得。

## 技術棧

| | |
| --- | --- |
| Language | C# 7.3, .NET Framework **4.8**, WinForms |
| Project | `TT.csproj` — 舊式 csproj + `packages.config` (**唔係** SDK-style) |
| DB | System.Data.SQLite 1.0.118.0 (NuGet)，native `SQLite.Interop.dll` 喺 `x64/` 子資料夾 |
| Platform | **淨係 x64** (32-bit 唔再支援；csproj 仲留住 AnyCPU config，但 `build.ps1` 唔會用) |
| Output | `bin\x64\Debug\` / `bin\x64\Release\` |

## Build

```powershell
.\build.ps1                                   # Debug|x64
.\build.ps1 -Release                          # Release|x64
.\build.ps1 -Configuration Release -Clean -Run
.\build.ps1 -Release -Package                 # 出埋 dist\tt_release_YYYYMMDD.zip
.\build.ps1 -Release -CloseRunning            # 有 TT.exe 行緊就自動 close 咗佢先
```

`build.ps1` 自己搵 MSBuild (vswhere → VS2022/2019 已知路徑 → Framework64 v4.0.30319)，
`packages\` 唔齊就自動 nuget restore (需要時會下載 `nuget.exe` 落 `.tools\`)。
**只 build x64**，冇 `-Platform` 呢個參數。

- 有個 `warning MSB3021: ... data\* ... Illegal characters in path` 係**原本就有**的，
  `AfterBuild` target 想 copy 一個唔存在的 `data\` 資料夾，可以無視。
- 程式係 single-instance (mutex `"TT"`)，而且 output folder 啲檔案會俾佢 lock 住，
  行緊就 build 唔到。`build.ps1` 會即刻停低叫你 close；加 `-CloseRunning` 就會幫你 close。
- `-Package` 打包時會剔走 `tt_settings.ini`、`files\user_freq.db`、`TT.pdb` 同舊 zip。
  (list 入面仲有 `sqlite3.dll` / `*.xml` / EF6 / Linq / `x86\` 一堆，純粹清走舊 `bin\`
  遺留落嚟的檔案 — 而家 build 根本唔會再出呢啲，見下面「SQLite 依賴」)
- `-PackageName tt_v1.2.3.zip` 可以自己改個 zip 名 (`release.ps1` 就係咁用)。
- Debug 唔會有 console；`Debug.WriteLine` 要用 VS 的 Output window 或 DebugView 睇。

## SQLite 依賴

**得兩個檔案**，唔好再加：

| 檔案 | 做乜 |
| --- | --- |
| `System.Data.SQLite.dll` | ADO.NET managed provider (`SQLiteConnection` / `SQLiteCommand`) |
| `x64\SQLite.Interop.dll` | 真身 — C 版 sqlite engine 靜態 link 埋 P/Invoke shim。provider 會自動去 `x64\` 搵佢 |

只需要 nuget package `Stub.System.Data.SQLite.Core.NetFramework` 一個 (上面兩個檔案都係佢嚟)。

以下嘢**曾經**喺 output 出現過，全部係死重量，剷咗：`sqlite3.dll` (另一個 package
`SQLite` 3.13.0 的上游 amalgamation，`System.Data.SQLite` 唔會 load 佢)、
`System.Data.SQLite.EF6.dll`、`System.Data.SQLite.Linq.dll` (EF 專用，冇用過 EF)、
`System.Data.SQLite.xml` (IntelliSense doc，1.2MB)、`x86\SQLite.Interop.dll` (x64 build 載入唔到)。

## Release (`release.ps1`)

一 key 做晒：**版本號 +1 → build Release → 上 GitHub release**。

```powershell
.\release.ps1                                     # 版本 +1、build、打包，唔上載
.\release.ps1 -Publish $true                      # 連 GitHub release 一齊出
.\release.ps1 -BumpVersion $false -Publish $true  # 唔加版本號，用返而家個版本出
.\release.ps1 -Part minor                         # 1.0.3 -> 1.1.0 (major/minor/patch)
.\release.ps1 -DryRun                             # 只講會做乜，唔郁任何嘢
```

| 參數 | 預設 | 做乜 |
| --- | --- | --- |
| `-BumpVersion` | **`$true`** | 改 `Properties\AssemblyInfo.cs` 的 `AssemblyVersion` / `AssemblyFileVersion` |
| `-Publish` | **`$false`** | commit + push + `gh release create` + 上載個 zip |
| `-Part` | `patch` | `patch` = 尾數 +1 (1.1.1 → 1.1.2)；仲有 `minor` / `major` |
| `-Notes` | 空 | release notes 內文；唔寫就自動搵 (見下) |
| `-Draft` | off | 開草稿 release，唔會即刻公開 |
| `-AllowDirty` | off | 有未 commit 的改動都照 publish |
| `-CloseRunning` | off | 有 TT.exe 行緊就 close 咗佢先 build |
| `-DryRun` | off | 乾行，乜都唔改 |

流程：

1. 由 `Properties\AssemblyInfo.cs` 讀版本 (寫落去係 4 位，尾嗰位一律 `0`)，計出新版本，
   `AssemblyVersion` 同 `AssemblyFileVersion` 兩行一齊改。
2. `build.ps1 -Release -Rebuild -Package -PackageName tt_v<version>.zip`
   → 出 `dist\tt_v1.0.1.zip`。
3. `-Publish $true` 先至會做落去：
   - 要有 `gh` CLI 而且 `gh auth status` 過到 (未裝：https://cli.github.com/)
   - working tree 除咗 `AssemblyInfo.cs` 之外唔可以有未 commit 的改動 (除非 `-AllowDirty`)
   - commit `release v1.0.1` → push 現時 branch → `gh release create v1.0.1 dist\tt_v1.0.1.zip`
   - tag 用 `v<version>`；`origin` 已經有同名 tag 就會停低，唔會覆蓋
4. release notes 次序：`-Notes` > `CHANGELOG.md` 入面 `## [1.0.1]` 嗰段 >
   `## [Unreleased]` 嗰段 > `gh --generate-notes`。

出 release 之前記得將 `CHANGELOG.md` 的 `## [Unreleased]` 改做新版本號，
唔係 release notes 會攞晒成段 Unreleased 落去。

## 檔案

| 檔案 | 做乜 |
| --- | --- |
| `Program.cs` | mutex 防止開兩份，起 `TTForm`，交俾 `InterceptKeys.init()` (佢入面先至 `Application.Run`) |
| `InterceptKeys.cs` | `WH_KEYBOARD_LL` 低階 hook。key down 時 callback 回 `true` = 食咗個掣，前景程式收唔到 |
| `TTForm.cs` | **成個程式嘅心臟** (~1400 行)：UI、按鍵狀態機、tray menu、ini 設定 |
| `TTCore.cs` | `files/dataset.db` 的 query wrapper |
| `TTFreq.cs` | `files/user_freq.db` 的常用字統計 (獨立 db，見下) |
| `TTForm.Designer.cs` | 得個空殼，所有 control 都係 `TTForm()` 入面手動 new |
| `files/dataset.db` | 字碼表 (repo 入面呢個係空碼表，見 readme 的法律說明) |
| `files/default90.png` | 九宮格筆劃圖 sprite sheet，橫 9 欄 x 直 10 行。**每格幾大唔緊要**，格size = sheet size ÷ 9x10 (900x1000、810x1000、1800x2000 都得，格可以唔係正方)。第 0 行 = 首頁 (原本 `0_1~0_9`)，第 1~9 行 = 各碼 (原本 `1_1~9_9`)。原本 `10_1~10_9` 嗰套改為喺 `TTForm()` 即場砌：第 0 行原大擺入一個大 1/0.5714 的白底畫布右下角 (`CompositingMode.SourceCopy`)，睇落就係縮細咗 |
| `tt_settings.ini` | runtime 設定，寫喺 **exe 隔籬**，唔入 repo |

## dataset.db schema

| Table | 用法 |
| --- | --- |
| `mapped_table(id, characters, weight)` | 主碼表。`id` = 打落去的數字碼 |
| `related_candidates_table(character, candidates)` | 關聯字，`candidates` 用**空格**分隔 |
| `word_meta(ping, ping2, char, sc)` | 同音字 (self-join `ping` 搵同音，`ping2` 相同排前) |
| `ts_chinese_table(traditional, simplified)` | 簡體輸出 |

`mapped_table.id` 的 magic number：

- `0` = 普通標點 (首頁㩒 0)
- `1` = 開關符號「」（）等，**兩個字一組**，`commandInput(openclose)` 會兩個兩個切開
- `10 ~ 999` = 正常字碼 (1~3 個 digit)
- `1000` = 速選表 0；`1001 ~ 1009` = 速選表 1~9

`characters` 入面的 `"*"` 係**留空位**用：唔會顯示 (`showPage` / `setButtonsText` 出空白格)，
㩒落去亦當冇撳過 (`selectWord()` 一開頭就 return)，排序時亦唔應該搬走佢。

## TTForm 主要 state

```
currCode      string  首頁㩒緊嘅碼 ("", "5", "53", "534")
selectMode    bool    true = 而家喺選字表，1~9 揀字、0 下一頁
selectWords   []      選字表全部內容；showPage() 每頁攞 9 個
currPage      int
homo          bool    numpad * toggle；true 時揀字唔出字，改為入該字的同音表
afterHomo     bool    同音揀完之後喺 title bar 顯示返個字碼
openclose     bool    而家喺開關符號表，揀完會 send 兩個字再 {Left}
lastWord      string  最後打出的單字，關聯字表用
prevTypedWord string  最後打出的單字，TTFreq 記連打用
```

按鍵入口全部經 `handleKey()` (`TTForm.cs:600`)，次序：

1. key **up** → `longPressUp()`
2. `switch` / `position` / `size` 熱鍵 (預設 F10 / F9 / F8)
3. `!active` 就直接 return
4. `!use_numpad` → 用 `altKeys` 那套 a-z 鍵
5. numpad (96~111)

0~9 同 `.` 一律行 `keyDown(input, keyCode)` (input `0~9`，`.` 用常數 `CANCEL_INPUT` = 10)；
`+ - * /` 直接叫 `commandInput()`。

## 長按 (`TTForm.cs:789` 起)

0~9 同 `.` 都唔會 auto repeat：同一個 keyCode 的重複 WM_KEYDOWN 會被 `keyDown()` 食走。
撳夠 `LONG_PRESS_MS` (400ms) 就行 `pickLongAction()` 揀好嘅動作：

| 情況 | 長按 | 等於原本邊個掣 |
| --- | --- | --- |
| 選字時，1~9，而且該候選係單字 | 入該字的同音表 | numpad `*` |
| 首頁未打 code，0 | 開關符號表 | numpad `/` |
| 首頁未打 code，1~9 | 1~9 號速選表 | numpad `-` |
| 首頁未打 code，`.` | 關聯(候選)字表 | numpad `+` |

**兩種 timing，要留意**：

- 選字表揀字 (`homo`) 短按會即刻 `SendKeys` 出字，收唔返，所以**短按動作要等放手先做** (`lpDeferred`)。
- 其餘情況短按只係改介面 (`currCode` / button 圖)，所以**即刻做**，長按夾硬蓋過去就得，
  打字手感唔會慢。譬如首頁㩒 5 → `currCode = "5"`，長按 tick 時 `commandInput(shortcut)`
  見到 `currCode.Length == 1` 就自然入到速選 5。

其他規矩：

- 仲㩒住數字掣就去㩒第二個數字掣 → `longPressFlush()` 先結算返上一個 (rolling)。
- 仲㩒住數字掣就去㩒 numpad `+ - * /` → 一樣 flush (但 `110` = `.` 要例外，
  佢自己行 `keyDown()`，flush 咗會令 auto-repeat 判斷失效)。
- **唔好**喺 `commandInput()` 入面 flush：`longPressTick` 會叫 `commandInput`，
  clear 咗 `lpKeyCode` 之後跟住嚟嘅 auto repeat 就會當成新按鍵，變返連續輸入。
- tray menu「長按功能」熄咗 = 完全走回舊路 (即刻做 + 有 auto repeat)。

## 常用字 (`TTFreq.cs` + `TTForm.cs:1338` 起)

`files/user_freq.db` 係**獨立** db，特登唔放入 `dataset.db`，因為使用者隨時會換走碼表。
刪咗佢只係清零，唔影響打字。

```sql
word_freq(word, count)        -- 打過的單字
pair_freq(prev, word, count)  -- 連續打過的兩個字
```

只記**單字** (`TTFreq.isSingleWord` 用 `StringInfo.LengthInTextElements`，emoji / 增補字算一個)。
`TTFreq.MinCount = 2`，即係要 `count > 2` (打過**超過**兩次) 先至算常用。

兩套排序，行為**故意唔同**：

| | `sortByFreq()` | `getRelateList()` |
| --- | --- | --- |
| 用喺 | 選字表 (碼表 / 同音 / 速選) | 關聯(候選)字表 |
| 睇邊個 table | `word_freq` | `pair_freq` |
| 第一頁 | **原封不動** | 可以動 |
| 第二頁之後 | 常用字調前 | — |
| 會唔會加新字入表 | 唔會 | **會** (連打過 >2 次但唔喺關聯表的字都加最前) |
| `"*"` 留空位 | 位置不變 | 跟住原表排喺後面 |

`startSelectWord(words, useFreqSort = true)` — 開關符號同關聯字要傳 `false`
(關聯字已經由 `getRelateList` 排好，唔可以再 `sortByFreq` 打亂)。

tray menu「常用字調前」只係關**排序**，**記錄照記**，開返轉頭即刻有得用。

## tt_settings.ini

```ini
[Window]  Left / Top / BoxSize
[system]  sc_output / use_numpad / long_press / freq_sort   ; 全部 0 或 1
[Key]     relate=107 prev=109 shortcut=109 homo=106 openclose=111
          switch=121(F10) position=120(F9) size=119(F8)
[AltKey]  num0~num9 / cancel / relate / prev / shortcut / homo / openclose  ; 唔用 numpad 時的 a-z
```

第一次行 (`ini.makeExcite()` 回 `false`) 會寫晒 default 落去。
**加新設定記得兩邊都改**：first-run 的 `ini.Write()` 區塊，同埋 `else` 入面的 `ini.ReadInt()`。

`[Key]` 支援組合鍵：`KeysStr` 用 `"162+65"` 咁樣的字串比對 `keydownStr`。

## 改嘢時要小心

- **SQL 全部係字串拼接**，唔係 parameter。`TTCore.esc()` 會將 `'` 變 `''`，
  新加 query 記得用返，唔係打個單引號標點就會爆。`TTFreq` 就全部用 parameter。
- **中文字元用 `StringInfo`，唔好用 `string.Length`**。增補字集 / emoji 係 surrogate pair，
  `Length == 2`。`TTCore.sql2strs()` 已經用 `SubstringByTextElements` 逐個字切。
  (注意：`selectWord()` 入面 `typeWord.Length == 1` 呢個舊判斷仍然係 UTF-16 長度，
  即係增補字唔會有關聯字 — 舊行為，未改。)
- `SendKeys.Send()` 有 special char (`+^%~(){}[]`)，標點表如果加呢啲字要自己 escape。
- `handleKey` 係喺 hook callback 入面行，即係 UI thread，`System.Windows.Forms.Timer` 夾得埋。
  唔好喺呢度做慢嘢，會 lag 到成個系統打字。
- Button 的字體大細係 `setText()` 逐個計出嚟的，resize 時 `renewFontsize()` 重新計。

## 常用參考

- keycode 查詢：https://www.toptal.com/developers/keycode
- 改碼表：DB Browser for SQLite https://sqlitebrowser.org/dl/

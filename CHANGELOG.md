# Changelog

所有值得留意的改動都會記喺呢度。

## [Unreleased]

### 新功能

#### 長按 (0~9 和 `.`)

- 0~9 同 `.` 長按**唔會再變成連續輸入**。就算嗰個情況冇設定長按功能，
  一路㩒住都只會當㩒咗一次。
- 㩒住 **0.4 秒**會觸發以下功能，等於唔使用 numpad 的 `+ - * /` 都用到呢四個功能：

  | 情況 | 長按 | 等於原本 |
  | --- | --- | --- |
  | 選字時 `1~9` (該候選係單字) | 入該字的同音表 | numpad `*` 之後揀字 |
  | 首頁未打 code，`0` | 開關符號表 (「」（）等) | numpad `/` |
  | 首頁未打 code，`1~9` | 1~9 號速選表 | 㩒一個 code 之後 numpad `-` |
  | 首頁未打 code，`.` | 關聯(候選)字表 | numpad `+` |

- 選字表入面嗰個候選唔係單字 (例如開關符號、速選表入面的詞) 就冇同音表入，
  嗰陣長按當普通短按處理。
- 打字手感照舊：除咗選字表揀字之外，短按動作仍然係㩒落去嗰刻即刻做，
  唔會等放手。選字表揀字因為一按即出字收唔返，所以改為放手先出。
- 仲㩒住數字掣就去㩒第二個掣 (數字或者 numpad `+ - * /`)，會先結算返上一個掣。
- **tray menu 加咗「長按功能」可以開／關**，關咗就完全回復舊行為 (即刻做 + 有連續輸入)。
  設定存喺 `tt_settings.ini` 的 `[system] long_press`。

#### 常用字調前

- 新開一個獨立 database **`files/user_freq.db`**，記低：
  - `word_freq` — 打過乜嘢字 (只記單字) 同次數
  - `pair_freq` — 連住打過邊兩個字 同次數
- 特登唔放入 `files/dataset.db`，咁樣你隨時換走／更新碼表都唔會失去自己的習慣；
  反過嚟刪咗呢個檔案亦只係將統計清零，唔會影響碼表。
- 只有打過**超過兩次** (次數 > 2) 的字先至算常用。
- **選字表**：打得多的字會調前，但**只限第二頁或以後**，第一頁的次序完全唔會變。
  碼表入面用嚟留空位的 `*` 亦唔會被搬走。
- **關聯(候選)字表**：連住打過超過兩次的字會排最前，次數越多排越前，
  就算本身唔喺關聯字表入面都會加入去。呢度**唔分第幾頁**，第一頁次序都會變。
- **tray menu 加咗「常用字調前」可以開／關**，設定存喺 `[system] freq_sort`。
  關咗只係唔再調前，記錄照樣繼續記，開返轉頭即刻有得用。
- tray menu 另外加咗「**清除常用字記錄**」，會清空成個 `user_freq.db`。

#### 筆劃圖改用一幅 sprite sheet

- 原本要 load `files/img/` 入面 100 幅細圖，而家改為只 load 一幅 `files/default90.png`，
  開程式時即場切開。
- `default90.png` 係**橫 9 欄 x 直 10 行**：第 0 行 = 首頁嗰套 (原本 `0_1~0_9`)，
  第 1~9 行 = 各碼 (原本 `1_1~9_9`)。
- **每格幾大唔緊要**，程式會用 sheet 尺寸除 9x10 算返出嚟：900x1000、810x1000 (格唔係正方)、
  1800x2000 都照用，唔使改 code。圖片 DPI 亦唔影響。
- 原本 `10_1~10_9` (選字時嗰套細圖) 唔再係圖檔，改為由第 0 行即場砌：
  原大擺入一個大 1/0.5714 的白底畫布右下角，睇落就係縮細咗。
- 換皮膚而家只需要換一幅圖。`files/img/` 已經冇用。

#### 改名做「三三」

為咗同「九方」(Q9) 的商標保持距離，成個程式改晒名：

| | 舊 | 新 |
| --- | --- | --- |
| 中文名 | 九万輸入法 | **三三正體中文輸入法** (短名「三三」) |
| 英文名 | Q9CS / TQ9 | **TT** (Three Three Traditional Chinese Input Method) |
| exe / assembly | `Q9CS.exe` | `TT.exe` |
| namespace | `Q9CS` | `TT` |
| 類 | `Q9Form` / `Q9Core` / `Q9Freq` / `q9command` | `TTForm` / `TTCore` / `TTFreq` / `ttcommand` |
| 檔案 | `Q9CS.sln` / `Q9CS.csproj` / `Q9*.cs` | `TT.sln` / `TT.csproj` / `TT*.cs` |
| 設定檔 | `tq9_settings.ini` | `tt_settings.ini` |
| zip | `tq9_*.zip` | `tt_*.zip` |
| single-instance mutex / 開機自動啟動的 registry value | `Q9CS` | `TT` |
| GitHub repo | `Hocti/Q9CS` | `Hocti/ThreeThree` |

**升級注意**：

- `tq9_settings.ini` 唔會自動搬做 `tt_settings.ini`，所有設定會回復預設。
  想保留就自己 rename 個檔案。
- 舊版如果開咗「開機自動啟動」，`HKCU\...\Run` 入面仲會有個叫 `Q9CS` 、
  指住舊 exe 的 value，要自己喺舊版熄咗佢，或者手動刪走。
- `files/user_freq.db` 同 `files/dataset.db` 冇改名，直接搬過嚟照用。

### 修正

- 選字表入面碼表用嚟留空位的 `"*"`，本身已經唔會 display，
  但㩒落去竟然會真係打個 `*` 出嚟。而家㩒空位當冇撳過，乜都唔會發生。
- 打單引號 `'` 之類的標點之後再查關聯字／同音字／簡繁，唔會再因為 SQL 拼接爆掉。
  (`TTCore` 加咗 `esc()`，`'` 會轉成 `''`)

### 開發

- 新增 **`build.ps1`**：由 command line build debug / release 版。
  自己搵 MSBuild (vswhere → VS2022/2019 已知路徑 → Framework64)，
  `packages\` 唔齊會自動 nuget restore。

  ```powershell
  .\build.ps1                                   # Debug|x64
  .\build.ps1 -Release                          # Release|x64
  .\build.ps1 -Configuration Release -Clean -Run
  .\build.ps1 -Release -Package                 # 打埋 dist\*.zip
  ```

- 新增 **`CLAUDE.md`**：codebase 結構、狀態機、db schema、改嘢要小心的地方。
- **清走冇用的 SQLite / EntityFramework 依賴**，打包細咗約 3.3MB：
  - `sqlite3.dll` — 由第二個 nuget package (`SQLite` 3.13.0，2016 年) 嚟。
    `System.Data.SQLite` 只會 P/Invoke `SQLite.Interop.dll` (engine 靜態 link 咗入面)，
    由頭到尾都冇 load 過佢。
  - `System.Data.SQLite.EF6.dll` / `System.Data.SQLite.Linq.dll` — Entity Framework 專用，
    程式只用 `SQLiteConnection` / `SQLiteCommand`，而且連 `EntityFramework.dll` 都冇 ship。
  - `System.Data.SQLite.xml` — 淨係 IntelliSense 用嘅 doc，runtime 唔會讀
    (csproj 加咗 `AllowedReferenceRelatedFileExtensions=none`)。
  - `x86\SQLite.Interop.dll` — x64-only build 載入唔到，改為喺 csproj 就唔 copy。
  - `App.config` 順手清走 `<entityFramework>`、`DbProviderFactories`、
    同埋 `SQLitePCLRaw.core` (根本冇裝) 的 binding redirect。
- **修正 fresh clone build 唔到**：csproj 硬性要求 `packages\SQLite.3.13.0\...\SQLite.props`
  存在，但 `packages.config` 冇列過佢，`packages\` 又 git-ignore 咗，
  所以新 clone 落嚟 restore 完仍然會 fail。連同上面一齊拆走咗。
- 新增 **`TTFreq.cs`**：常用字統計的 db wrapper (全部用 SQL parameter)。
- `user_freq.db` 同 `tt_settings.ini` 一樣，跟 **exe 的位置**，唔跟 working directory。

## 之前

- `afterHomo` — 同音字揀完之後喺 title bar 顯示返該字的字碼。
- 首個公開版本。

# 三三正體中文輸入法

Three Three Traditional Chinese Input Method (TT)

用一個 **已過期** 的專利 HK1035043 製作的中文輸入法
以numpad輸入2~3鍵即可開始選字

## 特色

- 免費
- 無需安裝、直接執行
- 支援emoji、香港增補字符集
- 用專用鍵進入"關聯字"表，不會影響首頁中 0 和 . 鍵功能
- 10個快速選字表，包括emoji，粵語字等 (可自行修改)
- 長按 0~9 和 . 就用到全部功能，唔用 numpad 都得
- 會記住你的用字習慣，常用字自動調前 (可關掉)
- 可自行以sqlite修改字碼、關聯字等
- numpad以外(如notebook)所有按鍵都可自行修改
- 可輸出簡體
- 可以拉到非常巨大 (正)

## 事先準備

.NET framework 4.8 或以上

如windows本身有安裝noto sans會優先使用，否則用回系統字體

由於可能存在法律爭議，碼表暫不公開，所以需要自己輸入字碼等資料\
玩家需自動修改files/default90.png呢幅圖 (橫9欄x直10行，總尺寸自由，只要係9和10的倍數)\
以及files/dataset.db內，table "mapped_table"中的id 10  ~999 的字碼表\

推薦使用 SQLite Database Browser 修改\
https://sqlitebrowser.org/dl/

**當然如果有朋友已修改完後，直接把成果給你就最好啦**

## 下載

到[Releases](https://github.com/Hocti/ThreeThree/releases/latest) 下載。

## 使用方法

|Key|功能|
| ---- | ---- |
|0~9.|和原專利一樣|
|+|進入"關聯字"表<br />打下一個字之前，隨時可以按 + 進入選關聯字|
|-|a) (選字時) 上一頁<br />b) (首頁中) 快速選字表 (碼表1000)<br />c) (首頁按1~9後) 1~9號快速選字表 (碼表1001~1009)|
|*|開/關打同音字 (打完一個字後再揀字)，可以任何時間隨時開/關|
|/|開關引號，一次過輸 開+關 後兩個碼，如「」|
|長按 0~9 .|見下面「長按」|
|F8|快速改變視窗大小|
|F9|快速改變視窗位置|
|F10|暫停/重啟輸入法|

## 長按

0~9 和 . **長按都不會變成連續輸入**，
按住 0.4 秒後會變成以下功能，即是不按 numpad 的 `+ - * /` 也用到這四個功能:

|情況|長按|等於|
| ---- | ---- | ---- |
|選字時 1~9|進入該字的同音字表|numpad `*` 後選字|
|首頁未輸入任何code，0|開關符號表 (「」（）等)|numpad `/`|
|首頁未輸入任何code，1~9|1~9號快速選字表|按一個code後 numpad `-`|
|首頁未輸入任何code，.|關聯(候選)字表|numpad `+`|

整個長按功能可以在 tray menu 中開／關。

## 常用字調前

會用一個獨立的 `files/user_freq.db` 記下你打過甚麼字、和連住打過哪兩個字。
打過**超過兩次**的字會自動調前:

- **選字表**: 只有第二頁或以後會調前，第一頁次序不變
- **關聯(候選)字表**: 不分頁數，連打次數越多排越前，本來不在關聯字表中的也會加進去

這個 db 和碼表 `dataset.db` 分開，所以換碼表不會失去習慣，
刪掉 `user_freq.db` 也只是把統計清零。

整個功能可以在 tray menu 中開／關，也可以在那裡「清除常用字記錄」。

## 快速選字表

在首頁按「numpad -」會進入快速選字表 0\
在首頁先按「1~9」再按「numpad -」會進入快速選字表 1~9\
可以自動修改files/dataset.db內，table "mapped_table"中的id 1000 ~1009 的 characters，改變這10個快速選字表內容

## 修改按鍵

除0-9.外，所有鍵都可以自行修改 tt_settings.ini 內的keycode改變\
如不想用到的鍵，正以將keycode設定成0\
上一頁(prev)和快速選字(shortcut)鍵可以重複，預設兩個鍵都係 numpad * 鍵\
\
可以在這裡查到keycode:\
https://www.toptal.com/developers/keycode

## 不使用numpad

預設的0~9.是
|||
| ---- | ---- |
|qzxcr|[引號]789[同音]|
|asdfg|[上頁]456[關聯]|
|zxcvb| 0 123 .|

以上鍵都可以自行修改\
但在不使用numpad模式下，a-z鍵都不會正常運作\
想打a-z可以先按 f10 暫停輸入法

## Tray

在window start bar 右下角(tray menu)會找到 "三三" 的icon\
右鍵點選可選擇 關閉/輸出簡體/切換numpad輸入/長按功能/常用字調前/清除常用字記錄 等

## 自行編譯

需要 Visual Studio 2022 (或 2019) 連 .NET desktop 工作負載

```powershell
.\build.ps1                                   # Debug|x64
.\build.ps1 -Release                          # Release|x64
.\build.ps1 -Configuration Release -Clean -Run
.\build.ps1 -Release -Package                 # 順便打包成 dist\*.zip
```
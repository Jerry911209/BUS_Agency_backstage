# 🚌 智慧巴士與復康乘車營運後台管理系統 (BUS Agency Backstage)

## 📌 專案簡介
本專案為針對 **機關復康巴士、長照失能巴士** 所設計的智慧化營運後台管理系統。採用 **ASP.NET Core MVC** 架構開發，骨幹連結遠端資料庫進行高可靠性的即時乘車預約、運力派遣、服務資格核定與跨區域數據稽核。

系統內建嚴密的商務防呆演算法，能精確自動校正過期單據，並提供豐富的資料統計與 XML 報表匯出功能，大幅提升本機關業務人員與調度中心的作業效率。

---

## 🛠️ 系統核心九大模組架構

本系統採用 `#region` 模組化分塊設計，結構清晰易維護：

### 1. 🔐 登入控管與基本首頁
* **SHA256 密碼雜湊**：資料庫拒絕存放明碼，全面提升資安規格。
* **首次登入強制作業**：識別生日明碼（如 `19960801`）攔截機制，強制導向密碼修改頁面。
* **Session 權限防禦**：區分 `Super`（最高管理員）、`CenterAdmin`（調度中心）、`Admin`（一般管理員），嚴防非法 URL 入侵。

### 2. 👤 使用者與權限管理
* 支援管理員帳號之維護、自主密碼變更與管理員密碼重置。
* **安全性鎖定**：一鍵切換帳號啟用/鎖定狀態（系統內建核心保護：最高管理員不可被鎖定或刪除）。

### 3. 📅 車輛預約申請維護
* **雙向預約時間防呆**：新增與修改預約單時，強制乘車時間（`PickupTime`）必須大於當前系統時間，防止建立過去的幽靈訂單。
* **UX 體驗優化**：編輯預約單時，**系統自動預填原本的乘客姓名**。若因日期或其他欄位輸入錯誤觸發後端攔截退回時，輸入框姓名依然穩固留存不蒸發。

### 4. 🚌 車輛與司機管理
* 機關復康巴士與長照車輛狀態控管、車籍資料維護。
* 營運司機基本資料管理與調度狀態追蹤。

### 5. 🔀 營運調度派車任務
* 媒合民眾預約單與空閒車輛、司機之排班任務。
* **狀態連動更新**：派遣任務成功存檔時，原始民眾預約單狀態自動同步強刷更新為 `1 (已排班)`。

### 6. 🔎 服務資格稽核與資料搜尋
* **資格審查流**：針對身心障礙與長照失能民眾之福利身分進行核定，待審案件優先置頂。
* **動態搜尋自動完成**：預約人輸入框整合異步 AJAX，打字即時動態跳出符合的乘客下拉選單供點選。
* **跨區域稽核搜尋**：依據地址前三個字切出服務區域，分流稽核「成功乘車案例」與「失敗/異常調度案例」。

### 7. 📈 數據統計與 XML 報表
* 統計總預約量、調度失敗量與候補失效量。
* **供需缺口演算法**：整合失敗與超時過期數據，精確分析出最缺乏公眾運具資源的分區黑名單排行。
* **XML 實體匯出**：一鍵將分區缺口數據轉換為標準 XML 文件並強制瀏覽器下載。

### 8. 📢 審核性公告系統
* 本機關最新消息發布與分類管理，首頁動態即時加載最新 3 則公告看板。

### 9. ❓ 常見問題管理 (FAQ)
* 獨立 `Faqs` 資料表維護，支援前端彈出視窗（Modal）非同步 JSON 載入與即時編輯存檔。

---

## 🌟 重點追加功能：車輛調度系統 (預留佔位)
* **🔀 車輛調度系統 (建置中)**：為配合團隊協同開發節奏，系統已預留專屬的 Action 路由與導覽列入口。
* 進去後會顯示高質感的動態齒輪旋轉（FontAwesome Animation）與進度條排版提示頁面，展現良好的架構擴充性，未來可無縫對接車流優化演算法。

---

## 💻 技術棧 (Tech Stack)
* **後端核心**: ASP.NET Core 8.0 (Web MVC)
* **資料庫 ORM**: Entity Framework Core
* **前端互動**: jQuery 3.6, Vanilla JS, HTML5 Datalist
* **網頁樣式**: Bootstrap 5.3 (隨附自適應流動佈局與警告框元件)
* **圖標支援**: FontAwesome 6.0

---

## 🚀 快速啟動與開發偵錯

請確保本機已安裝 `.NET 8 SDK`，並於專案根目錄執行以下指令：

1. **還原套件與編譯專案**
   ```bash
   dotnet build
   dotnet watch run
   dotnet clean

## 相關套件
* code --install-extension ms-ceintl.vscode-language-pack-zh-hant
* code --install-extension ms-dotnettools.csharp
* code --install-extension ms-dotnettools.vscode-dotnet-runtime
* code --install-extension ms-python.python
* code --install-extension ms-python.debugpy
* code --install-extension ms-python.vscode-pylance
* code --install-extension ms-toolsai.jupyter
* code --install-extension ms-mssql.mssql
* code --install-extension ms-mssql.sql-database-projects-vscode
* code --install-extension mechatroner.rainbow-csv


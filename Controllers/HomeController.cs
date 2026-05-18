using Microsoft.AspNetCore.Mvc;
using BUS_Agency_backstage.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace BUS_Agency_backstage.Controllers
{
    public class HomeController : Controller
    {
        // 資料庫上下文唯讀變數，用來與資料庫進行 Entity Framework 連線操作
        private readonly BusBookingDbContext _db;

        // 透過建構子注入 (Constructor Injection) 引入資料庫上下文實例
        public HomeController(BusBookingDbContext db)
        {
            _db = db;
        }

        #region 【工具方法：密碼雜湊加密 & 狀態自動校正】
        // =========================================================================
        // 🛠️ 內部輔助工具方法模組
        // =========================================================================

        /// <summary>
        /// 內部工具：將明文密碼進行 SHA256 雜湊加密，確保資料庫內部不儲存任何明文密碼，提高系統安全性
        /// </summary>
        /// <param name="password">使用者輸入的明文密碼</param>
        /// <returns>回傳加密後的 64 位元十六進位字串</returns>
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // 將密碼字串轉為位元組陣列並計算其雜湊值
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2")); // 轉換為十六進位小寫字串
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// 🌟 核心追加功能：自動校正過期預約單狀態
        /// 邏輯：檢查所有「乘車預約時間 (PickupTime)」已經小於現在時間的預約單。
        /// 如果狀態仍停留在 0 (待審核) 或 3 (補件中)，代表這筆訂單已超時且未指派，自動強刷更新為 6 (已過期)。
        /// 排除了已派車(1)、已完乘(2)、已駁回(4)、後補失效(5) 等已處理完畢的狀態。
        /// </summary>
        private void AutoCheckExpiredBookings()
        {
            var now = DateTime.Now; // 抓取當前的系統時間

            // 從資料庫中篩選出：乘車時間已過，且狀態還是待審核(0)或補件中(3)的預約單列表
            var expiredList = _db.Bookings
                .Where(b => b.PickupTime < now && (b.BookingStatus == 0 || b.BookingStatus == 3))
                .ToList();

            // 如果有找到符合過期條件的預約單，則進行批次更新
            if (expiredList.Any())
            {
                foreach (var booking in expiredList)
                {
                    booking.BookingStatus = 6; // 🌟 狀態代碼更新為 6 (代表已過期)
                }
                _db.SaveChanges(); // 正式將變更批次寫入遠端資料庫存檔
            }
        }
        #endregion

        #region 【模組一：登入與基本首頁控制】
        // =========================================================================
        // 🔐 登入控管、Session 寫入、登出清除與後台儀表板入口
        // =========================================================================

        /// <summary>
        /// [GET] 顯示管理員登入網頁畫面
        /// </summary>
        [HttpGet]
        public IActionResult Login() => View();

        /// <summary>
        /// [POST] 處理登入驗證邏輯
        /// 功能：包含初始密碼明碼生日攔截（強制改密碼）、SHA256 密碼雜湊比對、權限標籤寫入 Session 暫存
        /// </summary>
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // 依據前端輸入的帳號名稱去資料庫撈取相對應的使用者資料
            var user = _db.Accounts.FirstOrDefault(u => u.Username == username);

            if (user != null)
            {
                // 🔍 初始密碼判斷：若輸入的明文密碼跟資料庫欄位完全一致 (例如未改過的初始生日 19960801)
                // 代表該帳號是第一次登入，強制攔截並把帳號寫入臨時 Session，直接重導向至首次改密碼頁面
                if (password == user.PasswordHash)
                {
                    HttpContext.Session.SetString("TempUser", username);
                    return RedirectToAction("ChangePassword");
                }

                // 一般常規登入驗證：將使用者輸入的密碼進行 SHA256 加密，再與資料庫的密碼進行比對
                string hashedInput = HashPassword(password);
                if (user.PasswordHash == hashedInput)
                {
                    // 依據 RoleId 代碼給予相對應的文字權限標籤 (1:最高管理員 Super, 2:調度中心 CenterAdmin, 其餘:一般管理員 Admin)
                    string roleLabel = user.RoleId == 1 ? "Super" : (user.RoleId == 2 ? "CenterAdmin" : "Admin");

                    // 將正式的權限角色與唯一識別碼寫入 Session，做為後續每個網頁防禦檢查的依據
                    HttpContext.Session.SetString("UserRole", roleLabel);
                    HttpContext.Session.SetString("UserId", user.AccountId.ToString());

                    return RedirectToAction("Index"); // 驗證通關，導向後台儀表板首頁
                }
            }

            // 帳號密碼不符合，回傳錯誤提示訊息給前端登入視圖
            ViewBag.Error = "帳號或密碼錯誤";
            return View();
        }

        /// <summary>
        /// [GET] 後台管理系統首頁 (儀表板)
        /// 邏輯：檢查 Session 權限防禦，載入畫面瞬間自動調用過期狀態校正，並抓取最新 3 則公告顯示於看板
        /// </summary>
        public IActionResult Index()
        {
            // 安全防禦機制：如果 Session 沒有任何權限角色標籤，代表是外來非法入侵，直接踢回登入頁
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            // 🌟 核心功能執行：在打開首頁的當下，即時掃描、校正所有超時卻處於未處理狀態的預約單，強刷成過期狀態
            AutoCheckExpiredBookings();

            // 撈取已經公開發布（發布日期小於等於現在時間）的最新 3 則公告，按時間倒序排列
            ViewBag.Announcements = _db.Announcements
                .Where(a => a.PublishDate <= DateTime.Now)
                .OrderByDescending(a => a.PublishDate)
                .Take(3)
                .ToList();

            // 撈出所有的預約訂單資料傳遞給前端 View 視圖渲染列表
            var bookings = _db.Bookings.ToList();
            return View(bookings);
        }

        /// <summary>
        /// [GET] 登出系統
        /// 功能：完全清空當前瀏覽器持有的所有 Session 暫存資料，確保系統安全，並引導回登入畫面
        /// </summary>
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // 澈底清除所有 Session 欄位
            return RedirectToAction("Login");
        }
        #endregion

        #region 【模組二：使用者與權限管理】
        // =========================================================================
        // 👤 本機關業務人員與管理員帳號之維護、鎖定切換、密碼重置與修改
        // =========================================================================

        /// <summary>
        /// [GET] 查詢並顯示內部系統所有使用者 / 管理員之帳號列表
        /// 權限控管：限制只有 Super、Admin、CenterAdmin 才能檢視此管理清單
        /// </summary>
        public IActionResult UserList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            var users = _db.Accounts.ToList(); // 撈取全部帳號資料
            return View(users);
        }

        /// <summary>
        /// [GET] 開啟並顯示建立新管理員 / 業務使用者帳號的表單頁面
        /// </summary>
        public IActionResult CreateUser()
        {
            return View("Create");
        }

        /// <summary>
        /// [POST] 處理新增使用者帳號的存檔動作
        /// 邏輯：檢查帳號重複性、配置全新 GUID 主鍵、將前端依據生日填寫的預設明碼字串存入密碼欄位
        /// </summary>
        [HttpPost]
        public IActionResult CreateUser(string Username, string Password, int RoleId, string RealName)
        {
            // 防呆防重複：檢查前端輸入的帳號名稱是否已經在資料庫存在
            var exists = _db.Accounts.Any(a => a.Username == Username);
            if (exists)
            {
                ViewBag.Error = "該帳號已被使用，請更換帳號名稱。";
                return View("Create");
            }

            // 實例化全新帳號物件
            var newAccount = new Account
            {
                AccountId = Guid.NewGuid(),  // 生成不重複的唯一識別識別碼
                Username = Username,
                PasswordHash = Password,     // 根據規格書，初始密碼直接存明碼生日（例如 19960801），登入判斷用
                RoleId = RoleId,
                IsLocked = false             // 預設為啟用、未鎖定狀態
            };

            _db.Accounts.Add(newAccount);
            _db.SaveChanges(); // 儲存至資料庫

            return RedirectToAction("UserList"); // 建立完成後導回使用者清單
        }

        /// <summary>
        /// [GET] 顯示編輯特定使用者帳號細節的頁面
        /// </summary>
        [HttpGet]
        public IActionResult EditUser(Guid id)
        {
            var user = _db.Accounts.FirstOrDefault(u => u.AccountId == id);
            if (user == null) return NotFound();

            return View(user);
        }

        /// <summary>
        /// [POST] 處理並儲存修改後的使用者帳號欄位變更
        /// 防呆邏輯：只有當有輸入新密碼且不是預設的 "0000" 時才執行密碼雜湊更新，否則維持原密碼不變
        /// </summary>
        [HttpPost]
        public IActionResult EditUser(Account model)
        {
            var user = _db.Accounts.FirstOrDefault(u => u.AccountId == model.AccountId);
            if (user != null)
            {
                user.Username = model.Username;
                user.RoleId = model.RoleId;
                user.CenterId = model.CenterId;

                // 只有使用者有輸入新密碼，且不等於預設佔位符 "0000" 時，才進行雜湊加密存檔
                if (!string.IsNullOrEmpty(model.PasswordHash) && model.PasswordHash != "0000")
                {
                    user.PasswordHash = HashPassword(model.PasswordHash);
                }

                _db.SaveChanges(); // 儲存更新
                return RedirectToAction("UserList");
            }
            return View(model);
        }

        /// <summary>
        /// [POST AJAX] 非同步刪除特定的使用者帳號
        /// 終極安全限制：禁止刪除最高管理員 (RoleId==1) 帳號、禁止一般管理員刪除自己目前正在登入的帳號
        /// </summary>
        [HttpPost]
        public IActionResult DeleteUser(Guid id)
        {
            var targetUser = _db.Accounts.Find(id);
            if (targetUser == null) return NotFound();

            // 核心保護：無論是誰發起請求，一律絕對禁止刪除最高管理員 (Root) 帳號，防範系統癱瘓
            if (targetUser.RoleId == 1)
            {
                return BadRequest("系統保護：禁止刪除最高管理員 (Root) 帳號。");
            }

            var currentRole = HttpContext.Session.GetString("UserRole");
            var currentUserId = HttpContext.Session.GetString("UserId");

            // 安全機制：一般系統管理員 (Admin) 絕對不可以使用刪除按鈕把當前自己的登入帳號刪掉
            if (currentRole == "Admin" && targetUser.AccountId.ToString() == currentUserId)
            {
                return BadRequest("安全限制：系統管理員不可刪除自已。");
            }

            _db.Accounts.Remove(targetUser);
            _db.SaveChanges(); // 執行刪除
            return Ok();
        }

        /// <summary>
        /// [GET] 顯示首次登入強制作業：修改密碼網頁畫面
        /// </summary>
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("TempUser")))
                return RedirectToAction("Login");

            return View();
        }

        /// <summary>
        /// [POST] 處理首次登入強制修改密碼存檔
        /// 邏輯：兩次密碼輸入一致後，進行 SHA256 雜湊加密更新，清除臨時暫存，寫入正式登入權限標籤
        /// </summary>
        [HttpPost]
        public IActionResult ChangePassword(string newPassword, string confirmPassword)
        {
            var username = HttpContext.Session.GetString("TempUser");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

            // 檢查密碼與確認密碼是否一致
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "兩次密碼輸入不一致";
                return View();
            }

            var user = _db.Accounts.FirstOrDefault(u => u.Username == username);
            if (user != null)
            {
                // 對新密碼進行雜湊加密
                user.PasswordHash = HashPassword(newPassword);
                _db.SaveChanges();

                // 移除暫存的未通關標記，正式補上 UserRole 使其直接登入後台系統
                HttpContext.Session.Remove("TempUser");
                HttpContext.Session.SetString("UserRole", "Admin");
                return RedirectToAction("Index");
            }

            return RedirectToAction("Login");
        }

        /// <summary>
        /// [POST] 密碼初始化功能 (重置密碼)
        /// 規格書要求：管理員可以將忘記密碼的使用者，重置回「生日明碼狀態（如 19960801）」，並去掉橫線
        /// </summary>
        [HttpPost]
        public IActionResult ResetUserPassword(Guid id, string birthday)
        {
            var user = _db.Accounts.Find(id);
            if (user == null) return NotFound("找不到該使用者");

            if (string.IsNullOrEmpty(birthday)) return BadRequest("必須指定生日作為初始密碼");

            // 將生日日期格式中的 "-" 去除（例如 1996-08-01 轉化為 19960801 明碼），寫回資料庫
            user.PasswordHash = birthday.Replace("-", "");
            _db.SaveChanges();

            return Ok("密碼已重置為該用戶生日：" + user.PasswordHash);
        }

        /// <summary>
        /// [POST] 切換內部使用者帳號的鎖定與啟用狀態
        /// 規格書要求：包含使用者鎖定功能。內部加裝安全防禦，最高管理員不可被鎖定。
        /// </summary>
        [HttpPost]
        public IActionResult ToggleUserLock(Guid id)
        {
            var user = _db.Accounts.Find(id);
            if (user == null) return NotFound();

            // 防禦機制：系統保護，任何人都沒有資格去鎖定最高管理員的帳號
            if (user.RoleId == 1) return BadRequest("系統保護：不可鎖定最高管理員。");

            // 鎖定狀態取反向值進行一切換 (true 變 false，false 變 true)
            user.IsLocked = !(user.IsLocked ?? false);
            _db.SaveChanges();

            string statusMessage = user.IsLocked == true ? "帳號已鎖定" : "帳號已解除鎖定";
            return Ok(statusMessage);
        }

        /// <summary>
        /// [POST] 本機關業務人員個人端獨立密碼變更功能
        /// 規格書 C 項：本機關業務人員可透過此功能，輸入原密碼比對正確後，進行密碼自主變更。
        /// </summary>
        [HttpPost]
        public IActionResult UpdateMyPassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var currentUserIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(currentUserIdStr)) return RedirectToAction("Login");

            Guid currentUserId = Guid.Parse(currentUserIdStr);
            var user = _db.Accounts.Find(currentUserId);
            if (user == null) return RedirectToAction("Login");

            // 先將使用者輸入的舊密碼加密，比對是否與資料庫中的現行密碼雜湊對得上
            if (user.PasswordHash != HashPassword(oldPassword))
            {
                TempData["Error"] = "原密碼輸入錯誤";
                return RedirectToAction("Index");
            }

            // 驗證新密碼兩次輸入是否相同
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "兩次新密碼輸入不一致";
                return RedirectToAction("Index");
            }

            // 加密並儲存新密碼
            user.PasswordHash = HashPassword(newPassword);
            _db.SaveChanges();

            TempData["Success"] = "密碼修改成功！";
            return RedirectToAction("Index");
        }
        #endregion

        #region 【模組三：車輛預約申請維護】
        // =========================================================================
        // 📅 民眾巴士車輛預約單管理 (新增、編輯、刪除、取消與預約時間對齊)
        // =========================================================================

        /// <summary>
        /// [GET] 顯示人工代民眾手動新增巴士車輛預約單的網頁表單頁面
        /// </summary>
        [HttpGet]
        public IActionResult CreateBooking()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            return View();
        }

        /// <summary>
        /// [POST] 處理並儲存新增的車輛預約單
        /// 🌟 核心防呆：強制要求預約乘車時間（PickupTime）必須大於當前的系統時間，防止建立幽靈過去訂單
        /// 連動：自動檢查並連動 PassengerProfile 外鍵，並在後端將系統時間自動帶入 CreatedAt 欄位
        /// </summary>
        [HttpPost]
        public IActionResult CreateBooking(Booking model, string passengerName)
        {
            // 🌟 核心功能追加：防呆攔截，驗證填寫的乘車預約時間不可以小於或等於現在時間
            if (model.PickupTime <= DateTime.Now)
            {
                ViewBag.Error = "預約乘車時間不可小於或等於目前時間，請重新選擇未來的時間。";
                return View(model); // 阻擋並退回表單
            }

            // 驗證該名字的乘客是否存在，且其角色代碼必須是 4 (代表民眾乘客身分)
            var account = _db.Accounts.FirstOrDefault(a => a.Username == passengerName.Trim() && a.RoleId == 4);
            if (account == null)
            {
                ViewBag.Error = $"找不到乘客帳號「{passengerName}」，請確認名稱正確且該帳號為乘客權限。";
                return View(model);
            }

            // 抓取對應的民眾個人資料主鍵檔案 (PassengerProfile)
            var profile = _db.PassengerProfiles.FirstOrDefault(p => p.AccountId == account.AccountId);
            if (profile == null)
            {
                ViewBag.Error = "該帳號尚未填寫乘客詳細資料 (PassengerProfile)，無法建立預約。";
                return View(model);
            }

            // 賦值綁定
            model.PassengerId = profile.PassengerId;
            if (model.BookingType == 0) model.BookingType = 1; // 預設 1 為一般派車預約

            // 🌟 欄位精確對齊：手動新增預約單成功時，後端自動指派當前時間做為預約送出時間 CreatedAt
            model.CreatedAt = DateTime.Now;

            try
            {
                _db.Bookings.Add(model);
                _db.SaveChanges(); // 儲存變更
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var dbError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                ViewBag.Error = "資料庫寫入失敗：" + dbError;
                return View(model);
            }
        }

        /// <summary>
        /// [GET] 顯示修改特定巴士預約單資料的頁面
        /// </summary>
        // --- [GET] 顯示修改特定巴士預約單資料的頁面 (優化版：自動預填預約人姓名) ---
        [HttpGet]
        public IActionResult EditBooking(long id)
        {
            // 安全檢查：沒登入直接踢回
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            // 1. 撈出這筆預約單主體
            var booking = _db.Bookings.FirstOrDefault(b => b.BookingId == id);
            if (booking == null) return NotFound();

            // 2. 🌟 核心追加：透過預約單內的 PassengerId 向上追查 PassengerProfile -> Account 拿到人名
            string originalPassengerName = "";
            var profile = _db.PassengerProfiles.FirstOrDefault(p => p.PassengerId == booking.PassengerId);
            if (profile != null)
            {
                var account = _db.Accounts.FirstOrDefault(a => a.AccountId == profile.AccountId);
                if (account != null)
                {
                    originalPassengerName = account.Username; // 成功取得原本的預約人名字
                }
            }

            // 3. 將查到的人名塞入 ViewBag，讓前端頁面可以直接帶入文字框
            ViewBag.OriginalPassengerName = originalPassengerName;

            return View(booking);
        }

        /// <summary>
        /// [POST] 處理並儲存修改後的車輛預約單變更
        /// 🌟 核心防呆：強制要求修改後的預約乘車時間（PickupTime）必須大於當前的系統時間
        /// 連動：姓名變更時重新對接 PassengerId、防呆補齊可能缺失的 CreatedAt 欄位時間
        /// </summary>
        // =========================================================================
        // 📅 【模組三修正版】處理並儲存修改後的車輛預約單變更
        // 修正重點：時間輸入錯誤被攔截時，自動重新填補 ViewBag 避免網頁重新載入時人名蒸發
        // =========================================================================
        [HttpPost]
        public IActionResult EditBooking(Booking model, string passengerName)
        {
            // 🌟 1. 核心防呆攔截：防止修改時選到過去的時間
            if (model.PickupTime <= DateTime.Now)
            {
                // 💡 關鍵修復點：當日期錯誤要被退回頁面時，必須先把名字「擦乾淨」並傳回去
                // 如果原本前端有傳過來名字，直接還給它；萬一沒有，就用 model 裡面的 ID 重新查一次
                if (!string.IsNullOrEmpty(passengerName))
                {
                    ViewBag.OriginalPassengerName = passengerName;
                }
                else
                {
                    // 如果沒帶人名進來，就用現有的 PassengerId 反向查人名（安全防禦防空值）
                    var profileCheck = _db.PassengerProfiles.FirstOrDefault(p => p.PassengerId == model.PassengerId);
                    if (profileCheck != null)
                    {
                        var accountCheck = _db.Accounts.FirstOrDefault(a => a.AccountId == profileCheck.AccountId);
                        if (accountCheck != null)
                        {
                            ViewBag.OriginalPassengerName = accountCheck.Username;
                        }
                    }
                }

                // 噴出警告訊息給前端的警告框顯示
                ViewBag.Error = "修改後的預約乘車時間不可小於或等於目前時間，請重新選擇未來的時間。";
                return View(model); // 退回編輯網頁，這時 ViewBag 活著，名字就不會跑掉了！
            }

            // 2. 日期正確，執行正常存檔更新流程
            var dbEntry = _db.Bookings.Find(model.BookingId);
            if (dbEntry == null) return NotFound();

            // 連動機制：如果管理員在網頁上用下拉選單換了乘客，後端動態更新資料庫的 PassengerId 外鍵
            var account = _db.Accounts.FirstOrDefault(a => a.Username == passengerName && a.RoleId == 4);
            if (account != null)
            {
                var profile = _db.PassengerProfiles.FirstOrDefault(p => p.AccountId == account.AccountId);
                if (profile != null) dbEntry.PassengerId = profile.PassengerId;
            }

            // 更新其他核心商務欄位
            dbEntry.PickupTime = model.PickupTime;
            dbEntry.PickupAddr = model.PickupAddr;
            dbEntry.DropoffAddr = model.DropoffAddr;
            dbEntry.BookingStatus = model.BookingStatus;
            dbEntry.BookingType = model.BookingType;

            // 建立時間 (CreatedAt) 的防呆補齊
            if (dbEntry.CreatedAt == null)
            {
                dbEntry.CreatedAt = DateTime.Now;
            }

            _db.SaveChanges(); // 正式將正確的變更寫入遠端 203.64.84.56 資料庫
            return RedirectToAction("Index"); // 成功修改後重導向回首頁
        }

        /// <summary>
        /// [POST AJAX] 取消指定的預約單 (從資料庫直接移除該筆數據)
        /// </summary>
        [HttpPost]
        public IActionResult CancelBooking(long id)
        {
            var booking = _db.Bookings.Find(id);
            if (booking != null)
            {
                _db.Bookings.Remove(booking);
                _db.SaveChanges();
                return Ok();
            }
            return NotFound();
        }

        /// <summary>
        /// [POST AJAX] 永久刪除特定的巴士車輛預約單
        /// 權限：只有高階的 Super 或 Admin 的管理員身份，才可以執行實體資料刪除
        /// </summary>
        [HttpPost]
        public IActionResult DeleteBooking(long id)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Super" && role != "Admin")
            {
                return BadRequest("權限不足：只有管理員可以執行此操作。");
            }

            var booking = _db.Bookings.Find(id);
            if (booking == null) return NotFound("找不到該筆預約單。");

            _db.Bookings.Remove(booking);
            _db.SaveChanges(); // 執行實體移除
            return Ok();
        }
        #endregion

        #region 【模組四：車輛與司機管理】
        // =========================================================================
        // 🚌 機關復康巴士/長照巴士車輛清單維護、司機基本資料管理
        // =========================================================================

        /// <summary>
        /// [GET] 查詢並顯示系統內所有的接駁車輛基本資訊清單網頁
        /// </summary>
        public IActionResult VehicleManagement()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin"))
            {
                return RedirectToAction("Login");
            }

            var vehicles = _db.Vehicles.ToList();
            return View(vehicles);
        }

        /// <summary>
        /// [GET] 顯示新增車輛，或是修改舊車輛基本資料的表單輸入頁面
        /// </summary>
        [HttpGet]
        public IActionResult EditVehicle(int? id)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin"))
            {
                return RedirectToAction("Login");
            }

            if (id == null) return View(new Vehicle()); // 主鍵為空開啟空白表單（新增）

            var vehicle = _db.Vehicles.Find(id); // 有主鍵則去撈資料（修改）
            if (vehicle == null) return NotFound();

            return View(vehicle);
        }

        /// <summary>
        /// [POST] 處理並儲存車輛資料的新增或更新作業
        /// </summary>
        [HttpPost]
        public IActionResult SaveVehicle(Vehicle model)
        {
            if (model.VehicleId == 0) _db.Vehicles.Add(model); // 新增
            else _db.Vehicles.Update(model);                  // 修改

            _db.SaveChanges();
            return RedirectToAction("VehicleManagement");
        }

        /// <summary>
        /// [POST AJAX] 永久移除特定的營運車輛檔案
        /// </summary>
        [HttpPost]
        public IActionResult DeleteVehicle(int id)
        {
            var vehicle = _db.Vehicles.Find(id);
            if (vehicle != null)
            {
                _db.Vehicles.Remove(vehicle);
                _db.SaveChanges();
                return Ok();
            }
            return NotFound();
        }

        /// <summary>
        /// [GET] 顯示司機調度控管主面板網頁
        /// </summary>
        public IActionResult DriverManagement()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            var drivers = _db.Drivers.ToList();
            return View(drivers);
        }

        /// <summary>
        /// [GET] 顯示所有在職、營運司機的人員基本資料維護清單頁面
        /// </summary>
        public IActionResult DriverList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            var drivers = _db.Drivers.ToList();
            return View(drivers);
        }

        /// <summary>
        /// [POST] 處理並建立新的司機基本資料檔案
        /// </summary>
        [HttpPost]
        public IActionResult CreateDriver(Driver model)
        {
            if (ModelState.IsValid)
            {
                _db.Drivers.Add(model);
                _db.SaveChanges();
                return RedirectToAction("DriverList");
            }
            return View("DriverList", _db.Drivers.ToList());
        }
        #endregion

        #region 【模組五：營運調度派車任務】
        // =========================================================================
        // 🔀 營運管理核心：媒合民眾預約單、派遣指定車輛與司機的排班任務面板
        // =========================================================================

        /// <summary>
        /// [GET] 顯示目前已經生成的指派任務列表 (採用 Include 連同關聯的車輛、司機一併撈出避免 N+1 查詢問題)
        /// </summary>
        public IActionResult DispatchList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role)) return RedirectToAction("Login");

            var tasks = _db.DispatchTasks.Include(t => t.Vehicle).Include(t => t.Driver).ToList();
            return View(tasks);
        }

        /// <summary>
        /// [GET] 開啟排班調度、出車任務的指派表單畫面
        /// 邏輯：利用 ViewBag 打包所有「狀態為待審核(0)或補件中(3)」的預約單，以及營運中正常車輛與司機組成下拉選單
        /// </summary>
        [HttpGet]
        public IActionResult EditDispatch(int? id)
        {
            // 挑選出需要排班的有效預約單
            ViewBag.Bookings = _db.Bookings
                .Where(b => b.BookingStatus == 0 || b.BookingStatus == 3)
                .ToList();

            // 挑選出空閒、正常營運中的車輛
            ViewBag.Vehicles = _db.Vehicles.Where(v => v.Status == 0).ToList();

            // 撈取全體司機人員
            ViewBag.Drivers = _db.Drivers.ToList();

            if (id == null) return View(new DispatchTask());
            return View(_db.DispatchTasks.Find(id));
        }

        /// <summary>
        /// [POST] 處理並儲存派車排班調度任務
        /// 核心連動：當排班任務成功指派存檔時，程式碼自動將對應的那張民眾原始預約單狀態更新為「1 (已排班)」，達成連動
        /// </summary>
        [HttpPost]
        public IActionResult SaveDispatch(DispatchTask model)
        {
            if (model.TaskId == 0) _db.DispatchTasks.Add(model);
            else _db.DispatchTasks.Update(model);

            // 🌟 系統自動關聯更新：找到當前被分派的這張原始預約單
            var booking = _db.Bookings.Find(model.BookingId);
            if (booking != null)
            {
                booking.BookingStatus = 1; // 連動更新狀態代碼為 1 (已排班/指派成功)
            }

            _db.SaveChanges(); // 同時寫入調度表與更新預約狀態
            return RedirectToAction("DispatchList");
        }

        /// <summary>
        /// [POST AJAX] 永久刪除特定的派車排班任務
        /// </summary>
        [HttpPost]
        public IActionResult DeleteDispatch(int id)
        {
            var task = _db.DispatchTasks.Find(id);
            if (task == null) return NotFound();
            _db.DispatchTasks.Remove(task);
            _db.SaveChanges();
            return Ok();
        }
        #endregion

        #region 【模組六：服務資格稽核與資料搜尋】
        // =========================================================================
        // 🔎 身心障礙/長照會員身分資格核定審查、跨區域車輛預約稽核搜尋
        // =========================================================================

        /// <summary>
        /// [GET] 顯示身心障礙與長照失能民眾的福利身分「乘車服務資格核定清單」
        /// 排序：優先將 AuditStatus 為 0 (待審核) 的急件排在表格最上方，利於業務人員稽核
        /// </summary>
        public IActionResult AuditList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            var passengers = _db.PassengerProfiles
                                .OrderBy(p => p.AuditStatus ?? 0) // 待審核案件優先列出
                                .ThenBy(p => p.PassengerId)
                                .ToList();

            return View(passengers);
        }

        /// <summary>
        /// [GET AJAX] 異步載入單一筆民眾的身分審查詳細資料檔案
        /// 用途：提供給前端點選表格列時，跳出動態彈出視窗 (Modal) 顯示殘障證明等細節
        /// </summary>
        [HttpGet]
        public IActionResult GetAuditDetail(Guid id)
        {
            var passenger = _db.PassengerProfiles.FirstOrDefault(p => p.PassengerId == id);
            if (passenger == null) return NotFound("找不到該服務使用者");

            return Json(passenger); // 回傳 JSON 格式
        }

        /// <summary>
        /// [POST AJAX] 執行民眾乘車福利資格之核定與條件設定
        /// 功能：變更民眾的審核狀態、指定福利類別（1:復康身障, 2:長照失能）與失能等級備註
        /// </summary>
        [HttpPost]
        public IActionResult SubmitAudit(Guid passengerId, int auditStatus, int identityType, string disabilityLevel)
        {
            var passenger = _db.PassengerProfiles.FirstOrDefault(p => p.PassengerId == passengerId);
            if (passenger == null) return NotFound("找不到該服務使用者");

            // 變更核定欄位
            passenger.AuditStatus = auditStatus;         // 0:待審, 1:核准通過可搭車, 2:駁回
            passenger.IdentityType = identityType;       // 1:復康身障, 2:長照巴士
            passenger.DisabilityLevel = disabilityLevel; // 殘障等級備註

            _db.SaveChanges(); // 存檔
            return Ok("服務使用者資格審核與條件設定已順利儲存！");
        }

        /// <summary>
        /// [GET] 規格書跨區域稽核要求：車輛預約申請資料查詢與稽核功能
        /// 篩選邏輯：依地址前三個字切出服務區域，並分類篩選「成功乘車(1,2)」與「異常/調度失敗/已過期(4,5,6)」案例
        /// </summary>
        public IActionResult BookingAuditSearch(string searchArea, int? bookingStatus)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            // 🌟 核心功能執行：在跨區域搜尋稽核時，同步調用過期偵測校正，確保呈現的統計稽核報表無任何死角與遺漏
            AutoCheckExpiredBookings();

            var query = _db.Bookings.AsQueryable();

            // 1. 服務分區篩選：利用字串 StartsWith 判定地址前三個字（例如：花蓮市、冬山鄉）
            if (!string.IsNullOrEmpty(searchArea))
            {
                query = query.Where(b => b.PickupAddr != null && b.PickupAddr.StartsWith(searchArea));
            }

            // 2. 依據規格書要求的「申請成功」與「申請失敗/異常」案例進行快速分流稽核
            if (bookingStatus.HasValue)
            {
                if (bookingStatus == 1) // 成功乘車案例指標 (1:已排班, 2:已完乘)
                {
                    query = query.Where(b => b.BookingStatus == 1 || b.BookingStatus == 2);
                }
                else if (bookingStatus == 2) // 失敗與異常調度稽核 (4:調度失敗, 5:後補失效, 6:已過期)
                {
                    // 🌟 自動對齊：將全新追加的 6 (已過期) 代碼，一同編入失敗異常稽核的統計漏網中
                    query = query.Where(b => b.BookingStatus == 4 || b.BookingStatus == 5 || b.BookingStatus == 6);
                }
            }

            var result = query.OrderByDescending(b => b.PickupTime).ToList();

            // 智慧下拉選單演算法：從目前資料庫所有欄位中裁切出不重複的前3個字，自動填入前端的「調度服務區」下拉選單
            var availableAreas = _db.Bookings
                .Where(b => b.PickupAddr != null && b.PickupAddr.Length >= 3)
                .Select(b => b.PickupAddr.Substring(0, 3))
                .Distinct()
                .ToList();

            ViewBag.Areas = availableAreas;
            ViewBag.SelectedArea = searchArea;
            ViewBag.SelectedStatus = bookingStatus;

            return View(result);
        }

        /// <summary>
        /// [GET AJAX] 乘客名稱自動完成搜尋 API (提供給前端手動輸入姓名時動態即時查詢對帳)
        /// </summary>
        [HttpGet]
        public IActionResult GetPassengerList(string query)
        {
            var list = _db.Accounts
                .Where(a => a.Username.Contains(query) && a.RoleId == 4)
                .Select(a => new { a.Username })
                .ToList();
            return Json(list);
        }
        #endregion

        #region 【模組七：數據統計與 XML 報表】
        // ==========================================
        // 📈 營運分析：統計車輛預約失敗量、分析區域供需缺口、匯出標準 XML 評估報表
        // ==========================================

        /// <summary>
        /// [GET] 顯示車輛營運數據分區統計與缺口分析網頁
        /// 功能：計算總預約、調度失敗總數，並將「已過期(6)」也一同算入缺乏公眾運具資源的分區缺口排行中
        /// </summary>
        public IActionResult Statistics()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            // 數據庫計數統計
            ViewBag.TotalBookings = _db.Bookings.Count();
            ViewBag.FailedBookings = _db.Bookings.Count(b => b.BookingStatus == 4);
            ViewBag.WaitingFailed = _db.Bookings.Count(b => b.BookingStatus == 5);

            // 🌟 供需缺口演算法優化：將調度失敗(4)、後補失效(5)以及因為沒人理而超時過期(6)的預約單整合在一起
            // 依上車地址前三個字分組統計，精確定位出最缺乏車輛資源、叫不到車的供需缺口黑名單排行
            var areaGap = _db.Bookings
                .Where(b => b.BookingStatus == 4 || b.BookingStatus == 5 || b.BookingStatus == 6)
                .GroupBy(b => b.PickupAddr.Substring(0, 3))
                .Select(g => new { Area = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.AreaGap = areaGap;

            return View();
        }

        /// <summary>
        /// [GET] 匯出分區供需缺口數據為標準實體 XML 檔案
        /// </summary>
        public IActionResult ExportStatisticsXml()
        {
            // 撈取包含過期在內的所有失效訂單並分區統計
            var areaGap = _db.Bookings
                .Where(b => b.BookingStatus == 4 || b.BookingStatus == 5 || b.BookingStatus == 6)
                .GroupBy(b => b.PickupAddr.Substring(0, 3))
                .Select(g => new { Area = g.Key, Count = g.Count() })
                .ToList();

            // 建立 XML 資料樹結構
            XDocument xmlDoc = new XDocument(
                new XElement("StatisticsReport",
                    new XAttribute("ExportDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
                    new XElement("FailureAnalysis",
                        areaGap.Select(x => new XElement("AreaRecord",
                            new XElement("AreaName", x.Area),
                            new XElement("FailCount", x.Count),
                            new XElement("Priority", x.Count > 10 ? "High" : "Normal") // 失敗數大於 10 列為高優先級增車區
                        ))
                    )
                )
            );

            // 將 XML 文件轉換為位元組陣列，強制瀏覽器彈出實體 XML 檔案下載視窗
            var fileName = $"BusGapReport_{DateTime.Now:yyyyMMdd}.xml";
            return File(Encoding.UTF8.GetBytes(xmlDoc.ToString()), "application/xml", fileName);
        }
        #endregion

        #region 【模組八：審核性公告系統】
        // ==========================================
        // 📢 本機關最新消息發布與審核管理
        // ==========================================

        /// <summary>
        /// [GET] 顯示內部系統公告與最新消息管理清單網頁
        /// </summary>
        public IActionResult AnnouncementList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin"))
            {
                return RedirectToAction("Login");
            }

            var list = _db.Announcements.OrderByDescending(a => a.PublishDate).ToList();
            return View(list);
        }

        /// <summary>
        /// [GET] 顯示撰寫新公告或最新消息的輸入網頁表單頁面
        /// </summary>
        [HttpGet]
        public IActionResult CreateAnnouncement() => View();

        /// <summary>
        /// [POST] 處理公告資料的新增建檔或編輯儲存
        /// </summary>
        [HttpPost]
        public IActionResult SaveAnnouncement(Announcement model, string Category)
        {
            if (model.PostId == 0) // 新增
            {
                // 自動將下拉選單分類前綴（如：[營運公告]）與主題字串結合成完整的 Title
                model.Title = $"[{Category}] {model.Title}";
                _db.Announcements.Add(model);
            }
            else // 修改
            {
                _db.Announcements.Update(model);
            }

            _db.SaveChanges();
            return RedirectToAction("AnnouncementList");
        }

        /// <summary>
        /// [POST AJAX] 永久刪除或下架特定的公告消息
        /// </summary>
        [HttpPost]
        public IActionResult DeleteAnnouncement(int id)
        {
            var post = _db.Announcements.Find(id);
            if (post != null)
            {
                _db.Announcements.Remove(post);
                _db.SaveChanges();
                return Ok();
            }
            return NotFound();
        }
        #endregion

        #region 【模組九：正統常見問題管理 (FAQ)】
        // ==========================================
        // ❓ 系統常見問題維護 (增、刪、查、改完整獨立模組，走專屬 Faqs 資料表)
        // ==========================================

        /// <summary>
        /// [GET] 查詢並顯示獨立 Faqs 資料表中的常見問題解答清單網頁
        /// </summary>
        public IActionResult FaqList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            // 撈出所有 FAQ 問答項目，並按照建立時間倒序排列顯示
            var faqs = _db.Faqs.OrderByDescending(f => f.CreatedDate).ToList();
            return View(faqs);
        }

        /// <summary>
        /// [GET] 顯示新增 FAQ 常見問題與解答的表單頁面
        /// </summary>
        [HttpGet]
        public IActionResult CreateFaq()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        /// <summary>
        /// [POST] 處理並儲存新增的常見問題解答項目至 Faqs 資料表中
        /// </summary>
        [HttpPost]
        public IActionResult SaveFaq(Faq model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedDate = DateTime.Now; // 自動在後端指派當前的系統時間做為 FAQ 創立時間
                _db.Faqs.Add(model);
                _db.SaveChanges(); // 資料庫存檔
                return RedirectToAction("FaqList");
            }
            return View("CreateFaq", model);
        }

        /// <summary>
        /// [GET AJAX] 異步載入單一筆常見問題的詳細資料內容
        /// 用途：前端使用者點選表格右側「編輯」時，即時去遠端抓取該筆資料轉為 JSON 物件，自動填入前端彈出修改視窗中
        /// </summary>
        [HttpGet]
        public IActionResult GetFaqDetail(int id)
        {
            var faq = _db.Faqs.Find(id);
            if (faq == null) return NotFound("找不到該筆常見問題");
            return Json(faq); // 回傳完整實體 JSON
        }

        /// <summary>
        /// [POST] 處理並儲存編輯修改後的 FAQ 常見問題欄位變更
        /// </summary>
        [HttpPost]
        public IActionResult EditFaq(Faq model)
        {
            if (ModelState.IsValid)
            {
                var dbEntry = _db.Faqs.Find(model.FaqId);
                if (dbEntry == null) return NotFound("找不到該筆常見問題");

                // 更新分類、經常性問題描述與官方解答欄位
                dbEntry.Category = model.Category;
                dbEntry.Question = model.Question;
                dbEntry.Answer = model.Answer;

                _db.SaveChanges(); // 正式寫入存檔
                return RedirectToAction("FaqList");
            }
            return BadRequest("資料驗證失敗");
        }
        // 導向車輛調度主面板
        [HttpGet]
        public IActionResult VehicleDispatchSystem()
        {
            // 安全防禦：沒登入直接踢回登入頁
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            return View(); // 會自動尋找 Views/Home/VehicleDispatchSystem.cshtml
        }

        // 導向資料庫瀏覽面板 (選填，如果有需要的話)
        [HttpGet]
        public IActionResult DatabaseViewer()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            return View(); // 會自動尋找 Views/Home/DatabaseViewer.cshtml
        }
        /// <summary>
        /// [POST AJAX] 異步永久移除特定的常見問題問答項目
        /// </summary>
        [HttpPost]
        public IActionResult DeleteFaq(int id)
        {
            var faq = _db.Faqs.Find(id);
            if (faq != null)
            {
                _db.Faqs.Remove(faq);
                _db.SaveChanges(); // 執行實體移除
                return Ok(); // 回傳 200 OK 狀態碼
            }
            return NotFound();
        }
        // =========================================================================
        // 🔀 【預留新功能】車輛調度系統 (等待隊友對接中)
        // 邏輯：基本 Session 權限檢查，通過後導向預留的未完成提示視圖
        // =========================================================================
        // [HttpGet]
        // public IActionResult VehicleDispatchSystem()
        // {
        //     // 安全防禦：沒登入直接踢回登入頁
        //     var role = HttpContext.Session.GetString("UserRole");
        //     if (string.IsNullOrEmpty(role))
        //     {
        //         return RedirectToAction("Login");
        //     }

        //     return View(); // 導向 VehicleDispatchSystem.cshtml
        // }
        #endregion
    }
}
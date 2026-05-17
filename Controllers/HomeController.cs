using Microsoft.AspNetCore.Mvc;
using BUS_Agency_backstage.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;


namespace BUS_Agency_backstage.Controllers
{
    public class HomeController : Controller
    {
        // 在類別內加入這個工具方法
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // 將密碼字串轉為位元組陣列並計算雜湊
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));

                // 將位元組陣列轉回十六進位字串
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        // 關鍵修正：DBContext 改為 DbContext (與妳生成的檔案一致)
        private readonly BusBookingDbContext _db;

        public HomeController(BusBookingDbContext db)
        {
            _db = db;
        }
        // 補上這兩行，讓瀏覽器可以直接打開頁面
        [HttpGet]
        public IActionResult Login() => View();
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var user = _db.Accounts.FirstOrDefault(u => u.Username == username);

            if (user != null)
            {
                // 💡 規格書防禦重點：如果帳號已經被管理員鎖定，直接拒絕登入
                if (user.IsLocked == true)
                {
                    ViewBag.Error = "該帳號已被鎖定，無法登入系統。請聯絡系統管理員。";
                    return View();
                }

                // 🔍 關鍵修正：如果輸入的密碼跟資料庫裡的 PasswordHash 完全一致 (例如都是 19960801)
                // 代表他使用的是「未加密的生日初始密碼」，強制引導去修改密碼！
                if (password == user.PasswordHash)
                {
                    HttpContext.Session.SetString("TempUser", username);
                    return RedirectToAction("ChangePassword");
                }

                // 一般已加密的密碼驗證
                string hashedInput = HashPassword(password);
                if (user.PasswordHash == hashedInput)
                {
                    // 根據 RoleId 給予標籤
                    string roleLabel = user.RoleId == 1 ? "Super" : (user.RoleId == 2 ? "CenterAdmin" : "Admin");

                    HttpContext.Session.SetString("UserRole", roleLabel);
                    HttpContext.Session.SetString("UserId", user.AccountId.ToString());

                    return RedirectToAction("Index");
                }
            }

            ViewBag.Error = "帳號或密碼錯誤";
            return View();
        }
        [HttpPost]
        public IActionResult DeleteBooking(long id)
        {
            var role = HttpContext.Session.GetString("UserRole");

            // 檢查權限：只有 Super 或 Admin 可以永久刪除
            if (role != "Super" && role != "Admin")
            {
                return BadRequest("權限不足：只有管理員可以執行此操作。");
            }

            var booking = _db.Bookings.Find(id);
            if (booking == null) return NotFound("找不到該筆預約單。");

            _db.Bookings.Remove(booking);
            _db.SaveChanges();
            return Ok();
        }
        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            // 抓取「目前已發布」的最新 3 則公告
            ViewBag.Announcements = _db.Announcements
                .Where(a => a.PublishDate <= DateTime.Now)
                .OrderByDescending(a => a.PublishDate)
                .Take(3)
                .ToList();

            var bookings = _db.Bookings.ToList();
            return View(bookings);
        }

        // 司機管理：從資料庫抓取真實司機
        public IActionResult DriverManagement()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            var drivers = _db.Drivers.ToList(); // 抓取資料庫所有司機
            return View(drivers);
        }

        // 報表統計
        public IActionResult Statistics()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            ViewBag.TotalBookings = _db.Bookings.Count(); // 計算總預約數
            return View();
        }

        // --- 編輯：顯示表單 (GET) ---
        [HttpGet]
        public IActionResult EditBooking(long id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            // 從資料庫找這筆 ID 的資料
            var booking = _db.Bookings.FirstOrDefault(b => b.BookingId == id);

            if (booking == null) return NotFound(); // 找不到就報錯

            return View(booking); // 把資料丟給編輯視圖
        }

        // --- 1. 預約單修改 (POST)：手動更新欄位以保護 ID[cite: 5, 11] ---
        [HttpPost]
        public IActionResult EditBooking(Booking model, string passengerName)
        {
            var dbEntry = _db.Bookings.Find(model.BookingId);
            if (dbEntry == null) return NotFound();

            // 透過姓名連動 PassengerId
            var account = _db.Accounts.FirstOrDefault(a => a.Username == passengerName && a.RoleId == 4);
            if (account != null)
            {
                var profile = _db.PassengerProfiles.FirstOrDefault(p => p.AccountId == account.AccountId);
                if (profile != null) dbEntry.PassengerId = profile.PassengerId;
            }

            // 更新其餘欄位
            dbEntry.PickupTime = model.PickupTime;
            dbEntry.PickupAddr = model.PickupAddr;
            dbEntry.DropoffAddr = model.DropoffAddr;
            dbEntry.BookingStatus = model.BookingStatus;
            dbEntry.BookingType = model.BookingType;

            _db.SaveChanges();
            return RedirectToAction("Index");
        }

        // --- 取消預約：直接對 DB 刪除 (POST) ---
        [HttpPost]
        public IActionResult CancelBooking(long id)
        {
            // 1. 先確認這筆預約是否存在
            var booking = _db.Bookings.Find(id);

            if (booking != null)
            {
                // 2. 從資料庫移除
                _db.Bookings.Remove(booking);

                // 3. 正式儲存變更
                _db.SaveChanges();

                return Ok(); // 回傳 200 給 JavaScript
            }

            return NotFound(); // 沒找到回傳 404
        }

        [HttpGet] // 建議加上這行，明確表示這是處理 GET 請求的方法
        // 1. 顯示新增表單 (GET)
        public IActionResult CreateBooking()
        {
            // 檢查有沒有登入
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            return View();
        }
        [HttpPost]
        public IActionResult CreateBooking(Booking model, string passengerName)
        {
            var account = _db.Accounts.FirstOrDefault(a => a.Username == passengerName.Trim() && a.RoleId == 4);
            if (account == null)
            {
                ModelState.AddModelError("", "找不到該帳號");
                return View(model);
            }

            var profile = _db.PassengerProfiles.FirstOrDefault(p => p.AccountId == account.AccountId);
            if (profile == null)
            {
                ModelState.AddModelError("", "該乘客尚未填寫詳細基本資料");
                return View(model);
            }

            model.PassengerId = profile.PassengerId;

            // 🔍 關鍵修正：不要在這裡強制寫 model.BookingStatus = 1;
            // 這樣它才會抓取你在 CreateBooking.cshtml 裡選的值 (0 或 3)

            if (model.BookingType == 0) model.BookingType = 1;

            if (ModelState.IsValid)
            {
                _db.Bookings.Add(model);
                _db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 1. 顯示新增使用者頁面
        public IActionResult CreateUser()
        {
            return View("Create");
        }

        [HttpPost]
        public IActionResult CreateUser(string Username, string Password, int RoleId, string RealName)
        {
            // 檢查帳號是否重複
            var exists = _db.Accounts.Any(a => a.Username == Username);
            if (exists)
            {
                ViewBag.Error = "該帳號已被使用，請更換帳號名稱。";
                return View("Create");
            }

            // 建立新管理員帳號
            var newAccount = new Account
            {
                AccountId = Guid.NewGuid(),
                Username = Username,

                // 🔍 關鍵修正：將前端依生日生成的密碼（如 19960801）存入，
                // 這裡先保持明碼，方便登入時第一步做「初始密碼判斷」
                PasswordHash = Password,

                RoleId = RoleId, // 隱藏欄位傳過來的 2 (調度中心管理員)
                IsLocked = false
            };

            _db.Accounts.Add(newAccount);
            _db.SaveChanges();

            return RedirectToAction("UserList"); // 建立成功導向使用者列表
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("TempUser")))
                return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string newPassword, string confirmPassword)
        {
            var username = HttpContext.Session.GetString("TempUser");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "兩次密碼輸入不一致";
                return View();
            }

            // 找到該使用者並更新密碼 (這次要加密了！)
            var user = _db.Accounts.FirstOrDefault(u => u.Username == username);
            if (user != null)
            {
                user.PasswordHash = HashPassword(newPassword);
                _db.SaveChanges();

                // 清除暫存 Session 並完成登入
                HttpContext.Session.Remove("TempUser");
                HttpContext.Session.SetString("UserRole", "Admin");
                return RedirectToAction("Index");
            }

            return RedirectToAction("Login");
        }
        // 1. 查詢所有使用者列表
        // --- 1. 使用者列表頁 (GET) ---
        public IActionResult UserList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            // 抓取所有帳號供管理員檢視
            var users = _db.Accounts.ToList();
            return View(users);
        }

        // --- 2. 密碼初始化功能 (POST) ---
        // 根據規格書要求，管理員可將特定使用者的密碼「重置為初始狀態」
        [HttpPost]
        public IActionResult ResetUserPassword(Guid id, string birthday)
        {
            var user = _db.Accounts.Find(id);
            if (user == null) return NotFound("找不到該使用者");

            if (string.IsNullOrEmpty(birthday))
            {
                return BadRequest("必須指定生日作為初始密碼");
            }

            // 將密碼改回未加密的生日字串（如 19960801），使其下次登入時觸發首次登入改密碼機制
            user.PasswordHash = birthday.Replace("-", "");
            _db.SaveChanges();

            return Ok("密碼已重置為該用戶生日：" + user.PasswordHash);
        }

        // --- 3. 使用者鎖定 / 解鎖功能 (POST) ---
        // 規格書要求：包含使用者鎖定功能
        [HttpPost]
        public IActionResult ToggleUserLock(Guid id)
        {
            var user = _db.Accounts.Find(id);
            if (user == null) return NotFound();

            // 終極安全防禦：最高管理員 (Super) 不可被鎖定
            if (user.RoleId == 1)
            {
                return BadRequest("系統保護：不可鎖定最高管理員。");
            }

            // 切換鎖定狀態 (true -> false / false -> true)
            user.IsLocked = !(user.IsLocked ?? false);
            _db.SaveChanges();

            string statusMessage = user.IsLocked == true ? "帳號已鎖定" : "帳號已解除鎖定";
            return Ok(statusMessage);
        }

        // --- 4. 業務人員個人密碼修改功能 (POST) ---
        // 規格書 C 項：本機關業務人員可透過此功能，進行密碼修改。
        [HttpPost]
        public IActionResult UpdateMyPassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var currentUserIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(currentUserIdStr)) return RedirectToAction("Login");

            Guid currentUserId = Guid.Parse(currentUserIdStr);
            var user = _db.Accounts.Find(currentUserId);
            if (user == null) return RedirectToAction("Login");

            // 驗證舊密碼是否正確
            if (user.PasswordHash != HashPassword(oldPassword))
            {
                TempData["Error"] = "原密碼輸入錯誤";
                return RedirectToAction("Index"); // 或返回個人設定頁
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "兩次新密碼輸入不一致";
                return RedirectToAction("Index");
            }

            // 更新並加密新密碼
            user.PasswordHash = HashPassword(newPassword);
            _db.SaveChanges();

            TempData["Success"] = "密碼修改成功！";
            return RedirectToAction("Index");
        }

        // 2. 顯示修改使用者頁面 (GET)
        [HttpGet]
        public IActionResult EditUser(Guid id)
        {
            var user = _db.Accounts.FirstOrDefault(u => u.AccountId == id);
            if (user == null) return NotFound();

            return View(user);
        }

        // 3. 儲存修改後的資料 (POST)
        [HttpPost]
        public IActionResult EditUser(Account model)
        {
            var user = _db.Accounts.FirstOrDefault(u => u.AccountId == model.AccountId);
            if (user != null)
            {
                user.Username = model.Username;
                user.RoleId = model.RoleId;
                user.CenterId = model.CenterId;
                // 如果有輸入新密碼才修改，否則維持原樣
                if (!string.IsNullOrEmpty(model.PasswordHash) && model.PasswordHash != "0000")
                {
                    user.PasswordHash = HashPassword(model.PasswordHash);
                }

                _db.SaveChanges();
                return RedirectToAction("UserList");
            }
            return View(model);
        }
        [HttpPost]
        public IActionResult DeleteUser(Guid id)
        {
            var targetUser = _db.Accounts.Find(id);
            if (targetUser == null) return NotFound();

            // --- 終極防禦：禁止刪除任何 Root 帳號 ---
            if (targetUser.RoleId == 1)
            {
                // 不論發起者是誰，只要目標是 Root，一律拒絕
                return BadRequest("系統保護：禁止刪除最高管理員 (Root) 帳號。");
            }

            // --- 系統管理員 (Admin) 的額外限制 ---
            var currentRole = HttpContext.Session.GetString("UserRole");
            var currentUserId = HttpContext.Session.GetString("UserId");

            if (currentRole == "Admin")
            {
                // 系統管理員不能刪除自己
                if (targetUser.AccountId.ToString() == currentUserId)
                {
                    return BadRequest("安全限制：系統管理員不可刪除自已。");
                }
            }

            // 執行刪除
            _db.Accounts.Remove(targetUser);
            _db.SaveChanges();
            return Ok();
        }
        // --- 1. 公告管理列表頁 ---
        public IActionResult AnnouncementList()
        {
            var role = HttpContext.Session.GetString("UserRole");

            // 如果 Session 掉位或是角色不是管理員，就會被踢回登入頁
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin"))
            {
                return RedirectToAction("Login");
            }

            var list = _db.Announcements.OrderByDescending(a => a.PublishDate).ToList();
            return View(list);
            // if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            // // 抓取所有公告並按日期排序
            // var list = _db.Announcements.OrderByDescending(a => a.PublishDate).ToList();
            // return View(list);
        }

        // --- 2. 新增公告頁面 (GET) ---
        [HttpGet]
        public IActionResult CreateAnnouncement() => View();

        // --- 3. 處理新增/修改 (POST) ---
        [HttpPost]
        public IActionResult SaveAnnouncement(Announcement model, string Category)
        {
            if (model.PostId == 0) // 新增
            {
                // 加上分類標記並存檔
                model.Title = $"[{Category}] {model.Title}";
                _db.Announcements.Add(model);
            }
            else // 修改 (這部分可視需求擴充 Edit 功能)
            {
                _db.Announcements.Update(model);
            }

            _db.SaveChanges();
            return RedirectToAction("AnnouncementList");
        }

        // --- 4. 刪除公告 (POST) ---
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
        // --- 1. 車輛管理列表 ---
        public IActionResult VehicleManagement()
        {
            // 檢查登入與權限
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin"))
            {
                return RedirectToAction("Login");
            }

            var vehicles = _db.Vehicles.ToList();
            return View(vehicles);
        }

        // --- 2. 新增/編輯車輛頁面 (GET) ---
        [HttpGet]
        public IActionResult EditVehicle(int? id)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin"))
            {
                return RedirectToAction("Login");
            }

            if (id == null) return View(new Vehicle()); // 回傳空模型供新增

            var vehicle = _db.Vehicles.Find(id); // 依照 PK (VehicleID) 尋找
            if (vehicle == null) return NotFound();

            return View(vehicle);
        }

        // --- 3. 儲存車輛資料 (POST) ---
        [HttpPost]
        public IActionResult SaveVehicle(Vehicle model)
        {
            // 根據你的資料庫欄位：VehicleID
            if (model.VehicleId == 0)
            {
                _db.Vehicles.Add(model);
            }
            else
            {
                _db.Vehicles.Update(model);
            }
            _db.SaveChanges();
            return RedirectToAction("VehicleManagement");
        }

        // --- 4. 刪除車輛 (POST) ---
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


        // --- 2. 派車任務 (DispatchTasks) CRUD[cite: 5, 11] ---
        public IActionResult DispatchList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role)) return RedirectToAction("Login");

            var tasks = _db.DispatchTasks.Include(t => t.Vehicle).Include(t => t.Driver).ToList();
            return View(tasks);
        }

        [HttpGet]
        public IActionResult EditDispatch(int? id)
        {
            // 🔍 關鍵檢查點：篩選條件是否太嚴格？
            // 如果你的預約單狀態不是 0 (待審核) 或 3 (補件中)，就不會出現在清單裡
            ViewBag.Bookings = _db.Bookings
                .Where(b => b.BookingStatus == 0 || b.BookingStatus == 3)
                .ToList();

            ViewBag.Vehicles = _db.Vehicles.Where(v => v.Status == 0).ToList();
            ViewBag.Drivers = _db.Drivers.ToList();

            if (id == null) return View(new DispatchTask());
            return View(_db.DispatchTasks.Find(id));
        }
        // [HttpGet]
        // public IActionResult EditDispatch(int? id)
        // {
        //     // 🛠️ 暫時移除 Where 條件，顯示前 20 筆預約單進行測試
        //     ViewBag.Bookings = _db.Bookings
        //         .OrderByDescending(b => b.BookingId)
        //         .Take(20)
        //         .ToList();

        //     ViewBag.Vehicles = _db.Vehicles.Where(v => v.Status == 0).ToList();
        //     ViewBag.Drivers = _db.Drivers.ToList();

        //     if (id == null) return View(new DispatchTask());
        //     return View(_db.DispatchTasks.Find(id));
        // }
        [HttpPost]
        public IActionResult DeleteDispatch(int id)
        {
            var task = _db.DispatchTasks.Find(id);
            if (task == null) return NotFound();
            _db.DispatchTasks.Remove(task);
            _db.SaveChanges();
            return Ok();
        }

        // --- 3. 乘客搜尋 API (提供自動完成列表使用)[cite: 11] ---
        [HttpGet]
        public IActionResult GetPassengerList(string query)
        {
            var list = _db.Accounts
                .Where(a => a.Username.Contains(query) && a.RoleId == 4)
                .Select(a => new { a.Username })
                .ToList();
            return Json(list);
        }
        [HttpPost]
        public IActionResult SaveDispatch(DispatchTask model)
        {
            // 1. 存入派車任務
            if (model.TaskId == 0) _db.DispatchTasks.Add(model);
            else _db.DispatchTasks.Update(model);

            // 2. 自動連動：將對應的預約單改為「已排班」[cite: 7, 8]
            var booking = _db.Bookings.Find(model.BookingId);
            if (booking != null)
            {
                booking.BookingStatus = 1; // 狀態設為已排班
            }

            _db.SaveChanges(); // 同時儲存任務與預約單狀態變更[cite: 8]
            return RedirectToAction("DispatchList");
        }
        // ==========================================
        // 服務使用者審核功能 (精確對接 PassengerProfile 模型)
        // ==========================================

        // 1. 檢視所有身心障礙/長照服務使用者申請案列表
        public IActionResult AuditList()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Super" && role != "Admin" && role != "CenterAdmin"))
            {
                return RedirectToAction("Login");
            }

            // 撈出所有乘客資料，優先將 0:待審核 排序在最前面
            var passengers = _db.PassengerProfiles
                                .OrderBy(p => p.AuditStatus ?? 0)
                                .ThenBy(p => p.PassengerId)
                                .ToList();

            return View(passengers);
        }

        // 2. 異步載入單一申請者的詳細資料 (提供給彈出視窗使用)
        [HttpGet]
        public IActionResult GetAuditDetail(Guid id) // 依模型將型別設為 Guid
        {
            var passenger = _db.PassengerProfiles.FirstOrDefault(p => p.PassengerId == id);
            if (passenger == null) return NotFound("找不到該服務使用者");

            return Json(passenger);
        }

        // 3. 執行是否可以使用之核定及條件設定 (POST AJAX)
        [HttpPost]
        public IActionResult SubmitAudit(Guid passengerId, int auditStatus, int identityType, string disabilityLevel)
        {
            var passenger = _db.PassengerProfiles.FirstOrDefault(p => p.PassengerId == passengerId);
            if (passenger == null) return NotFound("找不到該服務使用者");

            // 依照業務人員核定的內容進行條件與狀態變更
            passenger.AuditStatus = auditStatus;         // 0:待審, 1:通過, 2:駁回
            passenger.IdentityType = identityType;       // 1:復康(身障), 2:長照(失能)
            passenger.DisabilityLevel = disabilityLevel; // 障礙等級/失能等級

            _db.SaveChanges();
            return Ok("服務使用者資格審核與條件設定已順利儲存！");
        }
    }
}
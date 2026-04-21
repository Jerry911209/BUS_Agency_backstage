using Microsoft.AspNetCore.Mvc;
using BUS_Agency_backstage.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace BUS_Agency_backstage.Controllers
{
    public class HomeController : Controller
    {
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
            // 關鍵修正：Accounts 是複數 (與 DbContext 裡的 DbSet 一致)
            var user = _db.Accounts.FirstOrDefault(u => u.Username == username && u.PasswordHash == password);

            if (user != null)
            {
                HttpContext.Session.SetString("UserRole", "Admin");
                return RedirectToAction("Index");
            }
            ViewBag.Error = "帳號或密碼錯誤";
            return View();
        }

        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole")))
                return RedirectToAction("Login");

            // 抓取真實資料庫預約
            var bookings = _db.Bookings.ToList();
            return View(bookings);
        }

        // 車輛管理：從資料庫抓取真實車輛
        public IActionResult VehicleManagement()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserRole"))) return RedirectToAction("Login");

            var vehicles = _db.Vehicles.ToList(); // 抓取資料庫所有車輛
            return View(vehicles);
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

        // --- 編輯：存入資料庫 (POST) ---
        [HttpPost]
        public IActionResult EditBooking(Booking model)
        {
            if (ModelState.IsValid)
            {
                // 1. 標記這筆資料已被修改
                _db.Bookings.Update(model);

                // 2. 存檔到 DB
                _db.SaveChanges();

                return RedirectToAction("Index");
            }
            return View(model);
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
        public IActionResult CreateBooking(Booking model)
        {
            // 如果你的 PassengerId 是 Guid 且必填
            // 這裡要確保它不是空的，否則 SaveChanges 會報錯
            if (model.PassengerId == Guid.Empty)
            {
                // 先暫時塞一個固定的 GUID 測試用 (請換成你資料庫裡有的 ID)
                model.PassengerId = Guid.Parse("你資料庫裡的某個GUID");
            }

            if (ModelState.IsValid)
            {
                _db.Bookings.Add(model);
                _db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(model);
        }
    }
}
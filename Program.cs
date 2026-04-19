using Microsoft.EntityFrameworkCore;
// 確保這行與你 Models 資料夾內的 namespace 一致
using BUS_Agency_backstage.Models; 

var builder = WebApplication.CreateBuilder(args);

// === 服務註冊區 ===
builder.Services.AddControllersWithViews();

// 註冊資料庫上下文
// 注意：類別名稱必須與 Models 裡的檔案名稱「完全一樣」
builder.Services.AddDbContext<BusBookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session 服務
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// === 中間件設定區 ===
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();
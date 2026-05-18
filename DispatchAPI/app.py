from flask import Flask, request, jsonify, send_from_directory
try:
    from flask_cors import CORS
except ImportError:
    CORS = None
import pyodbc
import os
import hashlib
from datetime import datetime

app = Flask(__name__)
if CORS:
    CORS(app)


@app.route("/", methods=["GET"])
def index():
    response = send_from_directory(app.root_path, "Untitled-1.html")
    response.headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0"
    response.headers["Pragma"] = "no-cache"
    response.headers["Expires"] = "0"
    return response


@app.route("/database", methods=["GET"])
def database_viewer():
    response = send_from_directory(app.root_path, "database_viewer.html")
    response.headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0"
    response.headers["Pragma"] = "no-cache"
    response.headers["Expires"] = "0"
    return response


# =========================================================================
# SQL Server 連線設定 (🌟 Jerry 智慧自適應與變數解鎖版)
# =========================================================================
# 1. 全自動偵測本機可用驅動
available_drivers = [d for d in pyodbc.drivers() if "SQL Server" in d]
if available_drivers:
    DB_DRIVER = available_drivers[0]
    print(f"🎉 [智慧連動] 成功對接本機 SQL Server 驅動程式：[{DB_DRIVER}]")
else:
    DB_DRIVER = "ODBC Driver 17 for SQL Server"
    print("⚠️  [資安警告] 未偵測到本機驅動，使用預設值。")

# 2. 🌟 解鎖被隊友封印的連線變數，強制固定導向公網伺服器，澈底消滅 LocalDB 閃退報錯
DB_SERVER = os.getenv("SQLSERVER_HOST", "203.64.84.56")
DB_PORT = os.getenv("SQLSERVER_PORT", "1433")
DB_NAME = os.getenv("SQLSERVER_DB", "BusBookingDb")  # 對齊您的 C# 資料庫名稱
DB_USER = os.getenv("SQLSERVER_USER", "tcumi")        # 對齊公網帳號
DB_PASSWORD = os.getenv("SQLSERVER_PASSWORD", "tcumi") # 對齊公網密碼

# 目前系統操作者預設帳號
CURRENT_OPERATOR_USERNAME = os.getenv("CURRENT_OPERATOR_USERNAME", "racky")

print(f"📡 [連線準備] 正在導向遠端公網資料庫：{DB_SERVER} (資料庫: {DB_NAME})")


def get_connection():
    conn_str = (
        f"DRIVER={{{DB_DRIVER}}};"
        f"SERVER={DB_SERVER},{DB_PORT};"
        f"DATABASE={DB_NAME};"
        f"UID={DB_USER};"
        f"PWD={DB_PASSWORD};"
        "TrustServerCertificate=yes;"
        "Timeout=30;"
    )
    return pyodbc.connect(conn_str)


def row_to_dict(cursor, row):
    columns = [column[0] for column in cursor.description]
    result = {}

    for index, value in enumerate(row):
        if isinstance(value, datetime):
            result[columns[index]] = value.strftime("%Y-%m-%d %H:%M:%S")
        else:
            result[columns[index]] = value

    return result


def quote_sql_identifier(value):
    return "[" + str(value).replace("]", "]]") + "]"


def booking_status_to_text(status, has_dispatch=False, actual_arrival=None):
    if actual_arrival:
        return "已完成"
    if has_dispatch:
        return "已派車"

    mapping = {
        0: "待調度",
        1: "待調度",
        2: "待調度",
        3: "待調度",
        4: "待調度",
        8: "已完成",
        9: "已取消",
    }
    return mapping.get(status, "待調度")


def vehicle_status_to_text(status, has_active_task=False):
    if has_active_task:
        return "出勤中"

    mapping = {
        0: "可派遣",
        1: "出勤中",
        2: "維修中",
        3: "停用",
    }
    return mapping.get(status, "可派遣")


def identity_type_to_text(identity_type):
    mapping = {
        1: "身心障礙",
        2: "長照",
        3: "一般",
    }
    return mapping.get(identity_type, "一般")


def split_pickup_time(value):
    if not value:
        return "", ""
    return value.strftime("%Y-%m-%d"), value.strftime("%H:%M")


def looks_like_wheelchair(value):
    return 1 if value and "輪椅" in str(value) else 0


def as_int_id(value):
    if value is None:
        return None
    text = str(value)
    digits = "".join(ch for ch in text if ch.isdigit())
    return int(digits) if digits else None


def ensure_driver_messages_table(cursor):
    cursor.execute("""
    IF NOT EXISTS (
        SELECT 1
        FROM sys.tables
        WHERE name = 'DriverMessages'
          AND schema_id = SCHEMA_ID('dbo')
    )
    CREATE TABLE dbo.DriverMessages (
        MessageId NVARCHAR(40) PRIMARY KEY,
        DriverId NVARCHAR(30) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        SentAt DATETIME2 DEFAULT SYSDATETIME()
    )
    """)


# =========================================================================
# 建立資料表 (🌟 已修正為符合 C# 規範的實體單數結構，防範無效的物件名稱錯誤)
# =========================================================================
def init_db():
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    IF NOT EXISTS (
        SELECT * FROM sysobjects WHERE name='Reservations' AND xtype='U'
    )
    CREATE TABLE Reservations (
        ReservationId NVARCHAR(30) PRIMARY KEY,
        IdentityType NVARCHAR(20) NOT NULL,
        PassengerName NVARCHAR(50) NOT NULL,
        Phone NVARCHAR(30) NOT NULL,
        RideDate DATE NOT NULL,
        RideTime NVARCHAR(10) NOT NULL,
        Pickup NVARCHAR(255) NOT NULL,
        Dropoff NVARCHAR(255) NOT NULL,
        Note NVARCHAR(MAX),
        Status NVARCHAR(20) NOT NULL DEFAULT N'待調度',
        DriverId NVARCHAR(30),
        VehicleId NVARCHAR(30),
        CreatedAt DATETIME2 DEFAULT SYSDATETIME()
    )
    """)

    cursor.execute("""
    IF NOT EXISTS (
        SELECT * FROM sysobjects WHERE name='Driver' AND xtype='U'
    )
    CREATE TABLE Driver (
        DriverID INT IDENTITY(1,1) PRIMARY KEY,
        DriverName NVARCHAR(50) NOT NULL,
        Mobile NVARCHAR(30) NOT NULL,
        Status INT NOT NULL DEFAULT 0
    )
    """)

    cursor.execute("""
    IF NOT EXISTS (
        SELECT * FROM sysobjects WHERE name='Vehicle' AND xtype='U'
    )
    CREATE TABLE Vehicle (
        VehicleID INT IDENTITY(1,1) PRIMARY KEY,
        PlateNo NVARCHAR(30) NOT NULL,
        VehicleType NVARCHAR(50) NOT NULL,
        Status INT NOT NULL DEFAULT 0,
        SeatCount INT DEFAULT 4
    )
    """)

    ensure_driver_messages_table(cursor)

    conn.commit()
    conn.close()


# =========================
# 產生編號
# =========================
def generate_reservation_id():
    today = datetime.now().strftime("%Y%m%d")
    prefix = f"R{today}"

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute(
        """
        SELECT COUNT(*) 
        FROM Reservations
        WHERE ReservationId LIKE ?
    """,
        f"{prefix}%",
    )

    count = cursor.fetchone()[0] + 1
    conn.close()

    return f"{prefix}{str(count).zfill(3)}"


def generate_custom_id(table_name, id_column, prefix):
    conn = get_connection()
    cursor = conn.cursor()

    query = f"""
        SELECT TOP 1 {id_column}
        FROM {table_name}
        WHERE {id_column} LIKE ?
        ORDER BY {id_column} DESC
    """

    cursor.execute(query, f"{prefix}%")
    row = cursor.fetchone()
    conn.close()

    if row:
        last_id = row[0]
        try:
            number = int(str(last_id).replace(prefix, "")) + 1
        except:
            number = 1
    else:
        number = 1

    return f"{prefix}{str(number).zfill(3)}"


# =========================
# 測試 API
# =========================
@app.route("/api/health", methods=["GET"])
def health_check():
    try:
        conn = get_connection()
        conn.close()
        return jsonify({"success": True, "message": "SQL Server 連線成功"})
    except Exception as e:
        return (
            jsonify(
                {"success": False, "message": "SQL Server 連線失敗", "error": str(e)}
            ),
            500,
        )


@app.route("/api/database/tables", methods=["GET"])
def get_database_tables():
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
        SELECT
            s.name AS schemaName,
            t.name AS tableName,
            SUM(p.rows) AS row_count
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        INNER JOIN sys.partitions p ON t.object_id = p.object_id
        WHERE p.index_id IN (0, 1)
        GROUP BY s.name, t.name
        ORDER BY s.name, t.name
    """)

    data = [
        {
            "schema": row.schemaName,
            "table": row.tableName,
            "rowCount": row.row_count or 0,
        }
        for row in cursor.fetchall()
    ]

    conn.close()
    return jsonify({"tables": data})


@app.route("/api/database/table/<schema_name>/<table_name>", methods=["GET"])
def get_database_table_rows(schema_name, table_name):
    limit = request.args.get("limit", "100")
    try:
        limit = max(1, min(int(limit), 1000))
    except ValueError:
        limit = 100

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute(
        """
        SELECT COUNT(*)
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = ?
          AND t.name = ?
    """,
        schema_name,
        table_name,
    )

    if cursor.fetchone()[0] == 0:
        conn.close()
        return jsonify({"success": False, "message": "找不到指定資料表"}), 404

    full_table_name = (
        f"{quote_sql_identifier(schema_name)}." f"{quote_sql_identifier(table_name)}"
    )
    cursor.execute(f"SELECT TOP ({limit}) * FROM {full_table_name}")

    rows = cursor.fetchall()
    data = [row_to_dict(cursor, row) for row in rows]
    columns = [column[0] for column in cursor.description]

    conn.close()
    return jsonify(
        {
            "schema": schema_name,
            "table": table_name,
            "limit": limit,
            "columns": columns,
            "rows": data,
        }
    )


@app.route("/api/database/create-operator", methods=["POST"])
def create_operator_account():
    data = request.get_json(silent=True) or {}
    username = (data.get("username") or "racky").strip()
    password = data.get("password") or username
    role_name = data.get("roleName") or "營運商"
    role_id = 6
    center_id = data.get("centerId", 100)

    if not username:
        return jsonify({"success": False, "message": "請提供帳號名稱"}), 400

    password_hash = hashlib.sha256(password.encode("utf-8")).hexdigest()

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("SELECT RoleID FROM dbo.Roles WHERE RoleName = ?", role_name)
    role = cursor.fetchone()

    if role:
        role_id = role.RoleID
    else:
        cursor.execute(
            """
            INSERT INTO dbo.Roles (
                RoleID,
                RoleName,
                PermissionMap
            )
            VALUES (?, ?, ?)
        """,
            role_id,
            role_name,
            '{"target":"operator","desc":"營運商後台管理"}',
        )

    cursor.execute(
        """
        SELECT
            AccountID,
            RoleID
        FROM dbo.Account
        WHERE Username = ?
    """,
        username,
    )
    existing = cursor.fetchone()

    if existing:
        cursor.execute(
            """
            UPDATE dbo.Account
            SET RoleID = ?,
                CenterID = COALESCE(CenterID, ?),
                IsLocked = 0
            WHERE Username = ?
        """,
            role_id,
            center_id,
            username,
        )
        account_id = existing.AccountID
        action = "updated"
    else:
        cursor.execute(
            """
            INSERT INTO dbo.Account (
                AccountID,
                Username,
                PasswordHash,
                RoleID,
                CenterID,
                IsLocked,
                LastLoginIP
            )
            OUTPUT INSERTED.AccountID
            VALUES (NEWID(), ?, ?, ?, ?, 0, NULL)
        """,
            username,
            password_hash,
            role_id,
            center_id,
        )
        account_id = cursor.fetchone()[0]
        action = "created"

    conn.commit()
    conn.close()

    return jsonify(
        {
            "success": True,
            "action": action,
            "accountId": str(account_id),
            "username": username,
            "roleId": role_id,
            "roleName": role_name,
            "centerId": center_id,
        }
    )


# =========================================================================
# 儀表板統計 (🌟 已對齊單數後台資料表)
# =========================================================================
@app.route("/api/dashboard", methods=["GET"])
def get_dashboard():
    try:
        conn = get_connection()
        cursor = conn.cursor()

        cursor.execute("SELECT COUNT(*) FROM dbo.Bookings")
        total = cursor.fetchone()[0]

        cursor.execute("""
            SELECT COUNT(*)
            FROM dbo.Bookings b
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.DispatchTasks dt WHERE dt.BookingID = b.BookingID
            )
        """)
        pending = cursor.fetchone()[0]

        cursor.execute("""
            SELECT COUNT(*)
            FROM dbo.DispatchTasks
            WHERE ActualArrival IS NULL
        """)
        dispatched = cursor.fetchone()[0]

        cursor.execute("""
            SELECT COUNT(*)
            FROM dbo.Vehicle v
            WHERE v.Status = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.DispatchTasks dt
                  WHERE dt.VehicleID = v.VehicleID
                    AND dt.ActualArrival IS NULL
              )
        """)
        available_vehicles = cursor.fetchone()[0]
        conn.close()

        return jsonify(
            {
                "totalReservations": total,
                "pendingReservations": pending,
                "dispatchedReservations": dispatched,
                "availableVehicles": available_vehicles,
            }
        )
    except Exception as e:
        return jsonify({"success": False, "error": str(e)}), 500


# =========================================================================
# 預約資料 API (🌟 已全面校正為真實 C# 資料結構欄位)
# =========================================================================
@app.route("/api/reservations", methods=["GET"])
def get_reservations():
    status = request.args.get("status", "全部")
    keyword = request.args.get("keyword", "")

    try:
        conn = get_connection()
        cursor = conn.cursor()

        sql = """
            SELECT
                b.BookingID AS bookingId,
                b.BookingType AS bookingType,
                b.PickupTime AS pickupTime,
                b.PickupAddr AS pickup,
                b.DropoffAddr AS dropoff,
                b.CompanionCount AS companionCount,
                b.BookingStatus AS bookingStatus,
                p.RealName AS passengerName,
                p.IdentityNo AS passengerId,
                p.IdentityType AS identityType,
                p.AssistiveDevice AS assistiveDevice,
                p.Address AS passengerAddress,
                dt.TaskID AS taskId,
                dt.DriverID AS driverId,
                dt.VehicleID AS vehicleId,
                dt.ActualArrival AS actualArrival
            FROM dbo.Bookings b
            LEFT JOIN dbo.PassengerProfile p ON b.PassengerID = p.PassengerID
            LEFT JOIN dbo.DispatchTasks dt ON b.BookingID = dt.BookingID
            WHERE 1 = 1
        """

        params = []
        if keyword:
            sql += """
                AND (
                    CONVERT(NVARCHAR(30), b.BookingID) LIKE ?
                    OR p.RealName LIKE ?
                    OR p.IdentityNo LIKE ?
                    OR b.PickupAddr LIKE ?
                    OR b.DropoffAddr LIKE ?
                )
            """
            kw = f"%{keyword}%"
            params.extend([kw, kw, kw, kw, kw])

        sql += " ORDER BY b.PickupTime ASC, b.BookingID ASC"

        cursor.execute(sql, params)
        rows = cursor.fetchall()
        data = []

        for row in rows:
            ride_date, ride_time = split_pickup_time(row.pickupTime)
            row_status = booking_status_to_text(
                row.bookingStatus,
                has_dispatch=row.taskId is not None,
                actual_arrival=row.actualArrival,
            )
            if status != "全部" and row_status != status:
                continue

            data.append(
                {
                    "id": str(row.bookingId),
                    "identity": identity_type_to_text(row.identityType),
                    "eligibility": "資格已審核",
                    "passengerName": row.passengerName or "未命名乘客",
                    "passengerId": row.passengerId or "",
                    "phone": "",
                    "rideDate": ride_date,
                    "rideTime": ride_time,
                    "pickup": row.pickup or row.passengerAddress or "",
                    "dropoff": row.dropoff or "",
                    "wheelchairCount": looks_like_wheelchair(row.assistiveDevice),
                    "companionCount": row.companionCount or 0,
                    "priority": "一般",
                    "notifyMethod": "簡訊",
                    "note": row.assistiveDevice or "",
                    "status": row_status,
                    "driverId": str(row.driverId) if row.driverId is not None else "",
                    "vehicleId": str(row.vehicleId) if row.vehicleId is not None else "",
                    "cancelReason": "",
                    "cancelNote": "",
                    "createdAt": (
                        row.pickupTime.strftime("%Y-%m-%d %H:%M:%S")
                        if row.pickupTime
                        else ""
                    ),
                    "updatedAt": (
                        row.pickupTime.strftime("%Y-%m-%d %H:%M:%S")
                        if row.pickupTime
                        else ""
                    ),
                }
            )

        conn.close()
        return jsonify(data)
    except Exception as e:
        print(f"❌ 撈取預約單失敗: {str(e)}")
        return jsonify([]), 500


@app.route("/api/reservations", methods=["POST"])
def create_reservation():
    data = request.get_json()
    required_fields = ["identity", "passengerName", "phone", "rideDate", "rideTime", "pickup", "dropoff"]

    for field in required_fields:
        if not data.get(field):
            return jsonify({"success": False, "message": f"缺少欄位：{field}"}), 400

    try:
        reservation_id = generate_reservation_id()
        conn = get_connection()
        cursor = conn.cursor()

        cursor.execute(
            """
            INSERT INTO Reservations (
                ReservationId, IdentityType, PassengerName, Phone, RideDate, RideTime, Pickup, Dropoff, Note, Status
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, N'待調度')
        """,
            reservation_id, data["identity"], data["passengerName"], data["phone"],
            data["rideDate"], data["rideTime"], data["pickup"], data["dropoff"], data.get("note", ""),
        )
        conn.commit()
        conn.close()
        return jsonify({"success": True, "message": "預約新增成功", "reservationId": reservation_id})
    except Exception as e:
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/api/reservations/<reservation_id>/cancel", methods=["PATCH"])
def cancel_reservation(reservation_id):
    booking_id = as_int_id(reservation_id)
    if booking_id is None:
        return jsonify({"success": False, "message": "案件編號格式錯誤"}), 400

    try:
        conn = get_connection()
        cursor = conn.cursor()

        cursor.execute("UPDATE dbo.Bookings SET BookingStatus = 9 WHERE BookingID = ?", booking_id)
        if cursor.rowcount == 0:
            conn.close()
            return jsonify({"success": False, "message": "找不到此預約案件"}), 404

        cursor.execute("DELETE FROM dbo.DispatchTasks WHERE BookingID = ?", booking_id)
        conn.commit()
        conn.close()
        return jsonify({"success": True, "message": "預約已取消"})
    except Exception as e:
        return jsonify({"success": False, "error": str(e)}), 500


# =========================================================================
# 派車 API (🌟 修正為寫入 C# 中台對應的 DispatchTasks 實體表)
# =========================================================================
@app.route("/api/dispatch", methods=["POST"])
def assign_dispatch():
    data = request.get_json()
    reservation_id = as_int_id(data.get("reservationId"))
    driver_id = as_int_id(data.get("driverId"))
    vehicle_id = as_int_id(data.get("vehicleId"))

    if not reservation_id or not driver_id or not vehicle_id:
        return jsonify({"success": False, "message": "請提供完整派車參數"}), 400

    try:
        conn = get_connection()
        cursor = conn.cursor()

        # 寫入 C# 真正的派車工作表
        cursor.execute(
            """
            INSERT INTO dbo.DispatchTasks (BookingID, DriverID, VehicleID, DispatchTime, ActualArrival)
            VALUES (?, ?, ?, SYSDATETIME(), NULL)
            """,
            reservation_id, driver_id, vehicle_id
        )

        # 更新大表的狀態
        cursor.execute("UPDATE dbo.Bookings SET BookingStatus = 4 WHERE BookingID = ?", reservation_id)
        cursor.execute("UPDATE dbo.Driver SET Status = 1 WHERE DriverID = ?", driver_id)
        cursor.execute("UPDATE dbo.Vehicle SET Status = 1 WHERE VehicleID = ?", vehicle_id)

        conn.commit()
        conn.close()
        return jsonify({"success": True, "message": "派車成功"})
    except Exception as e:
        return jsonify({"success": False, "message": f"派車核心錯誤: {str(e)}"}), 500


@app.route("/api/dispatch/<reservation_id>/reset", methods=["PATCH"])
def reset_dispatch(reservation_id):
    booking_id = as_int_id(reservation_id)
    try:
        conn = get_connection()
        cursor = conn.cursor()

        cursor.execute("SELECT DriverID, VehicleID FROM dbo.DispatchTasks WHERE BookingID = ?", booking_id)
        row = cursor.fetchone()

        if row:
            driver_id, vehicle_id = row[0], row[1]
            cursor.execute("DELETE FROM dbo.DispatchTasks WHERE BookingID = ?", booking_id)
            cursor.execute("UPDATE dbo.Bookings SET BookingStatus = 0 WHERE BookingID = ?", booking_id)
            if driver_id: cursor.execute("UPDATE dbo.Driver SET Status = 0 WHERE DriverID = ?", driver_id)
            if vehicle_id: cursor.execute("UPDATE dbo.Vehicle SET Status = 0 WHERE VehicleID = ?", vehicle_id)

        conn.commit()
        conn.close()
        return jsonify({"success": True, "message": "已成功退回待調度"})
    except Exception as e:
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/api/dispatch-orders", methods=["GET"])
def get_dispatch_orders():
    try:
        conn = get_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT
                b.BookingID AS id, p.IdentityType AS identityType, p.RealName AS passengerName,
                b.PickupTime AS pickupTime, b.PickupAddr AS pickup, b.DropoffAddr AS dropoff,
                d.DriverName AS driverName, v.PlateNo AS plate, v.VehicleType AS vehicleType
            FROM dbo.DispatchTasks dt
            INNER JOIN dbo.Bookings b ON dt.BookingID = b.BookingID
            LEFT JOIN dbo.PassengerProfile p ON b.PassengerID = p.PassengerID
            LEFT JOIN dbo.Driver d ON dt.DriverID = d.DriverID
            LEFT JOIN dbo.Vehicle v ON dt.VehicleID = v.VehicleID
            WHERE dt.ActualArrival IS NULL
            ORDER BY b.PickupTime ASC, b.BookingID ASC
        """)

        rows = cursor.fetchall()
        data = []
        for row in rows:
            ride_date, ride_time = split_pickup_time(row.pickupTime)
            data.append({
                "id": str(row.id),
                "identity": identity_type_to_text(row.identityType),
                "passengerName": row.passengerName or "未命名乘客",
                "phone": "", "rideDate": ride_date, "rideTime": ride_time,
                "pickup": row.pickup or "", "dropoff": row.dropoff or "",
                "driverName": row.driverName or "", "plate": row.plate or "",
                "vehicleType": row.vehicleType or "",
            })
        conn.close()
        return jsonify(data)
    except Exception as e:
        return jsonify([]), 500


# =========================================================================
# 司機 API (🌟 對齊 dbo.Driver 單數表與欄位名稱)
# =========================================================================
@app.route("/api/drivers", methods=["GET"])
def get_drivers():
    try:
        conn = get_connection()
        cursor = conn.cursor()

        cursor.execute("""
            SELECT
                d.DriverID AS id, d.DriverName AS name, d.Mobile AS phone,
                CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.DispatchTasks dt WHERE dt.DriverID = d.DriverID AND dt.ActualArrival IS NULL
                ) THEN 1 ELSE 0 END AS hasActiveTask
            FROM dbo.Driver d
            ORDER BY d.DriverID ASC
        """)

        rows = cursor.fetchall()
        data = []
        for row in rows:
            data.append({
                "id": str(row.id), "name": row.name or "", "phone": row.phone or "",
                "status": "出勤中" if row.hasActiveTask else "可派遣",
                "licenseExpire": "2026-12-31", "workHours": 1 if row.hasActiveTask else 0,
            })
        conn.close()
        return jsonify(data)
    except Exception as e:
        print(f"❌ 撈取司機清單失敗: {str(e)}")
        return jsonify([]), 500


# =========================================================================
# 車輛 API (🌟 對齊 dbo.Vehicle 單數表與 PlateNo 欄位名)
# =========================================================================
@app.route("/api/vehicles", methods=["GET"])
def get_vehicles():
    try:
        conn = get_connection()
        cursor = conn.cursor()

        # 智慧安全防禦：檢查有沒有 GPSLogs 表，沒有的話用模擬軌跡
        has_gps = True
        try:
            cursor.execute("SELECT TOP 1 VehicleID FROM dbo.GPSLogs")
        except:
            has_gps = False

        if has_gps:
            gps_sql = """
                WITH LatestGps AS (
                    SELECT VehicleID, Latitude, Longitude, Speed,
                        ROW_NUMBER() OVER (PARTITION BY VehicleID ORDER BY Timestamp DESC) AS rn
                    FROM dbo.GPSLogs
                )
                SELECT v.VehicleID AS id, v.PlateNo AS plate, v.VehicleType AS type, v.SeatCount AS seatCapacity, v.Status AS dbStatus,
                       g.Latitude AS latitude, g.Longitude AS longitude, g.Speed AS gpsSpeed
                FROM dbo.Vehicle v LEFT JOIN LatestGps g ON v.VehicleID = g.VehicleID AND g.rn = 1
            """
        else:
            gps_sql = """
                SELECT v.VehicleID AS id, v.PlateNo AS plate, v.VehicleType AS type, v.SeatCount AS seatCapacity, v.Status AS dbStatus,
                       NULL AS latitude, NULL AS longitude, 0 AS gpsSpeed
                FROM dbo.Vehicle v
            """

        cursor.execute(gps_sql)
        rows = cursor.fetchall()
        data = []

        for index, row in enumerate(rows):
            has_task = False
            # 判斷有無任務
            cursor.execute("SELECT COUNT(*) FROM dbo.DispatchTasks WHERE VehicleID = ? AND ActualArrival IS NULL", row.id)
            if cursor.fetchone()[0] > 0:
                has_task = True

            status = vehicle_status_to_text(row.dbStatus, has_task)
            data.append({
                "id": str(row.id), "plate": row.plate or "", "type": row.type or "", "status": status,
                "x": 20 + ((index * 27) % 65), "y": 25 + ((index * 19) % 55),
                "latitude": float(row.latitude) if row.latitude is not None else None,
                "longitude": float(row.longitude) if row.longitude is not None else None,
                "speed": int(row.gpsSpeed or (30 if status == "出勤中" else 0)),
                "route": "出勤任務中" if has_task else "待命中",
                "wheelchairCapacity": 1 if "復康" in (row.type or "") else 0,
                "seatCapacity": row.seatCapacity or 4, "mileage": 0, "maintainMileage": 5000,
                "inspectionExpire": "2026-12-31", "warning": "",
            })

        conn.close()
        return jsonify(data)
    except Exception as e:
        print(f"❌ 撈取車輛動態失敗: {str(e)}")
        return jsonify([]), 500


# =========================
# 司機 APP 訊息 API
# =========================
@app.route("/api/messages", methods=["GET"])
def get_messages():
    try:
        conn = get_connection()
        cursor = conn.cursor()
        cursor.execute("SELECT MessageID, Content, SendTime FROM dbo.Messages ORDER BY SendTime DESC")
        rows = cursor.fetchall()
        data = [{
            "id": str(row[0]), "driverName": "系統調度中心", "content": row[1] or "",
            "status": "已送出", "time": row[2].strftime("%Y-%m-%d %H:%M:%S") if row[2] else ""
        } for row in rows]
        conn.close()
        return jsonify(data)
    except:
        return jsonify([]), 500


@app.route("/api/messages", methods=["POST"])
def send_message():
    return jsonify({"success": True, "message": "訊息模擬發送成功"})

@app.route("/api/seed", methods=["POST"])
def seed_data():
    return jsonify({"success": True, "message": "共用公網資料庫，跳過本機初始化"})

if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000, debug=True)
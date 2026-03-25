# 🏢 SYSPRO Consulting Company Guide

> **As a Consulting Company: Complete Clarity on SYSPRO Integration Business**

---

## 📋 Quick Answers to Your Questions

| Question | Quick Answer |
|----------|--------------|
| Client se kya lena hai? | SYSPRO Server URL, Operator credentials, Company ID, License count |
| Multiple businesses onboard? | Each client = separate SYSPRO installation OR separate Company ID |
| Database kaun sa? | SYSPRO ka SQL Server (client side) + Your own DB (your side) |
| APIs se credentials nikal sakte? | ❌ NO - Client manually dega, API se nahi milte |
| SYSPRO internally kaise kaam? | e.net → Business Logic Engine → SQL Server |
| Local DB handling? | Your app ka alag DB (orders cache, users, logs) |

---

## 1️⃣ Client Se Kya Lena Hai? (Pre-Requisites Checklist)

### 🔴 MANDATORY - Bina iske kaam nahi chalega

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CLIENT SE ZAROOR LENA HAI                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. SYSPRO SERVER ACCESS                                                │
│     ├── Server URL/IP: e.g., http://192.168.1.50:30000                 │
│     ├── Port: Usually 30000 (WCF) or 80/443 (REST)                     │
│     └── VPN Access: If server is on-premise                            │
│                                                                          │
│  2. OPERATOR CREDENTIALS (for API access)                               │
│     ├── Operator Username: e.g., "API_USER"                            │
│     ├── Operator Password: Client IT team se milega                    │
│     └── NOTE: Ye SYSPRO Operator hai, Windows user nahi!               │
│                                                                          │
│  3. COMPANY DETAILS                                                      │
│     ├── Company ID: Usually "A", "B", "T" etc.                         │
│     ├── Company Password: Agar set hai (usually blank)                 │
│     └── Multiple Companies?: If yes, list all Company IDs              │
│                                                                          │
│  4. LICENSE INFORMATION                                                  │
│     ├── Total Licenses: e.g., 20 concurrent users                      │
│     ├── Integration Licenses: How many for API? (e.g., 5)              │
│     └── License Type: Named user vs Concurrent                         │
│                                                                          │
│  5. SYSPRO VERSION                                                       │
│     ├── SYSPRO 7: Only e.net (WCF/COM)                                 │
│     ├── SYSPRO 8: e.net + limited REST                                 │
│     └── SYSPRO 8+: Full REST available                                  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 🟡 RECOMMENDED - Better integration ke liye

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    OPTIONAL BUT HELPFUL                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  6. SQL SERVER READ ACCESS (for fast queries)                           │
│     ├── SQL Server: server name/IP                                      │
│     ├── Database Name: e.g., SYSPRO_CompanyA                           │
│     ├── Read-only User: e.g., "reporting_user"                         │
│     └── WHY: Direct SQL read is 10x faster than e.net query            │
│                                                                          │
│  7. BUSINESS OBJECT LIST                                                 │
│     ├── Which modules client uses?                                      │
│     │   □ Inventory (INV)                                               │
│     │   □ Sales Orders (SOR)                                            │
│     │   □ Purchase Orders (POR)                                         │
│     │   □ Accounts Receivable (AR)                                      │
│     │   □ Work in Progress (WIP)                                        │
│     └── Custom Business Objects?: Any customizations?                   │
│                                                                          │
│  8. TEST ENVIRONMENT                                                     │
│     ├── Test Company ID: Usually "T"                                    │
│     ├── Test Server URL: Same or different server                       │
│     └── Test Data: Sample customers, orders, inventory                  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 📝 Client Onboarding Form Template

```markdown
# SYSPRO Integration - Client Information Form

## Company Details
- Company Name: _______________________
- Contact Person: _____________________
- Email: _____________________________
- Phone: _____________________________

## SYSPRO Environment
- SYSPRO Version: [ ] 7  [ ] 8  [ ] 8+
- e.net URL: _________________________
- Company ID(s): _____________________
- Company Password: __________________

## Credentials (IT Team will provide)
- Operator Username: _________________
- Operator Password: _________________ (share securely)

## Licensing
- Total Licenses: ____________________
- Available for Integration: __________
- License Type: [ ] Named  [ ] Concurrent

## SQL Access (Optional but recommended)
- SQL Server: ________________________
- Database Name: _____________________
- Read-only Username: ________________
- Read-only Password: ________________

## Network
- [ ] Server accessible from internet
- [ ] VPN required (provide VPN credentials)
- [ ] Firewall rules needed (port: _____)

## Modules in Use
[ ] Inventory  [ ] Sales Orders  [ ] Purchase Orders
[ ] Accounts Receivable  [ ] Manufacturing/WIP
[ ] Other: ___________________________
```

---

## 2️⃣ Multiple Businesses Onboard Kaise Karein?

### Scenario A: Multiple Clients (Different Companies)

```
┌─────────────────────────────────────────────────────────────────────────┐
│              MULTIPLE CLIENTS - SEPARATE SYSPRO INSTALLATIONS           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Your SaaS Platform                                                      │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                                                                   │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │  │
│  │  │ Client A    │  │ Client B    │  │ Client C    │              │  │
│  │  │ Config      │  │ Config      │  │ Config      │              │  │
│  │  │             │  │             │  │             │              │  │
│  │  │ URL: xxx    │  │ URL: yyy    │  │ URL: zzz    │              │  │
│  │  │ Creds: aaa  │  │ Creds: bbb  │  │ Creds: ccc  │              │  │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘              │  │
│  │         │                │                │                       │  │
│  └─────────┼────────────────┼────────────────┼───────────────────────┘  │
│            │                │                │                          │
│            ▼                ▼                ▼                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                     │
│  │ Client A    │  │ Client B    │  │ Client C    │                     │
│  │ SYSPRO      │  │ SYSPRO      │  │ SYSPRO      │                     │
│  │ Server      │  │ Server      │  │ Server      │                     │
│  │             │  │             │  │             │                     │
│  │ (Their      │  │ (Their      │  │ (Their      │                     │
│  │  premise)   │  │  premise)   │  │  premise)   │                     │
│  └─────────────┘  └─────────────┘  └─────────────┘                     │
│                                                                          │
│  HOW TO IMPLEMENT:                                                       │
│  1. Store each client's config in YOUR database                         │
│  2. Tenant ID → maps to SYSPRO config                                   │
│  3. Each request includes tenant ID                                      │
│  4. Your code picks correct SYSPRO connection                           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Scenario B: One Client, Multiple Companies (Divisions)

```
┌─────────────────────────────────────────────────────────────────────────┐
│           ONE CLIENT - MULTIPLE SYSPRO COMPANIES                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Single Client's SYSPRO Installation                                     │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                                                                   │  │
│  │  Same SYSPRO Server (http://client.syspro:30000)                 │  │
│  │                                                                   │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │  │
│  │  │ Company A   │  │ Company B   │  │ Company T   │              │  │
│  │  │ (Main)      │  │ (Division)  │  │ (Test)      │              │  │
│  │  │             │  │             │  │             │              │  │
│  │  │ DB: SYSPRO_ │  │ DB: SYSPRO_ │  │ DB: SYSPRO_ │              │  │
│  │  │ CompanyA    │  │ CompanyB    │  │ CompanyT    │              │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘              │  │
│  │                                                                   │  │
│  │  Same credentials (ADMIN/password) work for all companies        │  │
│  │  BUT: Different CompanyId in login XML                           │  │
│  │                                                                   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  HOW TO IMPLEMENT:                                                       │
│  1. Same SYSPRO URL, same credentials                                   │
│  2. Different Company ID in Logon XML                                   │
│  3. Separate session pool per company                                   │
│  4. User selects company at login                                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Your Multi-Tenant Database Schema

```sql
-- YOUR database (not SYSPRO's database)

-- Store client/tenant configurations
CREATE TABLE Tenants (
    TenantId        UNIQUEIDENTIFIER PRIMARY KEY,
    TenantName      NVARCHAR(100) NOT NULL,
    IsActive        BIT DEFAULT 1,
    CreatedAt       DATETIME2 DEFAULT GETUTCDATE()
);

-- SYSPRO connection configs per tenant
CREATE TABLE SysproConnections (
    ConnectionId    UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER REFERENCES Tenants(TenantId),
    
    -- SYSPRO e.net details
    BaseUrl         NVARCHAR(200) NOT NULL,  -- e.g., http://client:30000
    CompanyId       CHAR(1) NOT NULL,         -- e.g., 'A'
    Operator        NVARCHAR(50) NOT NULL,    -- API user
    PasswordHash    NVARCHAR(256) NOT NULL,   -- Encrypted!
    CompanyPassword NVARCHAR(256) NULL,
    
    -- Pool settings
    PoolSize        INT DEFAULT 5,
    TimeoutSeconds  INT DEFAULT 120,
    
    -- Optional SQL access
    SqlServer       NVARCHAR(200) NULL,
    SqlDatabase     NVARCHAR(100) NULL,
    SqlUser         NVARCHAR(50) NULL,
    SqlPasswordHash NVARCHAR(256) NULL,
    
    -- Metadata
    SysproVersion   NVARCHAR(20) NULL,        -- '7', '8', '8+'
    IsActive        BIT DEFAULT 1,
    LastTestedAt    DATETIME2 NULL,
    
    UNIQUE(TenantId, CompanyId)
);

-- Your app's users (linked to tenants)
CREATE TABLE Users (
    UserId          UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER REFERENCES Tenants(TenantId),
    Email           NVARCHAR(200) NOT NULL,
    PasswordHash    NVARCHAR(256) NOT NULL,
    AllowedCompanies NVARCHAR(10) NULL,       -- e.g., 'A,B' or NULL for all
    IsActive        BIT DEFAULT 1
);

-- Cached/synced data from SYSPRO (optional, for performance)
CREATE TABLE CachedCustomers (
    TenantId        UNIQUEIDENTIFIER,
    CompanyId       CHAR(1),
    CustomerCode    NVARCHAR(20),
    CustomerName    NVARCHAR(100),
    LastSyncedAt    DATETIME2,
    PRIMARY KEY (TenantId, CompanyId, CustomerCode)
);

-- Your app's orders (before syncing to SYSPRO)
CREATE TABLE LocalOrders (
    OrderId         UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER,
    CompanyId       CHAR(1),
    CustomerCode    NVARCHAR(20),
    OrderData       NVARCHAR(MAX),            -- JSON
    Status          NVARCHAR(20),             -- 'pending', 'synced', 'failed'
    SysproOrderNum  NVARCHAR(20) NULL,        -- Filled after sync
    CreatedAt       DATETIME2,
    SyncedAt        DATETIME2 NULL,
    ErrorMessage    NVARCHAR(MAX) NULL
);
```

---

## 3️⃣ Database Handling - Complete Picture

### 🔴 SYSPRO ka Database (Client Side - READ ONLY)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SYSPRO DATABASE (SQL SERVER)                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Location: Client ke server par                                          │
│  Engine: Microsoft SQL Server (2016+)                                    │
│  Access: Read-only (via SQL) + Read/Write (via e.net API)               │
│                                                                          │
│  ⚠️  RULES:                                                              │
│  ✅ READ via SQL:  Fast, direct, allowed                                 │
│  ✅ WRITE via e.net: Required for transactions                          │
│  ❌ WRITE via SQL:  NEVER! Breaks SYSPRO!                               │
│                                                                          │
│  Common Tables (for reference):                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  InvMaster    - Inventory master data                           │   │
│  │  InvWarehouse - Inventory per warehouse                         │   │
│  │  ArCustomer   - Customer master                                 │   │
│  │  SorMaster    - Sales Order header                              │   │
│  │  SorDetail    - Sales Order lines                               │   │
│  │  PorMaster    - Purchase Order header                           │   │
│  │  ApSupplier   - Supplier master                                 │   │
│  │  WipMaster    - Work Order header                               │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 🟢 Your Database (Your Side - FULL CONTROL)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    YOUR APPLICATION DATABASE                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Location: Your server (cloud/on-premise)                               │
│  Engine: Any (SQL Server, PostgreSQL, MySQL, MongoDB)                   │
│  Access: Full read/write                                                 │
│                                                                          │
│  WHAT TO STORE:                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                                                                  │   │
│  │  1. TENANT/CLIENT CONFIGURATIONS                                │   │
│  │     - SYSPRO connection details per client                      │   │
│  │     - Credentials (encrypted)                                   │   │
│  │     - Company mappings                                          │   │
│  │                                                                  │   │
│  │  2. USER MANAGEMENT                                             │   │
│  │     - Your app's users (not SYSPRO operators)                  │   │
│  │     - Permissions, roles                                        │   │
│  │     - JWT refresh tokens                                        │   │
│  │                                                                  │   │
│  │  3. CACHED/SYNCED DATA                                          │   │
│  │     - Customers (for quick search)                              │   │
│  │     - Inventory (for quick lookup)                              │   │
│  │     - Sync periodically (every 15 min / hourly)                │   │
│  │                                                                  │   │
│  │  4. PENDING TRANSACTIONS                                        │   │
│  │     - Orders created offline                                    │   │
│  │     - Queue for SYSPRO sync                                     │   │
│  │     - Retry failed transactions                                 │   │
│  │                                                                  │   │
│  │  5. AUDIT LOGS                                                  │   │
│  │     - Who did what, when                                        │   │
│  │     - API call logs                                             │   │
│  │     - Error logs                                                │   │
│  │                                                                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Complete Data Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         DATA FLOW ARCHITECTURE                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  MOBILE/WEB APP                                                          │
│       │                                                                  │
│       ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    YOUR API SERVER                               │   │
│  │                                                                  │   │
│  │  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │   │
│  │  │ Auth        │    │ Business    │    │ Sync        │         │   │
│  │  │ Service     │    │ Logic       │    │ Service     │         │   │
│  │  └──────┬──────┘    └──────┬──────┘    └──────┬──────┘         │   │
│  │         │                  │                  │                 │   │
│  │         ▼                  ▼                  ▼                 │   │
│  │  ┌───────────────────────────────────────────────────────────┐ │   │
│  │  │                YOUR DATABASE                               │ │   │
│  │  │  • Users & Auth    • Cached Data    • Pending Orders      │ │   │
│  │  │  • Tenant Configs  • Sync Status    • Audit Logs          │ │   │
│  │  └───────────────────────────────────────────────────────────┘ │   │
│  │                              │                                  │   │
│  │                              │ (When SYSPRO operation needed)   │   │
│  │                              ▼                                  │   │
│  │  ┌───────────────────────────────────────────────────────────┐ │   │
│  │  │              SYSPRO SESSION POOL                          │ │   │
│  │  │  • Maintains e.net connections                            │ │   │
│  │  │  • One pool per tenant/company                            │ │   │
│  │  └───────────────────────────────────────────────────────────┘ │   │
│  │                              │                                  │   │
│  └──────────────────────────────┼──────────────────────────────────┘   │
│                                 │                                       │
│                    VPN / Secure Connection                              │
│                                 │                                       │
│                                 ▼                                       │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │                    CLIENT'S SYSPRO                                 │ │
│  │                                                                    │ │
│  │  ┌─────────────┐              ┌─────────────┐                    │ │
│  │  │ e.net       │              │ SQL Server  │                    │ │
│  │  │ Service     │ ────────────►│ (SYSPRO DB) │                    │ │
│  │  │ (Port 30000)│              │             │                    │ │
│  │  └─────────────┘              └─────────────┘                    │ │
│  │                                                                    │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 4️⃣ Kya APIs se Credentials Nikal Sakte Hain?

### ❌ Short Answer: NO

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CREDENTIALS - HOW TO GET                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ❌ CANNOT get from API:                                                 │
│     • Operator passwords                                                 │
│     • Company passwords                                                  │
│     • SQL connection strings                                             │
│     • License keys                                                       │
│                                                                          │
│  ✅ MUST get from client manually:                                       │
│     • Client's IT admin provides credentials                            │
│     • Usually via secure channel (encrypted email, password manager)    │
│     • Stored encrypted in YOUR database                                 │
│                                                                          │
│  ✅ CAN verify via API:                                                  │
│     • Test login with provided credentials                              │
│     • If login succeeds → credentials are valid                         │
│     • If login fails → ask client to re-check                           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### What CAN you get via API (after login)?

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    WHAT YOU CAN QUERY VIA e.net                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  AFTER SUCCESSFUL LOGIN, you can query:                                  │
│                                                                          │
│  ✅ System Information:                                                  │
│     • SYSPRO version                                                     │
│     • Company name, address                                              │
│     • Available modules                                                  │
│     • Operator permissions (what this user can do)                      │
│                                                                          │
│  ✅ Business Data:                                                       │
│     • Customers, Suppliers                                               │
│     • Inventory items                                                    │
│     • Orders, Invoices                                                   │
│     • All master and transaction data                                   │
│                                                                          │
│  ❌ CANNOT query:                                                        │
│     • Other operators' passwords                                         │
│     • License details                                                    │
│     • System configuration                                               │
│     • Database connection strings                                        │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 5️⃣ SYSPRO Internally Kaise Kaam Karta Hai?

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SYSPRO INTERNAL ARCHITECTURE                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  YOUR API CALL                                                           │
│       │                                                                  │
│       │  POST /saborw/Transaction?BusinessObject=SORTOI                 │
│       │  Body: <OrderToBuild>...</OrderToBuild>                         │
│       │                                                                  │
│       ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  LAYER 1: e.net WCF Service (Port 30000)                        │   │
│  │                                                                  │   │
│  │  • Receives HTTP/XML request                                    │   │
│  │  • Validates SessionId                                          │   │
│  │  • Routes to correct Business Object                            │   │
│  └──────────────────────────┬──────────────────────────────────────┘   │
│                              │                                          │
│                              ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  LAYER 2: Business Object (e.g., SORTOI)                        │   │
│  │                                                                  │   │
│  │  • Parses XML input                                             │   │
│  │  • Validates fields (required, format, ranges)                  │   │
│  │  • Checks business rules:                                       │   │
│  │    - Customer exists? ✓                                         │   │
│  │    - Customer on hold? ✗                                        │   │
│  │    - Credit limit OK? ✓                                         │   │
│  │    - Stock available? ✓                                         │   │
│  │    - Prices valid? ✓                                            │   │
│  │                                                                  │   │
│  └──────────────────────────┬──────────────────────────────────────┘   │
│                              │                                          │
│                              ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  LAYER 3: Business Logic Engine                                 │   │
│  │                                                                  │   │
│  │  • Calculates derived values:                                   │   │
│  │    - Line totals                                                │   │
│  │    - Tax amounts                                                │   │
│  │    - Discounts                                                  │   │
│  │    - Order total                                                │   │
│  │                                                                  │   │
│  │  • Triggers side effects:                                       │   │
│  │    - Allocates inventory                                        │   │
│  │    - Updates customer balance                                   │   │
│  │    - Creates audit trail                                        │   │
│  │    - Sends notifications (if configured)                        │   │
│  │                                                                  │   │
│  └──────────────────────────┬──────────────────────────────────────┘   │
│                              │                                          │
│                              ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  LAYER 4: Database Operations                                   │   │
│  │                                                                  │   │
│  │  • Wraps in transaction (BEGIN TRAN)                            │   │
│  │  • Inserts/Updates tables:                                      │   │
│  │    - SorMaster (order header)                                   │   │
│  │    - SorDetail (order lines)                                    │   │
│  │    - InvWarehouse (allocate stock)                              │   │
│  │    - ArCustomer (update balance)                                │   │
│  │  • Commits transaction (COMMIT)                                 │   │
│  │                                                                  │   │
│  └──────────────────────────┬──────────────────────────────────────┘   │
│                              │                                          │
│                              ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  LAYER 5: Response                                              │   │
│  │                                                                  │   │
│  │  • Generates result XML:                                        │   │
│  │    <SalesOrder>SO001234</SalesOrder>                            │   │
│  │  • Returns to your API                                          │   │
│  │                                                                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  WHY THIS MATTERS:                                                       │
│  • All business logic is in SYSPRO, not database                        │
│  • Direct SQL write would SKIP validation = corrupt data                │
│  • Always use e.net for writes, SQL only for reads                      │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 6️⃣ Complete Consulting Workflow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CONSULTING WORKFLOW                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  PHASE 1: CLIENT ONBOARDING (Week 1)                                    │
│  ─────────────────────────────────────                                   │
│  □ Send onboarding form to client                                       │
│  □ Receive SYSPRO server details                                        │
│  □ Receive operator credentials (securely)                              │
│  □ Get VPN access (if needed)                                           │
│  □ Verify connection with test login                                    │
│  □ Store config in your database (encrypted)                            │
│                                                                          │
│  PHASE 2: DISCOVERY (Week 1-2)                                          │
│  ─────────────────────────────────                                       │
│  □ Login to client's SYSPRO (via e.net)                                │
│  □ Query system info to get version                                     │
│  □ List available modules                                               │
│  □ Query sample data (customers, inventory)                             │
│  □ Identify required Business Objects                                   │
│  □ Document any customizations                                          │
│                                                                          │
│  PHASE 3: DEVELOPMENT (Week 2-4)                                        │
│  ────────────────────────────────                                        │
│  □ Setup session pool for client                                        │
│  □ Implement required endpoints                                          │
│  □ Add error handling                                                    │
│  □ Test with client's test company (usually "T")                        │
│                                                                          │
│  PHASE 4: UAT (Week 4-5)                                                │
│  ──────────────────────                                                  │
│  □ Client tests in test environment                                     │
│  □ Fix issues                                                            │
│  □ Get sign-off                                                          │
│                                                                          │
│  PHASE 5: GO-LIVE (Week 5-6)                                            │
│  ───────────────────────────                                             │
│  □ Switch to production company (usually "A")                           │
│  □ Monitor for issues                                                    │
│  □ Handover documentation                                                │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🎯 Summary - Consulting Checklist

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CONSULTING MUST-HAVES                                 │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  FROM CLIENT:                                                            │
│  ✓ SYSPRO Server URL + Port                                             │
│  ✓ Operator Username + Password                                         │
│  ✓ Company ID(s)                                                         │
│  ✓ License count for integration                                        │
│  ✓ VPN/Network access                                                   │
│  ✓ (Optional) SQL read-only access                                      │
│                                                                          │
│  YOUR INFRASTRUCTURE:                                                    │
│  ✓ Your database for configs, users, cache                              │
│  ✓ API server with session pool                                         │
│  ✓ Secure credential storage                                            │
│  ✓ Multi-tenant architecture                                            │
│                                                                          │
│  FOR EACH CLIENT/COMPANY:                                                │
│  ✓ Separate session pool                                                 │
│  ✓ Separate config entry                                                │
│  ✓ Test environment first                                               │
│  ✓ Then production                                                       │
│                                                                          │
│  REMEMBER:                                                               │
│  ✗ Cannot get credentials via API                                       │
│  ✗ Cannot write directly to SQL                                         │
│  ✓ All writes via e.net API                                             │
│  ✓ All reads via SQL (fast) or e.net (consistent)                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🔗 Related Documentation

- [03-ENET-SOLUTIONS.md](./SYSPRO-INTEGRATION-GUIDE/docs/03-ENET-SOLUTIONS.md) - e.net API details
- [13-MULTI-COMPANY-SETUP.md](./SYSPRO-INTEGRATION-GUIDE/docs/13-MULTI-COMPANY-SETUP.md) - Multi-company handling
- [11-FAQ.md](./SYSPRO-INTEGRATION-GUIDE/docs/11-FAQ.md) - Common questions
- [code-samples/complete-project/](./SYSPRO-INTEGRATION-GUIDE/code-samples/complete-project/) - Full working code

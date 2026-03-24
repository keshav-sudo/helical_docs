# Part 1: SYSPRO System Understanding

[← Back to Main Guide](../README.md) | [Next: Integration Architecture →](./02-INTEGRATION-ARCHITECTURE.md)

---

## 1.0 The Foundational Question: "SYSPRO Ki Web UI Hai, To Integration Kyu?"

This is the **first question** every new developer asks. SYSPRO already has a full web interface called **SYSPRO Avanti** (earlier called SYSPRO Web UI). Users can create sales orders, manage inventory, run reports — everything — from a browser. So why would any company spend months building a custom integration?

### What is SYSPRO Avanti?

SYSPRO Avanti is SYSPRO's official browser-based interface launched in SYSPRO 8. It mirrors the desktop client functionality and runs on any modern browser.

```
┌─────────────────────────────────────────────────────────────────┐
│                     SYSPRO AVANTI (WEB UI)                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  What Avanti CAN do:                    What Avanti CANNOT do:  │
│  ─────────────────────                  ──────────────────────── │
│  ✅ Create Sales Orders                 ❌ Accept orders from    │
│  ✅ Manage Inventory                       your eCommerce site   │
│  ✅ Create Customers                    ❌ Auto-sync with        │
│  ✅ Run Financial Reports                  Shopify/WooCommerce   │
│  ✅ Post Invoices                       ❌ Show real-time stock  │
│  ✅ View Dashboards                        on YOUR website       │
│  ✅ Manage Manufacturing Jobs           ❌ Send automated        │
│  ✅ Purchase Order Processing              notifications         │
│  ✅ User-level access control           ❌ Integrate with your   │
│  ✅ Workflow approvals                     CRM (Salesforce, etc.)│
│                                         ❌ Provide a customer    │
│  WHO uses it:                              self-service portal   │
│  • Internal warehouse staff             ❌ Handle EDI documents  │
│  • Finance team                            (850, 855, 810)       │
│  • Internal sales reps                  ❌ Connect IoT devices   │
│  • Managers running reports                for production data   │
│                                         ❌ Run custom business   │
│                                            logic/rules           │
│                                         ❌ Support mobile-native │
│                                            offline-first apps    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### The 7 Real Reasons Companies Build Custom Integrations

**Reason 1: eCommerce Sync (Most Common)**

Your company has a Shopify, WooCommerce, or Magento store. Customers place orders 24/7. Someone has to manually re-enter those orders into SYSPRO. With 50+ orders/day, that's a full-time data entry job.

```
WITHOUT Integration:                    WITH Integration:
───────────────────                     ──────────────────
Customer orders on Shopify              Customer orders on Shopify
       │                                       │
       ▼                                       ▼
Email notification to staff             Auto-sync to SYSPRO via API
       │                                       │
       ▼                                       ▼
Staff manually logs into SYSPRO         Sales Order created automatically
       │                                       │
       ▼                                       ▼
Staff re-types entire order             Inventory allocated instantly
(20 minutes per order)                  (2 seconds per order)
       │                                       │
       ▼                                       ▼
Typos, wrong prices, delays             Zero errors, real-time
```

**Reason 2: Customer Self-Service Portal**

Your B2B customers want to:
- Check their order status without calling your sales team
- See real-time inventory availability before placing orders
- Download their invoices and statements
- Track shipments

Avanti requires a SYSPRO license per user. You can't give 500 customers SYSPRO logins. A custom portal reads data from SYSPRO and presents it without consuming licenses.

**Reason 3: Multi-System Landscape**

Real enterprises don't run on SYSPRO alone. They also have:

```
┌───────────┐    ┌───────────┐    ┌───────────┐    ┌───────────┐
│ Salesforce │    │ Shopify   │    │ Power BI  │    │ Custom    │
│ (CRM)     │    │ (eComm)   │    │ (Reports) │    │ Mobile App│
└─────┬─────┘    └─────┬─────┘    └─────┬─────┘    └─────┬─────┘
      │                │                │                │
      └────────────────┴────────┬───────┴────────────────┘
                                │
                    ┌───────────┴───────────┐
                    │   YOUR MIDDLEWARE      │
                    │   (.NET Integration)   │
                    └───────────┬───────────┘
                                │
                    ┌───────────┴───────────┐
                    │       SYSPRO ERP       │
                    └───────────────────────┘
```

SYSPRO doesn't natively talk to Salesforce. Your middleware is the translator.

**Reason 4: Mobile/Field Applications**

Delivery drivers need to confirm deliveries on a phone. Warehouse staff need to scan barcodes with a tablet. These mobile apps need to read/write SYSPRO data through a lightweight REST API, not through Avanti's heavy web interface.

**Reason 5: Automation & Workflows**

- Auto-create purchase orders when inventory falls below reorder point
- Auto-send email when invoice is overdue by 30 days
- Auto-generate manufacturing jobs when sales order exceeds stock
- Nightly reconciliation between systems

SYSPRO has basic workflows, but complex multi-system automation requires custom code.

**Reason 6: Real-Time Dashboards**

Management wants a single screen showing:
- Today's orders (from all channels)
- Current inventory levels (with warnings)
- Revenue this month vs target
- SYSPRO sync health (all orders processed?)

Avanti's dashboards are limited to SYSPRO data. Custom dashboards aggregate data from multiple sources.

**Reason 7: Business Rule Layer**

Your company has rules SYSPRO doesn't enforce:
- "Customer X always gets 12% discount on product category Y"
- "Orders above $50K need VP approval before going to SYSPRO"
- "If warehouse A is out of stock, auto-check warehouse B and C"
- "Bundle products: when customer orders KIT-100, create SO lines for 5 components"

These rules live in your middleware, executed before/after SYSPRO transactions.

### The Analogy

```
SYSPRO = The Engine Room of a ship
  • Powerful — runs everything
  • Complex — not for passengers
  • Core — you can't operate without it

YOUR INTEGRATION = The Bridge (Captain's Deck)
  • User-friendly — designed for your crew
  • Selective — shows only what's needed
  • Connected — talks to engine room via intercom (e.net)
  • Custom — built for YOUR ship's specific mission
```

### When to Use Avanti vs Custom Integration

| Scenario | Use Avanti | Use Custom Integration |
|----------|-----------|----------------------|
| Internal warehouse staff entering receipts | ✅ | ❌ |
| Finance team posting journals | ✅ | ❌ |
| eCommerce orders flowing in automatically | ❌ | ✅ |
| Customer checking their order status online | ❌ | ✅ |
| Sales rep creating a quote on a tablet | ❌ | ✅ |
| Running standard SYSPRO financial reports | ✅ | ❌ |
| Custom dashboard for C-level executives | ❌ | ✅ |
| Automated inventory replenishment alerts | ❌ | ✅ |
| Ad-hoc manual purchase order | ✅ | ❌ |
| EDI document processing (850/855) | ❌ | ✅ |
| Multi-system data synchronization | ❌ | ✅ |
| Internal SYSPRO power users | ✅ | ❌ |

---

## 1.1 SYSPRO Architecture — What's Actually Running

### SYSPRO Version History (Why It Matters For Integration)

| Version | Era | Key Integration Impact |
|---------|-----|----------------------|
| SYSPRO 6.1 | 2008–2014 | COM+ only, VBScript customization, no web API |
| SYSPRO 7 | 2014–2018 | e.net Solutions (COM + WCF), first programmatic API |
| SYSPRO 8 | 2018–2023 | Avanti web UI, RESTful Services added, e.net enhanced |
| SYSPRO 8 (2023+) | Current | SYSPRO Harmony (iPaaS), cloud deployment options |

**Why this matters:** If your customer runs SYSPRO 7, you ONLY have e.net (COM/WCF). If they're on SYSPRO 8+, you have e.net AND REST API options. Your integration code must handle both.

### Runtime Architecture — Every Component Explained

```
┌──────────────────────────────────────────────────────────────────────┐
│                       SYSPRO RUNTIME STACK (DEEP VIEW)                │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌─────────────────────┐      ┌───────────────────────────────────┐  │
│  │  SYSPRO Client       │      │  SYSPRO Application Server        │  │
│  │  (Windows Desktop)   │─────►│  (Windows Service — SYSPROSvc)   │  │
│  │                      │ TCP  │                                   │  │
│  │  • Win32 application │      │  COMPONENTS:                      │  │
│  │  • Installed per user│      │  ┌─────────────────────────────┐  │  │
│  │  • Connects to app   │      │  │ 1. Business Logic Engine    │  │  │
│  │    server on port    │      │  │    • Validates all data      │  │  │
│  │    30661 (default)   │      │  │    • Enforces business rules │  │  │
│  │  • NOT a browser app │      │  │    • Calculates pricing,     │  │  │
│  │  • Handles UI only   │      │  │      tax, discounts          │  │  │
│  └─────────────────────┘      │  │    • Manages workflows       │  │  │
│                                │  └─────────────────────────────┘  │  │
│  ┌─────────────────────┐      │  ┌─────────────────────────────┐  │  │
│  │  SYSPRO Avanti       │      │  │ 2. Transaction Manager      │  │  │
│  │  (Web UI)            │─────►│  │    • ACID transactions      │  │  │
│  │                      │ HTTP │  │    • Rollback on failure     │  │  │
│  │  • HTML5/JS app      │      │  │    • Locking (pessimistic)   │  │  │
│  │  • Hosted on IIS or  │      │  │    • Audit trail generation  │  │  │
│  │    SYSPRO web server │      │  └─────────────────────────────┘  │  │
│  │  • Requires SYSPRO   │      │  ┌─────────────────────────────┐  │  │
│  │    license per user  │      │  │ 3. e.net Solutions Host     │  │  │
│  │  • Mirrors desktop   │      │  │    • COM+ objects            │  │  │
│  │    functionality     │      │  │    • WCF service endpoints   │  │  │
│  └─────────────────────┘      │  │    • Port 30661 (COM/WCF)   │  │  │
│                                │  │    • Processes XML in/out    │  │  │
│  ┌─────────────────────┐      │  │    • YOUR integration uses   │  │  │
│  │  YOUR .NET API       │      │  │      THIS component          │  │  │
│  │  (Custom Integration)│─────►│  └─────────────────────────────┘  │  │
│  │                      │ XML  │  ┌─────────────────────────────┐  │  │
│  │  • Your middleware   │ over │  │ 4. Reporting Engine          │  │  │
│  │  • Calls e.net       │ WCF  │  │    • Crystal Reports        │  │  │
│  │  • Sends XML, gets   │      │  │    • SSRS integration        │  │  │
│  │    XML back          │      │  │    • Document printing       │  │  │
│  └─────────────────────┘      │  └─────────────────────────────┘  │  │
│                                │  ┌─────────────────────────────┐  │  │
│                                │  │ 5. Workflow Services         │  │  │
│                                │  │    • Email triggers          │  │  │
│                                │  │    • Approval chains         │  │  │
│                                │  │    • Event notifications     │  │  │
│                                │  └─────────────────────────────┘  │  │
│                                └──────────────┬────────────────────┘  │
│                                               │                       │
│                                               ▼                       │
│                                ┌───────────────────────────────────┐  │
│                                │  SQL Server 2016+ (Database)      │  │
│                                │                                   │  │
│                                │  Instance: YOURSERVER\SYSPRO      │  │
│                                │                                   │  │
│                                │  Databases:                       │  │
│                                │  ├── SysproCompanyA               │  │
│                                │  │   ├── 800+ tables              │  │
│                                │  │   ├── Inventory, Sales, AR/AP  │  │
│                                │  │   └── All transactional data   │  │
│                                │  │                                │  │
│                                │  ├── SysproCompanyB               │  │
│                                │  │   └── (same schema, diff data) │  │
│                                │  │                                │  │
│                                │  └── SysproSystem                 │  │
│                                │      ├── Operators (users)        │  │
│                                │      ├── Company definitions      │  │
│                                │      ├── License information      │  │
│                                │      ├── Global settings          │  │
│                                │      └── Security policies        │  │
│                                └───────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

### Key Points Your Manager Won't Tell You

| # | Point | Why It Matters |
|---|-------|---------------|
| 1 | **One SQL Database per company** — Multi-company setups have separate DBs (e.g., `SysproCompanyA`, `SysproCompanyB`) | Your connection string changes per company. If a client has 5 companies, you need 5 DB connections. |
| 2 | **System database is shared** — `SysproSystem` holds users, operators, company configs | User authentication happens against this DB, not the company DB. |
| 3 | **e.net runs inside the App Server** — It's not a separate service; it's hosted within SYSPRO's application server process | If the App Server is down, e.net is down. There's no separate service to restart. |
| 4 | **COM+ is still involved** — Even in SYSPRO 8+, COM interop is used under the hood | Your integration server needs COM+ access. This means Windows only for direct integration. Linux servers must use the HTTP/WCF endpoint. |
| 5 | **Windows-only server** — SYSPRO Application Server runs exclusively on Windows Server | No, you can't run SYSPRO on Linux. Your API can be on Linux (Docker), but SYSPRO stays on Windows. |
| 6 | **Each session = 1 license seat** — SYSPRO charges per concurrent user. Your e.net sessions consume these seats | A session pool of 5 means 5 fewer Avanti users. Plan license counts with the customer. |
| 7 | **800+ tables in each company database** — The schema is massive, but you'll regularly use about 30-40 tables | Don't try to learn all tables. Focus on the module you're integrating with. |
| 8 | **No stored procedures for business logic** — SYSPRO's business rules are in the App Server, NOT in SQL triggers/procs | This is why direct SQL writes are dangerous — the rules aren't enforced at the DB level. |

### SYSPRO Licensing — What You Must Know

```
┌──────────────────────────────────────────────────────────────┐
│                   SYSPRO LICENSE MODEL                        │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  LICENSE TYPE 1: Named User License                          │
│  ─────────────────────────────────                            │
│  • Assigned to a specific person                             │
│  • Only that person can use it                               │
│  • Cheaper per seat                                          │
│  • Used for: power users, daily SYSPRO operators             │
│                                                               │
│  LICENSE TYPE 2: Concurrent User License                     │
│  ───────────────────────────────────────                      │
│  • Shared — anyone can use it                                │
│  • Limited by simultaneous connections                       │
│  • More expensive per seat                                   │
│  • Used for: occasional users, e.net integrations            │
│                                                               │
│  LICENSE TYPE 3: e.net License                               │
│  ─────────────────────────────                                │
│  • Specifically for programmatic access                      │
│  • Each active session = 1 e.net license consumed            │
│  • ⚠ If you have 5 e.net licenses and open 5 sessions,     │
│    the 6th call will FAIL with "No licenses available"       │
│  • This is why SESSION POOLING is critical                   │
│                                                               │
│  COST IMPACT:                                                │
│  ────────────                                                 │
│  • Named User: ~$3,000-5,000/year per user                  │
│  • Concurrent: ~$5,000-8,000/year per seat                  │
│  • e.net: Typically bundled or ~$2,000-4,000/year per seat   │
│  • (Prices vary by region and negotiation)                   │
│                                                               │
│  YOUR INTEGRATION'S IMPACT:                                  │
│  ──────────────────────────                                   │
│  • Session pool of 3 = 3 e.net licenses consumed            │
│  • If client has 10 total licenses, you're using 30%        │
│  • ALWAYS discuss license allocation with the client         │
│  • ALWAYS pool and reuse sessions (never login per request)  │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

---

## 1.2 Core Modules — Deep Dive Into Each

### Module Interaction Map — How They Talk to Each Other

```
┌──────────────────────────────────────────────────────────────────────┐
│                 MODULE INTERACTION MAP (Data Flows)                    │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│                          ┌──────────────┐                             │
│               ┌─────────►│  GENERAL     │◄─────────┐                 │
│               │          │  LEDGER (GL) │          │                 │
│               │          │              │          │                 │
│               │          │  Every module│          │                 │
│               │          │  posts here  │          │                 │
│               │          └──────────────┘          │                 │
│               │                ▲                   │                 │
│               │                │                   │                 │
│  ┌────────────┴───┐    ┌──────┴───────┐    ┌─────┴────────────┐    │
│  │  ACCOUNTS      │    │  ACCOUNTS    │    │  CASH BOOK       │    │
│  │  RECEIVABLE    │    │  PAYABLE     │    │                  │    │
│  │  (AR)          │    │  (AP)        │    │  Receipts from   │    │
│  │                │    │              │    │  AR, Payments    │    │
│  │  Customer      │    │  Supplier    │    │  to AP           │    │
│  │  invoices from │    │  invoices    │    │                  │    │
│  │  Sales module  │    │  from PO     │    └──────────────────┘    │
│  └───────▲────────┘    └──────▲───────┘                            │
│          │                    │                                     │
│          │                    │                                     │
│  ┌───────┴────────┐    ┌─────┴──────────┐                          │
│  │  SALES ORDER   │    │  PURCHASE      │                          │
│  │  MODULE        │    │  ORDER MODULE  │                          │
│  │                │    │                │                          │
│  │  Creates SO    │    │  Creates PO    │                          │
│  │  Allocates     │    │  Receives goods│                          │
│  │  stock         │    │  into warehouse│                          │
│  │  Ships goods   │    │                │                          │
│  │  Invoices      │    └───────┬────────┘                          │
│  └───────┬────────┘            │                                   │
│          │                     │                                   │
│          │       ┌─────────────┴─────────────┐                     │
│          └──────►│  INVENTORY MODULE          │◄────────┐          │
│                  │                            │         │          │
│   SO allocates   │  QtyOnHand, QtyAllocated   │  PO     │          │
│   and ships      │  QtyOnOrder                │  adds   │          │
│   (reduces qty)  │  Every movement tracked    │  stock  │          │
│                  └─────────────┬──────────────┘         │          │
│                                │                        │          │
│                                ▼                        │          │
│                  ┌─────────────────────────────┐        │          │
│                  │  MANUFACTURING (WIP)        │────────┘          │
│                  │                             │                   │
│                  │  Consumes raw materials     │  Creates finished │
│                  │  from Inventory             │  goods into       │
│                  │  (issues components)        │  Inventory        │
│                  │  Produces finished goods    │                   │
│                  │  (receipts to stock)        │                   │
│                  └─────────────────────────────┘                   │
└──────────────────────────────────────────────────────────────────────┘
```

### 📦 Inventory Module (The Heart of SYSPRO)

The Inventory module is the **most integrated** module — almost every other module reads from or writes to inventory tables. Understanding it deeply is non-negotiable.

**What it manages**: Stock items (products/materials), warehouses, bin locations, serial/lot tracking, costing methods, and every single movement of goods.

#### Core Tables — Complete Reference

| Table | Purpose | Key Columns | Row Count (Typical) |
|-------|---------|-------------|-------------------|
| `InvMaster` | **Item master** — one row per stock code | `StockCode`, `Description`, `LongDesc`, `StockUom`, `AlternateUom`, `Mass`, `Volume`, `TariffCode`, `Decimals`, `ProductClass`, `StockType` (B=Buy, M=Make, N=Non-stocked) | 5K–50K items |
| `InvWarehouse` | **Stock levels per warehouse** — one row per StockCode × Warehouse | `StockCode`, `Warehouse`, `QtyOnHand`, `QtyAllocated`, `QtyOnOrder`, `QtyInTransit`, `QtyOnBackOrder`, `UnitCost`, `AvgCost`, `LastCost`, `StdCost`, `MinimumQty`, `MaximumQty`, `ReorderQty`, `LastReceiptDate`, `LastIssueDate` | 10K–200K rows |
| `InvMovement` | **Every movement ever** — receipt, issue, transfer, adjustment | `StockCode`, `Warehouse`, `TrnDate`, `TrnTime`, `TrnQty`, `TrnValue`, `TrnType`, `Reference`, `Journal`, `Operator`, `Source` | 100K–10M+ rows |
| `InvMultiBin` | **Bin-level tracking** (within a warehouse) | `StockCode`, `Warehouse`, `Bin`, `BinQty`, `BinLocation` | Varies |
| `InvSerials` | **Serial number tracking** | `StockCode`, `SerialNumber`, `Status` (A=Available, S=Sold, I=In transit), `CurrentWarehouse`, `ReceiptDate`, `SalesOrder`, `Customer` | Varies |
| `InvPrice` | **Selling prices** — multiple pricing structures | `StockCode`, `PriceCode`, `SellingPrice`, `PriceUom`, `CommissionCode`, `QuantityBreak1..5`, `PriceBreak1..5` | 20K–100K |
| `InvSupplier` | **Preferred suppliers per item** | `StockCode`, `Supplier`, `SupStockCode`, `SupUom`, `ConvFactMul`, `LeadTime`, `SupUnitPrice` | 10K–100K |

#### Transaction Types (TrnType) — The Movement DNA

Every movement in `InvMovement` has a `TrnType`. These are the ones you'll encounter:

| TrnType | Meaning | Source | Impact on QtyOnHand |
|---------|---------|--------|-------------------|
| `R` | Receipt (Purchase) | Purchase Order GRN | ⬆ Increases |
| `I` | Issue (Sales Shipment) | Sales Order Dispatch | ⬇ Decreases |
| `T` | Transfer Out | Warehouse Transfer | ⬇ Decreases (source) |
| `T+` | Transfer In | Warehouse Transfer | ⬆ Increases (destination) |
| `A` | Adjustment (+/-) | Manual Adjustment | ⬆ or ⬇ |
| `P` | Physical Count | Stocktake | Sets to counted qty |
| `M` | Manufacturing Receipt | Job Receipt | ⬆ Increases (finished goods) |
| `W` | WIP Issue | Job Material Issue | ⬇ Decreases (components) |
| `C` | Credit Note Return | Customer Return | ⬆ Increases |

#### Costing Methods — What Changes Your Integration

SYSPRO supports multiple costing methods. The method determines which cost value your system sees:

| Method | How Cost is Calculated | When to Use |
|--------|----------------------|-------------|
| **Average Cost** | Running weighted average of all receipts | Distribution companies, general use |
| **Standard Cost** | Pre-set cost, variances tracked separately | Manufacturing companies |
| **Last Cost** | Price from the most recent purchase | Simple tracking |
| **FIFO** | First-In-First-Out layer tracking | Perishable goods, compliance |
| **LIFO** | Last-In-First-Out layer tracking | Tax optimization (US) |

**Critical business logic:**
```
Available Qty = QtyOnHand - QtyAllocated

Reorder needed when:
  AvailableQty + QtyOnOrder < MinimumQty

Economic Order Qty:
  MAX(ReorderQty, MinimumQty - (QtyOnHand - QtyAllocated + QtyOnOrder))
```

#### Real Queries You'll Use Constantly

**1. Full inventory snapshot with availability:**
```sql
SELECT 
    m.StockCode,
    m.Description,
    m.StockUom AS UOM,
    m.ProductClass,
    m.StockType,
    w.Warehouse,
    w.QtyOnHand,
    w.QtyAllocated,
    (w.QtyOnHand - w.QtyAllocated) AS AvailableQty,
    w.QtyOnOrder,
    w.QtyOnBackOrder,
    w.UnitCost,
    w.AvgCost,
    w.LastCost,
    w.MinimumQty AS ReorderPoint,
    w.MaximumQty AS MaxLevel,
    w.ReorderQty,
    w.LastReceiptDate,
    w.LastIssueDate,
    CASE 
        WHEN (w.QtyOnHand - w.QtyAllocated) <= 0 THEN 'OUT_OF_STOCK'
        WHEN (w.QtyOnHand - w.QtyAllocated) <= w.MinimumQty THEN 'LOW_STOCK'
        ELSE 'IN_STOCK'
    END AS StockStatus
FROM InvMaster m
INNER JOIN InvWarehouse w ON m.StockCode = w.StockCode
WHERE w.Warehouse = 'WH01'
  AND m.StockType IN ('B', 'M')  -- Only buyable/makeable items
ORDER BY m.StockCode;
```

**2. Stock movement history (last 30 days):**
```sql
SELECT 
    mv.TrnDate,
    mv.TrnTime,
    mv.StockCode,
    m.Description,
    mv.Warehouse,
    mv.TrnType,
    CASE mv.TrnType 
        WHEN 'R' THEN 'Receipt'
        WHEN 'I' THEN 'Issue/Ship'
        WHEN 'T' THEN 'Transfer'
        WHEN 'A' THEN 'Adjustment'
        WHEN 'M' THEN 'Mfg Receipt'
        WHEN 'W' THEN 'WIP Issue'
    END AS MovementType,
    mv.TrnQty,
    mv.TrnValue,
    mv.Reference,
    mv.Operator
FROM InvMovement mv
INNER JOIN InvMaster m ON mv.StockCode = m.StockCode
WHERE mv.TrnDate >= DATEADD(day, -30, GETDATE())
ORDER BY mv.TrnDate DESC, mv.TrnTime DESC;
```

**3. Items below reorder point (needs purchasing):**
```sql
SELECT 
    m.StockCode,
    m.Description,
    w.Warehouse,
    w.QtyOnHand,
    w.QtyAllocated,
    (w.QtyOnHand - w.QtyAllocated) AS AvailableQty,
    w.QtyOnOrder,
    w.MinimumQty AS ReorderPoint,
    w.ReorderQty AS SuggestedOrderQty,
    s.Supplier AS PreferredSupplier,
    s.LeadTime AS LeadTimeDays
FROM InvMaster m
INNER JOIN InvWarehouse w ON m.StockCode = w.StockCode
LEFT JOIN InvSupplier s ON m.StockCode = s.StockCode AND s.Supplier = (
    SELECT TOP 1 Supplier FROM InvSupplier 
    WHERE StockCode = m.StockCode ORDER BY Supplier
)
WHERE (w.QtyOnHand - w.QtyAllocated + w.QtyOnOrder) < w.MinimumQty
  AND w.MinimumQty > 0
  AND m.StockType = 'B'  -- Only buyable items
ORDER BY 
    (w.QtyOnHand - w.QtyAllocated) ASC;  -- Most critical first
```

---

### 🛒 Sales Order Module — Deep Dive

The Sales Order module handles the **entire order-to-cash cycle**: quotations, sales orders, back orders, delivery notes, invoicing, and credit notes.

#### Core Tables — Complete Reference

| Table | Purpose | Key Columns | Notes |
|-------|---------|-------------|-------|
| `SorMaster` | **Sales order header** | `SalesOrder`, `Customer`, `OrderDate`, `ReqShipDate`, `OrderStatus`, `OrderType` (O=Order, B=Backorder, C=CreditNote), `Warehouse`, `Salesperson`, `Branch`, `Currency`, `ExchangeRate`, `OrderTotalValue`, `OrderTotalCost`, `TaxValue`, `DiscountValue` | One row per SO |
| `SorDetail` | **Sales order lines** | `SalesOrder`, `SalesOrderLine`, `MStockCode`, `MStockDes`, `MWarehouse`, `MOrderQty`, `MShipQty`, `MBackOrderQty`, `MOrderPrice`, `MOrderDisc`, `MOrderDiscPct`, `MLineShipDate`, `MProductClass`, `MOrderUom`, `MReviewFlag`, `LineTotal` | Multiple rows per SO |
| `SorMaster+` | **Extended header** | `SalesOrder`, extended custom fields | SYSPRO supports custom fields via `+` tables |
| `CusSorMaster` | **Customer SO cross-ref** | `Customer`, `SalesOrder`, `CustomerPoNumber` | Lookup SO by customer PO |
| `QuoMaster` | **Quotation header** | `Quotation`, `Customer`, `QuoteDate`, `ExpiryDate`, `QuoteStatus`, `Salesperson` | Can be converted to SO |
| `QuoDetail` | **Quotation lines** | `Quotation`, `QuoteLine`, `StockCode`, `QuoteQty`, `QuotePrice` | Line-level details |
| `SalSalesperson` | **Salesperson master** | `Salesperson`, `Name`, `Branch`, `CommissionCode`, `Email` | Commission tracking |

#### Order Status Codes — Complete Reference

| Status | Meaning | What Happens Next | Can Your API Create? |
|--------|---------|-------------------|---------------------|
| `1` | **Open** | Ready for picking/shipping | ✅ Yes (default) |
| `2` | **In Progress** | Partially shipped, remaining lines open | ❌ SYSPRO sets this |
| `3` | **Complete** | Fully shipped and invoiced | ❌ SYSPRO sets this |
| `4` | **Forward Order** | Future-dated, not yet actionable | ✅ Yes (set ship date in future) |
| `8` | **Cancelled** | Cancelled, stock de-allocated | ✅ Via separate cancel BO |
| `9` | **Suspended** | On hold — credit hold, manager review, stock issue | ❌ SYSPRO sets this |
| `\` | **Blanket/Scheduled** | Standing order, released in batches | ✅ Yes (advanced) |

#### Order Type Codes

| Type | Meaning | Use Case |
|------|---------|----------|
| `O` | **Order** | Standard sales order |
| `B` | **Back Order** | Created when stock is insufficient |
| `C` | **Credit Note** | Return/refund — reverses original |
| `I` | **Inter-branch** | Transfer between company branches |
| `Q` | **Quotation** | (in QuoMaster/QuoDetail, not SorMaster) |

#### Real-world Sales Order Queries

**1. Open orders with shipping details:**
```sql
SELECT 
    h.SalesOrder,
    h.Customer,
    c.Name AS CustomerName,
    h.OrderDate,
    h.ReqShipDate AS RequestedShipDate,
    h.OrderStatus,
    CASE h.OrderStatus 
        WHEN '1' THEN 'Open'
        WHEN '2' THEN 'In Progress'  
        WHEN '4' THEN 'Forward Order'
        WHEN '9' THEN 'Suspended'
    END AS StatusText,
    h.Salesperson,
    sp.Name AS SalespersonName,
    h.OrderTotalValue,
    h.TaxValue,
    d.SalesOrderLine,
    d.MStockCode AS StockCode,
    im.Description AS StockDescription,
    d.MOrderQty AS OrderedQty,
    d.MShipQty AS ShippedQty,
    (d.MOrderQty - d.MShipQty) AS OutstandingQty,
    d.MBackOrderQty AS BackorderQty,
    d.MOrderPrice AS UnitPrice,
    d.MOrderDiscPct AS DiscountPct,
    (d.MOrderQty * d.MOrderPrice * (1 - d.MOrderDiscPct/100)) AS LineTotal,
    d.MLineShipDate AS LineShipDate,
    csm.CustomerPoNumber
FROM SorMaster h
INNER JOIN SorDetail d ON h.SalesOrder = d.SalesOrder
INNER JOIN ArCustomer c ON h.Customer = c.Customer
INNER JOIN InvMaster im ON d.MStockCode = im.StockCode
LEFT JOIN SalSalesperson sp ON h.Salesperson = sp.Salesperson
LEFT JOIN CusSorMaster csm ON h.SalesOrder = csm.SalesOrder
WHERE h.OrderStatus IN ('1', '2', '4')
  AND h.OrderDate >= DATEADD(month, -6, GETDATE())
ORDER BY h.OrderDate DESC, h.SalesOrder, d.SalesOrderLine;
```

**2. Customer order history (for portal/dashboard):**
```sql
SELECT 
    h.SalesOrder,
    h.OrderDate,
    h.OrderStatus,
    h.OrderTotalValue AS TotalValue,
    csm.CustomerPoNumber,
    COUNT(d.SalesOrderLine) AS LineCount,
    SUM(d.MShipQty) AS TotalShipped,
    SUM(d.MOrderQty) AS TotalOrdered,
    CASE 
        WHEN SUM(d.MShipQty) = 0 THEN 'Not Shipped'
        WHEN SUM(d.MShipQty) < SUM(d.MOrderQty) THEN 'Partially Shipped'
        ELSE 'Fully Shipped'
    END AS ShipStatus
FROM SorMaster h
INNER JOIN SorDetail d ON h.SalesOrder = d.SalesOrder
LEFT JOIN CusSorMaster csm ON h.SalesOrder = csm.SalesOrder
WHERE h.Customer = '0000100'
GROUP BY h.SalesOrder, h.OrderDate, h.OrderStatus, 
         h.OrderTotalValue, csm.CustomerPoNumber
ORDER BY h.OrderDate DESC;
```

---

### 📋 Purchase Order Module

**Missing from the original guide — critical for complete understanding.**

The Purchase Order module handles procurement: requesting goods from suppliers, receiving goods (GRN), and supplier invoicing.

#### Core Tables

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `PorMasterHdr` | PO header | `PurchaseOrder`, `Supplier`, `OrderDate`, `OrderStatus`, `DeliveryDate`, `Warehouse`, `Currency`, `OrderTotalValue`, `Buyer` |
| `PorMasterDetail` | PO lines | `PurchaseOrder`, `PurchaseOrderLine`, `MStockCode`, `MOrderQty`, `MReceivedQty`, `MOrderPrice`, `MStockingUom`, `MDeliveryDate` |
| `GrnMatching` | GRN records | `GrnNumber`, `PurchaseOrder`, `PurchaseOrderLine`, `QtyReceived`, `ReceiptDate` |

#### PO Status Codes

| Status | Meaning |
|--------|---------|
| `1` | Open |
| `2` | Partially received |
| `3` | Complete (fully received) |
| `4` | Blanket/scheduled |
| `8` | Cancelled |

---

### 🏭 Manufacturing Module (WIP) — Deep Dive

| Table | Purpose | Key Columns | Detail |
|-------|---------|-------------|--------|
| `WipMaster` | **Job header** | `Job`, `StockCode`, `QtyToMake`, `QtyManufactured`, `JobStatus`, `JobStartDate`, `JobEndDate`, `JobClassification` | One row per manufacturing job |
| `BomStructure` | **Bill of Materials** | `ParentPart`, `Component`, `QtyPer`, `ScrapFactor`, `ScrapQty`, `Warehouse`, `OperOffset` | Defines what components make up a finished product |
| `BomRoute` | **Routing/operations** | `StockCode`, `Operation`, `WorkCentre`, `RunTime`, `SetupTime`, `Description`, `QtyPerHour` | Defines HOW to make the product (steps/operations) |
| `WipLabor` | **Labor bookings** | `Job`, `Operation`, `Employee`, `ActualHours`, `BookDate`, `StartTime`, `EndTime` | Time tracking per operation |
| `WipMaterial` | **Material allocations** | `Job`, `StockCode`, `QtyRequired`, `QtyIssued`, `QtyOutstanding`, `Warehouse` | Tracks components consumed |
| `MfgWorkCentre` | **Work centres** | `WorkCentre`, `Description`, `CostRate`, `Capacity`, `Department` | Named production areas |

#### BOM Explosion Example

A finished product "BIKE-100" has this BOM:
```
BIKE-100 (Finished Bicycle)
├── FRAME-A (1 pc)     ← ParentPart=BIKE-100, Component=FRAME-A, QtyPer=1
├── WHEEL-22 (2 pcs)   ← ParentPart=BIKE-100, Component=WHEEL-22, QtyPer=2
├── SEAT-STD (1 pc)    ← ParentPart=BIKE-100, Component=SEAT-STD, QtyPer=1
├── CHAIN-50 (1 pc)    ← ParentPart=BIKE-100, Component=CHAIN-50, QtyPer=1
└── BRAKE-SET (1 set)  ← ParentPart=BIKE-100, Component=BRAKE-SET, QtyPer=1
    ├── BRAKE-PAD (2)  ← Sub-BOM: ParentPart=BRAKE-SET
    └── CABLE-BRK (2)  ← Sub-BOM: ParentPart=BRAKE-SET
```

**BOM explosion query (single level):**
```sql
SELECT 
    b.ParentPart,
    b.Component,
    im.Description AS ComponentDescription,
    b.QtyPer,
    b.ScrapFactor,
    (b.QtyPer * (1 + b.ScrapFactor/100)) AS AdjustedQtyPer,
    w.QtyOnHand AS ComponentOnHand,
    (w.QtyOnHand - w.QtyAllocated) AS ComponentAvailable,
    im.StockUom
FROM BomStructure b
INNER JOIN InvMaster im ON b.Component = im.StockCode
LEFT JOIN InvWarehouse w ON b.Component = w.StockCode AND w.Warehouse = 'WH01'
WHERE b.ParentPart = 'BIKE-100'
ORDER BY b.Component;
```

---

### 💰 Finance Module (AR/AP/GL) — Deep Dive

#### Accounts Receivable (AR) — Customer Side

| Table | Purpose | Key Columns | Detail |
|-------|---------|-------------|--------|
| `ArCustomer` | **Customer master** | `Customer`, `Name`, `ShortName`, `SoldToAddr1..5`, `Telephone`, `Email`, `CreditLimit`, `Balance`, `TermsCode`, `TaxStatus`, `Currency`, `Salesperson`, `Branch`, `InvoiceCount`, `CreditStatus` (N=Normal, H=Hold, S=Stop) | One row per customer |
| `ArInvoice` | **Outstanding invoices** | `Customer`, `Invoice`, `InvoiceDate`, `DueDate`, `OriginalValue`, `BalanceOutstanding`, `DiscountValue`, `InvoiceType` (I=Invoice, D=Debit Note, C=Credit Note) | Only unpaid invoices (paid ones move to history) |
| `ArTrnDetail` | **AR transaction history** | `Customer`, `Invoice`, `TrnDate`, `TrnType`, `TrnValue`, `Reference` | All AR transactions |

**Customer credit status check (essential before SO creation):**
```sql
SELECT 
    c.Customer,
    c.Name,
    c.CreditLimit,
    c.Balance AS CurrentBalance,
    (c.CreditLimit - c.Balance) AS AvailableCredit,
    c.CreditStatus,
    CASE c.CreditStatus
        WHEN 'N' THEN 'Normal - OK to transact'
        WHEN 'H' THEN 'ON HOLD - Requires approval'
        WHEN 'S' THEN 'STOPPED - No transactions allowed'
    END AS CreditStatusText,
    c.TermsCode,
    COUNT(i.Invoice) AS OutstandingInvoices,
    SUM(i.BalanceOutstanding) AS TotalOutstanding,
    MAX(DATEDIFF(day, i.DueDate, GETDATE())) AS OldestOverdueDays
FROM ArCustomer c
LEFT JOIN ArInvoice i ON c.Customer = i.Customer
WHERE c.Customer = '0000100'
GROUP BY c.Customer, c.Name, c.CreditLimit, c.Balance, 
         c.CreditStatus, c.TermsCode;
```

#### Accounts Payable (AP) — Supplier Side

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `ApSupplier` | Supplier master | `Supplier`, `SupplierName`, `TermsCode`, `Balance`, `Currency`, `TaxStatus` |
| `ApInvoice` | Outstanding supplier invoices | `Supplier`, `Invoice`, `InvoiceDate`, `DueDate`, `OriginalValue`, `BalanceOutstanding` |

#### General Ledger (GL) — The Financial Core

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `GlMaster` | Chart of accounts | `GlCode`, `Description`, `AccountType` (B=Balance Sheet, I=Income, E=Expense), `SubAccountOf` |
| `GlJournal` | All GL transactions | `GlCode`, `JournalDate`, `JournalValue`, `Source` (AR, AP, IN, SO, etc.), `JournalNumber`, `Reference`, `EntryType` |
| `GlBudget` | Budget figures | `GlCode`, `BudgetCode`, `Period01..12`, `Year` |

---

## 1.3 Real Business Flow — End to End (With DB Changes)

```
COMPLETE ORDER-TO-CASH FLOW WITH ALL DB OPERATIONS
═══════════════════════════════════════════════════

Step 1: QUOTE (Optional)
─────────────────────────
Tables: QuoMaster + QuoDetail (INSERT)
• Salesperson creates quote for customer
• Calculates pricing, discounts, tax
• Sets expiry date (e.g., valid 30 days)
• Status: Open

       │ Customer accepts quote
       ▼

Step 2: SALES ORDER
─────────────────────────
Tables: SorMaster + SorDetail (INSERT)
        InvWarehouse (UPDATE: QtyAllocated += OrderQty)
        CusSorMaster (INSERT: links customer PO to SO)
• Quote converts to Sales Order (or SO created directly)
• SYSPRO assigns next SO number (auto-increment)
• Inventory ALLOCATED (not shipped yet — stock reserved)
• Status: 1 (Open)

>>> THIS IS WHERE YOUR INTEGRATION TYPICALLY STARTS
>>> Your API creates the SO via e.net SORTOI business object

       │ Warehouse picks goods
       ▼

Step 3: DISPATCH / SHIPMENT
─────────────────────────
Tables: SorDetail (UPDATE: MShipQty += shipped)
        InvWarehouse (UPDATE: QtyOnHand -= shipped, QtyAllocated -= shipped)
        InvMovement (INSERT: TrnType = 'I' for issue)
        InvSerials (UPDATE: Status = 'S' if serial tracked)
• Warehouse staff scans and ships items
• Movement recorded with reference to SO
• Partial shipments possible (Status changes to 2)
• Full shipment (Status remains 1 until invoiced)

       │ Finance department processes
       ▼

Step 4: INVOICE
─────────────────────────
Tables: ArInvoice (INSERT: new invoice record)
        GlJournal (INSERT: Revenue, AR, Tax, COGS entries)
        SorMaster (UPDATE: Status → 3 if fully invoiced)
        ArCustomer (UPDATE: Balance += invoice value)
• Invoice generated from shipped lines
• Multiple GL entries created automatically:
  DR: Accounts Receivable (asset)
  CR: Revenue (income)
  CR: Tax Payable (liability)
  DR: Cost of Goods Sold (expense)
  CR: Inventory (asset reduction)

       │ Customer pays
       ▼

Step 5: PAYMENT RECEIPT
─────────────────────────
Tables: CshDetail (INSERT: payment record)
        ArInvoice (UPDATE or DELETE: BalanceOutstanding reduced)
        GlJournal (INSERT: Cash DR, AR CR)
        ArCustomer (UPDATE: Balance -= payment)
• Payment matched to invoice(s)
• If fully paid, invoice removed from ArInvoice (moves to history)
• Cash book updated
• GL entries: DR Cash, CR Accounts Receivable
```

---

## 1.4 Database Relationship Diagram (Extended)

```
┌─────────────────┐
│   ArCustomer    │
│─────────────────│
│ PK: Customer    │◄─────────────────────────────────────────────┐
│    Name         │                                              │
│    CreditLimit  │                                              │
│    Balance      │    ┌──────────────┐       ┌──────────────┐  │
│    CreditStatus │    │  SorMaster   │       │  SorDetail   │  │
│    TermsCode    │    │──────────────│       │──────────────│  │
│    Currency     │    │PK: SalesOrder│──────►│PK: SalesOrder│  │
└────────┬────────┘    │FK: Customer──┼───────│PK: SOLine    │  │
         │             │   OrderDate  │       │   MStockCode │  │
         │             │   OrderStatus│       │   MOrderQty  │  │
         │             │   Warehouse  │       │   MShipQty   │  │
         │             │   Salesperson│       │   MOrderPrice│  │
         │             │   TotalValue │       │   LineTotal  │  │
         │             └──────────────┘       └──────┬───────┘  │
         │                                           │          │
         │                                  FK: MStockCode      │
         │                                           │          │
         │             ┌──────────────┐       ┌──────┴───────┐  │
         │             │ InvWarehouse │       │  InvMaster   │  │
         │             │──────────────│       │──────────────│  │
         │             │PK: StockCode │◄──────│PK: StockCode │  │
         │             │PK: Warehouse │       │   Description│  │
         │             │   QtyOnHand  │       │   StockUom   │  │
         │             │   QtyAllocated       │   StockType  │  │
         │             │   QtyOnOrder │       │   ProductClass  │
         │             │   UnitCost   │       └──────┬───────┘  │
         │             └──────────────┘              │          │
         │                                           │          │
         │             ┌──────────────┐       ┌──────┴───────┐  │
         │             │ InvMovement  │       │ BomStructure │  │
         │             │──────────────│       │──────────────│  │
         │             │FK: StockCode │       │FK: ParentPart│  │
         │             │FK: Warehouse │       │FK: Component │  │
         │             │   TrnDate    │       │   QtyPer     │  │
         │             │   TrnQty     │       │   ScrapFactor│  │
         │             │   TrnType    │       └──────────────┘  │
         │             └──────────────┘                         │
         │                                                      │
         │             ┌──────────────┐       ┌──────────────┐  │
         └────────────►│  ArInvoice   │       │  GlJournal   │  │
                       │──────────────│       │──────────────│  │
                       │FK: Customer  │──────►│   GlCode     │  │
                       │PK: Invoice   │ posts │   JournalDate│  │
                       │   InvoiceDate│  GL   │   JournalValue  │
                       │   OrigValue  │entries│   Source     │  │
                       │   BalanceOut │       │   Reference  │  │
                       └──────────────┘       └──────────────┘  │
                                                                │
         ┌──────────────┐       ┌──────────────┐               │
         │  ApSupplier  │       │ PorMasterHdr │               │
         │──────────────│       │──────────────│               │
         │PK: Supplier  │◄──────│FK: Supplier  │               │
         │   SupplierName│       │PK: PurchOrder│               │
         │   Balance    │       │   OrderDate  │               │
         └──────────────┘       │   OrderStatus│               │
                                └──────┬───────┘               │
                                       │                       │
                                ┌──────┴───────┐               │
                                │PorMasterDetail               │
                                │──────────────│               │
                                │FK: PurchOrder│               │
                                │PK: POLine    │               │
                                │   MStockCode─┼───────────────┘
                                │   MOrderQty  │
                                │   MRecvdQty  │
                                └──────────────┘
```

---

## 1.5 SYSPRO Naming Conventions — Complete Reference

| Prefix | Module | Tables | When You'll Use Them |
|--------|--------|--------|---------------------|
| `Inv` | Inventory | `InvMaster`, `InvWarehouse`, `InvMovement`, `InvMultiBin`, `InvSerials`, `InvPrice`, `InvSupplier` | Always — every integration touches inventory |
| `Sor` | Sales Orders | `SorMaster`, `SorDetail` | eCommerce, order portals, CRM integration |
| `Ar` | Accounts Receivable | `ArCustomer`, `ArInvoice`, `ArTrnDetail` | Customer portals, credit checks, payment processing |
| `Ap` | Accounts Payable | `ApSupplier`, `ApInvoice` | Supplier portals, automated payment runs |
| `Gl` | General Ledger | `GlMaster`, `GlJournal`, `GlBudget` | Financial dashboards, reporting |
| `Wip` | Work In Progress | `WipMaster`, `WipJob`, `WipLabor`, `WipMaterial` | Manufacturing dashboards, IoT integration |
| `Bom` | Bill of Materials | `BomStructure`, `BomRoute` | Product configurators, co~sting tools |
| `Por` | Purchase Orders | `PorMasterHdr`, `PorMasterDetail` | Procurement automation, supplier portals |
| `Quo` | Quotations | `QuoMaster`, `QuoDetail` | CRM integration, sales portals |
| `Csh` | Cash Book | `CshDetail` | Payment processing, bank reconciliation |
| `Trn` | Transaction History | `TrnHistory` | Historical reporting |
| `Sal` | Sales Analysis | `SalSalesperson`, `SalBranch` | Sales dashboards, commission tracking |
| `Mfg` | Manufacturing | `MfgWorkCentre`, `MfgResource` | Shop floor systems |
| `Lot` | Lot Tracking | `LotTracking`, `LotDetail` | Pharmaceutical, food industry compliance |
| `Cus` | Customer-related | `CusSorMaster`, `CusContact` | Customer PO tracking |
| `Grn` | Goods Received | `GrnMatching` | Receiving, 3-way matching |
| `Tax` | Tax | `TaxHistory`, `TaxCode` | Tax compliance, VAT/GST reporting |
| `Adm` | Administration | `AdmFormDef`, `AdmOperator` | System config (rarely accessed) |

### The `+` Table Convention

SYSPRO allows custom fields via **plus tables**. For any core table, a corresponding `+` table may exist:

```
SorMaster   → SorMaster+    (custom SO header fields)
InvMaster   → InvMaster+    (custom item fields)
ArCustomer  → ArCustomer+   (custom customer fields)
```

**Query example with custom fields:**
```sql
SELECT 
    m.StockCode,
    m.Description,
    p.CustomField1,    -- Custom field defined by the client
    p.CustomField2
FROM InvMaster m
LEFT JOIN [InvMaster+] p ON m.StockCode = p.StockCode;
```

⚠ **Important:** Plus tables are optional. Not every SYSPRO installation has them. Always use `LEFT JOIN`, never `INNER JOIN` on plus tables.

---

## 1.6 SYSPRO Version Differences — Impact on Your Integration

| Feature | SYSPRO 7 | SYSPRO 8 (Pre-Avanti) | SYSPRO 8 (With Avanti) |
|---------|----------|----------------------|----------------------|
| **Primary UI** | Desktop client only | Desktop + basic web | Desktop + Avanti (full web) |
| **e.net access** | COM+ or WCF | COM+ or WCF | COM+ or WCF or REST |
| **REST API** | ❌ Not available | ⚠ Limited | ✅ Available (SYSPRO RESTful Services) |
| **Authentication** | Operator + Company | Operator + Company | Operator + Company + OAuth (REST) |
| **Custom BOs** | ✅ Supported | ✅ Supported | ✅ Supported |
| **Cloud hosting** | ❌ On-prem only | ⚠ Managed hosting | ✅ SYSPRO Cloud option |
| **Harmony (iPaaS)** | ❌ | ❌ | ✅ Low-code integration |
| **SQL Server version** | 2008 R2+ | 2012+ | 2016+ recommended |

**What this means for your code:**
- Target SYSPRO 7 compatibility → use e.net COM/WCF (XML-based)
- Target SYSPRO 8+ only → can use REST API (JSON-based, simpler)
- Best practice → support both via abstraction layer

---

[← Back to Main Guide](../README.md) | [Next: Integration Architecture →](./02-INTEGRATION-ARCHITECTURE.md)

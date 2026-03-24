# Part 3: SYSPRO e.net Solutions (Core — Deep Dive)

[← Back to Main Guide](../README.md) | [Previous: Integration Architecture](./02-INTEGRATION-ARCHITECTURE.md) | [Next: .NET Implementation →](./04-DOTNET-IMPLEMENTATION.md)

---

## 3.1 What e.net Actually Is — The Complete Picture

e.net Solutions is SYSPRO's **programmatic interface** — the way external code talks to SYSPRO. It predates REST APIs and uses **XML over COM/WCF**. Every button click in the SYSPRO client ultimately goes through the same business logic that e.net exposes.

Think of it this way:

```
┌────────────────────────────────────────────────────────────────────┐
│                  HOW ALL SYSPRO ACCESS WORKS                        │
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  SYSPRO Desktop Client ──► Business Logic Engine ──► SQL Database  │
│  SYSPRO Avanti (Web) ────► Business Logic Engine ──► SQL Database  │
│  YOUR Code (via e.net) ──► Business Logic Engine ──► SQL Database  │
│                                                                     │
│  ALL THREE go through the SAME engine.                             │
│  e.net is just the "door" for external code.                       │
│                                                                     │
│  The Business Logic Engine:                                        │
│  • Validates all data (credit limits, stock levels, etc.)         │
│  • Calculates dependent values (tax, discounts, totals)           │
│  • Enforces constraints (required fields, valid codes)            │
│  • Creates audit trail entries                                     │
│  • Posts to General Ledger (when applicable)                      │
│  • Triggers workflows (email notifications, approvals)            │
│  • Updates related tables (inventory allocation, etc.)            │
│                                                                     │
│  This is WHY you must use e.net for writes —                      │
│  the business logic doesn't live in the database.                  │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

### e.net Technology Stack

```
┌──────────────────────────────────────────────────────────────────┐
│                    e.net TECHNOLOGY LAYERS                         │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  LAYER 1: YOUR CODE (.NET, Java, Python, etc.)                   │
│  ──────────────────────────────────────────────                   │
│  Generates XML strings, calls e.net methods                      │
│                                                                   │
│  LAYER 2: TRANSPORT (How you call e.net)                         │
│  ──────────────────────────────────────────                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  COM/DCOM    │  │  WCF         │  │  REST API    │          │
│  │  (legacy)    │  │  (SYSPRO 7+) │  │  (SYSPRO 8+) │          │
│  │              │  │              │  │              │          │
│  │  • Windows   │  │  • Cross-    │  │  • JSON in/  │          │
│  │    only      │  │    platform  │  │    out       │          │
│  │  • Same      │  │  • TCP/HTTP  │  │  • Standard  │          │
│  │    machine   │  │  • Port      │  │    HTTP      │          │
│  │    or DCOM   │  │    30661     │  │  • OAuth     │          │
│  │  • Fastest   │  │  • XML       │  │  • Newer     │          │
│  │              │  │    in/out    │  │  • Limited   │          │
│  │  Port: N/A   │  │              │  │    BOs       │          │
│  │  (in-proc)   │  │  Recommended │  │              │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│                                                                   │
│  LAYER 3: e.net SOLUTIONS ENGINE (Inside SYSPRO App Server)     │
│  ──────────────────────────────────────────────────────────       │
│  • Receives XML                                                   │
│  • Identifies Business Object                                    │
│  • Loads BO-specific logic                                       │
│  • Validates input                                               │
│  • Executes transaction                                          │
│  • Returns XML result                                            │
│                                                                   │
│  LAYER 4: SQL SERVER (Database)                                  │
│  ──────────────────────────────                                   │
│  • Data persisted here                                           │
│  • But business logic is NOT here                                │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3.2 Authentication — Complete Deep Dive

### Session Lifecycle

```
┌──────────────────────────────────────────────────────────────────┐
│                    SESSION LIFECYCLE                               │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. LOGON                                                        │
│  ────────                                                         │
│  You send: Operator, Password, CompanyId, CompanyPassword        │
│  SYSPRO returns: SessionId (GUID) — e.g., "A3B8D1B6-..."       │
│  SYSPRO does:                                                     │
│  • Validates operator exists in SysproSystem.dbo.AdmOperator    │
│  • Checks password hash                                         │
│  • Checks operator is marked for e.net access                   │
│  • Checks company exists and is accessible to this operator     │
│  • Checks e.net license availability (seats)                    │
│  • Creates session record in memory                              │
│  • Returns GUID or error message                                 │
│                                                                   │
│  2. ACTIVE SESSION                                               │
│  ─────────────────                                                │
│  • Use SessionId in every subsequent call                        │
│  • Session is tied to: Operator + Company + Language             │
│  • Session timeout: configurable (default 20 min inactivity)    │
│  • Each e.net call resets the timeout counter                    │
│  • Session is NOT thread-safe — one session per thread           │
│                                                                   │
│  3. LOGOFF                                                       │
│  ─────────                                                        │
│  You send: SessionId                                              │
│  SYSPRO does:                                                     │
│  • Releases the license seat                                     │
│  • Clears session memory                                         │
│  • If you DON'T logoff, session hangs until timeout              │
│  • Hanging sessions = wasted license seats = $$$ wasted          │
│                                                                   │
│  4. SESSION EXPIRED / INVALID                                    │
│  ────────────────────────────                                     │
│  If you use an expired/invalid SessionId:                        │
│  • e.net returns error: "Invalid Logon ID"                       │
│  • Your code should: invalidate session, create new one, retry   │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

### Logon XML — Every Field Explained

```xml
<!-- Complete Logon XML with all possible parameters -->
<Logon>
  <!-- REQUIRED FIELDS -->
  <Operator>API_USER</Operator>          <!-- SYSPRO operator code (from AdmOperator table) -->
  <OperatorPassword>p@ssw0rd</OperatorPassword>  <!-- Operator's password -->
  <CompanyId>A</CompanyId>               <!-- Company code (single char or short code) -->
  <CompanyPassword></CompanyPassword>    <!-- Company-level password (usually blank) -->
  
  <!-- OPTIONAL FIELDS -->
  <Language>05</Language>                <!-- Language code: 05=English, 01=Afrikaans, etc. -->
  <LogonStyle>01</LogonStyle>            <!-- 01=Normal, 02=Light (less memory) -->
  <OperatorType>02</OperatorType>        <!-- 01=Desktop, 02=e.net, 03=Web -->
  <CompanyTitle></CompanyTitle>          <!-- For display only, not authentication -->
  <XmlIn></XmlIn>                        <!-- Additional XML parameters (rare) -->
</Logon>
```

### Setting Up a SYSPRO Operator for e.net (Admin Steps)

This is what the SYSPRO administrator needs to do before your integration can connect:

```
Step 1: Create an Operator in SYSPRO
  SYSPRO Menu → Administration → Security → Operator Maintenance
  • Operator Code: API_USER (or similar)
  • Full Name: "Integration API User"
  • Operator Type: e.net Solutions ← CRITICAL
  • Password: [strong password, stored in Key Vault]

Step 2: Assign Company Access
  • Enable access to required company (e.g., Company A)
  • Set operator group (for module-level permissions)

Step 3: Set Permissions
  • Grant ONLY the business objects your integration needs:
    ✅ SORTOI (Sales Order Transaction) — if creating orders
    ✅ SORQRY (Sales Order Query) — if reading orders
    ✅ INVQRY (Inventory Query) — if reading stock
    ✅ ARSTOP (Customer Setup) — if creating customers
    ❌ GLMPST (GL Posting) — probably not needed
    ❌ ADMCFG (Admin Config) — definitely not needed

Step 4: Check e.net License Count
  SYSPRO Menu → Administration → License Details
  • Ensure sufficient e.net seats are available
  • Each concurrent session uses 1 seat
```

### Critical Session Rules — Complete List

| Rule | Detail | What Happens If Violated |
|------|--------|--------------------------|
| **Session = License Seat** | Each active session consumes 1 license | Extra sessions fail with "No licenses available" |
| **Session Timeout** | Default 20 min inactivity, configurable | Session becomes invalid, next call returns error |
| **Pool Sessions** | ALWAYS maintain a pool, never login per request | Login takes 500-2000ms; doing this per request adds massive latency |
| **Always Logoff** | Call Logoff when done, in `finally` blocks | Orphaned sessions eat licenses until timeout |
| **Thread Safety** | One session per thread ONLY | Concurrent calls on same session cause data corruption or deadlocks |
| **Error Handling** | If e.net returns session error, invalidate immediately | Continued use of bad session causes cascading failures |
| **Company-Specific** | Session is bound to one company | To access CompanyB, you need a separate session |
| **Operator Permissions** | Session inherits operator's permissions | If operator can't create SOs, Transaction("SORTOI") will fail |

---

## 3.3 XML Request/Response — Complete Pattern

Every e.net interaction follows this precise pattern:

```
YOUR CODE                    e.net                         SQL SERVER
────────                    ─────                         ──────────

1. Build Parameters XML
   (HOW to process)
   ┌──────────────────┐
   │ <Parameters>     │
   │   PostToGL=Y     │
   │   ValidateAll=Y  │
   │ </Parameters>    │
   └────────┬─────────┘
            │
2. Build Document XML    ──► 4. e.net receives both    ──► 6. SQL INSERT/
   (WHAT data to process)      XMLs + SessionId              UPDATE/DELETE
   ┌──────────────────┐        Validates everything
   │ <Orders>         │        against business rules   ◄── 7. SQL returns
   │   <OrderHeader>  │                                       success/error
   │     Customer=100 │    5. If valid, proceeds
   │   </OrderHeader> │       If invalid, returns
   │   <OrderDetails> │       error immediately
   │     StockCode=A1 │       (no SQL changes)
   │   </OrderDetails>│
   │ </Orders>        │
   └────────┬─────────┘
            │
3. Call e.net method   ──► (steps 4-7 happen)          
   Transaction(                                         
     sessionId,        ◄── 8. e.net returns             
     "SORTOI",              Result XML                   
     paramsXml,             ┌──────────────────┐        
     docXml                 │ <SalesOrders>    │        
   )                        │   <SalesOrder>   │        
                            │     000123       │        
            │               │   </SalesOrder>  │        
            ◄───────────────│ </SalesOrders>   │        
                            └──────────────────┘        
9. Parse response XML                                   
   Extract SO number                                    
   Check for errors                                     
```

### Complete XML Examples — Sales Order

**Parameters XML (Controls how SYSPRO processes the order):**
```xml
<SetupSorToi>
  <Parameters>
    <!-- Core settings -->
    <PostSalesOrders>Y</PostSalesOrders>           <!-- Y=post immediately, N=validate only -->
    <ValidateSalesOrderLines>Y</ValidateSalesOrderLines>  <!-- Validate each line -->
    <AllowDuplicateOrderNumber>N</AllowDuplicateOrderNumber>
    <DefaultOrderType>O</DefaultOrderType>         <!-- O=Order, B=Backorder, C=Credit -->
    <AllowNonStockedItems>N</AllowNonStockedItems> <!-- If Y, allows free-text items -->
    
    <!-- Validation behavior -->
    <ApplyIfEntireDocumentValid>Y</ApplyIfEntireDocumentValid>  <!-- CRITICAL: atomic -->
    <IgnoreWarnings>N</IgnoreWarnings>             <!-- N=treat warnings as errors -->
    <ValidateCustomerPoNumber>Y</ValidateCustomerPoNumber>
    <AllowZeroPrice>N</AllowZeroPrice>
    
    <!-- What to default if not specified -->
    <DefaultShipQuantity>N</DefaultShipQuantity>   <!-- Don't auto-ship -->
    <AllowChangeOfSalesOrder>N</AllowChangeOfSalesOrder>
    <RoundPriceToDecimals>2</RoundPriceToDecimals>
    
    <!-- Output control -->
    <ReturnOrderNumber>Y</ReturnOrderNumber>       <!-- Return the generated SO# -->
  </Parameters>
</SetupSorToi>
```

**Document XML (The actual order data):**
```xml
<SalesOrders>
  <Orders>
    <OrderHeader>
      <!-- Customer identification -->
      <Customer>0000100</Customer>               <!-- SYSPRO customer code -->
      <OrderDate>2024-03-15</OrderDate>          <!-- Date format: YYYY-MM-DD -->
      <CustomerPoNumber>PO-2024-001</CustomerPoNumber>  <!-- Customer's PO# -->
      <Warehouse>WH01</Warehouse>                <!-- Default warehouse for this order -->
      
      <!-- Optional header fields -->
      <ReqShipDate>2024-03-20</ReqShipDate>      <!-- Requested ship date -->
      <Salesperson>JD01</Salesperson>              <!-- Salesperson code -->
      <Branch>01</Branch>                          <!-- Branch code -->
      <ShippingInstrs>Handle with care</ShippingInstrs>
      <SpecialInstrs>Call before delivery</SpecialInstrs>
      <OrderType>O</OrderType>                    <!-- O=Order (default) -->
      
      <!-- Ship-to address (if different from sold-to) -->
      <ShipAddress1>456 Ship Street</ShipAddress1>
      <ShipAddress2>Unit 7</ShipAddress2>
      <ShipAddress3>Houston</ShipAddress3>
      <ShipAddress4>TX</ShipAddress4>
      <ShipAddress5>77001</ShipAddress5>
      
      <!-- Initialize order -->
      <SalesOrderInitSalesOrder>Y</SalesOrderInitSalesOrder>
    </OrderHeader>
    
    <OrderDetails>
      <!-- Line 1: Standard stock item -->
      <StockLine>
        <StockCode>A100</StockCode>              <!-- SYSPRO stock code -->
        <OrderQty>10</OrderQty>                   <!-- Quantity to order -->
        <Price>25.50</Price>                       <!-- Unit price (excl. tax) -->
        <PriceUom>EA</PriceUom>                   <!-- Price unit of measure -->
        <Warehouse>WH01</Warehouse>               <!-- Can override per line -->
        <!-- Optional line fields -->
        <OrderLineShipDate>2024-03-20</OrderLineShipDate>
        <ProductClass>FIN</ProductClass>           <!-- Product classification -->
        <DiscountPercent1>5.00</DiscountPercent1> <!-- Line discount -->
      </StockLine>
      
      <!-- Line 2: Another item -->
      <StockLine>
        <StockCode>B200</StockCode>
        <OrderQty>5</OrderQty>
        <Price>100.00</Price>
        <PriceUom>EA</PriceUom>
        <Warehouse>WH01</Warehouse>
      </StockLine>
      
      <!-- Line 3: Comment line (no stock) -->
      <CommentLine>
        <Comment>Please include packing list</Comment>
      </CommentLine>
    </OrderDetails>
  </Orders>
</SalesOrders>
```

**Success Response:**
```xml
<SalesOrders Language="05" Language2="EN" CssStyle="" DecFormat="1" 
             DateFormat="01" Role="01" Version="8.0" 
             OperatorPrimaryRole="01">
  <Orders>
    <OrderHeader>
      <SalesOrder>000123</SalesOrder>         <!-- ← YOUR NEW SO NUMBER -->
      <Customer>0000100</Customer>
      <OrderDate>2024-03-15</OrderDate>
      <OrderStatus>1</OrderStatus>            <!-- 1 = Open -->
      <OrderType>O</OrderType>
      <OrderTotalValue>755.00</OrderTotalValue>
      <TaxValue>79.28</TaxValue>
      <OrderTotalIncTax>834.28</OrderTotalIncTax>
    </OrderHeader>
  </Orders>
</SalesOrders>
```

**Error Responses (Real Examples):**
```xml
<!-- Error 1: Customer on credit hold -->
<SalesOrders>
  <ErrorDescription>Customer 0000100 is on credit hold. 
    Credit status: H. Contact accounts department.</ErrorDescription>
</SalesOrders>

<!-- Error 2: Stock code not found -->
<SalesOrders>
  <Orders>
    <OrderDetails>
      <StockLine>
        <StockCode>INVALID_CODE</StockCode>
        <ErrorDescription>Stock code INVALID_CODE not on file</ErrorDescription>
      </StockLine>
    </OrderDetails>
  </Orders>
</SalesOrders>

<!-- Error 3: Insufficient stock (warning or error depending on config) -->
<SalesOrders>
  <Orders>
    <OrderDetails>
      <StockLine>
        <StockCode>A100</StockCode>
        <WarningDescription>Insufficient stock for A100 in WH01. 
          Available: 3, Requested: 10. Backorder will be created.</WarningDescription>
      </StockLine>
    </OrderDetails>
  </Orders>
</SalesOrders>

<!-- Error 4: Duplicate PO number -->
<SalesOrders>
  <ErrorDescription>Customer PO number PO-2024-001 already exists 
    on Sales Order 000098</ErrorDescription>
</SalesOrders>

<!-- Error 5: Required field missing -->
<SalesOrders>
  <ErrorDescription>Warehouse is a required field and has not been 
    supplied</ErrorDescription>
</SalesOrders>
```

---

### Complete XML Examples — Purchase Order

**Parameters:**
```xml
<SetupPorToi>
  <Parameters>
    <PostPurchaseOrders>Y</PostPurchaseOrders>
    <ValidatePurchaseOrderLines>Y</ValidatePurchaseOrderLines>
    <ApplyIfEntireDocumentValid>Y</ApplyIfEntireDocumentValid>
  </Parameters>
</SetupPorToi>
```

**Document:**
```xml
<PurchaseOrders>
  <Orders>
    <OrderHeader>
      <Supplier>SUP001</Supplier>
      <OrderDate>2024-03-15</OrderDate>
      <DeliveryDate>2024-04-15</DeliveryDate>
      <Warehouse>WH01</Warehouse>
      <Buyer>BUYER01</Buyer>
      <SupplierReference>SINV-9988</SupplierReference>
    </OrderHeader>
    <OrderDetails>
      <StockLine>
        <StockCode>RAW-100</StockCode>
        <OrderQty>500</OrderQty>
        <Price>12.50</Price>
        <PriceUom>EA</PriceUom>
        <DeliveryDate>2024-04-15</DeliveryDate>
      </StockLine>
    </OrderDetails>
  </Orders>
</PurchaseOrders>
```

### Complete XML Examples — Inventory Receipt (GRN)

```xml
<!-- Parameters for Inventory Receipt -->
<SetupInvTmR>
  <Parameters>
    <PostInventoryReceipts>Y</PostInventoryReceipts>
    <ApplyIfEntireDocumentValid>Y</ApplyIfEntireDocumentValid>
  </Parameters>
</SetupInvTmR>

<!-- Document for Inventory Receipt -->
<InventoryReceipts>
  <Item>
    <StockCode>A100</StockCode>
    <Warehouse>WH01</Warehouse>
    <Quantity>100</Quantity>
    <UnitCost>20.00</UnitCost>
    <Reference>GRN-001</Reference>
    <Notation>Received from warehouse transfer</Notation>
    <!-- Optional: Bin location -->
    <Bin>A-01-03</Bin>
    <!-- Optional: Lot tracking -->
    <Lot>LOT2024031501</Lot>
    <LotExpiryDate>2025-03-15</LotExpiryDate>
  </Item>
</InventoryReceipts>
```

### Complete XML Examples — Inventory Transfer

```xml
<!-- Parameters for Inventory Transfer -->
<SetupInvTmT>
  <Parameters>
    <PostInventoryTransfers>Y</PostInventoryTransfers>
    <ApplyIfEntireDocumentValid>Y</ApplyIfEntireDocumentValid>
  </Parameters>
</SetupInvTmT>

<!-- Document: Transfer 50 units of A100 from WH01 to WH02 -->
<InventoryTransfers>
  <Item>
    <StockCode>A100</StockCode>
    <FromWarehouse>WH01</FromWarehouse>
    <ToWarehouse>WH02</ToWarehouse>
    <Quantity>50</Quantity>
    <Reference>TRF-001</Reference>
    <Notation>Replenish secondary warehouse</Notation>
  </Item>
</InventoryTransfers>
```

---

## 3.4 Complete Business Object Reference

### Transaction Business Objects (Create/Update/Delete)

| Business Object | Code | Purpose | Method | Parameters XML Root | Document XML Root |
|----------------|------|---------|--------|--------------------|--------------------|
| Sales Order Post | `SORTOI` | Create/Update sales orders | `Transaction` | `SetupSorToi` | `SalesOrders` |
| Sales Order Cancel | `SORTOC` | Cancel sales order | `Transaction` | `SetupSorToc` | `SalesOrders` |
| Credit Note | `SORTCI` | Create credit note (return) | `Transaction` | `SetupSorTci` | `SalesOrders` |
| Customer Post | `ARSTOP` | Create/Update customers | `SetupAdd/Update` | `SetupArsTos` | `SetupArsTos` |
| Inventory Receipt | `INVTMR` | Receive goods into stock | `Transaction` | `SetupInvTmR` | `InventoryReceipts` |
| Inventory Issue | `INVTMI` | Issue goods from stock | `Transaction` | `SetupInvTmI` | `InventoryIssues` |
| Inventory Transfer | `INVTMT` | Transfer between warehouses | `Transaction` | `SetupInvTmT` | `InventoryTransfers` |
| Inventory Adjustment | `INVTMA` | Adjust stock qty/value | `Transaction` | `SetupInvTmA` | `InventoryAdjustments` |
| Purchase Order Post | `PORTOI` | Create/Update POs | `Transaction` | `SetupPorToi` | `PurchaseOrders` |
| GRN Post | `PORTGR` | Goods received note | `Transaction` | `SetupPorTgr` | `GoodsReceivedNotes` |
| Invoice Post | `SORTIS` | Post SO invoice | `Transaction` | `SetupSorTis` | `PostInvoice` |
| Cash Receipt | `ARSTCR` | Post customer payment | `Transaction` | `SetupArsTcr` | `CashReceipts` |
| Stock Code Setup | `INVSTS` | Create/Update stock items | `SetupAdd/Update` | `SetupInvSts` | `SetupInvSts` |
| Supplier Post | `APSTOP` | Create/Update suppliers | `SetupAdd/Update` | `SetupApsTos` | `SetupApsTos` |
| GL Journal | `GLMPST` | Post GL journal entry | `Transaction` | `SetupGlmPst` | `GlJournals` |
| Job Receipt | `WIPTJR` | Manufacturing job receipt | `Transaction` | `SetupWipTjr` | `JobReceipts` |

### Query Business Objects (Read Single Record)

| Business Object | Code | Purpose | Key Filter |
|----------------|------|---------|-----------|
| Sales Order Query | `SORQRY` | Query SO details | `<SalesOrder>000123</SalesOrder>` |
| Inventory Query | `INVQRY` | Query stock levels | `<StockCode>A100</StockCode>` |
| Customer Query | `ARSQRY` | Query customer details | `<Customer>0000100</Customer>` |
| Price Query | `SALPQY` | Get selling price | `<StockCode>A100</StockCode><Customer>0000100</Customer>` |
| Purchase Order Query | `PORQRY` | Query PO details | `<PurchaseOrder>PO001</PurchaseOrder>` |
| Supplier Query | `APSQRY` | Query supplier details | `<Supplier>SUP001</Supplier>` |
| GL Account Query | `GLMQRY` | Query GL balance | `<GlCode>1000</GlCode>` |

### Browse Business Objects (List/Search)

| Business Object | Code | Purpose | Filter Options |
|----------------|------|---------|---------------|
| Stock Browse | `INVSBR` | List stock codes | Description, Product Class, Warehouse |
| Customer Browse | `ARSCBR` | List customers | Name, Branch, Salesperson |
| SO Browse | `SORBRS` | List sales orders | Customer, Date Range, Status |
| PO Browse | `PORBRS` | List purchase orders | Supplier, Date Range, Status |
| Supplier Browse | `APSBRS` | List suppliers | Name, City |

---

## 3.5 e.net Method Types — When to Use Which

| Method | What It Does | When to Use | Performance |
|--------|-------------|-------------|-------------|
| `Logon` | Authenticate, get SessionId | First call always | 500-2000ms |
| `Logoff` | Release session + license | Cleanup (always in finally) | 50-200ms |
| `SetupAdd` | Create a new master record | New customer, new stock item | 300-1500ms |
| `SetupUpdate` | Update existing master record | Change customer address | 200-1000ms |
| `Transaction` | Create/post a transaction document | Sales order, inventory receipt | 500-3000ms |
| `TransactionBuild` | Build multi-part document incrementally | Complex orders built line-by-line | 100-500ms per call |
| `TransactionPost` | Post a document built with Build | Finalize built document | 500-2000ms |
| `Query` | Get one record by key | Single SO/customer/item lookup | 100-500ms |
| `Browse` | Search/list records with filters | List of customers, search items | 200-1000ms |
| `Fetch` | Get next page of Browse results | Pagination through large lists | 100-300ms |

### Build vs Transaction — When to Use Build

```
SIMPLE SCENARIO (use Transaction — one call):
──────────────────────────────────────────────
You have the entire order ready:
  Transaction(sessionId, "SORTOI", paramsXml, docXml)
  → Returns result in one call
  → Best for: orders with known lines, max ~20 lines

COMPLEX SCENARIO (use TransactionBuild + TransactionPost):
─────────────────────────────────────────────────────────

You're building the order dynamically (conditional lines, user adding lines one by one):

  // Set up parameters
  TransactionBuild(sessionId, "SORTOI", paramsXml, headerXml)
  
  // Add lines one by one (maybe from different sources)
  foreach (var line in lines) {
      TransactionBuild(sessionId, "SORTOI", "", lineXml)
      // Check intermediate response for warnings
  }
  
  // Now post the entire document
  TransactionPost(sessionId, "SORTOI")
  → Posts everything that was built
  → Best for: interactive order entry, complex multi-source orders, 100+ lines

KEY DIFFERENCES:
  • Build accumulates in SYSPRO memory (not yet committed to DB)
  • Post commits everything atomically
  • If connection drops during Build, nothing is committed
  • Build lets you check intermediate validation before committing
```

---

## 3.6 e.net vs SYSPRO REST API — Which to Use

SYSPRO 8+ introduced **SYSPRO RESTful Services** alongside e.net. Here's a complete comparison:

```
┌──────────────────────────────────────────────────────────────────┐
│              e.net vs SYSPRO REST API COMPARISON                  │
├──────────────────┬──────────────────┬────────────────────────────┤
│ Feature          │ e.net (XML/WCF)  │ REST API (JSON/HTTP)       │
├──────────────────┼──────────────────┼────────────────────────────┤
│ Available since  │ SYSPRO 7         │ SYSPRO 8                   │
│ Data format      │ XML in/out       │ JSON in/out                │
│ Protocol         │ WCF (TCP/HTTP)   │ HTTP/HTTPS                 │
│ Authentication   │ Session-based    │ OAuth 2.0 / Bearer Token   │
│ Business objects │ ALL BOs (full)   │ SUBSET (growing)           │
│ Maturity         │ 10+ years        │ Relatively new             │
│ Documentation    │ Extensive        │ Growing                    │
│ Custom BOs       │ ✅ Supported     │ ⚠ Limited                 │
│ Complexity       │ Higher (XML)     │ Lower (JSON)               │
│ Language support │ .NET (COM/WCF)   │ Any (HTTP)                 │
│ Performance      │ Slightly faster  │ Slightly slower (HTTP)     │
│ Cross-platform   │ ⚠ WCF mainly    │ ✅ Any platform            │
│ Community        │ Larger           │ Growing                    │
├──────────────────┼──────────────────┼────────────────────────────┤
│ RECOMMENDATION   │ Use for SYSPRO 7 │ Use for SYSPRO 8+          │
│                  │ or when you need │ new projects, if BO        │
│                  │ ALL business     │ coverage is sufficient     │
│                  │ objects          │                            │
└──────────────────┴──────────────────┴────────────────────────────┘
```

### REST API Example (SYSPRO 8+)

```csharp
// Login via REST
var loginResponse = await httpClient.PostAsync(
    "http://syspro-server/SYSPRORestApi/v1/api/logon",
    new StringContent(JsonSerializer.Serialize(new {
        Operator = "API_USER",
        OperatorPassword = "password",
        CompanyId = "A"
    }), Encoding.UTF8, "application/json"));

var token = await loginResponse.Content.ReadAsStringAsync();

// Create Sales Order via REST  
httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token);

var soResponse = await httpClient.PostAsync(
    "http://syspro-server/SYSPRORestApi/v1/api/salesorders",
    new StringContent(JsonSerializer.Serialize(new {
        Customer = "0000100",
        OrderDate = "2024-03-15",
        Lines = new[] {
            new { StockCode = "A100", OrderQty = 10, Price = 25.50 }
        }
    }), Encoding.UTF8, "application/json"));

// Response is JSON, not XML!
var result = await soResponse.Content.ReadFromJsonAsync<SalesOrderResult>();
```

**When to use REST vs e.net:**
- If SYSPRO 7 → e.net only (no REST API exists)
- If SYSPRO 8+ and all needed BOs are available in REST → use REST (simpler)
- If SYSPRO 8+ but you need a BO not yet in REST → use e.net for that BO
- If you need to support both SYSPRO 7 and 8 → use e.net (works on both)

---

## 3.7 Error Classification — Complete Guide

```
┌──────────────────────────────────────────────────────────────────┐
│                 ERROR CLASSIFICATION MATRIX                       │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  TYPE 1: VALIDATION ERROR (DO NOT RETRY)                         │
│  ────────────────────────────────────────                         │
│  Source: Business rule violation                                  │
│  Examples:                                                        │
│  • "Customer 0000100 is on credit hold"                          │
│  • "Stock code A100 not on file"                                 │
│  • "Insufficient stock for A100. Available: 3"                   │
│  • "Price cannot be zero"                                        │
│  • "Customer PO number already exists on SO 000098"              │
│  • "Warehouse WH99 is not valid"                                 │
│  Action: Return error to user, let them fix and resubmit         │
│  HTTP Status: 400 Bad Request                                    │
│                                                                   │
│  TYPE 2: SESSION ERROR (RETRY WITH NEW SESSION)                  │
│  ──────────────────────────────────────────────                   │
│  Source: Session expired / invalid                                │
│  Examples:                                                        │
│  • "Invalid Logon ID"                                            │
│  • "Session has expired"                                         │
│  • "Operator is not authorized for e.net"                        │
│  Action: Invalidate session, acquire new one, retry ONCE         │
│  HTTP Status: 502 Bad Gateway (if retry also fails)              │
│                                                                   │
│  TYPE 3: INFRASTRUCTURE ERROR (RETRY WITH BACKOFF)               │
│  ─────────────────────────────────────────────────                │
│  Source: Network / server issues                                  │
│  Examples:                                                        │
│  • TCP connection refused (SYSPRO server down)                   │
│  • WCF timeout (SYSPRO overloaded)                               │
│  • HTTP 500 from SYSPRO REST API                                 │
│  • "Could not connect to SYSPRO Application Server"             │
│  Action: Retry with exponential backoff (2s, 4s, 8s)            │
│  HTTP Status: 503 Service Unavailable                            │
│                                                                   │
│  TYPE 4: LICENSE ERROR (DO NOT RETRY IMMEDIATELY)                │
│  ────────────────────────────────────────────────                 │
│  Source: All e.net license seats consumed                         │
│  Examples:                                                        │
│  • "No e.net license available"                                  │
│  • "Maximum number of concurrent sessions reached"               │
│  Action: Wait 30-60 seconds, then retry (a session might free)   │
│  HTTP Status: 503 with Retry-After header                        │
│                                                                   │
│  TYPE 5: DATA ERROR (HANDLE CASE-BY-CASE)                        │
│  ─────────────────────────────────────────                        │
│  Source: Record not found or locked                               │
│  Examples:                                                        │
│  • "Sales Order 000123 not found"                                │
│  • "Record is locked by another user"                            │
│  • "Customer 0000100 does not exist"                             │
│  Action:                                                          │
│  • Not found → Return 404 to caller                             │
│  • Locked → Wait and retry (another user has it open)            │
│  HTTP Status: 404 or 409                                         │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

### Error Detection in C#

```csharp
public enum SysproErrorType
{
    None,
    BusinessValidation,    // Do NOT retry
    SessionExpired,        // Retry with new session
    Infrastructure,        // Retry with backoff
    LicenseExhausted,      // Wait then retry
    RecordNotFound,        // Return 404
    RecordLocked,          // Wait then retry
    Unknown                // Log and alert
}

public static SysproErrorType ClassifyError(string xmlResponse)
{
    if (string.IsNullOrWhiteSpace(xmlResponse))
        return SysproErrorType.Infrastructure;

    var doc = XDocument.Parse(xmlResponse);
    var errors = doc.Descendants("ErrorDescription")
        .Select(e => e.Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToList();
    
    var warnings = doc.Descendants("WarningDescription")
        .Select(e => e.Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToList();

    if (!errors.Any() && !warnings.Any())
        return SysproErrorType.None;

    var allMessages = string.Join(" | ", errors.Concat(warnings));
    
    // Session errors
    if (allMessages.Contains("Invalid Logon", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("session", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("expired", StringComparison.OrdinalIgnoreCase))
        return SysproErrorType.SessionExpired;

    // License errors
    if (allMessages.Contains("license", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("concurrent sessions", StringComparison.OrdinalIgnoreCase))
        return SysproErrorType.LicenseExhausted;

    // Not found
    if (allMessages.Contains("not on file", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        return SysproErrorType.RecordNotFound;

    // Locked
    if (allMessages.Contains("locked", StringComparison.OrdinalIgnoreCase))
        return SysproErrorType.RecordLocked;

    // Known validation patterns
    if (allMessages.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("exceeds", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("insufficient", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("required field", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("not valid", StringComparison.OrdinalIgnoreCase) ||
        allMessages.Contains("cannot be", StringComparison.OrdinalIgnoreCase))
        return SysproErrorType.BusinessValidation;

    return SysproErrorType.Unknown;
}
```

---

## 3.8 SYSPRO Workflow Services — Event Triggers

SYSPRO has its own workflow engine that can trigger actions within SYSPRO when certain events happen. Your integration should be **aware** of these:

```
┌──────────────────────────────────────────────────────────────┐
│                SYSPRO WORKFLOW TRIGGERS                        │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  SYSPRO can trigger actions when:                            │
│  • Sales order is created → Send email to warehouse          │
│  • Sales order exceeds $10K → Require manager approval       │
│  • Customer goes over credit limit → Suspend order           │
│  • Invoice is posted → Send PDF to customer email            │
│  • Stock falls below reorder point → Create purchase req     │
│  • GRN is posted → Notify purchasing department              │
│                                                               │
│  WHY THIS MATTERS FOR YOUR INTEGRATION:                      │
│  ───────────────────────────────────────                      │
│  1. Your e.net calls TRIGGER these workflows                 │
│     • If you create an SO via e.net, the workflow fires      │
│     • This is GOOD — consistent behavior                    │
│                                                               │
│  2. Workflows might BLOCK your transactions                  │
│     • If a workflow requires approval, your SO gets           │
│       status 9 (Suspended) instead of 1 (Open)              │
│     • Your code must handle this case                        │
│                                                               │
│  3. Workflows might slow down your transactions              │
│     • If workflow sends email, it adds 1-3 seconds           │
│     • For batch processing, this adds up                     │
│                                                               │
│  4. You CANNOT bypass workflows via e.net                    │
│     • This is by design — ensures business rule consistency  │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

---

## 3.9 Custom Business Objects

SYSPRO allows companies to create **custom business objects** for company-specific logic. Your integration may need to call these.

### How Custom BOs Work

```
Standard BO:  SORTOI (Sales Order Transaction Input)
              → Defined by SYSPRO, universal across all installations

Custom BO:    ZORD01 (Custom Order Validator)
              → Defined by THIS company, specific to their business
              → Uses same e.net methods (Transaction, Query, etc.)
              → Has its own XML schema (documented by the implementer)
```

### Calling a Custom BO

```csharp
// Custom BOs are called the same way as standard BOs
// The BO code and XML schema are defined by the SYSPRO implementer

var response = await _enetClient.TransactionAsync(
    sessionId,
    "ZORD01",    // Custom BO code (Z prefix is convention)
    "<Parameters><ValidateOnly>N</ValidateOnly></Parameters>",
    "<CustomOrder><OrderRef>REF-001</OrderRef>...</CustomOrder>"
);
```

**Key points about custom BOs:**
- Usually prefixed with `Z` (e.g., ZORD01, ZCUST01)
- XML schema varies per customer — always get documentation from the SYSPRO implementer
- Same authentication and session management as standard BOs
- Can call multiple standard BOs internally (e.g., create customer + create SO in one call)

---

[← Back to Main Guide](../README.md) | [Previous: Integration Architecture](./02-INTEGRATION-ARCHITECTURE.md) | [Next: .NET Implementation →](./04-DOTNET-IMPLEMENTATION.md)

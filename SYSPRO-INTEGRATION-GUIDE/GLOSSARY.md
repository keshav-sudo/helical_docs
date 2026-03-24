# 📚 SYSPRO Glossary — Terms You'll See Everywhere

> Quick reference for terms used in SYSPRO documentation and code

---

## A

| Term | Full Form | Meaning |
|------|-----------|---------|
| **AP** | Accounts Payable | Money you owe to suppliers |
| **AR** | Accounts Receivable | Money customers owe you |
| **ARSTOP** | AR Setup Operator | Business Object to create/update customers |

## B

| Term | Full Form | Meaning |
|------|-----------|---------|
| **BO** | Business Object | A SYSPRO component that handles one entity (Order, Customer, etc.) |
| **BOM** | Bill of Materials | Recipe/formula for manufacturing a product |

## C

| Term | Full Form | Meaning |
|------|-----------|---------|
| **Company** | — | SYSPRO's term for a separate database/business unit |
| **Costing Method** | — | How SYSPRO calculates inventory value (Average, FIFO, etc.) |

## E

| Term | Full Form | Meaning |
|------|-----------|---------|
| **e.net** | SYSPRO e.net Solutions | The API gateway for programmatic access to SYSPRO |
| **EDI** | Electronic Data Interchange | Standard format for B2B documents (850=PO, 855=Ack, etc.) |

## G

| Term | Full Form | Meaning |
|------|-----------|---------|
| **GL** | General Ledger | The core accounting system |
| **GRN** | Goods Received Note | Document recording receipt of inventory |

## I

| Term | Full Form | Meaning |
|------|-----------|---------|
| **InvMaster** | Inventory Master | Main table for stock items |
| **InvWarehouse** | Inventory by Warehouse | Stock levels per warehouse |
| **INVQRY** | Inventory Query | Business Object for reading inventory |

## L

| Term | Full Form | Meaning |
|------|-----------|---------|
| **Logon/Logoff** | — | Session start/end in e.net. Returns SessionId |

## M

| Term | Full Form | Meaning |
|------|-----------|---------|
| **MRP** | Material Requirements Planning | Planning module for manufacturing |

## O

| Term | Full Form | Meaning |
|------|-----------|---------|
| **Operator** | — | SYSPRO's term for a user account |

## P

| Term | Full Form | Meaning |
|------|-----------|---------|
| **PO** | Purchase Order | Order placed with a supplier |
| **PORTOI** | PO Release Transaction Input | Business Object to create POs |

## Q

| Term | Full Form | Meaning |
|------|-----------|---------|
| **Query** | — | e.net method for reading data (SELECT) |

## S

| Term | Full Form | Meaning |
|------|-----------|---------|
| **SessionId** | — | GUID returned after Logon. Used for all e.net calls |
| **SO** | Sales Order | Order from a customer |
| **SorMaster** | Sales Order Master | Main header table for sales orders |
| **SorDetail** | Sales Order Detail | Line items for sales orders |
| **SORTOI** | SO Release Transaction Input | Business Object to create sales orders |
| **StockCode** | — | Unique identifier for an inventory item |

## T

| Term | Full Form | Meaning |
|------|-----------|---------|
| **Transaction** | — | e.net method for write operations (INSERT/UPDATE) |
| **TrnType** | Transaction Type | Code indicating type of inventory movement |

## W

| Term | Full Form | Meaning |
|------|-----------|---------|
| **Warehouse** | — | Physical or logical location for inventory |
| **WIP** | Work in Progress | Manufacturing module for jobs in production |

---

## Common Business Object Codes

| Code | Purpose |
|------|---------|
| `INVQRY` | Query inventory |
| `SORTOI` | Create/update Sales Order |
| `SORQRY` | Query Sales Orders |
| `ARSTOP` | Create/update Customer |
| `ARSQRY` | Query Customers |
| `PORTOI` | Create/update Purchase Order |
| `PORQRY` | Query Purchase Orders |
| `WIPTOI` | Create/update WIP Job |

---

## Common Status Codes

### Sales Order Status
| Code | Meaning |
|------|---------|
| 1 | Open |
| 2 | In Progress |
| 3 | Partially Shipped |
| 4 | Complete |
| 8 | Cancelled |
| 9 | Suspended |

### Order Type
| Code | Meaning |
|------|---------|
| O | Order |
| Q | Quote |
| B | Blanket Order |
| S | Scheduled Order |
| C | Credit Note |

---

## Table Prefixes

| Prefix | Module |
|--------|--------|
| `Sor*` | Sales Orders |
| `Por*` | Purchase Orders |
| `Inv*` | Inventory |
| `Ar*` | Accounts Receivable (Customers) |
| `Ap*` | Accounts Payable (Suppliers) |
| `Gl*` | General Ledger |
| `Bom*` | Bill of Materials |
| `Wip*` | Work in Progress |

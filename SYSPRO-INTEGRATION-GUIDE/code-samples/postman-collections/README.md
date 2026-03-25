# SYSPRO e.net Postman Collections

Ready-to-use Postman collections for testing SYSPRO e.net API.

## 📁 Files

| File | Description |
|------|-------------|
| `SYSPRO-enet-API.postman_collection.json` | Complete API collection |
| `SYSPRO-enet-Environment.postman_environment.json` | Environment template |

## 🚀 Quick Setup

### Step 1: Import into Postman

1. Open Postman
2. Click **Import** (top-left)
3. Drag both `.json` files into the import window
4. Click **Import**

### Step 2: Configure Environment

1. Go to **Environments** (sidebar)
2. Select **SYSPRO e.net Environment**
3. Fill in your values:
   - `baseUrl`: Your SYSPRO server URL (e.g., `http://192.168.1.100:30000`)
   - `operator`: Your SYSPRO username (e.g., `ADMIN`)
   - `password`: Your password
   - `companyId`: Target company (e.g., `A`, `T`)
4. Click **Save**
5. Select this environment from dropdown (top-right)

### Step 3: Test Connection

1. Open collection: **SYSPRO e.net API Collection**
2. Run **1. Authentication > Login**
3. If successful, `sessionId` is auto-saved!
4. Try other requests...
5. **Always run Logout when done!** ⚠️

## 📋 Collection Contents

### 1. Authentication
- **Login** - Get session, auto-saves `sessionId`
- **Logout** - Release session (free license!)

### 2. Inventory
- Query single item (INVQRY)
- Query all items
- Post inventory movement (INVTMM)

### 3. Customers
- Query customer (ARSQRY)
- Search by name
- Create customer (ARSTOP)

### 4. Sales Orders
- Query order (SORQRY)
- Query by customer
- Create order (SORTOI)
- Update order (SORTOI)

### 5. Purchase Orders
- Query PO (PORQRY)
- Create PO (PORTOR)

### 6. Work Orders
- Query job (WIPQRY)
- Create job (WIPTJB)

### 7. Health & Diagnostics
- Test connection (ping)
- Get server info

## 💡 Tips

### 1. Session Management
```
┌─────────────────────────────────────────────────┐
│  ALWAYS:                                        │
│  1. Run Login FIRST                             │
│  2. Do your testing                             │
│  3. Run Logout LAST                             │
│                                                 │
│  If you forget to logout:                       │
│  • Session stays open for 20 minutes            │
│  • Uses a license the whole time!               │
└─────────────────────────────────────────────────┘
```

### 2. Testing in Correct Company
- Change `companyId` in environment to switch companies
- Must **logout and login again** after changing company
- Session is tied to company at login time

### 3. Error Responses
If you see XML like:
```xml
<ErrorMessage>Session has expired</ErrorMessage>
```
Just run **Login** again to get a new session.

### 4. Using Collection Variables
The collection auto-saves `sessionId` after login. All other requests use `{{sessionId}}` automatically.

## ⚠️ Common Issues

| Issue | Solution |
|-------|----------|
| "Connection refused" | Check `baseUrl` and server is running |
| "Invalid operator" | Check `operator` and `password` |
| "Session expired" | Run Login again |
| "Company not found" | Verify `companyId` exists |
| "No available licenses" | Wait or ask admin to free licenses |

## 🔗 Related Documentation

- [../docs/03-ENET-SOLUTIONS.md](../../docs/03-ENET-SOLUTIONS.md) - e.net API reference
- [../docs/14-SYSPRO-ERROR-CODES.md](../../docs/14-SYSPRO-ERROR-CODES.md) - Error code lookup
- [../basic-enet-client/](../basic-enet-client/) - C# client code

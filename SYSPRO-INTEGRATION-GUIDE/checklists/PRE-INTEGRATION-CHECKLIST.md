# 📋 Pre-Integration Checklist

> Complete this checklist BEFORE starting your integration project

---

## ✅ Phase 1: Information Gathering

### SYSPRO Access & Credentials

| Item | Value | Status |
|------|-------|--------|
| SYSPRO Version | _______ (e.g., 8.0, 7.0) | ⬜ |
| e.net Solutions URL | _______ | ⬜ |
| Company ID | _______ | ⬜ |
| Test Operator Code | _______ | ⬜ |
| Test Operator Password | _______ | ⬜ |
| Default Warehouse Code | _______ | ⬜ |

### Database Access (Read-Only)

| Item | Value | Status |
|------|-------|--------|
| SQL Server Name | _______ | ⬜ |
| Database Name | _______ | ⬜ |
| Read-Only Username | _______ | ⬜ |
| Read-Only Password | _______ | ⬜ |

### Network & Firewall

| Check | Status |
|-------|--------|
| Can reach SYSPRO server from dev machine? | ⬜ |
| Firewall rules allow HTTP/HTTPS to e.net port? | ⬜ |
| VPN access configured (if needed)? | ⬜ |
| SQL Server port (1433) accessible? | ⬜ |

---

## ✅ Phase 2: Environment Setup

### Development Machine

| Item | Status |
|------|--------|
| .NET 8 SDK installed | ⬜ |
| VS Code / Visual Studio installed | ⬜ |
| Docker Desktop installed | ⬜ |
| Git installed | ⬜ |
| SQL Server Management Studio (or Azure Data Studio) | ⬜ |
| Postman / Insomnia (API testing) | ⬜ |

### Project Structure Created

| Item | Status |
|------|--------|
| Git repository initialized | ⬜ |
| Solution file created | ⬜ |
| appsettings.json configured | ⬜ |
| .gitignore includes sensitive files | ⬜ |
| README.md with setup instructions | ⬜ |

---

## ✅ Phase 3: First Connection Test

### Tests to Pass

| Test | Command/Action | Status |
|------|---------------|--------|
| Connect to SYSPRO SQL | Run `SELECT 1` via SSMS | ⬜ |
| Query InvMaster table | `SELECT TOP 10 * FROM InvMaster` | ⬜ |
| e.net Logon | POST to `/saborw/Logon` | ⬜ |
| e.net Query | INVQRY business object | ⬜ |
| e.net Logoff | Release session | ⬜ |

---

## ✅ Phase 4: Code Samples Working

| Sample | File | Status |
|--------|------|--------|
| Basic e.net client | `SysproEnetClient.cs` | ⬜ |
| Inventory query | `InventoryService.cs` | ⬜ |
| Session pooling | `SysproSessionPool.cs` | ⬜ |
| Error handling | Global exception handler | ⬜ |
| Health check endpoint | `/health` returns OK | ⬜ |

---

## 📝 Notes Section

```
Date Started: _______________
SYSPRO Admin Contact: _______________
IT Contact: _______________
Project Deadline: _______________

Notes:
_________________________________________
_________________________________________
_________________________________________
```

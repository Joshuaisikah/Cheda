# Cheda — M-Pesa Finance Tracker for Android

Cheda (Swahili: *to watch closely*) is a personal finance app built for Kenya. It reads your M-Pesa SMS confirmations and turns them into a clean spending history — no manual entry, no bank integration required.

---

## Features

### Automatic Transaction Import
- Reads M-Pesa SMS in real time as they arrive (`SmsBroadcastReceiver`)
- Auto-scans on every app open and resume to catch any messages the receiver missed
- Handles all known M-Pesa sender IDs: `MPESA`, `M-PESA`, `M-PESA APP`, `22141`
- Full historical inbox import from the Settings page or XML backup file
- Dual-SIM aware — tracks which SIM sent/received each transaction

### Transaction Types Parsed
| Type | Description |
|------|-------------|
| Sent | Person-to-person send |
| Received | Incoming money |
| PaidTill | Buy Goods (till number) |
| PaidPaybill | Paybill payment (KPLC, Zuku, etc.) |
| Withdrawn | Agent cash withdrawal |
| Deposit | Agent cash deposit |
| Airtime | Airtime top-up |
| Fuliza | Fuliza M-PESA drawdown / repayment |
| MShwari | M-Shwari locked savings |
| KcbMpesa | KCB M-Pesa savings |
| Zidii | Zidii savings product |
| Reversal | Transaction reversal |
| Unknown | Fallback — stored for manual review |

Equity Bank SMS is also parsed (`EquityBankParser`).

### Smart Categorization
- 40+ Kenya-specific categories (Matatu/Bus Fare, Mama Mboga/Market, Boda/Taxi, SHA/NHIF, Chama/SACCO, etc.)
- Rule-based categorizer with learned mappings — corrections you make teach the app
- Confidence scoring: amount-band and time-of-day signals feed soft confidence inputs
- Uncategorized transactions surface as a review queue on the Dashboard

### Analytics
- **Overview tab** — savings rate, income vs expenses, daily spend, top category, M-Pesa fees, savings account balances
- **Trends tab** — monthly income/expense bar chart for the selected period
- **Categories tab** — donut chart with top-10 categories and percentage breakdown
- **Payees tab** — top counterparties by frequency and volume
- Six time periods: Last 7 Days, This Month, Last 30 Days, Last 3 Months, Last Year, All Time

### Budgets & Bills
- Monthly budget per category with 75% / 90% / overspent alert levels
- Recurring bill tracking

### Notifications
- Real-time alerts for new transactions, large transactions, Fuliza drawdowns, budget breaches
- Terracotta accent colour, emoji-prefixed titles (⬆️ sent, ⬇️ received, ⚠️ large, ⚡ Fuliza, 🔴🟠🟡 budgets)
- Three separate Android notification channels: M-PESA Alerts, Budget Warnings, Daily Summary
- Quiet hours, daily cap, per-type toggles
- Tap notification to open app directly

### Security
- PIN lock with bcrypt hashing
- Biometric (fingerprint) unlock
- Configurable lock delay (immediate / 1 min / 5 min / 15 min)
- SQLite database encrypted with a PIN-derived key via `IDatabaseKeyProvider`
- Biometric key stored in Android Keystore (`SecureBiometricKeyStore`)

---

## Architecture

```
Cheda.slnx
├── src/
│   ├── Cheda.Core/          # Platform-agnostic business logic
│   │   ├── Models/          # Transaction, Budget, Bill, NotificationSettings
│   │   ├── Parsing/         # ISourceParser, MpesaParser, EquityBankParser
│   │   ├── Categorization/  # RuleBasedCategorizer, DefaultCategories (40+)
│   │   ├── Analytics/       # AnalyticsEngine, DateRange, TrendGranularity
│   │   ├── Budgets/         # BudgetEngine, AlertLevel
│   │   ├── Bills/           # BillEngine
│   │   ├── Notifications/   # AlertEvaluator, AlertCoordinator, NotificationSettings
│   │   ├── Sms/             # ImportService, ISmsReader, IImportService
│   │   └── Security/        # PinHashService, IAppLockService
│   │
│   └── Cheda.App/           # .NET MAUI Android app
│       ├── Pages/
│       │   ├── Lock/        # PIN + biometric auth screen
│       │   ├── Onboarding/  # First-run PIN setup
│       │   ├── Dashboard/   # Balance, recent transactions, insights strip
│       │   ├── Transactions/ # Full history, filters, transaction edit/categorize
│       │   ├── Review/      # Uncategorized / low-confidence queue
│       │   ├── Analytics/   # 4-tab analytics (Overview, Trends, Categories, Payees)
│       │   ├── Plan/        # Budgets + recurring bills
│       │   └── Settings/    # SMS scan, XML import, PIN change, biometric toggle
│       ├── Platforms/Android/
│       │   ├── Sms/         # AndroidSmsReader (inbox), SmsBroadcastReceiver (real-time)
│       │   ├── Notifications/ # AndroidNotificationService, DigestScheduler
│       │   └── Security/    # AndroidBiometricService
│       ├── Storage/         # SQLite via sqlite-net-pcl, all repositories
│       └── Controls/        # DonutChartView (ICanvas-based, no SkiaSharp)
│
└── tests/
    └── Cheda.Tests/         # 232 unit tests, xUnit + FluentAssertions
        ├── MpesaParserTests
        ├── CategorizerTests
        ├── AnalyticsEngineTests
        ├── BudgetEngineTests
        ├── NotificationTests (AlertEvaluator, AlertCoordinator)
        ├── ImportServiceTests
        └── ...
```

### Key Design Decisions

**Core is fully platform-agnostic.** `Cheda.Core` has zero Android dependencies — all I/O surfaces as interfaces (`ISmsReader`, `INotificationService`, `ITransactionRepository`). This keeps the 232-test suite fast and runnable anywhere.

**Background threading via `ViewModelBase.RunAsync`.** All data loads run on `Task.Run` so the UI thread is never blocked by SQLite reads. Property changes from background threads are safe in MAUI Android because the binding infrastructure handles dispatch.

**Dedup by transaction code.** The M-Pesa transaction code (e.g. `QFT8R5X2A1`) is the unique key for deduplication, not message timestamp. Re-scanning the full inbox never creates duplicates.

**CommandParameter always arrives as `string`.** MAUI XAML always passes `CommandParameter` as `string`. Any `RelayCommand<T>` with `T != string` will throw. All tab/period selection commands accept `string` and parse with `int.TryParse`.

---

## Requirements

| Requirement | Version |
|-------------|---------|
| .NET | 10.0 |
| .NET MAUI | 10.x |
| Android | 8.0+ (API 26+) |
| CommunityToolkit.Mvvm | 8.4.0 |
| sqlite-net-pcl | 1.9.172 |

---

## Building

```bash
# Run tests
dotnet test tests/Cheda.Tests/

# Build Android APK
dotnet build src/Cheda.App/Cheda.App.csproj -f net10.0-android -c Release

# Build and install on a connected device
dotnet build src/Cheda.App/Cheda.App.csproj -f net10.0-android -c Release \
  -t:Install -p:AdbTarget="-s <device-serial>"

# Find your device serial
adb devices
```

---

## Android Permissions

| Permission | Why |
|------------|-----|
| `READ_SMS` | Scan existing M-Pesa inbox on first run and manual rescan |
| `RECEIVE_SMS` | Catch new M-Pesa confirmations in real time |
| `POST_NOTIFICATIONS` | Transaction alerts and budget warnings (Android 13+) |
| `USE_BIOMETRIC` | Fingerprint unlock |
| `USE_FINGERPRINT` | Fingerprint unlock (legacy API) |

The app never reads OTP or marketing SMS — the parser only processes messages that contain a valid M-Pesa transaction code and confirmation keywords.

---

## SMS Import Pipeline

```
Android SMS Inbox / Real-time Broadcast
        │
        ▼
 AndroidSmsReader / SmsBroadcastReceiver
   (filters by known sender IDs)
        │
        ▼
    ImportService
        │
        ├─ IParserEngine.TryParse()
        │      ├─ MpesaParser  (14 transaction subtypes)
        │      └─ EquityBankParser
        │
        ├─ ICategorizer.Categorize()
        │      └─ RuleBasedCategorizer
        │             ├─ Recipient rules  (phone/account → category)
        │             ├─ Pattern rules    (keyword → category)
        │             └─ Learned mappings (user corrections)
        │
        ├─ Dedup check (transaction code)
        │
        └─ ITransactionRepository.Insert()
                │
                ▼
         SQLite (encrypted)
```

---

## MIUI / Dual-SIM Notes

- The inbox is queried from both `content://sms/inbox` and `content://sms/` (MIUI fallback)
- SIM slot is read from `subscription_id`, `sim_id`, or `sim_slot` columns (ROM-dependent)
- MIUI requires **Auto-start** permission: MIUI Settings → Apps → Cheda → Other permissions → Auto-start → Allow
- Self-transfers between two SIMs on the same device are detected and de-duplicated automatically

---

## Project Status

Active development. Current focus:
- Real-time SMS reliability across OEM ROMs
- Notification polish and budget alert accuracy
- Analytics depth (trends, counterparty patterns)

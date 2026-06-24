using Cheda.App.Storage.Entities;
using Cheda.Core.Categorization;
using SQLite;

namespace Cheda.App.Storage;

/// <summary>
/// Seeds the RecipientRules table with common Kenyan merchants, utilities, and services
/// so the categoriser works well out of the box without requiring manual labelling.
/// Called once by DatabaseService when the rules table is empty (fresh install or reset).
/// </summary>
internal static class KenyaDefaultRules
{
    internal static void SeedIfEmpty(SQLiteConnection db)
    {
        if (db.Table<RecipientRuleEntity>().Count() > 0) return;
        db.InsertAll(Build().Select(RecipientRuleEntity.From));
    }

    private static IEnumerable<RecipientRule> Build()
    {
        int p = 0;

        RecipientRule R(string label, string category, params string[] keywords) =>
            new() { Priority = p++, Label = label, Category = category, Keywords = keywords };

        // ── Utilities ──────────────────────────────────────────────────────
        yield return R("Kenya Power / KPLC",   DefaultCategories.Electricity, "KPLC", "KENYA POWER", "KPLC PREPAID", "KPLC POSTPAID", "888880", "888888");
        yield return R("Nairobi Water",         DefaultCategories.Water,       "NAIROBI WATER", "NCWSC", "NAWASSCO", "915000");
        yield return R("Mombasa Water",         DefaultCategories.Water,       "MOMBASA WATER", "MOWASCO");
        yield return R("Cooking Gas",           DefaultCategories.CookingGas,  "TOTAL GAS", "PRO GAS", "K-GAS", "ORION GAS", "AFRICA GAS");

        // ── Telecom / Internet ─────────────────────────────────────────────
        yield return R("Safaricom",             DefaultCategories.MobileData,  "SAFARICOM", "SCOM");
        yield return R("Airtel Kenya",          DefaultCategories.MobileData,  "AIRTEL", "BHARTI");
        yield return R("Telkom Kenya",          DefaultCategories.MobileData,  "TELKOM", "TELKOM KENYA", "ORANGE");
        yield return R("Zuku / Wananchi",       DefaultCategories.Internet,    "ZUKU", "WANANCHI");
        yield return R("Faiba / JTL",           DefaultCategories.Internet,    "FAIBA", "JTL");
        yield return R("Safaricom Home Fibre",  DefaultCategories.Internet,    "SAFARICOM HOME");
        yield return R("Starlink",              DefaultCategories.Internet,    "STARLINK");

        // ── TV / Streaming ─────────────────────────────────────────────────
        yield return R("DStv / MultiChoice",    DefaultCategories.Subscriptions, "DSTV", "MULTICHOICE", "SHOWMAX", "400200");
        yield return R("GoTV",                  DefaultCategories.Subscriptions, "GOTV");
        yield return R("Netflix",               DefaultCategories.Subscriptions, "NETFLIX");
        yield return R("Spotify",               DefaultCategories.Subscriptions, "SPOTIFY");
        yield return R("YouTube Premium",       DefaultCategories.Subscriptions, "YOUTUBE");
        yield return R("Canal+",                DefaultCategories.Subscriptions, "CANAL");

        // ── Supermarkets / Groceries ──────────────────────────────────────
        yield return R("Naivas",                DefaultCategories.Groceries,   "NAIVAS");
        yield return R("Carrefour",             DefaultCategories.Groceries,   "CARREFOUR", "MAJID");
        yield return R("Quickmart",             DefaultCategories.Groceries,   "QUICKMART", "QUICK MART");
        yield return R("Chandarana",            DefaultCategories.Groceries,   "CHANDARANA");
        yield return R("Eastmatt",              DefaultCategories.Groceries,   "EASTMATT");
        yield return R("Cleanshelf",            DefaultCategories.Groceries,   "CLEANSHELF");
        yield return R("Uchumi",                DefaultCategories.Groceries,   "UCHUMI");
        yield return R("Ottomans",              DefaultCategories.Groceries,   "OTTOMANS", "OTTOMAN");
        yield return R("Tuskys",                DefaultCategories.Groceries,   "TUSKYS");

        // ── Eating Out / Food Delivery ────────────────────────────────────
        yield return R("Glovo",                 DefaultCategories.EatingOut,   "GLOVO");
        yield return R("Jumia Food",            DefaultCategories.EatingOut,   "JUMIA FOOD");
        yield return R("Uber Eats",             DefaultCategories.EatingOut,   "UBER EATS");
        yield return R("KFC Kenya",             DefaultCategories.EatingOut,   "KFC");
        yield return R("Java House",            DefaultCategories.EatingOut,   "JAVA HOUSE", "JAVA");
        yield return R("Pizza Inn / Debonairs",  DefaultCategories.EatingOut,  "PIZZA INN", "DEBONAIRS");
        yield return R("Chicken Inn",           DefaultCategories.EatingOut,   "CHICKEN INN");
        yield return R("Galito's",              DefaultCategories.EatingOut,   "GALITO");
        yield return R("McDonald's Kenya",      DefaultCategories.EatingOut,   "MCDONALDS");

        // ── Ride Hailing / Transport ──────────────────────────────────────
        yield return R("Uber",                  DefaultCategories.BodaTaxi,    "UBER KENYA", "UBER TRIP");
        yield return R("Bolt",                  DefaultCategories.BodaTaxi,    "BOLT");
        yield return R("Little Cab",            DefaultCategories.BodaTaxi,    "LITTLE CAB", "LITTLE RIDE");
        yield return R("inDrive",               DefaultCategories.BodaTaxi,    "INDRIVE");
        yield return R("Kenya Railways",        DefaultCategories.MatatuFare,  "KENYA RAILWAYS", "SGR");

        // ── Fuel Stations ─────────────────────────────────────────────────
        yield return R("Shell / Vivo",          DefaultCategories.Fuel,        "SHELL", "VIVO ENERGY");
        yield return R("Total / TotalEnergies", DefaultCategories.Fuel,        "TOTAL ENERGIES", "TOTALENERGIES");
        yield return R("Rubis",                 DefaultCategories.Fuel,        "RUBIS");
        yield return R("Kenol / Kobil",         DefaultCategories.Fuel,        "KENOL", "KOBIL");
        yield return R("OiLibya",               DefaultCategories.Fuel,        "OILIBYA", "OIL LIBYA");
        yield return R("Hashi",                 DefaultCategories.Fuel,        "HASHI");

        // ── Health ────────────────────────────────────────────────────────
        yield return R("Pharmacy / Chemist",    DefaultCategories.Medical,     "PHARMACY", "CHEMIST", "MEDS", "GOODLIFE", "MEDISEL");
        yield return R("Hospital / Clinic",     DefaultCategories.Medical,     "HOSPITAL", "CLINIC", "DISPENSARY", "NAIROBI HOSPITAL", "AGA KHAN", "M.P. SHAH", "KENYATTA HOSPITAL");
        yield return R("SHA / NHIF",            DefaultCategories.ShaNhif,     "SHA", "NHIF", "NATIONAL HEALTH");

        // ── Insurance ─────────────────────────────────────────────────────
        yield return R("Jubilee Insurance",     DefaultCategories.Insurance,   "JUBILEE");
        yield return R("Britam",                DefaultCategories.Insurance,   "BRITAM");
        yield return R("UAP Old Mutual",        DefaultCategories.Insurance,   "UAP", "OLD MUTUAL");
        yield return R("APA Insurance",         DefaultCategories.Insurance,   "APA INSURANCE");
        yield return R("CIC Insurance",         DefaultCategories.Insurance,   "CIC INSURANCE", "CIC GROUP");
        yield return R("Sanlam Kenya",          DefaultCategories.Insurance,   "SANLAM");
        yield return R("Pioneer Insurance",     DefaultCategories.Insurance,   "PIONEER");

        // ── Savings products ──────────────────────────────────────────────
        yield return R("NSSF",                  DefaultCategories.Savings,       "NSSF", "NATIONAL SOCIAL");
        yield return R("Zidii Savings",         DefaultCategories.ZidiiSavings,  "ZIDII");
        yield return R("KCB M-Pesa Savings",    DefaultCategories.KcbMpesa,      "KCB M-PESA", "KCB MPESA");
        yield return R("M-Shwari",              DefaultCategories.MShwari,       "M-SHWARI", "MSHWARI", "LOCK SAVINGS");
        yield return R("Equity Savings",        DefaultCategories.Savings,       "EQUITY SAVINGS", "EAZZY SAVINGS");

        // ── Banks / Loans ─────────────────────────────────────────────────
        yield return R("KCB Bank",              DefaultCategories.LoanRepayment, "KCB BANK", "KCB LOAN");
        yield return R("Equity Bank",           DefaultCategories.LoanRepayment, "EQUITY BANK", "EAZZY");
        yield return R("Co-op Bank",            DefaultCategories.LoanRepayment, "CO-OP BANK", "COOP BANK");
        yield return R("Stanbic Bank",          DefaultCategories.LoanRepayment, "STANBIC");
        yield return R("ABSA Bank",             DefaultCategories.LoanRepayment, "ABSA");
        yield return R("I&M Bank",              DefaultCategories.LoanRepayment, "I&M BANK");
        yield return R("DTB",                   DefaultCategories.LoanRepayment, "DTB", "DIAMOND TRUST");
        yield return R("NCBA",                  DefaultCategories.LoanRepayment, "NCBA", "NCBA BANK");
        yield return R("HELB",                  DefaultCategories.LoanRepayment, "HELB", "HIGHER EDUCATION LOANS");
        yield return R("Tala / Branch",         DefaultCategories.LoanRepayment, "TALA", "BRANCH", "ZENKA");
        yield return R("Timiza",                DefaultCategories.LoanRepayment, "TIMIZA");
        yield return R("Okolea",                DefaultCategories.LoanRepayment, "OKOLEA");

        // ── Tax / Government ──────────────────────────────────────────────
        yield return R("KRA",                   DefaultCategories.Savings,     "KRA", "KENYA REVENUE", "ITAX");
        yield return R("eCitizen",              DefaultCategories.Savings,     "ECITIZEN", "E-CITIZEN");
        yield return R("NTSA",                  DefaultCategories.Savings,     "NTSA");

        // ── Education ─────────────────────────────────────────────────────
        yield return R("School Fees",           DefaultCategories.SchoolFees,  "SCHOOL", "COLLEGE", "UNIVERSITY", "ACADEMY");
        yield return R("Elimu",                 DefaultCategories.SchoolFees,  "ELIMU");

        // ── Online Shopping ───────────────────────────────────────────────
        yield return R("Jumia Kenya",           DefaultCategories.Shopping,    "JUMIA");
        yield return R("Kilimall",              DefaultCategories.Shopping,    "KILIMALL");
        yield return R("Jiji",                  DefaultCategories.Shopping,    "JIJI");

        // ── Betting ───────────────────────────────────────────────────────
        yield return R("Sportybet",             DefaultCategories.Betting,     "SPORTYBET", "SPORTY");
        yield return R("Betika",                DefaultCategories.Betting,     "BETIKA");
        yield return R("Mozzartbet",            DefaultCategories.Betting,     "MOZZART", "MOZZARTBET");
        yield return R("Odibets",               DefaultCategories.Betting,     "ODIBETS");
        yield return R("Dafabet",               DefaultCategories.Betting,     "DAFABET");
        yield return R("1xBet",                 DefaultCategories.Betting,     "1XBET");
        yield return R("Parimatch",             DefaultCategories.Betting,     "PARIMATCH");
        yield return R("Betway",                DefaultCategories.Betting,     "BETWAY");

        // ── Church / Tithe ────────────────────────────────────────────────
        yield return R("Church / Mosque",       DefaultCategories.Tithe,       "CHURCH", "MOSQUE", "TITHE", "OFFERING");

        // ── M-Pesa transaction fees ───────────────────────────────────────
        // (already handled by parser but keep as fallback)
        yield return R("M-Pesa Fees",           DefaultCategories.MpesaFees,   "TRANSACTION COST", "MPESA FEE");
    }
}

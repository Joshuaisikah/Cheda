namespace Cheda.Core.Categorization;

public static class DefaultCategories
{
    // ── Name constants — use these in code, not raw strings ──────────────────
    public const string Salary           = "Salary/Wages";
    public const string BusinessIncome   = "Business Income";
    public const string ReceivedPersonal = "Received from Family/Friends";
    public const string RefundsReversals = "Refunds/Reversals";
    public const string OtherIncome      = "Other Income";
    public const string Groceries        = "Groceries";
    public const string EatingOut        = "Eating Out/Delivery";
    public const string MamaMboga        = "Mama Mboga/Market";
    public const string MatatuFare       = "Matatu/Bus Fare";
    public const string BodaTaxi         = "Boda/Taxi";
    public const string Fuel             = "Fuel";
    public const string VehicleMaint     = "Vehicle Maintenance";
    public const string Rent             = "Rent";
    public const string Electricity      = "Electricity";
    public const string Water            = "Water";
    public const string Internet         = "Internet/WiFi";
    public const string CookingGas       = "Cooking Gas";
    public const string Airtime          = "Airtime";
    public const string MobileData       = "Mobile Data";
    public const string SchoolFees       = "School Fees";
    public const string Medical          = "Medical/Pharmacy";
    public const string ShaNhif          = "SHA/NHIF";
    public const string FamilySupport    = "Family Support";
    public const string Childcare        = "Childcare/House Help";
    public const string Shopping         = "Shopping/Clothing";
    public const string Entertainment    = "Entertainment";
    public const string Subscriptions    = "Subscriptions";
    public const string PersonalCare     = "Personal Care/Salon";
    public const string Betting          = "Betting/Gambling";
    public const string Savings          = "Savings";
    public const string MShwari          = "M-Shwari/Locked Savings";
    public const string SaccoChama       = "SACCO/Chama";
    public const string LoanRepayment    = "Loan Repayment";
    public const string Fuliza           = "Fuliza";
    public const string Insurance        = "Insurance";
    public const string Investments      = "Investments";
    public const string Tithe            = "Church/Mosque/Tithe";
    public const string Donations        = "Donations/Harambee";
    public const string TransfersPeople  = "Transfers (to people)";
    public const string MpesaFees        = "M-Pesa Charges/Fees";
    public const string Withdrawals      = "Withdrawals (Cash)";
    public const string Uncategorized    = "Uncategorized";

    public static readonly IReadOnlyList<Category> All =
    [
        new(Salary,           CategoryGroup.Income),
        new(BusinessIncome,   CategoryGroup.Income),
        new(ReceivedPersonal, CategoryGroup.Income),
        new(RefundsReversals, CategoryGroup.Income),
        new(OtherIncome,      CategoryGroup.Income),
        new(Groceries,        CategoryGroup.Food),
        new(EatingOut,        CategoryGroup.Food),
        new(MamaMboga,        CategoryGroup.Food),
        new(MatatuFare,       CategoryGroup.Transport),
        new(BodaTaxi,         CategoryGroup.Transport),
        new(Fuel,             CategoryGroup.Transport),
        new(VehicleMaint,     CategoryGroup.Transport),
        new(Rent,             CategoryGroup.Bills),
        new(Electricity,      CategoryGroup.Bills),
        new(Water,            CategoryGroup.Bills),
        new(Internet,         CategoryGroup.Bills),
        new(CookingGas,       CategoryGroup.Bills),
        new(Airtime,          CategoryGroup.Airtime),
        new(MobileData,       CategoryGroup.Airtime),
        new(SchoolFees,       CategoryGroup.PersonalFamily),
        new(Medical,          CategoryGroup.PersonalFamily),
        new(ShaNhif,          CategoryGroup.PersonalFamily),
        new(FamilySupport,    CategoryGroup.PersonalFamily),
        new(Childcare,        CategoryGroup.PersonalFamily),
        new(Shopping,         CategoryGroup.Lifestyle),
        new(Entertainment,    CategoryGroup.Lifestyle),
        new(Subscriptions,    CategoryGroup.Lifestyle),
        new(PersonalCare,     CategoryGroup.Lifestyle),
        new(Betting,          CategoryGroup.Lifestyle),
        new(Savings,          CategoryGroup.Financial),
        new(MShwari,          CategoryGroup.Financial),
        new(SaccoChama,       CategoryGroup.Financial),
        new(LoanRepayment,    CategoryGroup.Financial),
        new(Fuliza,           CategoryGroup.Financial),
        new(Insurance,        CategoryGroup.Financial),
        new(Investments,      CategoryGroup.Financial),
        new(Tithe,            CategoryGroup.Giving),
        new(Donations,        CategoryGroup.Giving),
        new(TransfersPeople,  CategoryGroup.Other),
        new(MpesaFees,        CategoryGroup.Other),
        new(Withdrawals,      CategoryGroup.Other),
        new(Uncategorized,    CategoryGroup.Other),
    ];
}

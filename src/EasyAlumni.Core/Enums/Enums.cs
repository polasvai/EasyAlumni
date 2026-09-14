namespace EasyAlumni.Core.Enums
{
    public enum RegistrationStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Cancelled = 3
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public enum PaymentMode
    {
        bKashManual = 1,
        NagadManual = 2,
        RocketManual = 3,
        BankTransfer = 4,
        Cash = 5,
        bKashOnline = 6,
        SSLCommerz = 7
    }

    public enum TShirtSize
    {
        S = 1,
        M = 2,
        L = 3,
        XL = 4,
        XXL = 5,
        XXXL = 6
    }

    public enum AccountHeadType
    {
        Income = 1,
        Expense = 2
    }

    public enum UserRole
    {
        SuperAdmin,
        Admin,
        Accounts,
        Volunteer,
        Alumni
    }
}

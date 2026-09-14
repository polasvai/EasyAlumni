namespace EasyAlumni.Core.Entities
{
    public class PackageGiftItem
    {
        public int Id { get; set; }

        public int RegistrationPackageId { get; set; }
        public RegistrationPackage? RegistrationPackage { get; set; }

        public int GiftItemId { get; set; }
        public GiftItem? GiftItem { get; set; }
    }
}

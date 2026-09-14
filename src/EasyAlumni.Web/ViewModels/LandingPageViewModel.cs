using EasyAlumni.Core.Entities;

namespace EasyAlumni.Web.ViewModels
{
    public class LandingPageViewModel
    {
        public ReunionEvent? Event { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new();
        public List<Committee> Committees { get; set; } = new();
        public List<NoticePost> Notices { get; set; } = new();
        public List<RegistrationPackage> Packages { get; set; } = new();
        public List<GalleryImage> GalleryImages { get; set; } = new();
        public int TotalRegisteredCount { get; set; }
        public int TotalApprovedCount { get; set; }
    }
}

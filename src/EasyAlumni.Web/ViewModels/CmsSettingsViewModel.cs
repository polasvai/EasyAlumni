using Microsoft.AspNetCore.Http;
using System;

namespace EasyAlumni.Web.ViewModels
{
    public class CmsSettingsViewModel
    {
        // Hero Section
        public string LandingHeroTitle { get; set; } = string.Empty;
        public string LandingHeroSubtitle { get; set; } = string.Empty;
        public string LandingHeroTagline { get; set; } = string.Empty;
        public string LandingHeroBgImage { get; set; } = string.Empty;
        public IFormFile? HeroBgFile { get; set; }

        // About Section
        public string AboutTitle { get; set; } = string.Empty;
        public string AboutSubtitle { get; set; } = string.Empty;
        public string AboutStory { get; set; } = string.Empty;
        public string AboutImageUrl { get; set; } = string.Empty;
        public IFormFile? AboutImageFile { get; set; }

        // Venue & Schedule
        public string EventVenue { get; set; } = string.Empty;
        public string VenueAddress { get; set; } = string.Empty;
        public string GoogleMapEmbedUrl { get; set; } = string.Empty;
        public DateTime? ReunionDate { get; set; }
        public DateTime? RegistrationDeadline { get; set; }

        // Payment Information
        public string ManualBkashNumber { get; set; } = string.Empty;
        public string ManualNagadNumber { get; set; } = string.Empty;
        public string ManualRocketNumber { get; set; } = string.Empty;
        public string PaymentBankDetails { get; set; } = string.Empty;

        // Contact Information
        public string ContactHotline { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;

        // Footer Customization
        public string FooterAboutText { get; set; } = string.Empty;
        public string FooterFacebookUrl { get; set; } = string.Empty;
        public string FooterYoutubeUrl { get; set; } = string.Empty;
        public string FooterWhatsappUrl { get; set; } = string.Empty;
        public string FooterCopyright { get; set; } = string.Empty;
        public string FooterTagline { get; set; } = string.Empty;
        public string FooterMadeWith { get; set; } = "Made with ❤️";

        // Active Tab Tracker
        public string ActiveTab { get; set; } = "hero";
    }
}

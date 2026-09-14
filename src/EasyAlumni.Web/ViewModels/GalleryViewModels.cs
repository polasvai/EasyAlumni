using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EasyAlumni.Web.ViewModels
{
    public class GalleryImageUploadViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; } = string.Empty;

        public string? Caption { get; set; }

        public string Category { get; set; } = "Campus Life";

        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public IFormFile? ImageFile { get; set; }

        public string? ImageUrl { get; set; }
    }

    public class GalleryImageEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; } = string.Empty;

        public string? Caption { get; set; }

        public string Category { get; set; } = "Campus Life";

        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public string? ExistingImagePath { get; set; }

        public IFormFile? NewImageFile { get; set; }
    }
}

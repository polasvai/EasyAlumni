using EasyAlumni.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AlumniProfile> AlumniProfiles => Set<AlumniProfile>();
        public DbSet<ReunionEvent> ReunionEvents => Set<ReunionEvent>();
        public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
        public DbSet<RegistrationPayment> RegistrationPayments => Set<RegistrationPayment>();
        public DbSet<GiftItem> GiftItems => Set<GiftItem>();
        public DbSet<GiftItemSizeStock> GiftItemSizeStocks => Set<GiftItemSizeStock>();
        public DbSet<GiftDistribution> GiftDistributions => Set<GiftDistribution>();
        public DbSet<Committee> Committees => Set<Committee>();
        public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
        public DbSet<Volunteer> Volunteers => Set<Volunteer>();
        public DbSet<AccountHead> AccountHeads => Set<AccountHead>();
        public DbSet<CashEntry> CashEntries => Set<CashEntry>();
        public DbSet<BudgetHead> BudgetHeads => Set<BudgetHead>();
        public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<NoticePost> NoticePosts => Set<NoticePost>();
        public DbSet<RegistrationPackage> RegistrationPackages => Set<RegistrationPackage>();
        public DbSet<PackageGiftItem> PackageGiftItems => Set<PackageGiftItem>();
        public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
        public DbSet<GuestCategory> GuestCategories => Set<GuestCategory>();
        public DbSet<RegistrationGuest> RegistrationGuests => Set<RegistrationGuest>();
        public DbSet<EventCustomQuestion> EventCustomQuestions => Set<EventCustomQuestion>();
        public DbSet<RegistrationQuestionResponse> RegistrationQuestionResponses => Set<RegistrationQuestionResponse>();
        public DbSet<RegistrationGiftChoice> RegistrationGiftChoices => Set<RegistrationGiftChoice>();
        public DbSet<Donation> Donations => Set<Donation>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Indexes
            builder.Entity<AlumniProfile>()
                .HasIndex(a => a.UserCode)
                .IsUnique();

            builder.Entity<EventRegistration>()
                .HasIndex(r => r.RegistrationNo)
                .IsUnique();

            builder.Entity<Donation>()
                .HasIndex(d => d.DonationTrackingNo)
                .IsUnique();

            builder.Entity<SystemSetting>()
                .HasIndex(s => s.SettingKey)
                .IsUnique();

            // Decimal Precisions
            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            // Relationship behaviors to prevent multiple cascade paths in SQL Server
            builder.Entity<AlumniProfile>()
                .HasOne(a => a.ApplicationUser)
                .WithOne(u => u.AlumniProfile)
                .HasForeignKey<AlumniProfile>(a => a.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<EventRegistration>()
                .HasOne(r => r.AlumniProfile)
                .WithMany(a => a.Registrations)
                .HasForeignKey(r => r.AlumniProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EventRegistration>()
                .HasOne(r => r.ReunionEvent)
                .WithMany(e => e.Registrations)
                .HasForeignKey(r => r.ReunionEventId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RegistrationPayment>()
                .HasOne(p => p.EventRegistration)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.EventRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationPayment>()
                .HasOne(p => p.ApprovedByUser)
                .WithMany()
                .HasForeignKey(p => p.ApprovedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<GiftDistribution>()
                .HasOne(d => d.EventRegistration)
                .WithMany(r => r.GiftDistributions)
                .HasForeignKey(d => d.EventRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GiftDistribution>()
                .HasOne(d => d.GiftItem)
                .WithMany(g => g.Distributions)
                .HasForeignKey(d => d.GiftItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiftDistribution>()
                .HasOne(d => d.VolunteerUser)
                .WithMany()
                .HasForeignKey(d => d.VolunteerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<CashEntry>()
                .HasOne(c => c.AccountHead)
                .WithMany(h => h.CashEntries)
                .HasForeignKey(c => c.AccountHeadId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CashEntry>()
                .HasOne(c => c.RelatedRegistration)
                .WithMany()
                .HasForeignKey(c => c.RelatedRegistrationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<CashEntry>()
                .HasOne(c => c.CreatedByUser)
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<BudgetHead>()
                .HasOne(b => b.ReunionEvent)
                .WithMany(e => e.BudgetHeads)
                .HasForeignKey(b => b.ReunionEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BudgetEntry>()
                .HasOne(e => e.BudgetHead)
                .WithMany(h => h.Entries)
                .HasForeignKey(e => e.BudgetHeadId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationPackage>()
                .HasOne(p => p.ReunionEvent)
                .WithMany(e => e.Packages)
                .HasForeignKey(p => p.ReunionEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EventRegistration>()
                .HasOne(r => r.RegistrationPackage)
                .WithMany(p => p.Registrations)
                .HasForeignKey(r => r.RegistrationPackageId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<GalleryImage>()
                .HasOne(g => g.ReunionEvent)
                .WithMany(e => e.GalleryImages)
                .HasForeignKey(g => g.ReunionEventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Dynamic Package Gift Items
            builder.Entity<PackageGiftItem>()
                .HasOne(p => p.RegistrationPackage)
                .WithMany(pkg => pkg.PackageGiftItems)
                .HasForeignKey(p => p.RegistrationPackageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PackageGiftItem>()
                .HasOne(p => p.GiftItem)
                .WithMany()
                .HasForeignKey(p => p.GiftItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Dynamic Guest Categories & Registration Guests
            builder.Entity<GuestCategory>()
                .HasOne(c => c.ReunionEvent)
                .WithMany()
                .HasForeignKey(c => c.ReunionEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationGuest>()
                .HasOne(g => g.EventRegistration)
                .WithMany(r => r.Guests)
                .HasForeignKey(g => g.EventRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationGuest>()
                .HasOne(g => g.GuestCategory)
                .WithMany(c => c.Guests)
                .HasForeignKey(g => g.GuestCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Dynamic Custom Questions & Responses
            builder.Entity<EventCustomQuestion>()
                .HasOne(q => q.ReunionEvent)
                .WithMany()
                .HasForeignKey(q => q.ReunionEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationQuestionResponse>()
                .HasOne(resp => resp.EventRegistration)
                .WithMany(r => r.QuestionResponses)
                .HasForeignKey(resp => resp.EventRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationQuestionResponse>()
                .HasOne(resp => resp.CustomQuestion)
                .WithMany(q => q.Responses)
                .HasForeignKey(resp => resp.EventCustomQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Dynamic Gift Size Choices
            builder.Entity<RegistrationGiftChoice>()
                .HasOne(c => c.EventRegistration)
                .WithMany(r => r.GiftChoices)
                .HasForeignKey(c => c.EventRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RegistrationGiftChoice>()
                .HasOne(c => c.GiftItem)
                .WithMany()
                .HasForeignKey(c => c.GiftItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

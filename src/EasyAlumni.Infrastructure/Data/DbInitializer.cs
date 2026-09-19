using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // 1. Seed Roles
            var roles = new[] { "SuperAdmin", "Admin", "Accounts", "Volunteer", "Alumni" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed SuperAdmin User
            var adminEmail = "admin@easyalumni.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Super Administrator",
                    UserCode = "SUPERADMIN",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 3. Seed Default System Settings
            var defaultSettings = new Dictionary<string, (string Value, string Description)>
            {
                ["SMSAPIURL"] = ("http://esms.rampsbd.com/smsapi?api_key=$api_key&type=text&contacts=$sms_number&senderid=$sender_Id&msg=$sms_text", "HTTP API endpoint for SMS gateway"),
                ["SenderID"] = ("8809601001020", "Approved SMS Sender Mask/ID"),
                ["SmsAPIKey"] = ("C2001271629ef209a6b217.42728096", "SMS Gateway Secret API Key"),
                ["SMSLock"] = ("0", "Set to 1 to pause/lock outbound SMS sending"),
                ["SMSBalanceAPI"] = ("http://esms.rampsbd.com/miscapi/C2001271629ef209a6b217.42728096/getBalance", "URL to fetch SMS credit balance"),
                ["WhatsAppApiKey"] = ("", "Meta WhatsApp Cloud API Bearer Token"),
                ["WhatsAppPhoneId"] = ("", "Meta WhatsApp Phone Number ID"),
                ["EnrollSMS"] = ("Congratulations {Name}! Your alumni enrollment is successful. Your User ID is: {UserId} and password: {Password}", "Template sent on registration"),
                ["PaymentSMS"] = ("Dear {Name}, your payment of {Amount} BDT for {EventName} is APPROVED! Ticket No: {TicketNo}. Digital Pass: {PassUrl}", "Template sent on payment approval"),
                ["PendingPaymentSMS"] = ("Dear {Name}, your registration #{TicketNo} is received. Please pay {Amount} BDT via bKash/Nagad to confirm.", "Template sent on pending registration"),
                ["LandingHeroTitle"] = ("Grand Alumni Reunion 2026", "Main heading on landing page"),
                ["LandingHeroSubtitle"] = ("শহীদ নাজমুল হক বালিকা উচ্চ বিদ্যালয় প্রাক্তন ছাত্রী পুনর্মিলনী উৎসব", "Bengali subheading on landing page"),
                ["LandingHeroTagline"] = ("A grand reunion of cherished memories, heartfelt reunions, and boundless nostalgia. Welcome home, dear alumni!", "Tagline text displayed under hero title"),
                ["LandingHeroBgImage"] = ("https://images.unsplash.com/photo-1523580494863-6f3031224c94?auto=format&fit=crop&w=1920&q=80", "Hero banner background image URL"),
                ["ReunionDate"] = ("2026-12-25T09:00:00", "Date and time of the reunion event"),
                ["EventVenue"] = ("বিদ্যালয় প্রাঙ্গণ ও মিলনায়তন, রাজশাহী", "Location venue for reunion"),
                ["VenueAddress"] = ("Hetem Khan, Boalia, Rajshahi - 6000", "Full street address of the event venue"),
                ["AboutTitle"] = ("About the Grand Reunion", "Title for About section on home page"),
                ["AboutSubtitle"] = ("A historic day where old classmates, mentors, and friends reunite under one grand roof.", "Subtitle for About section"),
                ["AboutStory"] = ("শহীদ নাজমুল হক বালিকা উচ্চ বিদ্যালয় সুদীর্ঘ কাল ধরে নারী শিক্ষা ও ব্যক্তিত্ব বিকাশে অনন্য ভূমিকা রেখে আসছে। আমাদের প্রিয় ক্যাম্পাসে কাটানো সোনালী দিনগুলো আমাদের জীবনের শ্রেষ্ঠ স্মৃতি। এই জমকালো পুনর্মিলনী উৎসবটি সেই মধুর স্মৃতি রোমন্থন, প্রিয় সহপাঠী ও শ্রদ্ধেয় শিক্ষকদের সাথে মিলিত হবার এক মহতী মিলনমেলা।", "Full story / text for About section"),
                ["AboutImageUrl"] = ("https://images.unsplash.com/photo-1511578314322-379afb476865?auto=format&fit=crop&w=800&q=80", "Main photo displayed in About section"),
                ["ManualBkashNumber"] = ("01700000000 (Merchant/Personal)", "bKash number displayed to attendees"),
                ["ManualNagadNumber"] = ("01800000000 (Merchant/Personal)", "Nagad number displayed to attendees"),
                ["ManualRocketNumber"] = ("01900000000-8", "Rocket number displayed to attendees"),
                ["PaymentBankDetails"] = ("Islami Bank Bangladesh Ltd, Rajshahi Branch, A/C: 2050123456789012, Routing: 12581000", "Bank transfer account information"),
                ["ContactHotline"] = ("025888 54512, +880 1711-000000", "Hotline phone numbers for queries"),
                ["ContactEmail"] = ("snhghs_raj@yahoo.com", "Official inquiry email address"),
                ["GoogleMapEmbedUrl"] = ("https://maps.google.com/maps?q=Shahid+Nazmul+Huq+Girls+High+School+Rajshahi&hl=en&z=16&output=embed", "Google Maps iframe embed URL for venue"),
                ["FooterAboutText"] = ("The ultimate event and alumni community management platform. Connecting alumni, celebrating lifelong friendships, and organizing grand reunions seamlessly.", "Brief narrative under logo in footer"),
                ["FooterFacebookUrl"] = ("https://facebook.com", "Facebook page or group link"),
                ["FooterYoutubeUrl"] = ("https://youtube.com", "YouTube channel link"),
                ["FooterCopyright"] = ("© 2026 EasyAlumni Association. All rights reserved.", "Copyright line in footer"),
                ["FooterTagline"] = ("Designed for School, College & University Grand Reunions", "Bottom tagline in footer"),
                ["FooterMadeWith"] = ("Made with ❤️", "Sub-badge text under footer about story"),

                // JanataPay Online Gateway Configuration
                ["JanataPayBaseUrl"] = ("https://sandbox-pg.janatapay.com", "JanataPay API Base URL (e.g. sandbox or live)"),
                ["JanataPayMerchantUid"] = ("6521171673", "JanataPay Merchant UID assigned by Janata Bank"),
                ["JanataPayUsername"] = ("j8Dx4778daD160", "JanataPay API Username"),
                ["JanataPayPassword"] = ("ksD7dNf58X", "JanataPay API Password"),
                ["JanataPayPublicKey"] = ("MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAp0/eVwL7WJG5KFnANQrkDLJkodPvDpVvbi9XuIKI5tJKdyXiTq0whTUpXbBk/Z0fpwDXhdkTBNTi0C5fkyTxkUXc1yLHs0BRxSZQ8DphA0eTOJKsVDsCCLOqYhtWKt7CnSpswDzqj4pdnkstKAzDEdKZyViTJv7G6TVKiK1cyJjuvi0be1ZDqSe/japXmInipFwW5elTHcw2hYOKZsSrNgFfSpsc/rJUlzf3fradw6KdiRlWsQZ/nze4NJ8YsNWs4VVbZfxd6YGd/eNmF8Pl0M+OTESD9fXEMlDQBFRBlVPz+lQzVwriHaZ28O0QfPnT0g6QhcMG62m/O2bdL9fvMQIDAQAB", "JanataPay 2048-bit RSA Gateway Public Key (Base64)"),
                ["JanataPayUseProxy"] = ("1", "Route API requests through Bangladesh SOCKS5 tunnel (1=Yes, 0=No)"),
                ["JanataPaySocks5Proxy"] = ("socks5://127.0.0.1:1080", "SOCKS5 Proxy URL for Bangladesh IP egress"),
                ["JanataPayCallbackBaseUrl"] = ("https://alumni.snhghs.edu.bd", "Callback Base URL for redirects after payment"),

                // Payment Method Activation Toggles & Default Gateway
                ["Payment_Enable_JanataPay"] = ("1", "Enable Janata Bank / JanataPay Online Gateway (1=Enabled, 0=Disabled)"),
                ["Payment_Enable_bKashManual"] = ("1", "Enable Manual bKash Send Money option (1=Enabled, 0=Disabled)"),
                ["Payment_Enable_NagadManual"] = ("1", "Enable Manual Nagad Send Money option (1=Enabled, 0=Disabled)"),
                ["Payment_Enable_RocketManual"] = ("1", "Enable Manual Rocket option (1=Enabled, 0=Disabled)"),
                ["Payment_Enable_BankTransfer"] = ("1", "Enable Direct Bank Transfer option (1=Enabled, 0=Disabled)"),
                ["Payment_Enable_Cash"] = ("1", "Enable Cash Handover to Committee option (1=Enabled, 0=Disabled)"),
                ["Payment_Default_Method"] = ("JanataPay", "Default preselected payment method on registration form")
            };

            foreach (var s in defaultSettings)
            {
                var existingSetting = await context.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == s.Key);
                if (existingSetting == null)
                {
                    context.SystemSettings.Add(new SystemSetting
                    {
                        SettingKey = s.Key,
                        SettingValue = s.Value.Value,
                        Description = s.Value.Description,
                        IsSecret = s.Key.Contains("Key") || s.Key.Contains("Token")
                    });
                }
            }

            // 4. Seed Default Reunion Event if none exists
            var activeReunion = await context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
            if (activeReunion == null)
            {
                var reunion = new ReunionEvent
                {
                    EventTitle = "Shahid Nazmul Huq Girls' High School Alumni Reunion 2026",
                    TitleBangla = "শহীদ নাজমুল হক বালিকা উচ্চ বিদ্যালয় প্রাক্তন ছাত্রী পুনর্মিলনী ২০২৬",
                    Description = "স্মৃতিময় দিনগুলোর রোমন্থন ও সোনালী শৈশবে ফিরে যাওয়ার এক অনন্য উৎসব। শহীদ নাজমুল হক বালিকা উচ্চ বিদ্যালয়ের সকল ব্যাচের প্রাক্তন ছাত্রীদের নিয়ে আয়োজিত হতে যাচ্ছে জমকালো পুনর্মিলনী উৎসব ২০২৬।",
                    EventDate = new DateTime(2026, 12, 25, 9, 0, 0),
                    RegistrationDeadline = new DateTime(2026, 12, 10, 23, 59, 59),
                    VenueName = "বিদ্যালয় প্রাঙ্গণ ও মিলনায়তন",
                    VenueAddress = "শহীদ নাজমুল হক বালিকা উচ্চ বিদ্যালয়, রাজশাহী",
                    BaseAlumniFee = 1500m,
                    SpouseFee = 800m,
                    ChildFee = 400m,
                    GuestFee = 500m,
                    IsAllowSpouse = true,
                    IsAllowChild = true,
                    IsAllowGuest = true,
                    IsActive = true
                };

                context.ReunionEvents.Add(reunion);
                await context.SaveChangesAsync();
                activeReunion = reunion;

                // Seed Budget Heads for this event
                context.BudgetHeads.AddRange(
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Catering, Lunch & Refreshments", AllocatedAmount = 500000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Stage, Sound & Grand Lighting", AllocatedAmount = 150000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Tents, Seating & Venue Decoration", AllocatedAmount = 120000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Gifts, T-Shirts, Bags & Hampers", AllocatedAmount = 350000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Souvenir Magazine & ID Printing", AllocatedAmount = 100000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Cultural Artists & Band Performance", AllocatedAmount = 80000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Photography, Video & Drone Media", AllocatedAmount = 50000m },
                    new BudgetHead { ReunionEventId = reunion.Id, HeadName = "Security, Volunteers & Logistics", AllocatedAmount = 50000m }
                );
                await context.SaveChangesAsync();
            }

            // 5. Seed Gift Items & Size Stocks
            if (!await context.GiftItems.AnyAsync())
            {
                var tShirt = new GiftItem
                {
                    ItemName = "Reunion Premium Polo T-Shirt",
                    Description = "High quality 220 GSM combed cotton polo shirt with embroidered reunion crest",
                    TotalStockQuantity = 1000,
                    IsSizeSpecific = true,
                    SizeStocks = new List<GiftItemSizeStock>
                    {
                        new GiftItemSizeStock { SizeName = "S", TotalStock = 100 },
                        new GiftItemSizeStock { SizeName = "M", TotalStock = 250 },
                        new GiftItemSizeStock { SizeName = "L", TotalStock = 350 },
                        new GiftItemSizeStock { SizeName = "XL", TotalStock = 200 },
                        new GiftItemSizeStock { SizeName = "XXL", TotalStock = 70 },
                        new GiftItemSizeStock { SizeName = "XXXL", TotalStock = 30 }
                    }
                };

                var hamper = new GiftItem
                {
                    ItemName = "Reunion Gift Hamper",
                    Description = "Commemorative package including reunion mug, pen, diary, and key ring",
                    TotalStockQuantity = 1000,
                    IsSizeSpecific = false
                };

                var backpack = new GiftItem
                {
                    ItemName = "Alumni Commemorative Backpack",
                    Description = "Water-resistant executive laptop backpack with reunion monogram",
                    TotalStockQuantity = 1000,
                    IsSizeSpecific = false
                };

                var idBadge = new GiftItem
                {
                    ItemName = "Laminated Digital Pass & Lanyard",
                    Description = "VIP Attendee badge with QR code and premium satin lanyard",
                    TotalStockQuantity = 1000,
                    IsSizeSpecific = false
                };

                context.GiftItems.AddRange(tShirt, hamper, backpack, idBadge);
            }

            // 6. Seed Account Heads
            if (!await context.AccountHeads.AnyAsync())
            {
                context.AccountHeads.AddRange(
                    // Incomes
                    new AccountHead { HeadName = "Ticket Registration Fees", Type = AccountHeadType.Income, Code = "INC-REG" },
                    new AccountHead { HeadName = "Corporate Sponsorships", Type = AccountHeadType.Income, Code = "INC-SPON" },
                    new AccountHead { HeadName = "Patron & Donor Contributions", Type = AccountHeadType.Income, Code = "INC-DON" },
                    new AccountHead { HeadName = "Souvenir Advertisements", Type = AccountHeadType.Income, Code = "INC-SOUV" },
                    new AccountHead { HeadName = "Miscellaneous Income", Type = AccountHeadType.Income, Code = "INC-MISC" },

                    // Expenses
                    new AccountHead { HeadName = "Catering, Lunch & Breakfast", Type = AccountHeadType.Expense, Code = "EXP-CAT" },
                    new AccountHead { HeadName = "Stage, Sound & Lighting", Type = AccountHeadType.Expense, Code = "EXP-STAGE" },
                    new AccountHead { HeadName = "Venue Tents & Floral Decor", Type = AccountHeadType.Expense, Code = "EXP-VENUE" },
                    new AccountHead { HeadName = "Gift Hampers, T-Shirts & Bags", Type = AccountHeadType.Expense, Code = "EXP-GIFTS" },
                    new AccountHead { HeadName = "Printing, ID Badges & Souvenirs", Type = AccountHeadType.Expense, Code = "EXP-PRINT" },
                    new AccountHead { HeadName = "Cultural Performers & Sound Artists", Type = AccountHeadType.Expense, Code = "EXP-CULT" },
                    new AccountHead { HeadName = "Photography, Video & Media", Type = AccountHeadType.Expense, Code = "EXP-MEDIA" },
                    new AccountHead { HeadName = "Cleaning, Waste Disposal & Security", Type = AccountHeadType.Expense, Code = "EXP-SEC" },
                    new AccountHead { HeadName = "Emergency Contingency Fund", Type = AccountHeadType.Expense, Code = "EXP-EMERG" }
                );
            }

            // 7. Seed Sample Committees
            if (!await context.Committees.AnyAsync())
            {
                var exec = new Committee
                {
                    CommitteeName = "Executive Organizing Committee",
                    CommitteeNameBangla = "কেন্দ্রীয় নির্বাহী পরিষদ",
                    Description = "Core leadership overseeing all event logistics and arrangements",
                    OrderIndex = 1,
                    Members = new List<CommitteeMember>
                    {
                        new CommitteeMember { FullName = "Prof. Md. Abdur Rashid", Designation = "Convener", BatchYear = 1995, Mobile = "01711000001", OrderIndex = 1 },
                        new CommitteeMember { FullName = "Engr. Tanvir Ahmed", Designation = "Member Secretary", BatchYear = 2002, Mobile = "01711000002", OrderIndex = 2 },
                        new CommitteeMember { FullName = "Dr. Farzana Yasmin", Designation = "Joint Convener", BatchYear = 2005, Mobile = "01711000003", OrderIndex = 3 },
                        new CommitteeMember { FullName = "Kazi Mahmudul Hasan", Designation = "Treasurer", BatchYear = 2000, Mobile = "01711000004", OrderIndex = 4 }
                    }
                };

                var food = new Committee
                {
                    CommitteeName = "Food & Refreshment Sub-Committee",
                    CommitteeNameBangla = "খাদ্য ও আপ্যায়ন উপ-কমিটি",
                    Description = "Managing breakfast, buffet lunch, snacks, and catering quality",
                    OrderIndex = 2,
                    Members = new List<CommitteeMember>
                    {
                        new CommitteeMember { FullName = "Shahriar Kabir", Designation = "Sub-Committee Lead", BatchYear = 2008, Mobile = "01711000005", OrderIndex = 1 },
                        new CommitteeMember { FullName = "Nayeem Islam", Designation = "Coordinator", BatchYear = 2012, Mobile = "01711000006", OrderIndex = 2 }
                    }
                };

                context.Committees.AddRange(exec, food);
            }

            // 8. Seed Sample Notice
            if (!await context.NoticePosts.AnyAsync())
            {
                context.NoticePosts.Add(new NoticePost
                {
                    Title = "Registration Open for Grand Alumni Reunion 2026!",
                    Category = "Notice",
                    ContentHtml = "<p>We are delighted to announce that registration for the Grand Alumni Reunion 2026 is now officially open! All batches from 1995 to 2025 are warmly invited to enroll. Please complete your registration and select your T-shirt size before the deadline.</p>",
                    PublishDate = DateTime.UtcNow,
                    IsPublished = true
                });
            }

            // 9. Seed Default Registration Packages
            if (!await context.RegistrationPackages.AnyAsync())
            {
                var reunion = await context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
                if (reunion != null)
                {
                    context.RegistrationPackages.AddRange(
                        new RegistrationPackage
                        {
                            ReunionEventId = reunion.Id,
                            PackageName = "Regular Alumnus",
                            PackageNameBangla = "সাধারণ প্রাক্তন ছাত্রী নিবন্ধন",
                            Description = "Full day access, all gourmet meals, 1x custom Polo T-Shirt, kit bag with commemorative gift hamper, souvenir booklet, and 1x raffle draw coupon.",
                            Fee = 1500m,
                            IncludesTShirt = true,
                            IncludesKitBag = true,
                            IncludesFood = true,
                            IncludesRaffle = true,
                            IncludedGuests = 0,
                            DisplayOrder = 1,
                            IsFeatured = true,
                            BadgeText = "Most Popular",
                            IsActive = true
                        },
                        new RegistrationPackage
                        {
                            ReunionEventId = reunion.Id,
                            PackageName = "Silver Couple Package",
                            PackageNameBangla = "সিলভার যুগল প্যাকেজ (প্রাক্তন ছাত্রী + স্বামী)",
                            Description = "All-inclusive registration for alumnus and accompanying spouse. Includes 1x Polo T-Shirt, 2x full meals, gift hamper, and 2x cultural night passes.",
                            Fee = 2500m,
                            IncludesTShirt = true,
                            IncludesKitBag = true,
                            IncludesFood = true,
                            IncludesRaffle = true,
                            IncludedGuests = 1,
                            DisplayOrder = 2,
                            IsFeatured = false,
                            BadgeText = "Couple Favorite",
                            IsActive = true
                        },
                        new RegistrationPackage
                        {
                            ReunionEventId = reunion.Id,
                            PackageName = "VIP Patron Alumnus",
                            PackageNameBangla = "ভিআইপি পৃষ্ঠপোষক প্যাকেজ",
                            Description = "VIP front-row seating, stage citation felicitation, 2x Polo T-Shirts, executive souvenir plaque, family access (spouse + 2 kids meals), and 5x mega raffle coupons.",
                            Fee = 5000m,
                            IncludesTShirt = true,
                            IncludesKitBag = true,
                            IncludesFood = true,
                            IncludesRaffle = true,
                            IncludedGuests = 3,
                            DisplayOrder = 3,
                            IsFeatured = false,
                            BadgeText = "VIP Patron",
                            IsActive = true
                        },
                        new RegistrationPackage
                        {
                            ReunionEventId = reunion.Id,
                            PackageName = "Fresh Graduate Alumnus",
                            PackageNameBangla = "নবীন প্রাক্তন ছাত্রী বিশেষ ছাড়",
                            Description = "Subsidized youth fee for recent batches (passing year 2022-2025). Includes Polo T-Shirt, full meals, backpack, and networking session access.",
                            Fee = 1000m,
                            IncludesTShirt = true,
                            IncludesKitBag = true,
                            IncludesFood = true,
                            IncludesRaffle = true,
                            IncludedGuests = 0,
                            DisplayOrder = 4,
                            IsFeatured = false,
                            BadgeText = "Youth Discount",
                            IsActive = true
                        }
                    );
                }
            }

            // 10. Seed Initial Gallery Images
            if (!await context.GalleryImages.AnyAsync())
            {
                var reunion = await context.ReunionEvents.FirstOrDefaultAsync(r => r.IsActive);
                if (reunion != null)
                {
                    context.GalleryImages.AddRange(
                        new GalleryImage
                        {
                            ReunionEventId = reunion.Id,
                            Title = "Campus Golden Days",
                            Caption = "School assembly and morning prayers at the historic school field",
                            ImagePath = "https://images.unsplash.com/photo-1523580494863-6f3031224c94?auto=format&fit=crop&w=800&q=80",
                            Category = "Campus Life",
                            DisplayOrder = 1,
                            IsActive = true
                        },
                        new GalleryImage
                        {
                            ReunionEventId = reunion.Id,
                            Title = "Previous Reunion Joy",
                            Caption = "Heartfelt embraces and laughter at the previous batch celebration",
                            ImagePath = "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?auto=format&fit=crop&w=800&q=80",
                            Category = "Reunions",
                            DisplayOrder = 2,
                            IsActive = true
                        },
                        new GalleryImage
                        {
                            ReunionEventId = reunion.Id,
                            Title = "Batch Classmates Friendship",
                            Caption = "Reuniting with best friends and sharing timeless nostalgic memories",
                            ImagePath = "https://images.unsplash.com/photo-1529156069898-49953e39b3ac?auto=format&fit=crop&w=800&q=80",
                            Category = "Campus Memories",
                            DisplayOrder = 3,
                            IsActive = true
                        },
                        new GalleryImage
                        {
                            ReunionEventId = reunion.Id,
                            Title = "Annual Sports & Cultural Extravaganza",
                            Caption = "Honoring athletic triumphs, musical performances, and dance pageants",
                            ImagePath = "https://images.unsplash.com/photo-1511578314322-379afb476865?auto=format&fit=crop&w=800&q=80",
                            Category = "Cultural & Sports",
                            DisplayOrder = 4,
                            IsActive = true
                        },
                        new GalleryImage
                        {
                            ReunionEventId = reunion.Id,
                            Title = "Grand Stage & Evening Festivities",
                            Caption = "Lighting the ceremonial lamp and celebrating with faculty mentors",
                            ImagePath = "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?auto=format&fit=crop&w=800&q=80",
                            Category = "Reunions",
                            DisplayOrder = 5,
                            IsActive = true
                        },
                        new GalleryImage
                        {
                            ReunionEventId = reunion.Id,
                            Title = "Faculty & Mentors Tribute",
                            Caption = "Paying tribute to our beloved teachers whose guidance shaped our lives",
                            ImagePath = "https://images.unsplash.com/photo-1577495508048-b635879837f1?auto=format&fit=crop&w=800&q=80",
                            Category = "Teachers & Mentors",
                            DisplayOrder = 6,
                            IsActive = true
                        }
                    );
                }

                // 8. Seed Dynamic Guest Categories
                if (!await context.GuestCategories.AnyAsync(g => g.ReunionEventId == reunion.Id))
                {
                    context.GuestCategories.AddRange(
                        new GuestCategory
                        {
                            ReunionEventId = reunion.Id,
                            CategoryName = "Spouse",
                            CategoryNameBangla = "স্বামী / স্ত্রী",
                            Fee = 1000,
                            EligibilityRules = "Spouse of registered alumna (Max 1)",
                            MaxAllowed = 1,
                            DisplayOrder = 1,
                            IsActive = true
                        },
                        new GuestCategory
                        {
                            ReunionEventId = reunion.Id,
                            CategoryName = "Child",
                            CategoryNameBangla = "সন্তান",
                            Fee = 500,
                            EligibilityRules = "Male child under 5 yrs, Female child no age restrictions",
                            MaxAge = 5,
                            MaxAllowed = 5,
                            DisplayOrder = 2,
                            IsActive = true
                        },
                        new GuestCategory
                        {
                            ReunionEventId = reunion.Id,
                            CategoryName = "Driver / Attendant",
                            CategoryNameBangla = "ড্রাইভার / সহযোগী",
                            Fee = 500,
                            EligibilityRules = "Personal driver or attendant for the day",
                            MaxAllowed = 2,
                            DisplayOrder = 3,
                            IsActive = true
                        }
                    );
                }

                // 9. Seed Dynamic Custom Questions
                if (!await context.EventCustomQuestions.AnyAsync(q => q.ReunionEventId == reunion.Id))
                {
                    context.EventCustomQuestions.AddRange(
                        new EventCustomQuestion
                        {
                            ReunionEventId = reunion.Id,
                            QuestionText = "Do you want to participate in the Cultural Program?",
                            QuestionTextBangla = "আপনি কি সাংস্কৃতিক অনুষ্ঠানে অংশগ্রহণ করতে চান?",
                            FieldType = "YesNoWithSubQuestion",
                            SubQuestionText = "If yes, which part? (e.g. Singing, Drama, Recitation, Dance)",
                            DisplayOrder = 1,
                            IsRequired = false,
                            IsActive = true
                        },
                        new EventCustomQuestion
                        {
                            ReunionEventId = reunion.Id,
                            QuestionText = "Are you interested in volunteering for the reunion event?",
                            QuestionTextBangla = "আপনি কি পুনর্মিলনী অনুষ্ঠানে ভলান্টিয়ার হিসেবে কাজ করতে আগ্রহী?",
                            FieldType = "YesNo",
                            DisplayOrder = 2,
                            IsRequired = false,
                            IsActive = true
                        },
                        new EventCustomQuestion
                        {
                            ReunionEventId = reunion.Id,
                            QuestionText = "Would you like to make an additional voluntary donation?",
                            QuestionTextBangla = "আপনি কি কোনো স্বেচ্ছাসেবী অনুদান প্রদান করতে চান?",
                            FieldType = "YesNoWithSubQuestion",
                            SubQuestionText = "If yes, pledge amount (BDT) & purpose",
                            DisplayOrder = 3,
                            IsRequired = false,
                            IsActive = true
                        }
                    );
                }

                // 10. Link Gifts to Packages & Setup Passing Year Windows
                var packages = await context.RegistrationPackages.Where(p => p.ReunionEventId == reunion.Id).ToListAsync();
                var giftItems = await context.GiftItems.Where(g => g.IsActive).ToListAsync();

                foreach (var pkg in packages)
                {
                    if (pkg.PackageName.Contains("Regular", StringComparison.OrdinalIgnoreCase) && !pkg.MinPassingYear.HasValue)
                    {
                        pkg.MinPassingYear = 1972;
                        pkg.MaxPassingYear = 2020;
                    }
                    else if (pkg.PackageName.Contains("New", StringComparison.OrdinalIgnoreCase) && !pkg.MinPassingYear.HasValue)
                    {
                        pkg.MinPassingYear = 2021;
                        pkg.MaxPassingYear = 2026;
                    }

                    if (!await context.PackageGiftItems.AnyAsync(pg => pg.RegistrationPackageId == pkg.Id))
                    {
                        foreach (var gift in giftItems)
                        {
                            context.PackageGiftItems.Add(new PackageGiftItem
                            {
                                RegistrationPackageId = pkg.Id,
                                GiftItemId = gift.Id
                            });
                        }
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}

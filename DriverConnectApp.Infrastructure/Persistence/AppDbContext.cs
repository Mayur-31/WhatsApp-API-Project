using DriverConnectApp.Domain.Entities;
using DriverConnectApp.Domain.Enums;
using DriverConnectApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DriverConnectApp.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Driver> Drivers { get; set; }
        public DbSet<DriverConnectApp.Domain.Entities.Thread> Threads { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Depot> Depots { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupParticipant> GroupParticipants { get; set; }
        public DbSet<MessageRecipient> MessageRecipients { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamConfiguration> TeamConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure MessageType as string in database
            modelBuilder.Entity<Message>()
                .Property(m => m.MessageType)
                .HasConversion<string>();

            // Configure cascade delete for Message relationships
            modelBuilder.Entity<Message>()
                .HasMany(m => m.Recipients)
                .WithOne(mr => mr.Message)
                .HasForeignKey(mr => mr.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasMany(m => m.Reactions)
                .WithOne(mr => mr.Message)
                .HasForeignKey(mr => mr.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.ReplyToMessage)
                .WithMany()
                .HasForeignKey(m => m.ReplyToMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.ForwardedFromMessage)
                .WithMany()
                .HasForeignKey(m => m.ForwardedFromMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure cascade delete for Conversation -> Messages
            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure cascade delete for Driver -> Conversations
            modelBuilder.Entity<Driver>()
                .HasMany(d => d.Conversations)
                .WithOne(c => c.Driver)
                .HasForeignKey(c => c.DriverId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Team
            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
                entity.Property(t => t.CountryCode).HasMaxLength(3).HasDefaultValue("44");
                entity.Property(t => t.WhatsAppPhoneNumberId).HasMaxLength(100);
                entity.Property(t => t.WhatsAppAccessToken).HasMaxLength(500);
                entity.Property(t => t.WhatsAppBusinessAccountId).HasMaxLength(100);
                entity.Property(t => t.WhatsAppPhoneNumber).HasMaxLength(20);
                entity.Property(t => t.ApiVersion).HasMaxLength(10).IsRequired();
                entity.Property(t => t.IsActive).IsRequired();
                entity.Property(t => t.CreatedAt).IsRequired();

                // Navigation properties
                entity.HasMany<ApplicationUser>()
                      .WithOne(u => u.Team)
                      .HasForeignKey(u => u.TeamId)
                      .IsRequired(false);

                entity.HasMany(t => t.Drivers)
                      .WithOne(d => d.Team)
                      .HasForeignKey(d => d.TeamId)
                      .IsRequired(false);

                entity.HasMany(t => t.Conversations)
                      .WithOne(c => c.Team)
                      .HasForeignKey(c => c.TeamId)
                      .IsRequired(false);

                entity.HasMany(t => t.Groups)
                      .WithOne(g => g.Team)
                      .HasForeignKey(g => g.TeamId)
                      .IsRequired(false);
            });

            // Configure ApplicationUser relationships
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Team)
                      .WithMany()
                      .HasForeignKey(u => u.TeamId)
                      .IsRequired(false);
            });

            modelBuilder.Entity<TeamConfiguration>(entity =>
            {
                entity.HasKey(tc => tc.Id);
                entity.HasOne(tc => tc.Team)
                      .WithOne()
                      .HasForeignKey<TeamConfiguration>(tc => tc.TeamId)
                      .IsRequired();
            });

            modelBuilder.Entity<MessageRecipient>(entity =>
            {
                entity.HasKey(mr => mr.Id);

                entity.HasOne(mr => mr.Message)
                      .WithMany(m => m.Recipients)
                      .HasForeignKey(mr => mr.MessageId)
                      .IsRequired();

                entity.HasOne(mr => mr.Driver)
                      .WithMany()
                      .HasForeignKey(mr => mr.DriverId)
                      .IsRequired(false);

                entity.HasOne(mr => mr.GroupParticipant)
                      .WithMany()
                      .HasForeignKey(mr => mr.GroupParticipantId)
                      .IsRequired(false);
            });

            modelBuilder.Entity<MessageReaction>(entity =>
            {
                entity.HasKey(mr => mr.Id);

                entity.HasOne(mr => mr.Message)
                      .WithMany(m => m.Reactions)
                      .HasForeignKey(mr => mr.MessageId)
                      .IsRequired();

                // Configure relationship with ApplicationUser
                entity.HasOne<ApplicationUser>()
                      .WithMany(u => u.MessageReactions)
                      .HasForeignKey(mr => mr.UserId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(mr => mr.Driver)
                      .WithMany()
                      .HasForeignKey(mr => mr.DriverId)
                      .IsRequired(false);
            });

            // Configure MessageStatus as string in database
            modelBuilder.Entity<Message>()
                .Property(m => m.Status)
                .HasConversion<string>();

            modelBuilder.Entity<MessageRecipient>()
                .Property(mr => mr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<GroupParticipant>(entity =>
            {
                entity.HasKey(gp => gp.Id);
                entity.Property(gp => gp.PhoneNumber).HasMaxLength(20);
                entity.Property(gp => gp.ParticipantName).HasMaxLength(100);
                entity.Property(gp => gp.Role).HasMaxLength(20).IsRequired();
                entity.Property(gp => gp.JoinedAt).IsRequired();
                entity.Property(gp => gp.IsActive).IsRequired();

                // Relationship with Group
                entity.HasOne(gp => gp.Group)
                      .WithMany(g => g.Participants)
                      .HasForeignKey(gp => gp.GroupId)
                      .IsRequired();

                // Relationship with Driver
                entity.HasOne(gp => gp.Driver)
                      .WithMany()
                      .HasForeignKey(gp => gp.DriverId)
                      .IsRequired(false);
            });

            // Configure Department
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
                entity.Property(d => d.Description).HasMaxLength(500);
                entity.Property(d => d.CreatedAt).IsRequired();
                entity.Property(d => d.IsActive).IsRequired();

                // Navigation properties
                entity.HasMany(d => d.Conversations)
                      .WithOne(c => c.Department)
                      .HasForeignKey(c => c.DepartmentId)
                      .IsRequired(false);
            });

            // Configure ApplicationUser relationships with Department
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Department)
                      .WithMany()
                      .HasForeignKey(u => u.DepartmentId)
                      .IsRequired(false);
            });

            // Configure Depot
            modelBuilder.Entity<Depot>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
                entity.Property(d => d.Location).HasMaxLength(200);
                entity.Property(d => d.City).HasMaxLength(100);
                entity.Property(d => d.Address).HasMaxLength(500);
                entity.Property(d => d.PostalCode).HasMaxLength(20);
                entity.Property(d => d.CreatedAt).IsRequired();
                entity.Property(d => d.IsActive).IsRequired();

                // Navigation properties
                entity.HasMany(d => d.Drivers)
                      .WithOne(driver => driver.Depot)
                      .HasForeignKey(driver => driver.DepotId)
                      .IsRequired(false);
            });

            // Configure ApplicationUser relationships with Depot
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Depot)
                      .WithMany()
                      .HasForeignKey(u => u.DepotId)
                      .IsRequired(false);
            });

            // Configure Driver
            modelBuilder.Entity<Driver>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
                entity.Property(d => d.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(d => d.CreatedAt).IsRequired();
                entity.Property(d => d.IsActive).IsRequired();

                // Relationship with Depot
                entity.HasOne(d => d.Depot)
                      .WithMany(depot => depot.Drivers)
                      .HasForeignKey(d => d.DepotId)
                      .IsRequired(false);

                entity.HasOne(d => d.Team)
                      .WithMany(t => t.Drivers)
                      .HasForeignKey(d => d.TeamId)
                      .IsRequired(false);

                // Navigation properties
                entity.HasMany(d => d.Conversations)
                      .WithOne(c => c.Driver)
                      .HasForeignKey(c => c.DriverId)
                      .IsRequired(false);
            });

            // Configure Group
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.WhatsAppGroupId).IsRequired().HasMaxLength(100);
                entity.Property(g => g.Name).IsRequired().HasMaxLength(200);
                entity.Property(g => g.Description).HasMaxLength(500);
                entity.Property(g => g.CreatedAt).IsRequired();
                entity.Property(g => g.LastActivityAt).IsRequired(false);
                entity.Property(g => g.IsActive).IsRequired();

                entity.HasIndex(g => g.WhatsAppGroupId).IsUnique();

                entity.HasOne(g => g.Team)
                      .WithMany(t => t.Groups)
                      .HasForeignKey(g => g.TeamId)
                      .IsRequired(false);

                // Navigation properties
                entity.HasMany(g => g.Participants)
                      .WithOne(gp => gp.Group)
                      .HasForeignKey(gp => gp.GroupId)
                      .IsRequired();

                entity.HasMany(g => g.Conversations)
                      .WithOne(c => c.Group)
                      .HasForeignKey(c => c.GroupId)
                      .IsRequired(false);
            });

            // Configure Conversation - SIMPLIFIED FIXED VERSION
            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Topic).IsRequired();
                entity.Property(c => c.CreatedAt).IsRequired();
                entity.Property(c => c.IsAnswered).IsRequired();
                entity.Property(c => c.WhatsAppGroupId).HasMaxLength(100);
                entity.Property(c => c.GroupName).HasMaxLength(200);
                entity.Property(c => c.IsGroupConversation).IsRequired();
                entity.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(c => c.LastInboundMessageAt).IsRequired(false);

                // Relationship with Driver (nullable for group conversations)
                entity.HasOne(c => c.Driver)
                      .WithMany(d => d.Conversations)
                      .HasForeignKey(c => c.DriverId)
                      .IsRequired(false);

                // Relationship with Group via GroupId
                entity.HasOne(c => c.Group)
                      .WithMany(g => g.Conversations)
                      .HasForeignKey(c => c.GroupId)
                      .IsRequired(false);

                // Relationship with Department
                entity.HasOne(c => c.Department)
                      .WithMany(d => d.Conversations)
                      .HasForeignKey(c => c.DepartmentId)
                      .IsRequired(false);

                // Relationship with Team
                entity.HasOne(c => c.Team)
                      .WithMany(t => t.Conversations)
                      .HasForeignKey(c => c.TeamId)
                      .IsRequired(false);

                // Relationship with ApplicationUser (AssignedToUser)
                entity.HasOne<ApplicationUser>()
                      .WithMany(u => u.AssignedConversations)
                      .HasForeignKey(c => c.AssignedToUserId)
                      .IsRequired(false);
            });

            // Configure Message
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Content).IsRequired();
                entity.Property(m => m.SentAt).IsRequired();
                entity.Property(m => m.IsFromDriver).IsRequired();
                entity.Property(m => m.MessageType).IsRequired();
                entity.Property(m => m.SenderPhoneNumber).HasMaxLength(20);
                entity.Property(m => m.SenderName).HasMaxLength(100);
                entity.Property(m => m.IsGroupMessage).IsRequired();

                entity.Property(m => m.IsTemplateMessage).HasDefaultValue(false);
                entity.Property(m => m.TemplateName).HasMaxLength(100);
                entity.Property(m => m.TemplateParametersJson).HasColumnType("TEXT");

                // Relationship with Conversation
                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .IsRequired();
            });

            // Configure ApplicationUser relationships
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                // Relationship with Driver
                entity.HasOne(u => u.Driver)
                      .WithMany()
                      .HasForeignKey(u => u.DriverId)
                      .IsRequired(false);

                // Navigation properties
                entity.HasMany(u => u.MessageReactions)
                      .WithOne()
                      .HasForeignKey(mr => mr.UserId)
                      .IsRequired(false);

                entity.HasMany(u => u.AssignedConversations)
                      .WithOne()
                      .HasForeignKey(c => c.AssignedToUserId)
                      .IsRequired(false);
            });
        }
    }
}
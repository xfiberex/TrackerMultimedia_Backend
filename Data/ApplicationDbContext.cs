using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrackerMultimedia.Domain.Entities;
using TrackerMultimedia.Domain.Enums;

namespace TrackerMultimedia.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
	public DbSet<UserCategory> UserCategories => Set<UserCategory>();
	public DbSet<UserFormat> UserFormats => Set<UserFormat>();
	public DbSet<MediaItemCategory> MediaItemCategories => Set<MediaItemCategory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<MediaItem>()
			.Property(item => item.SourceType)
			.HasDefaultValue(MediaItemSourceType.Manual)
			.HasSentinel(MediaItemSourceType.Manual);

		// Relación MediaItem → ApplicationUser
		modelBuilder.Entity<MediaItem>()
			.HasOne(item => item.User)
			.WithMany()
			.HasForeignKey(item => item.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserCategory>()
			.HasOne(category => category.User)
			.WithMany()
			.HasForeignKey(category => category.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserFormat>()
			.HasOne(format => format.User)
			.WithMany()
			.HasForeignKey(format => format.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<UserCategory>()
			.HasIndex(category => new { category.UserId, category.NormalizedName })
			.IsUnique();

		modelBuilder.Entity<UserFormat>()
			.HasIndex(format => new { format.UserId, format.NormalizedName })
			.IsUnique();

		modelBuilder.Entity<MediaItemCategory>()
			.HasKey(link => new { link.MediaItemId, link.UserCategoryId });

		modelBuilder.Entity<MediaItemCategory>()
			.HasOne(link => link.MediaItem)
			.WithMany(item => item.MediaItemCategories)
			.HasForeignKey(link => link.MediaItemId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<MediaItemCategory>()
			.HasOne(link => link.UserCategory)
			.WithMany(category => category.MediaItemCategories)
			.HasForeignKey(link => link.UserCategoryId)
			.OnDelete(DeleteBehavior.Cascade);

		// Índice único parcial: un mismo ID externo no puede repetirse para el mismo usuario, fuente y tipo de medio.
		modelBuilder.Entity<MediaItem>()
			.HasIndex(item => new { item.UserId, item.SourceType, item.ExternalId, item.ExternalMediaKind })
			.IsUnique()
			.HasFilter("\"ExternalId\" IS NOT NULL AND \"ExternalMediaKind\" IS NOT NULL");

		// OAuthState: índice en StateValue para búsquedas O(1) en callback
		modelBuilder.Entity<OAuthState>()
			.HasIndex(s => s.StateValue)
			.IsUnique();
	}
}
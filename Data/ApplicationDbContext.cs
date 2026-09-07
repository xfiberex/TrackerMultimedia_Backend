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

        // La navegación ApplicationUser.ExternalLogins tiene que colgar de la MISMA clave
        // foránea que ya configura IdentityDbContext (UserId). Sin esta línea, EF Core
        // interpreta la navegación como una relación adicional y crea una FK sombra
        // (ApplicationUserId) que UserManager.AddLoginAsync nunca rellena, con lo que la
        // colección queda siempre vacía y el usuario nunca ve sus proveedores vinculados.
        modelBuilder.Entity<IdentityUserLogin<Guid>>()
            .HasOne<ApplicationUser>()
            .WithMany(user => user.ExternalLogins)
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

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

        modelBuilder.Entity<MediaItem>()
            .HasOne(item => item.UserFormat)
            .WithMany()
            .HasForeignKey(item => item.UserFormatId)
            .OnDelete(DeleteBehavior.SetNull);

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

        // Hubo aquí un índice único parcial sobre (UserId, SourceType, ExternalId,
        // ExternalMediaKind) que impedía importar dos veces el mismo título de un
        // catálogo. Cayó el 2026-09-06 con «Descubrir», junto a las cuatro columnas que
        // lo formaban: sin procedencia externa no hay duplicado externo que evitar.
        //
        // Este es el que cubre el "WHERE UserId = @p" de las consultas de biblioteca, e
        // incluye CreatedAtUtc porque es la ordenación por defecto del listado. Con el
        // parcial fuera, pasa a ser el único índice de MediaItems (T1-07).
        modelBuilder.Entity<MediaItem>()
            .HasIndex(item => new { item.UserId, item.CreatedAtUtc });

        // Refresh y logout buscan por TokenHash: sin índice era un recorrido secuencial
        // de toda la tabla en cada renovación de sesión.
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(token => token.TokenHash)
            .IsUnique();

        // OAuthState: índice en StateValue para búsquedas O(1) en callback
        modelBuilder.Entity<OAuthState>()
            .HasIndex(s => s.StateValue)
            .IsUnique();
    }
}

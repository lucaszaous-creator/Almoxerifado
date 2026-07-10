using ALMOXPRO.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ALMOXPRO.Persistence;

/// <summary>Usado apenas pelo dotnet-ef para gerar migrations.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AlmoxProDbContext>
{
    public AlmoxProDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AlmoxProDbContext>()
            .UseNpgsql("Host=localhost;Database=almoxpro;Username=postgres;Password=postgres")
            .Options;
        return new AlmoxProDbContext(options);
    }
}

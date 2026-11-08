using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Domain.Entities.Configuration;

namespace ALMOXPRO.Application.Services;

public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
    Task<Dictionary<string, string?>> GetAllAsync(CancellationToken ct = default);
}

public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _uow;

    public SettingsService(IUnitOfWork uow) => _uow = uow;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        (await _uow.Settings.GetByKeyAsync(key, ct))?.Value;

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var setting = await _uow.Settings.GetByKeyAsync(key, ct);
        if (setting is null)
        {
            setting = new AppSetting { Key = key, Value = value };
            await _uow.Settings.AddAsync(setting, ct);
        }
        else
        {
            setting.Value = value;
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string?>> GetAllAsync(CancellationToken ct = default) =>
        (await _uow.Settings.GetAllAsync(ct)).ToDictionary(s => s.Key, s => s.Value);
}

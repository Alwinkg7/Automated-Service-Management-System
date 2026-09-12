using ServiceApp.Core.Entities;


namespace ServiceApp.Core.Interfaces
{
    public interface IServiceZoneRepository : IRepository<ServiceZone>
    {
        // Find which zone a pin code belongs to
        Task<ServiceZone?> GetByPinCodeAsync(string pinCode);

        // All active zones
        Task<IEnumerable<ServiceZone>> GetActiveZonesAsync();
    }
}

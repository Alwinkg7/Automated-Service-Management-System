using ServiceApp.Core.Models.Queries;

namespace ServiceApp.Core.Interfaces;

/// <summary>CSV export of service requests (UTF-8 with BOM, spreadsheet formula injection escaped).</summary>
public interface IRequestExportService
{
    /// <summary>Returns the CSV bytes and the number of exported rows. Paging in <paramref name="criteria"/> is ignored.</summary>
    Task<(byte[] Content, int RowCount)> ExportCsvAsync(RequestSearchCriteria criteria, CancellationToken ct = default);
}

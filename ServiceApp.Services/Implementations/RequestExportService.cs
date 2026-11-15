using System.Globalization;
using System.Text;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;

namespace ServiceApp.Services.Implementations;

/// <summary>CSV export of filtered service requests (filtering runs in SQL).</summary>
public class RequestExportService : IRequestExportService
{
    private readonly IRequestQueries _queries;

    public RequestExportService(IRequestQueries queries) => _queries = queries;

    public async Task<(byte[] Content, int RowCount)> ExportCsvAsync(RequestSearchCriteria criteria, CancellationToken ct = default)
    {
        var all = new RequestSearchCriteria
        {
            CustomerId = criteria.CustomerId,
            TechnicianProfileId = criteria.TechnicianProfileId,
            Statuses = criteria.Statuses,
            Category = criteria.Category,
            Search = criteria.Search,
            CreatedFrom = criteria.CreatedFrom,
            CreatedTo = criteria.CreatedTo,
            PageSize = 0
        };
        var rows = (await _queries.SearchAsync(all, ct)).Items;

        var csv = new StringBuilder();
        csv.AppendLine("Request ID,Customer,Phone,Category,Description,Address,Pin Code," +
                       "Preferred Time,Technician,Status,Created At,Bill Amount,Payment Status");

        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(",",
                r.RequestId.ToString(CultureInfo.InvariantCulture),
                Escape(r.Customer?.FullName),
                Escape(r.Customer?.Phone),
                r.Category,
                Escape(r.Description),
                Escape(r.Address),
                Escape(r.PinCode),
                r.PreferredDateTime.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                Escape(r.AssignedTechnician?.User?.FullName),
                r.Status,
                r.CreatedAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                r.Bill?.TotalAmount.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                r.Bill?.PaymentStatus.ToString() ?? string.Empty));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return (bytes, rows.Count);
    }

    /// <summary>
    /// CSV-escapes a value. Values starting with = + - @ tab or CR get a leading
    /// apostrophe so spreadsheet apps do not evaluate them as formulas.
    /// </summary>
    public static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
            value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}

using System.Globalization;
using System.IO.Compression;
using System.Text;
using ExcelDataReader;
using HrETracker.Models;
using Microsoft.VisualBasic.FileIO;

namespace HrETracker.Services;

public static class ImportFileParser
{
    public const int MaxRows = 10000;
    public const int MaxBytes = 10 * 1024 * 1024;
    private static readonly string[] RequiredHeaders = ["Employee ID", "Full Name", "Department", "Enrollment Date"];

    public static List<ImportRow> Parse(Stream stream, string extension, CancellationToken ct)
    {
        try
        {
            if (extension == ".csv")
            {
                using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true, true)
                { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
                parser.SetDelimiters(",");
                var header = parser.ReadFields() ?? throw new ArgumentException("The file is empty.");
                var map = Headers(header);
                var rows = new List<ImportRow>();
                var number = 1;
                while (!parser.EndOfData)
                {
                    ct.ThrowIfCancellationRequested();
                    var fields = parser.ReadFields()!;
                    number++;
                    if (fields.All(string.IsNullOrWhiteSpace)) continue;
                    var row = Row(fields.Cast<object?>().ToArray(), map, number);
                    if (fields.Length != header.Length) row.ErrorsJson = "[\"Column count does not match the header.\"]";
                    rows.Add(row);
                    Limit(rows.Count);
                }
                return Nonempty(rows);
            }
            if (extension == ".xlsx")
            {
                // Bound uncompressed package size before allowing the spreadsheet parser to read XML.
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    long total = 0;
                    foreach (var entry in archive.Entries)
                    {
                        total = checked(total + entry.Length);
                        if (total > 100 * 1024 * 1024) throw new ArgumentException("The expanded spreadsheet exceeds 100 MB.");
                    }
                }
                stream.Position = 0;
            }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var reader = extension == ".xlsx" ? ExcelReaderFactory.CreateOpenXmlReader(stream) : ExcelReaderFactory.CreateBinaryReader(stream);
            if (!reader.Read()) throw new ArgumentException("The file is empty.");
            if (reader.FieldCount > 50) throw new ArgumentException("At most 50 columns are allowed.");
            var headers = Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "").ToArray();
            var columns = Headers(headers);
            var result = new List<ImportRow>();
            var rowNumber = 1;
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                rowNumber++;
                var values = Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray();
                if (values.All(v => v is null || string.IsNullOrWhiteSpace(Convert.ToString(v, CultureInfo.InvariantCulture)))) continue;
                result.Add(Row(values, columns, rowNumber));
                Limit(result.Count);
            }
            if (reader.NextResult()) throw new ArgumentException("Use a workbook with one worksheet per import.");
            return Nonempty(result);
        }
        catch (Exception ex) when (ex is MalformedLineException or DecoderFallbackException or InvalidDataException or ExcelDataReader.Exceptions.ExcelReaderException or System.Xml.XmlException or OverflowException)
        { throw new ArgumentException("The file is malformed, encrypted, or does not match its extension."); }
    }

    private static Dictionary<string, int> Headers(string[] headers)
    {
        if (headers.Length > 50) throw new ArgumentException("At most 50 columns are allowed.");
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var name = headers[i].Trim().TrimStart('\uFEFF');
            if (name.Length == 0 || !map.TryAdd(name, i)) throw new ArgumentException("Headers must be nonempty and unique.");
        }
        if (RequiredHeaders.Any(h => !map.ContainsKey(h))) throw new ArgumentException("Required headers: Employee ID, Full Name, Department, Enrollment Date.");
        return map;
    }

    private static ImportRow Row(object?[] values, Dictionary<string, int> map, int number)
    {
        object? Value(string key) => map.TryGetValue(key, out var i) && i < values.Length ? values[i] : null;
        string Text(string key) => (Convert.ToString(Value(key), CultureInfo.InvariantCulture) ?? "").Trim();
        var dateValue = Value("Enrollment Date");
        DateOnly? date = dateValue is DateTime d ? DateOnly.FromDateTime(d) : null;
        if (date is null && DateOnly.TryParseExact(Text("Enrollment Date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) date = parsed;
        // ExcelDataReader returns date-formatted numeric cells as DateTime (including the workbook's date system).
        // Unformatted serial numbers are ambiguous and must be formatted as dates in Excel.
        return new ImportRow
        {
            RowNumber = number, EmployeeNumber = Text("Employee ID").ToUpperInvariant(), FullName = Text("Full Name"),
            Department = Text("Department"), EnrollmentDate = date, JobTitle = Null(Text("Job Title")), Notes = Null(Text("Notes"))
        };
    }
    private static string? Null(string value) => value.Length == 0 ? null : value;
    private static void Limit(int count) { if (count > MaxRows) throw new ArgumentException("At most 10,000 employee rows are allowed."); }
    private static List<ImportRow> Nonempty(List<ImportRow> rows) => rows.Count > 0 ? rows : throw new ArgumentException("At least one employee row is required.");
}

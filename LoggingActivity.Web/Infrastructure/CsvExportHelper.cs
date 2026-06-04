using System.Text;

namespace LoggingActivity.Web.Infrastructure;

public static class CsvExportHelper
{
    public static byte[] Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, headers);

        foreach (var row in rows)
        {
            AppendRow(builder, row.Select(value => value ?? string.Empty));
        }

        var csvBytes = Encoding.UTF8.GetBytes(builder.ToString());
        var bom = Encoding.UTF8.GetPreamble();
        var output = new byte[bom.Length + csvBytes.Length];
        Buffer.BlockCopy(bom, 0, output, 0, bom.Length);
        Buffer.BlockCopy(csvBytes, 0, output, bom.Length, csvBytes.Length);
        return output;
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string> values)
    {
        var isFirst = true;
        foreach (var value in values)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            builder.Append(Escape(value));
            isFirst = false;
        }

        builder.Append("\r\n");
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
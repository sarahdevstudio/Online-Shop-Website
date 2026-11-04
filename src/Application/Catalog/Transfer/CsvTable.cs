using System.Text;

namespace Shop.Application.Catalog.Transfer;

/// <summary>CSV in and out: UTF-8 (a BOM is accepted and written, so Excel shows Persian correctly), quotes, comma / semicolon / tab.</summary>
public static class CsvTable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static TableFile Read(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw TableFileException.Of("api.import.file-empty");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw TableFileException.Of("api.import.file-not-utf8");
        }

        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        if (text.Contains('\0'))
        {
            throw TableFileException.Of("api.import.file-damaged");
        }

        var delimiter = DetectDelimiter(text);
        var rows = new List<TableRow>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowStart = 1;
        var wasQuoted = false;

        void EndCell()
        {
            if (cell.Length > TableLimits.MaxCellLength)
            {
                throw TableFileException.Of("api.import.cell-too-long", ("row", rowStart), ("max", TableLimits.MaxCellLength));
            }

            cells.Add(cell.ToString());
            cell.Clear();
            wasQuoted = false;
            if (cells.Count > TableLimits.MaxColumns)
            {
                throw TableFileException.Of("api.import.too-many-columns", ("max", TableLimits.MaxColumns));
            }
        }

        void EndRow()
        {
            EndCell();
            if (cells.Any(c => c.Length > 0))
            {
                rows.Add(new TableRow(rowStart, cells.ToArray()));
                if (rows.Count > TableLimits.MaxRows + 1)
                {
                    throw TableFileException.Of("api.import.too-many-rows", ("max", TableLimits.MaxRows));
                }
            }

            cells.Clear();
            rowStart = line + 1;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0 && !wasQuoted)
            {
                inQuotes = true;
                wasQuoted = true;
            }
            else if (c == delimiter)
            {
                EndCell();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                EndRow();
                line++;
                rowStart = line;
            }
            else
            {
                cell.Append(c);
            }
        }

        if (inQuotes)
        {
            throw TableFileException.Of("api.import.csv-unterminated-quote");
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            EndRow();
        }

        if (rows.Count == 0)
        {
            throw TableFileException.Of("api.import.file-empty");
        }

        return new TableFile(rows[0].Cells.Select(h => h.Trim()).ToList(), rows.Skip(1).ToList());
    }

    private static char DetectDelimiter(string text)
    {
        var first = 0;
        var inQuotes = false;
        for (; first < text.Length; first++)
        {
            if (text[first] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && text[first] is '\r' or '\n')
            {
                break;
            }
        }

        var header = text[..Math.Min(first, text.Length)];
        char[] candidates = [',', ';', '\t'];
        return candidates.OrderByDescending(d => header.Count(c => c == d)).First();
    }

    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.Append('﻿');
        AppendLine(builder, header);
        foreach (var row in rows)
        {
            AppendLine(builder, row);
        }

        return new UTF8Encoding(false).GetBytes(builder.ToString());
    }

    private static void AppendLine(StringBuilder builder, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var value = cells[i];
            if (value.AsSpan().IndexOfAny(",\"\r\n") >= 0)
            {
                builder.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                builder.Append(value);
            }
        }

        builder.Append("\r\n");
    }
}

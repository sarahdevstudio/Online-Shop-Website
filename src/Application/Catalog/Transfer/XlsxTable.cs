using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Shop.Application.Catalog.Transfer;

/// <summary>
/// Excel (.xlsx) in and out without a library. Reading is defensive: the archive is size-limited while it is read (declared sizes
/// are not trusted), XML is parsed with DTDs prohibited and no resolver (no entity bombs, no external reads), and only the
/// first worksheet's values are used (formulas are never evaluated: the cached value, if any, is read as text).
/// </summary>
public static class XlsxTable
{
    private static readonly XmlReaderSettings Safe = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = TableLimits.MaxUncompressedBytes,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static TableFile Read(byte[] bytes)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (archive.Entries.Count > TableLimits.MaxZipEntries)
            {
                throw TableFileException.Of("api.import.file-damaged");
            }

            var budget = new Budget(TableLimits.MaxUncompressedBytes);
            var sheetPath = FindFirstSheet(archive, budget);
            var shared = ReadSharedStrings(archive, budget);
            var rows = ReadSheet(archive, sheetPath, shared, budget);
            if (rows.Count == 0)
            {
                throw TableFileException.Of("api.import.file-empty");
            }

            var width = rows[0].Cells.Count;
            return new TableFile(rows[0].Cells.Select(c => c.Trim()).ToList(), rows.Skip(1).Select(r => new TableRow(r.Number, Pad(r.Cells, width))).ToList());
        }
        catch (InvalidDataException)
        {
            throw TableFileException.Of("api.import.file-damaged");
        }
        catch (XmlException)
        {
            throw TableFileException.Of("api.import.file-damaged");
        }
        catch (IOException)
        {
            throw TableFileException.Of("api.import.file-damaged");
        }
    }

    private static List<string> Pad(IReadOnlyList<string> cells, int width)
    {
        var list = cells.ToList();
        while (list.Count < width)
        {
            list.Add(string.Empty);
        }

        return list;
    }

    /// <summary>Counts every byte read from the archive, so a file that inflates beyond the limit stops the read.</summary>
    private sealed class Budget(long max)
    {
        private long _used;

        public Stream Open(ZipArchiveEntry entry) => new Counting(entry.Open(), this);

        private void Spend(int count)
        {
            _used += count;
            if (_used > max)
            {
                throw TableFileException.Of("api.import.file-too-large");
            }
        }

        private sealed class Counting(Stream inner, Budget budget) : Stream
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = inner.Read(buffer, offset, count);
                budget.Spend(read);
                return read;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }

    private static ZipArchiveEntry? Entry(ZipArchive archive, string path) =>
        archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));

    private static string FindFirstSheet(ZipArchive archive, Budget budget)
    {
        var workbook = Entry(archive, "xl/workbook.xml");
        var rels = Entry(archive, "xl/_rels/workbook.xml.rels");
        if (workbook is null || rels is null)
        {
            throw TableFileException.Of("api.import.file-not-excel");
        }

        string? relId = null;
        using (var reader = XmlReader.Create(budget.Open(workbook), Safe))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet" && reader.NamespaceURI == MainNs)
                {
                    relId = reader.GetAttribute("id", RelNs);
                    break;
                }
            }
        }

        if (relId is null)
        {
            throw TableFileException.Of("api.import.file-empty");
        }

        string? target = null;
        using (var reader = XmlReader.Create(budget.Open(rels), Safe))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship" && reader.GetAttribute("Id") == relId)
                {
                    target = reader.GetAttribute("Target");
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(target))
        {
            throw TableFileException.Of("api.import.file-damaged");
        }

        var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        return Entry(archive, path) is null ? throw TableFileException.Of("api.import.file-damaged") : path;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive, Budget budget)
    {
        var strings = new List<string>();
        var entry = Entry(archive, "xl/sharedStrings.xml");
        if (entry is null)
        {
            return strings;
        }

        using var reader = XmlReader.Create(budget.Open(entry), Safe);
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si" && reader.NamespaceURI == MainNs)
            {
                strings.Add(TextOf((XElement)XNode.ReadFrom(reader)));
                if (strings.Count > TableLimits.MaxRows * TableLimits.MaxColumns)
                {
                    throw TableFileException.Of("api.import.file-too-large");
                }
            }
            else
            {
                reader.Read();
            }
        }

        return strings;
    }

    /// <summary>All &lt;t&gt; runs of a shared or inline string, without the phonetic hints (&lt;rPh&gt;).</summary>
    private static string TextOf(XElement element) =>
        string.Concat(element.Descendants(XName.Get("t", MainNs)).Where(t => !t.Ancestors(XName.Get("rPh", MainNs)).Any()).Select(t => t.Value));

    private static List<TableRow> ReadSheet(ZipArchive archive, string path, List<string> shared, Budget budget)
    {
        var rows = new List<TableRow>();
        using var reader = XmlReader.Create(budget.Open(Entry(archive, path)!), Safe);
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row" || reader.NamespaceURI != MainNs)
            {
                reader.Read();
                continue;
            }

            var row = (XElement)XNode.ReadFrom(reader);
            var number = int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : rows.Count + 1;
            if (number > TableLimits.MaxRows + 1)
            {
                throw TableFileException.Of("api.import.too-many-rows", ("max", TableLimits.MaxRows));
            }

            var cells = new List<string>();
            foreach (var cell in row.Elements(XName.Get("c", MainNs)))
            {
                var column = ColumnIndex((string?)cell.Attribute("r"), cells.Count);
                if (column >= TableLimits.MaxColumns)
                {
                    throw TableFileException.Of("api.import.too-many-columns", ("max", TableLimits.MaxColumns));
                }

                while (cells.Count < column)
                {
                    cells.Add(string.Empty);
                }

                cells.Add(CellText(cell, shared, number));
            }

            if (cells.Any(c => c.Length > 0))
            {
                rows.Add(new TableRow(number, cells));
            }
        }

        return rows;
    }

    private static int ColumnIndex(string? reference, int fallback)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return fallback;
        }

        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsAsciiLetter(ch))
            {
                break;
            }

            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            if (index > 10_000)
            {
                break;
            }
        }

        return index == 0 ? fallback : index - 1;
    }

    private static string CellText(XElement cell, List<string> shared, int row)
    {
        var type = (string?)cell.Attribute("t");
        var value = cell.Element(XName.Get("v", MainNs))?.Value ?? string.Empty;
        var text = type switch
        {
            "s" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= 0 && i < shared.Count ? shared[i] : string.Empty,
            "inlineStr" => cell.Element(XName.Get("is", MainNs)) is { } inline ? TextOf(inline) : string.Empty,
            "e" => string.Empty, // an error value (#REF!, ...) is an empty cell
            "b" => value == "1" ? "1" : "0",
            "str" => value,
            _ => NormalizeNumber(value),
        };
        if (text.Length > TableLimits.MaxCellLength)
        {
            throw TableFileException.Of("api.import.cell-too-long", ("row", row), ("max", TableLimits.MaxCellLength));
        }

        return text;
    }

    /// <summary>Excel stores 150000 as "150000" but also 1.5E+05 or 150000.0: a whole number is written without exponent or fraction.</summary>
    private static string NormalizeNumber(string value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == decimal.Truncate(number) && Math.Abs(number) < 1e15m
            ? decimal.Truncate(number).ToString(CultureInfo.InvariantCulture)
            : value;

    // ---- writing ----------------------------------------------------------------------------------------------------------

    /// <summary>One sheet, right-to-left, header row bold and frozen. Cells in <paramref name="numericColumns"/> are numbers, the rest text.</summary>
    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows, ISet<int> numericColumns, string sheetName)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "[Content_Types].xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Add(archive, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(archive, "xl/_rels/workbook.xml.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Add(archive, "xl/workbook.xml",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{MainNs}" xmlns:r="{RelNs}"><sheets><sheet name="{System.Security.SecurityElement.Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add(archive, "xl/styles.xml",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="{MainNs}"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs></styleSheet>""");

            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
            xml.WriteStartDocument(true);
            xml.WriteStartElement("worksheet", MainNs);
            xml.WriteStartElement("sheetViews");
            xml.WriteStartElement("sheetView");
            xml.WriteAttributeString("rightToLeft", "1");
            xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane");
            xml.WriteAttributeString("ySplit", "1");
            xml.WriteAttributeString("topLeftCell", "A2");
            xml.WriteAttributeString("activePane", "bottomLeft");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteStartElement("sheetData");
            WriteRow(xml, 1, header, null, header: true);
            var rowNumber = 2;
            foreach (var row in rows)
            {
                WriteRow(xml, rowNumber++, row, numericColumns, header: false);
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return output.ToArray();
    }

    private static void Add(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteRow(XmlWriter xml, int number, IReadOnlyList<string> cells, ISet<int>? numeric, bool header)
    {
        xml.WriteStartElement("row");
        xml.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < cells.Count; i++)
        {
            var value = cells[i];
            if (value.Length == 0)
            {
                continue;
            }

            xml.WriteStartElement("c");
            xml.WriteAttributeString("r", ColumnName(i) + number.ToString(CultureInfo.InvariantCulture));
            if (header)
            {
                xml.WriteAttributeString("s", "1");
            }

            if (numeric is not null && numeric.Contains(i) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                xml.WriteElementString("v", value);
            }
            else
            {
                xml.WriteAttributeString("t", "inlineStr");
                xml.WriteStartElement("is");
                xml.WriteStartElement("t");
                xml.WriteAttributeString("xml", "space", null, "preserve");
                xml.WriteString(XmlSafe(value));
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    /// <summary>Characters that are not allowed in XML 1.0 are dropped.</summary>
    private static string XmlSafe(string value) => new(value.Where(XmlConvert.IsXmlChar).ToArray());

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + (n - 1) % 26) + name;
        }

        return name;
    }
}

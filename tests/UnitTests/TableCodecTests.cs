using System.IO.Compression;
using System.Text;
using Shop.Application.Catalog.Transfer;

namespace Shop.UnitTests;

/// <summary>CSV and Excel reading and writing, including files that are malformed or hostile (docs/16).</summary>
public class TableCodecTests
{
    private static TableFile Csv(string text, bool bom = false) =>
        TableCodec.Read(new MemoryStream((bom ? [0xEF, 0xBB, 0xBF] : Array.Empty<byte>()).Concat(Encoding.UTF8.GetBytes(text)).ToArray()), "products.csv");

    private static Exception Rejected(Func<TableFile> read) => Assert.Throws<TableFileException>(() => read());

    // ---- CSV ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Csv_ReadsPersianText_QuotesAndEmbeddedNewlines_WithOrWithoutBom()
    {
        var table = Csv("sku,name,description\r\nA1,\"لامپ، ۹ وات\",\"سطر اول\r\nسطر دوم \"\"نقل‌قول\"\"\"\r\nA2,x,\r\n", bom: true);
        Assert.Equal(["sku", "name", "description"], table.Header);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("لامپ، ۹ وات", table.Rows[0].Cells[1]);
        Assert.Equal("سطر اول\r\nسطر دوم \"نقل‌قول\"", table.Rows[0].Cells[2]);
        Assert.Equal(2, table.Rows[0].Number);
        Assert.Equal(4, table.Rows[1].Number); // the quoted cell spans two physical lines
    }

    [Theory]
    [InlineData(';')]
    [InlineData('\t')]
    public void Csv_DetectsTheDelimiterExcelUses(char delimiter)
    {
        var table = Csv($"sku{delimiter}price{delimiter}stock\nA1{delimiter}1000{delimiter}5\n");
        Assert.Equal(["sku", "price", "stock"], table.Header);
        Assert.Equal(["A1", "1000", "5"], table.Rows[0].Cells);
    }

    [Fact]
    public void Csv_SkipsBlankLines_AndKeepsRowNumbers()
    {
        var table = Csv("a,b\n\n1,2\n,\n3,4\n");
        Assert.Equal([3, 5], table.Rows.Select(r => r.Number));
    }

    [Fact]
    public void Csv_Rejects_Empty_NotUtf8_NulBytes_UnterminatedQuote()
    {
        Assert.Contains("file-empty", Rejected(() => Csv("")).Message);
        Assert.Contains("file-empty", Rejected(() => Csv("\n\n")).Message);
        Assert.Contains("not-utf8", Rejected(() => TableCodec.Read(new MemoryStream([0x61, 0x2C, 0xFF, 0xFE, 0x0A]), "x.csv")).Message);
        Assert.Contains("file-damaged", Rejected(() => Csv("a,b\n1,\0\n")).Message);
        Assert.Contains("unterminated", Rejected(() => Csv("a,b\n1,\"oops\n")).Message);
    }

    [Fact]
    public void Csv_EnforcesRowColumnCellAndSizeLimits()
    {
        var rows = string.Concat(Enumerable.Repeat("x,y\n", TableLimits.MaxRows + 5));
        Assert.Contains("too-many-rows", Rejected(() => Csv("a,b\n" + rows)).Message);
        Assert.Contains("too-many-columns", Rejected(() => Csv(string.Join(',', Enumerable.Range(0, TableLimits.MaxColumns + 1)) + "\n")).Message);
        Assert.Contains("cell-too-long", Rejected(() => Csv("a\n" + new string('x', TableLimits.MaxCellLength + 1))).Message);
        Assert.Contains("file-too-large", Rejected(() => TableCodec.Read(new MemoryStream(new byte[TableLimits.MaxFileBytes + 1]), "big.csv")).Message);
        // Too large without the length being known up front (a non-seekable stream).
        Assert.Contains("file-too-large", Rejected(() => TableCodec.Read(new NonSeekable(new byte[TableLimits.MaxFileBytes + 1]), "big.csv")).Message);
    }

    [Fact]
    public void Csv_WriteThenRead_RoundTrips_AndQuotesWhatNeedsIt()
    {
        var bytes = CsvTable.Write(["a", "b"], [["x,y", "say \"hi\""], ["سطر\nدو", ""]]);
        Assert.Equal(0xEF, bytes[0]); // BOM, so Excel opens Persian text correctly
        var table = CsvTable.Read(bytes);
        Assert.Equal(["x,y", "say \"hi\""], table.Rows[0].Cells);
        Assert.Equal(["سطر\nدو", ""], table.Rows[1].Cells);
    }

    // ---- XLSX -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Xlsx_WriteThenRead_RoundTrips_PersianNumbersAndXmlSpecialCharacters()
    {
        var bytes = XlsxTable.Write(["sku", "name", "price"], [["A1", "لامپ <LED> & \"قاب\"", "150000"], ["A2", "", "0"], ["A3", "x\u0001y", "7"]], new HashSet<int> { 2 }, "products");
        var table = TableCodec.Read(new MemoryStream(bytes), "export.xlsx");
        Assert.Equal(["sku", "name", "price"], table.Header);
        Assert.Equal(["A1", "لامپ <LED> & \"قاب\"", "150000"], table.Rows[0].Cells);
        Assert.Equal(["A2", "", "0"], table.Rows[1].Cells);
        Assert.Equal("xy", table.Rows[2].Cells[1]); // a character XML cannot carry is dropped, the file stays valid
    }

    [Fact]
    public void Xlsx_ReadsSharedStrings_RichText_InlineStrings_SparseCells_AndNormalizesNumbers()
    {
        var sheet = """
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
              <row r="1"><c r="A1" t="s"><v>0</v></c><c r="C1" t="s"><v>1</v></c></row>
              <row r="2"><c r="A2" t="inlineStr"><is><t>inline</t></is></c><c r="B2"><v>1.5E+05</v></c><c r="C2"><v>150000.0</v></c></row>
              <row r="3"/>
              <row r="4"><c r="B4" t="b"><v>1</v></c><c r="C4" t="e"><v>#REF!</v></c><c r="D4"><f>SUM(A1)</f><v>3</v></c></row>
            </sheetData></worksheet>
            """;
        var shared = """
            <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <si><t>sku</t></si><si><r><t>pri</t></r><r><t>ce</t></r><rPh sb="0" eb="1"><t>ignored</t></rPh></si>
            </sst>
            """;
        var table = TableCodec.Read(new MemoryStream(Workbook(sheet, shared)), "a.xlsx");
        Assert.Equal(["sku", "", "price"], table.Header);
        Assert.Equal(["inline", "150000", "150000"], table.Rows[0].Cells);
        Assert.Equal(["", "1", "", "3"], table.Rows[1].Cells.Concat([""]).Take(4)); // boolean, error value as empty, formula's cached value
        Assert.Equal([2, 4], table.Rows.Select(r => r.Number));
    }

    [Fact]
    public void Xlsx_Rejects_NotAZip_MissingParts_TruncatedFiles_AndTypeMismatches()
    {
        Assert.Contains("type-mismatch", Rejected(() => TableCodec.Read(new MemoryStream("a,b\n1,2"u8.ToArray()), "x.xlsx")).Message);
        Assert.Contains("type-mismatch", Rejected(() => TableCodec.Read(new MemoryStream(XlsxTable.Write(["a"], [["1"]], new HashSet<int>(), "s")), "x.csv")).Message);
        Assert.Contains("file-type", Rejected(() => TableCodec.Read(new MemoryStream([1, 2, 3]), "x.xls")).Message);
        Assert.Contains("file-type", Rejected(() => TableCodec.Read(new MemoryStream([1, 2, 3]), "x")).Message);

        // A real zip that is not a workbook.
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open());
            writer.Write("hello");
        }

        Assert.Contains("not-excel", Rejected(() => TableCodec.Read(new MemoryStream(zip.ToArray()), "x.xlsx")).Message);

        // Cut in the middle: damaged, never an unhandled exception.
        var good = XlsxTable.Write(["a", "b"], Enumerable.Range(0, 50).Select(i => (IReadOnlyList<string>)[i.ToString(), "متن"]), new HashSet<int>(), "s");
        Assert.Contains("file-damaged", Rejected(() => TableCodec.Read(new MemoryStream(good[..(good.Length / 2)]), "x.xlsx")).Message);
        Assert.Contains("file-damaged", Rejected(() => TableCodec.Read(new MemoryStream("PK\u0003\u0004garbage"u8.ToArray()), "x.xlsx")).Message);
    }

    [Fact]
    public void Xlsx_Rejects_AnEntityBomb_AndAnArchiveThatInflatesBeyondTheLimit()
    {
        var bomb = """
            <?xml version="1.0"?><!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">]>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>&lol2;</t></is></c></row></sheetData></worksheet>
            """;
        Assert.Contains("file-damaged", Rejected(() => TableCodec.Read(new MemoryStream(Workbook(bomb, null)), "x.xlsx")).Message);

        // 30 MB of spaces compress to a few KB: the read stops at the limit instead of filling memory.
        var padding = new string(' ', 30 * 1024 * 1024);
        var big = $"""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{padding}</sheetData></worksheet>""";
        var bytes = Workbook(big, null);
        Assert.True(bytes.Length < TableLimits.MaxFileBytes);
        Assert.Contains("file-too-large", Rejected(() => TableCodec.Read(new MemoryStream(bytes), "x.xlsx")).Message);
    }

    [Fact]
    public void Xlsx_EnforcesRowAndColumnLimits()
    {
        var rows = string.Concat(Enumerable.Range(1, TableLimits.MaxRows + 5).Select(i => $"""<row r="{i}"><c r="A{i}" t="inlineStr"><is><t>x</t></is></c></row>"""));
        var sheet = $"""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{rows}</sheetData></worksheet>""";
        Assert.Contains("too-many-rows", Rejected(() => TableCodec.Read(new MemoryStream(Workbook(sheet, null)), "x.xlsx")).Message);

        var wide = """<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="ZZ1" t="inlineStr"><is><t>x</t></is></c></row></sheetData></worksheet>""";
        Assert.Contains("too-many-columns", Rejected(() => TableCodec.Read(new MemoryStream(Workbook(wide, null)), "x.xlsx")).Message);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------------------------

    private static byte[] Workbook(string sheetXml, string? sharedXml)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(archive, "xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="s" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add(archive, "xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="x" Target="worksheets/sheet1.xml"/></Relationships>""");
            Add(archive, "xl/worksheets/sheet1.xml", sheetXml);
            if (sharedXml is not null)
            {
                Add(archive, "xl/sharedStrings.xml", sharedXml);
            }
        }

        return output.ToArray();
    }

    private static void Add(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private sealed class NonSeekable(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}

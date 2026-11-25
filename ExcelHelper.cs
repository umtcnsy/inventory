using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace EnvanterSayimApp
{
    public class ExcelImportResult
    {
        public int RoomsImported { get; set; }
        public int ProductsAdded { get; set; }
        public int CellEntriesUpdated { get; set; }
        public int SerialsImported { get; set; }
        public int SerialsUpdated { get; set; }
    }

    public static class ExcelHelper
    {
        // =====================================================================
        // 1. GERÇEK .XLSX EXPORT (2 SEKME: "Envanter" MATRİS + "Laptop_Seri_Nolar")
        // =====================================================================
        public static void ExportToXlsx(InventoryStore store, string filePath)
        {
            bool isEn = store.Language == "en";
            string sheet1Name = isEn ? "Inventory" : "Envanter";
            string sheet2Name = isEn ? "Laptop_Serials" : "Laptop_Seri_Nolar";

            // Sheet 1 (Matris): Room | Ürün 1 | Ürün 2 | ...
            var products = store.Products.ToList();
            var sheet1Rows = new List<List<object>>();

            var header1 = new List<object> { "Room" };
            foreach (var p in products)
                header1.Add(AppHelper.LocalizeText(p.Name, isEn));
            sheet1Rows.Add(header1);

            foreach (var room in store.Rooms)
            {
                var row = new List<object> { room.Name };
                foreach (var p in products)
                {
                    int qty = room.GetQuantity(p.Id);
                    if (qty > 0)
                        row.Add(qty);
                    else
                        row.Add("");
                }
                sheet1Rows.Add(row);
            }

            // Sheet 2 (Laptop/PC Seri Numaraları): Room | Laptop | NO | SERİ NO | DURUM
            var sheet2Rows = BuildLaptopSheetRows(store, null, null);

            // .xlsx (OpenXML ZIP) dosyasını oluştur
            if (File.Exists(filePath)) File.Delete(filePath);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteZipText(zip, "[Content_Types].xml", BuildContentTypesXml());
                WriteZipText(zip, "_rels/.rels", BuildRootRelsXml());
                WriteZipText(zip, "xl/workbook.xml", BuildWorkbookXml(sheet1Name, sheet2Name));
                WriteZipText(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
                WriteZipText(zip, "xl/styles.xml", BuildStylesXml());
                WriteZipText(zip, "xl/worksheets/sheet1.xml", BuildWorksheetXml(sheet1Rows, new[] { 20.0 }, 18.0));
                WriteZipText(zip, "xl/worksheets/sheet2.xml", BuildWorksheetXml(sheet2Rows, new[] { 20.0, 22.0, 8.0, 28.0, 22.0 }, 20.0));
            }
        }

        // =====================================================================
        // 1B. SADECE LAPTOPLAR İÇİN AYRI .XLSX VE .CSV EXPORT
        // =====================================================================
        public static void ExportLaptopsToXlsx(InventoryStore store, string filePath, RoomData filterRoom = null, ProductItem filterProduct = null)
        {
            bool isEn = store.Language == "en";
            string sheetName = isEn ? "Laptop_List" : "Laptop_Listesi";
            var rows = BuildLaptopSheetRows(store, filterRoom, filterProduct);

            if (File.Exists(filePath)) File.Delete(filePath);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteZipText(zip, "[Content_Types].xml", BuildSingleSheetContentTypesXml());
                WriteZipText(zip, "_rels/.rels", BuildRootRelsXml());
                WriteZipText(zip, "xl/workbook.xml", BuildSingleSheetWorkbookXml(sheetName));
                WriteZipText(zip, "xl/_rels/workbook.xml.rels", BuildSingleSheetWorkbookRelsXml());
                WriteZipText(zip, "xl/styles.xml", BuildStylesXml());
                WriteZipText(zip, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows, new[] { 20.0, 22.0, 8.0, 28.0, 22.0 }, 20.0));
            }
        }

        public static void ExportLaptopsToCsv(InventoryStore store, string filePath, RoomData filterRoom = null, ProductItem filterProduct = null)
        {
            var rows = BuildLaptopSheetRows(store, filterRoom, filterProduct);
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                var cells = row.Select(v =>
                {
                    string s = v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
                    return $"\"{s.Replace("\"", "\"\"")}\"";
                });
                sb.AppendLine(string.Join(";", cells));
            }
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        // =====================================================================
        // 1C. AYLIK KARŞILAŞTIRMA RAPORU .XLSX EXPORT
        // =====================================================================
        public static void ExportComparisonToXlsx(InventoryStore store, string page1Label, string page2Label, List<ComparisonRow> items, string filePath)
        {
            bool isEn = store.Language == "en";
            string sheetName = isEn ? "Comparison" : "Karsilastirma";
            var rows = new List<List<object>>
            {
                new List<object>
                {
                    isEn ? "Product Name" : "Ürün Adı",
                    isEn ? $"{page1Label} Qty" : $"{page1Label} Miktarı",
                    isEn ? $"{page2Label} Qty" : $"{page2Label} Miktarı",
                    isEn ? "Diff (+/-)" : "Fark (+/-)",
                    isEn ? "Status" : "Durum"
                }
            };

            foreach (var it in items)
            {
                string diffText = it.Diff > 0 ? "+" + it.Diff : it.Diff.ToString();
                rows.Add(new List<object>
                {
                    AppHelper.LocalizeText(it.ProductName, isEn),
                    it.Qty1,
                    it.Qty2,
                    diffText,
                    AppHelper.GetComparisonStatusLabel(it.StatusCode, isEn)
                });
            }

            if (File.Exists(filePath)) File.Delete(filePath);
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteZipText(zip, "[Content_Types].xml", BuildSingleSheetContentTypesXml());
                WriteZipText(zip, "_rels/.rels", BuildRootRelsXml());
                WriteZipText(zip, "xl/workbook.xml", BuildSingleSheetWorkbookXml(sheetName));
                WriteZipText(zip, "xl/_rels/workbook.xml.rels", BuildSingleSheetWorkbookRelsXml());
                WriteZipText(zip, "xl/styles.xml", BuildStylesXml());
                WriteZipText(zip, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows, new[] { 30.0, 24.0, 24.0, 14.0, 16.0 }, 20.0));
            }
        }

        private static List<List<object>> BuildLaptopSheetRows(InventoryStore store, RoomData filterRoom, ProductItem filterProduct)
        {
            bool isEn = store.Language == "en";
            var products = store.Products.ToList();
            var rows = new List<List<object>>();
            rows.Add(new List<object>
            {
                "Room",
                "Laptop",
                "NO",
                isEn ? "SERIAL NO" : "SERİ NO",
                isEn ? "STATUS" : "DURUM"
            });

            var targetRooms = filterRoom != null ? new List<RoomData> { filterRoom } : store.Rooms;
            bool anySerialWritten = false;

            foreach (var room in targetRooms)
            {
                foreach (var item in room.Items)
                {
                    if (filterProduct != null && item.ProductId != filterProduct.Id) continue;

                    var prod = products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (prod == null) continue;

                    var serials = item.Serials ?? new List<SerialRecord>();
                    bool isLaptop = AppHelper.IsLaptopProduct(prod.Name);
                    if (filterProduct == null && !isLaptop && serials.Count == 0) continue;

                    string prodLabel = AppHelper.LocalizeText(prod.Name, isEn);
                    int writtenForThis = 0;

                    if (serials.Count > 0)
                    {
                        for (int i = 0; i < serials.Count; i++)
                        {
                            rows.Add(new List<object>
                            {
                                room.Name,
                                prodLabel,
                                i + 1,
                                serials[i].SerialNo ?? "",
                                AppHelper.LocalizeText(serials[i].Status ?? "", isEn)
                            });
                            anySerialWritten = true;
                            writtenForThis++;
                        }
                    }

                    // Adet > kayıtlı seri no ise kullanıcının Excel'de rahat doldurması için kalan boş şablon satırlarını ekle
                    int targetRows = Math.Min(item.Quantity, 50);
                    if ((isLaptop || filterProduct != null) && targetRows > writtenForThis)
                    {
                        for (int k = writtenForThis + 1; k <= targetRows; k++)
                        {
                            rows.Add(new List<object>
                            {
                                room.Name,
                                prodLabel,
                                k,
                                "",
                                isEn ? "Active" : "Aktif"
                            });
                        }
                    }
                }
            }

            // Hiç satır yoksa kullanıcıya hazır örnek başlık altı boş şablon satırı bırak
            if (!anySerialWritten && rows.Count == 1)
            {
                string defaultRoom = filterRoom != null ? filterRoom.Name : (store.Rooms.FirstOrDefault()?.Name ?? "inventory");
                string defaultProd = filterProduct != null ? AppHelper.LocalizeText(filterProduct.Name, isEn) : "dell laptop";
                rows.Add(new List<object> { defaultRoom, defaultProd, 1, "", isEn ? "Active" : "Aktif" });
            }

            return rows;
        }

        private static void WriteZipText(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var sw = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                sw.Write(content);
            }
        }

        private static string BuildContentTypesXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet2.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>";
        }

        private static string BuildRootRelsXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";
        }

        private static string BuildWorkbookXml(string sheet1Name, string sheet2Name)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""{XmlEscape(sheet1Name)}"" sheetId=""1"" r:id=""rId1""/>
    <sheet name=""{XmlEscape(sheet2Name)}"" sheetId=""2"" r:id=""rId2""/>
  </sheets>
</workbook>";
        }

        private static string BuildWorkbookRelsXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet2.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";
        }

        private static string BuildSingleSheetContentTypesXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>";
        }

        private static string BuildSingleSheetWorkbookXml(string sheetName)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""{XmlEscape(sheetName)}"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>";
        }

        private static string BuildSingleSheetWorkbookRelsXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";
        }

        private static string BuildStylesXml()
        {
            // Style 0: normal, Style 1: Header (koyu arka plan, beyaz kalın yazı, kenarlık), Style 2: Veri hücresi (kenarlıklı)
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font><sz val=""11""/><name val=""Calibri""/></font>
    <font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font>
  </fonts>
  <fills count=""3"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FF18181B""/><bgColor indexed=""64""/></patternFill></fill>
  </fills>
  <borders count=""2"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style=""thin""><color rgb=""FFD4D4D8""/></left>
      <right style=""thin""><color rgb=""FFD4D4D8""/></right>
      <top style=""thin""><color rgb=""FFD4D4D8""/></top>
      <bottom style=""thin""><color rgb=""FFD4D4D8""/></bottom>
      <diagonal/>
    </border>
  </borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""3"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1"">
      <alignment horizontal=""left"" vertical=""center""/>
    </xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1"">
      <alignment vertical=""center""/>
    </xf>
  </cellXfs>
</styleSheet>";
        }

        private static string BuildWorksheetXml(List<List<object>> rows, double[] fixedWidths, double defaultWidth)
        {
            int maxCols = rows.Count > 0 ? rows.Max(r => r.Count) : 1;
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");

            sb.AppendLine("  <cols>");
            for (int c = 1; c <= maxCols; c++)
            {
                double w = (fixedWidths != null && c - 1 < fixedWidths.Length) ? fixedWidths[c - 1] : defaultWidth;
                sb.AppendLine($@"    <col min=""{c}"" max=""{c}"" width=""{w.ToString(CultureInfo.InvariantCulture)}"" customWidth=""1""/>");
            }
            sb.AppendLine("  </cols>");

            sb.AppendLine("  <sheetData>");
            for (int rIdx = 0; rIdx < rows.Count; rIdx++)
            {
                int excelRow = rIdx + 1;
                string styleIdx = (rIdx == 0) ? "1" : "2";
                string rowHeight = (rIdx == 0) ? @" ht=""24"" customHeight=""1""" : @" ht=""20"" customHeight=""1""";
                sb.AppendLine($@"    <row r=""{excelRow}""{rowHeight}>");

                var row = rows[rIdx];
                for (int cIdx = 0; cIdx < row.Count; cIdx++)
                {
                    object val = row[cIdx];
                    string colRef = ColumnIndexToLetter(cIdx) + excelRow;
                    if (val == null || (val is string sVal && string.IsNullOrEmpty(sVal)))
                    {
                        sb.AppendLine($@"      <c r=""{colRef}"" s=""{styleIdx}""/>");
                    }
                    else if (val is int || val is long || val is double)
                    {
                        sb.AppendLine($@"      <c r=""{colRef}"" s=""{styleIdx}""><v>{Convert.ToString(val, CultureInfo.InvariantCulture)}</v></c>");
                    }
                    else
                    {
                        sb.AppendLine($@"      <c r=""{colRef}"" t=""inlineStr"" s=""{styleIdx}""><is><t>{XmlEscape(val.ToString())}</t></is></c>");
                    }
                }
                sb.AppendLine("    </row>");
            }
            sb.AppendLine("  </sheetData>");
            sb.AppendLine("</worksheet>");
            return sb.ToString();
        }

        private static string ColumnIndexToLetter(int zeroBasedCol)
        {
            int dividend = zeroBasedCol + 1;
            string colName = "";
            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                colName = Convert.ToChar('A' + modulo) + colName;
                dividend = (dividend - modulo) / 26;
            }
            return colName;
        }

        private static int CellRefToColIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return 0;
            int col = 0;
            foreach (char ch in cellRef)
            {
                if (char.IsLetter(ch))
                {
                    col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
                }
                else break;
            }
            return Math.Max(0, col - 1);
        }

        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }

        // =====================================================================
        // 2. GERÇEK .XLSX VE .CSV IMPORT (MATRİS + LAPTOP SERİ NO DESTEKLİ)
        // =====================================================================
        public static ExcelImportResult ImportFromExcelOrCsv(InventoryStore store, string filePath, bool replaceExistingRooms)
        {
            var result = new ExcelImportResult();
            string ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";

            var allSheets = new List<List<List<string>>>();

            if (ext == ".xlsx")
            {
                allSheets = ReadXlsxSheets(filePath);
            }
            else
            {
                var csvRows = ReadCsvRows(filePath);
                if (csvRows.Count > 0) allSheets.Add(csvRows);
            }

            if (allSheets.Count == 0)
                throw new Exception("Dosyada okunabilir veri bulunamadı.");

            if (replaceExistingRooms)
            {
                store.Rooms.Clear();
            }

            var allTouchedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sheetRows in allSheets)
            {
                if (sheetRows == null || sheetRows.Count < 2) continue;

                var header = sheetRows[0].Select(h => (h ?? "").Trim()).ToList();
                if (header.Count < 2) continue;

                // Bu sekme bir "Laptop Seri No Tablosu" mu? (SERİ NO / SERIAL NO başlığı içeriyorsa)
                var normHeader = header.Select(NormalizeHeader).ToList();
                int serialColIdx = normHeader.FindIndex(h =>
                    h == "SERI NO" || h == "SERIAL NO" || h == "SERIAL" ||
                    h == "SERI NUMARASI" || h == "SERIAL NUMBER" || h == "SN");

                if (serialColIdx >= 0)
                {
                    ImportSerialSheet(store, sheetRows, normHeader, serialColIdx, result, allTouchedRooms);
                }
                else
                {
                    // Matris formatı: | Room | kablolu mouse | HyperX headset | logitec | ...
                    ImportMatrixSheet(store, sheetRows, header, result, allTouchedRooms);
                }
            }

            result.RoomsImported = allTouchedRooms.Count;
            return result;
        }

        private static void ImportMatrixSheet(InventoryStore store, List<List<string>> rows, List<string> header, ExcelImportResult result, HashSet<string> touchedRooms)
        {
            // Sütun 1..N arası ürün adları
            var colToProduct = new Dictionary<int, ProductItem>();

            for (int c = 1; c < header.Count; c++)
            {
                string rawProdName = (header[c] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(rawProdName)) continue;

                var prod = FindOrCreateProduct(store, rawProdName, result);
                colToProduct[c] = prod;
            }

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null || row.Count == 0) continue;

                string roomName = (row[0] ?? "").Trim();
                // Boş oda satırlarını (örneğin kullanıcının ekran görüntüsündeki 6-7. boş satırlar) atla
                if (string.IsNullOrWhiteSpace(roomName)) continue;

                // Tekrarlayan başlık veya özet satırı ise atla
                if (roomName.Equals("Room", StringComparison.OrdinalIgnoreCase) ||
                    roomName.Equals("Oda", StringComparison.OrdinalIgnoreCase) ||
                    roomName.StartsWith("GENEL TOPLAM", StringComparison.OrdinalIgnoreCase))
                    continue;

                var room = FindOrCreateRoom(store, roomName);
                touchedRooms.Add(room.Id);

                foreach (var kv in colToProduct)
                {
                    int colIdx = kv.Key;
                    var prod = kv.Value;
                    if (colIdx >= row.Count) continue;

                    string cellText = (row[colIdx] ?? "").Trim();
                    if (string.IsNullOrEmpty(cellText)) continue;

                    // Sayısal değeri ayrıştır (1, 2.0, 15 vb.)
                    if (double.TryParse(cellText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                    {
                        int qty = (int)Math.Round(dVal);
                        if (qty < 0) qty = 0;
                        if (qty > 0 || room.HasProduct(prod.Id))
                        {
                            room.SetQuantity(prod.Id, qty);
                            result.CellEntriesUpdated++;
                        }
                    }
                }
            }
        }

        // =====================================================================
        // 2B. SADECE LAPTOPLAR İÇİN AYRI .XLSX / .CSV IMPORT
        // (Diğer ürünlere ve odalara dokunmadan sadece laptop seri no listelerini yükler)
        // =====================================================================
        public static ExcelImportResult ImportLaptopsFromExcelOrCsv(
            InventoryStore store,
            string filePath,
            bool replaceExistingSerials,
            RoomData targetRoom = null,
            ProductItem targetProduct = null)
        {
            var result = new ExcelImportResult();
            string ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";

            var allSheets = new List<List<List<string>>>();
            if (ext == ".xlsx")
                allSheets = ReadXlsxSheets(filePath);
            else
            {
                var csvRows = ReadCsvRows(filePath);
                if (csvRows.Count > 0) allSheets.Add(csvRows);
            }

            if (allSheets.Count == 0)
                throw new Exception("Dosyada okunabilir laptop verisi bulunamadı.");

            if (replaceExistingSerials)
            {
                if (targetRoom != null && targetProduct != null)
                {
                    targetRoom.GetSerials(targetProduct.Id).Clear();
                }
                else
                {
                    foreach (var r in store.Rooms)
                    {
                        foreach (var it in r.Items)
                        {
                            var prod = store.Products.FirstOrDefault(p => p.Id == it.ProductId);
                            if (prod != null && (AppHelper.IsLaptopProduct(prod.Name) || (it.Serials != null && it.Serials.Count > 0)))
                            {
                                it.Serials?.Clear();
                            }
                        }
                    }
                }
            }

            // Önce açıkça SERİ NO / SERIAL NO başlığı taşıyan sekmeleri bul
            var serialSheets = new List<Tuple<List<List<string>>, List<string>, int>>();
            foreach (var sheetRows in allSheets)
            {
                if (sheetRows == null || sheetRows.Count == 0) continue;
                var header = sheetRows[0].Select(h => (h ?? "").Trim()).ToList();
                var normHeader = header.Select(NormalizeHeader).ToList();
                int serialColIdx = normHeader.FindIndex(h =>
                    h == "SERI NO" || h == "SERIAL NO" || h == "SERIAL" ||
                    h == "SERI NUMARASI" || h == "SERIAL NUMBER" || h == "SN");
                if (serialColIdx >= 0)
                {
                    serialSheets.Add(Tuple.Create(sheetRows, normHeader, serialColIdx));
                }
            }

            var allTouchedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (serialSheets.Count > 0)
            {
                foreach (var tuple in serialSheets)
                {
                    ImportSerialSheet(store, tuple.Item1, tuple.Item2, tuple.Item3, result, allTouchedRooms, targetRoom, targetProduct);
                }
            }
            else if (targetRoom != null && targetProduct != null)
            {
                // Tek laptop tablosundan yükleniyorsa ve başlık yoksa (ör. düz 1 veya 2 sütunlu seri no listesi)
                var sheetRows = allSheets[0];
                foreach (var row in sheetRows)
                {
                    if (row == null || row.Count == 0) continue;
                    string sNo = "";
                    string status = "";
                    if (row.Count == 1)
                    {
                        sNo = (row[0] ?? "").Trim();
                    }
                    else
                    {
                        string c0 = (row[0] ?? "").Trim();
                        string c1 = (row[1] ?? "").Trim();
                        string c2 = row.Count > 2 ? (row[2] ?? "").Trim() : "";
                        // Eğer 1. sütun sıra numarası (1, 2, 3...) ise 2. sütun seri no, 3. sütun durumdur
                        if (int.TryParse(c0, out _) && !string.IsNullOrWhiteSpace(c1))
                        {
                            sNo = c1;
                            status = c2;
                        }
                        else
                        {
                            sNo = c0;
                            status = c1;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(sNo)) continue;
                    string hCheck = NormalizeHeader(sNo);
                    if (hCheck == "SERI NO" || hCheck == "SERIAL NO" || hCheck == "NO") continue;

                    if (string.IsNullOrWhiteSpace(status)) status = store.Language == "en" ? "Active" : "Aktif";
                    UpsertSerial(targetRoom, targetProduct, sNo, status, result, allTouchedRooms);
                }
            }
            else
            {
                throw new Exception("Laptop listesinde 'SERİ NO' veya 'SERIAL NO' sütun başlığı bulunamadı.\nÖrnek başlık: Room | Laptop | NO | SERİ NO | DURUM");
            }

            result.RoomsImported = allTouchedRooms.Count;
            return result;
        }

        private static void ImportSerialSheet(
            InventoryStore store,
            List<List<string>> rows,
            List<string> normHeader,
            int serialColIdx,
            ExcelImportResult result,
            HashSet<string> touchedRooms,
            RoomData defaultRoom = null,
            ProductItem defaultProduct = null)
        {
            int roomColIdx = normHeader.FindIndex(h =>
                h == "ROOM" || h == "ODA" || h == "ODA ADI");

            int prodColIdx = normHeader.FindIndex(h =>
                h == "LAPTOP" || h == "LAPTOP MODEL" || h == "PC" || h == "BILGISAYAR" ||
                h == "PRODUCT" || h == "URUN" || h == "URUN ADI" ||
                h == "MODEL" || h == "CIHAZ" || h == "DEVICE");

            int statusColIdx = normHeader.FindIndex(h =>
                h == "DURUM" || h == "STATUS");

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null || serialColIdx >= row.Count) continue;

                string serialNo = (row[serialColIdx] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(serialNo)) continue; // Boş şablon satırlarını atla

                string roomName = (roomColIdx >= 0 && roomColIdx < row.Count ? row[roomColIdx] : "").Trim();
                string prodName = (prodColIdx >= 0 && prodColIdx < row.Count ? row[prodColIdx] : "").Trim();
                string status = (statusColIdx >= 0 && statusColIdx < row.Count ? row[statusColIdx] : "").Trim();

                if (string.IsNullOrWhiteSpace(status)) status = store.Language == "en" ? "Active" : "Aktif";

                RoomData room;
                if (defaultRoom != null && (string.IsNullOrWhiteSpace(roomName) || defaultProduct != null))
                    room = defaultRoom;
                else
                    room = FindOrCreateRoom(store, string.IsNullOrWhiteSpace(roomName) ? (defaultRoom?.Name ?? "inventory") : roomName);

                ProductItem prod;
                if (defaultProduct != null && (string.IsNullOrWhiteSpace(prodName) || defaultRoom != null))
                    prod = defaultProduct;
                else
                    prod = FindOrCreateProduct(store, string.IsNullOrWhiteSpace(prodName) ? (defaultProduct?.Name ?? "laptop") : prodName, result);

                UpsertSerial(room, prod, serialNo, status, result, touchedRooms);
            }
        }

        private static void UpsertSerial(RoomData room, ProductItem prod, string serialNo, string status, ExcelImportResult result, HashSet<string> touchedRooms)
        {
            if (!room.HasProduct(prod.Id))
            {
                room.SetQuantity(prod.Id, 1);
            }

            var list = room.GetSerials(prod.Id);
            var existingSerial = list.FirstOrDefault(x => x.SerialNo.Equals(serialNo, StringComparison.OrdinalIgnoreCase));
            if (existingSerial != null)
            {
                existingSerial.Status = status;
                result.SerialsUpdated++;
            }
            else
            {
                list.Add(new SerialRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SerialNo = serialNo,
                    Status = status
                });
                result.SerialsImported++;
            }

            touchedRooms.Add(room.Id);

            // Eğer odadaki adet, tablodaki seri no sayısından azsa otomatik yükselt
            if (room.GetQuantity(prod.Id) < list.Count)
            {
                room.SetQuantity(prod.Id, list.Count);
            }
        }

        private static RoomData FindOrCreateRoom(InventoryStore store, string roomName)
        {
            string normalized = NormalizeSpaces(roomName);
            var existing = store.Rooms.FirstOrDefault(r =>
                NormalizeSpaces(r.Name).Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var newRoom = new RoomData
            {
                Id = "r_" + Guid.NewGuid().ToString("N").Substring(0, 10),
                Name = normalized,
                Note = ""
            };
            store.Rooms.Add(newRoom);
            return newRoom;
        }

        private static ProductItem FindOrCreateProduct(InventoryStore store, string rawName, ExcelImportResult result)
        {
            string clean = NormalizeSpaces(rawName);
            string cleanEn = AppHelper.LocalizeText(clean, true);

            // Mevcut ürün havuzunda Türkçe veya İngilizce eşleşme ara (böylece fotoğraflı ürünler korunur!)
            var existing = store.Products.FirstOrDefault(p =>
            {
                string pNorm = NormalizeSpaces(p.Name);
                string pEn = AppHelper.LocalizeText(pNorm, true);
                return pNorm.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                       pEn.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                       pEn.Equals(cleanEn, StringComparison.OrdinalIgnoreCase);
            });

            if (existing != null) return existing;

            var created = new ProductItem
            {
                Id = "p_" + Guid.NewGuid().ToString("N").Substring(0, 10),
                Name = clean,
                PhotoBase64 = null
            };
            store.Products.Add(created);
            result.ProductsAdded++;
            return created;
        }

        private static string NormalizeSpaces(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var parts = input.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        private static string NormalizeHeader(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string s = NormalizeSpaces(input)
                .Replace('İ', 'I').Replace('ı', 'I').Replace('i', 'I')
                .Replace('Ü', 'U').Replace('ü', 'U')
                .Replace('Ö', 'O').Replace('ö', 'O')
                .Replace('Ş', 'S').Replace('ş', 'S')
                .Replace('Ğ', 'G').Replace('ğ', 'G')
                .Replace('Ç', 'C').Replace('ç', 'C');
            return s.ToUpperInvariant();
        }

        // OpenXML (.xlsx) Okuyucu (Microsoft Excel / Google Sheets / LibreOffice Tam Uyumlu)
        private static List<List<List<string>>> ReadXlsxSheets(string filePath)
        {
            var sheetsData = new List<List<List<string>>>();

            // Excel dosyası açık olsa bile okuyabilmek için FileShare.ReadWrite kullan
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                // 1. SharedStrings tablosunu oku (Microsoft Excel metinleri genelde burada tutar)
                var sharedStrings = new List<string>();
                var ssEntry = zip.GetEntry("xl/sharedStrings.xml");
                if (ssEntry != null)
                {
                    using (var stream = ssEntry.Open())
                    {
                        var doc = XDocument.Load(stream);
                        XNamespace ns = doc.Root.GetDefaultNamespace();
                        foreach (var si in doc.Root.Elements(ns + "si"))
                        {
                            // Hem basit <t> hem zengin metin <r><t> parçalarını birleştir
                            string fullText = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                            sharedStrings.Add(fullText);
                        }
                    }
                }

                // 2. Worksheet yollarını workbook.xml sırasına göre bul
                var sheetPaths = new List<string>();
                var wbEntry = zip.GetEntry("xl/workbook.xml");
                var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");

                if (wbEntry != null && relsEntry != null)
                {
                    var ridToTarget = new Dictionary<string, string>();
                    using (var rs = relsEntry.Open())
                    {
                        var rdoc = XDocument.Load(rs);
                        XNamespace rns = rdoc.Root.GetDefaultNamespace();
                        foreach (var rel in rdoc.Root.Elements(rns + "Relationship"))
                        {
                            string id = (string)rel.Attribute("Id");
                            string target = (string)rel.Attribute("Target");
                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                                ridToTarget[id] = target.TrimStart('/');
                        }
                    }

                    using (var ws = wbEntry.Open())
                    {
                        var wdoc = XDocument.Load(ws);
                        XNamespace wns = wdoc.Root.GetDefaultNamespace();
                        XNamespace rAttrNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                        foreach (var sheetEl in wdoc.Descendants(wns + "sheet"))
                        {
                            string rid = (string)sheetEl.Attribute(rAttrNs + "id");
                            if (!string.IsNullOrEmpty(rid) && ridToTarget.TryGetValue(rid, out string target))
                            {
                                string fullPath = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                                    ? target
                                    : "xl/" + target;
                                sheetPaths.Add(fullPath);
                            }
                        }
                    }
                }

                if (sheetPaths.Count == 0)
                {
                    sheetPaths = zip.Entries
                        .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(e => e.FullName)
                        .Select(e => e.FullName)
                        .ToList();
                }

                // 3. Her worksheet'i satır/sütun ızgarasına dönüştür
                foreach (var path in sheetPaths)
                {
                    var entry = zip.GetEntry(path);
                    if (entry == null) continue;

                    var grid = new List<List<string>>();
                    using (var stream = entry.Open())
                    {
                        var doc = XDocument.Load(stream);
                        XNamespace ns = doc.Root.GetDefaultNamespace();

                        foreach (var rowEl in doc.Descendants(ns + "row"))
                        {
                            var rowCells = new List<string>();
                            int fallbackCol = 0;

                            foreach (var cEl in rowEl.Elements(ns + "c"))
                            {
                                string cellRef = (string)cEl.Attribute("r");
                                int colIdx = !string.IsNullOrEmpty(cellRef) ? CellRefToColIndex(cellRef) : fallbackCol;
                                fallbackCol = colIdx + 1;

                                while (rowCells.Count <= colIdx)
                                    rowCells.Add("");

                                string cellType = (string)cEl.Attribute("t") ?? "";
                                string cellValue = "";

                                if (cellType == "s")
                                {
                                    string v = cEl.Element(ns + "v")?.Value;
                                    if (int.TryParse(v, out int ssIdx) && ssIdx >= 0 && ssIdx < sharedStrings.Count)
                                        cellValue = sharedStrings[ssIdx];
                                }
                                else if (cellType == "inlineStr")
                                {
                                    cellValue = string.Concat(cEl.Descendants(ns + "t").Select(t => t.Value));
                                }
                                else
                                {
                                    cellValue = cEl.Element(ns + "v")?.Value ?? "";
                                }

                                rowCells[colIdx] = cellValue;
                            }

                            if (rowCells.Any(x => !string.IsNullOrWhiteSpace(x)))
                                grid.Add(rowCells);
                        }
                    }

                    if (grid.Count > 0)
                        sheetsData.Add(grid);
                }
            }

            return sheetsData;
        }

        // CSV Okuyucu (Noktalı virgül, virgül veya tab ayraçlı)
        private static List<List<string>> ReadCsvRows(string filePath)
        {
            var result = new List<List<string>>();
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return result;

            char sep = ';';
            if (!lines[0].Contains(";") && lines[0].Contains(",")) sep = ',';
            else if (!lines[0].Contains(";") && lines[0].Contains("\t")) sep = '\t';

            foreach (var rawLine in lines)
            {
                result.Add(ParseCsvLine(rawLine, sep));
            }
            return result;
        }

        private static List<string> ParseCsvLine(string line, char sep)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == sep && !inQuotes)
                {
                    cells.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            cells.Add(sb.ToString().Trim());
            return cells;
        }
    }
}

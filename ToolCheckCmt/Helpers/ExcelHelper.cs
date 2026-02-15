using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ToolCheckCmt {
    public static class ExcelHelper {
        public static List<string> ReadLinksFromExcel(string filePath) {
            List<string> loadedLinks = new List<string>();
            try {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(new FileInfo(filePath))) {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet != null && worksheet.Dimension != null) {
                        int rowCount = worksheet.Dimension.Rows;
                        for (int row = 1; row <= rowCount; row++) {
                            string cellValue = worksheet.Cells[row, 1].Text;
                            if (!string.IsNullOrWhiteSpace(cellValue)) {
                                loadedLinks.Add(cellValue.Trim());
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                throw new Exception("Lỗi đọc Excel: " + ex.Message);
            }
            return loadedLinks;
        }

        public static void ExportToExcel(List<ResultModel> exportData, string filePath) {
            try {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                if (File.Exists(filePath)) File.Delete(filePath);

                using (var p = new ExcelPackage(new FileInfo(filePath))) {
                    var ws = p.Workbook.Worksheets.Add("Data");
                    ws.Cells[1, 1].Value = "Link";

                    int r = 2;
                    foreach (var item in exportData) {
                        ws.Cells[r, 1].Value = item.Link;
                        r++;
                    }
                    ws.Column(1).Width = 50;
                    p.Save();
                }
            } catch (Exception ex) {
                throw new Exception("Lỗi xuất Excel: " + ex.Message);
            }
        }
    }
}
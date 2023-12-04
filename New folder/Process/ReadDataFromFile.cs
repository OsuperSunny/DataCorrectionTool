using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GemBox.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using ExcelDataReader;
using System.Reflection.PortableExecutable;
using NPOI.SS.UserModel;
using static System.Net.Mime.MediaTypeNames;
using Syncfusion.XlsIO;
using IWorkbook = Syncfusion.XlsIO.IWorkbook;

namespace DataMigrationTool.Process
{
    internal static class ReadDataFromFile
    {
        //due to license can only read 150 records
        public static List<string> ReadData()
        {
            string fileName = "Cifs Record.xlsx";
            // If you are using the Professional version, enter your serial key below.
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");
            string filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);
            //Cifs.Add("0");            
            var workbook = ExcelFile.Load(filePath);             // Select the first worksheet from the file.
            var worksheet = workbook.Worksheets[0];             // Create DataTable from an Excel worksheet.
            var dataTable = worksheet.CreateDataTable(new CreateDataTableOptions()
            {
                ColumnHeaders = true,
                //StartRow = 2,
                //NumberOfColumns = 1,
                //NumberOfRows = worksheet.Rows.Count - 1,
                Resolution = ColumnTypeResolution.AutoPreferStringCurrentCulture
            });             // Write DataTable content
            var sb = new StringBuilder();
            sb.AppendLine("DataTable content:");
            var values = dataTable.AsEnumerable().Select(r => r["CIF"].ToString()).ToList();
            return values;
        }

        

        public static DataTable ConvertExcel(string path)
        {
            try
            {
                using (Stream inputStream = File.OpenRead(path))
                {
                    using (ExcelEngine excelEngine = new ExcelEngine())
                    {
                        IApplication application = excelEngine.Excel;
                        IWorkbook workbook = application.Workbooks.Open(inputStream);
                        IWorksheet worksheet = workbook.Worksheets[0];

                        DataTable dataTable = worksheet.ExportDataTable(worksheet.UsedRange, ExcelExportDataTableOptions.ColumnNames);
                        return dataTable;
                    }
                }
            }catch(Exception ex) { return null; }
            
        }

        public static IEnumerable<string> GetFilesByExtension(string directoryPath, string extension, SearchOption searchOption)
        {
            return
                Directory.EnumerateFiles(directoryPath, "*" + extension, searchOption)
                    .Where(x => string.Equals(Path.GetExtension(x), extension, StringComparison.InvariantCultureIgnoreCase));
        }



    }
}
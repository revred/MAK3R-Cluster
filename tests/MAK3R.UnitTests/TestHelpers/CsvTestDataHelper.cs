using System.Text;

namespace MAK3R.UnitTests.TestHelpers;

public static class CsvTestDataHelper
{
    public static MemoryStream CreateValidProductCsv()
    {
        var csvContent = @"Name,SKU,Price,Description,Active,Category
Test CNC Machine,CNC-TEST-001,45000.00,High precision test CNC machine,true,Machinery
Test Hydraulic Press,PRESS-TEST-001,32000.50,Industrial test hydraulic press,true,Machinery
Test Sensor Kit,SENSOR-TEST-001,1200.00,IoT sensor kit for testing,false,Components
Test Controller,PLC-TEST-001,8500.00,Test automation controller,true,Components";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateValidMachineCsv()
    {
        var csvContent = @"Machine_Name,Model,Serial_Number,Location,Status,Last_Maintenance
CNC Mill 001,XYZ-3000,SN123456,Factory Floor A,Running,2023-12-01
Hydraulic Press 002,ABC-500,SN789012,Factory Floor B,Maintenance,2023-11-15
Quality Control Station,QC-200,SN345678,Quality Lab,Idle,2023-12-10";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateValidInventoryCsv()
    {
        var csvContent = @"Product_SKU,Quantity,Location,Min_Stock,Max_Stock,Last_Updated
CNC-001,50,Warehouse A,10,100,2023-12-15
PRESS-001,25,Warehouse B,5,50,2023-12-14
SENSOR-001,200,Components Room,50,500,2023-12-16";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateCsvWithMissingRequiredFields()
    {
        var csvContent = @"Name,SKU,Price,Description
Valid Product,VALID-001,100.00,Valid product description
,MISSING-NAME,200.00,This product has no name
No SKU Product,,150.00,This product has no SKU
,BOTH-MISSING,300.00,This product has no name or SKU";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateCsvWithInvalidData()
    {
        var csvContent = @"Name,SKU,Price,Description,Active
Valid Product,VALID-001,100.00,Valid product,true
Invalid Price Product,INVALID-PRICE,not_a_number,Product with invalid price,true
Invalid Boolean Product,INVALID-BOOL,200.00,Product with invalid boolean,maybe";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateLargeCsv(int recordCount = 1000)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,SKU,Price,Description,Active,Category");

        for (int i = 1; i <= recordCount; i++)
        {
            sb.AppendLine($"Test Product {i:D4},TEST-{i:D6},{(i * 10.5):F2},Generated test product {i},true,Manufacturing");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public static MemoryStream CreateCsvWithSpecialCharacters()
    {
        var csvContent = @"Name,SKU,Price,Description
""Product with, comma"",COMMA-001,100.00,""Description with, comma""
Product with ""quotes"",QUOTE-001,200.00,Description with ""quotes""
Product with newline,NEWLINE-001,300.00,""Description with
newline""
Product with unicode éñ,UNICODE-001,400.00,Description with unicode characters éñ";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateEmptyCsv()
    {
        var csvContent = "Name,SKU,Price,Description"; // Header only
        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateCsvWithoutHeaders()
    {
        var csvContent = @"Product 1,SKU-001,100.00,Description 1
Product 2,SKU-002,200.00,Description 2";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateCsvWithDuplicateHeaders()
    {
        var csvContent = @"Name,SKU,Price,Name,Description
Product 1,SKU-001,100.00,Duplicate Name,Description 1
Product 2,SKU-002,200.00,Another Name,Description 2";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    public static MemoryStream CreateCsvWithVariableColumnCount()
    {
        var csvContent = @"Name,SKU,Price,Description
Product 1,SKU-001,100.00,Description 1
Product 2,SKU-002,200.00,Description 2,Extra Column
Product 3,SKU-003,300.00";

        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }
}
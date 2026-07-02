using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
namespace CodeGenarator
{
    public class clsHelper
    {
        public static string tableName = "";
        public struct Column { public string name; public string type; public string isNullable; public bool composition; };
        public static string objectName = "";
        public static string className = "";
        public static List<Column> Columns;
        public static List<Column> mappedColumns;
        public static List<Column> ColumnsForCsharp;

        public static Column makeMappedColumnByName(string name)
        {
            return mappedColumns.Find(n => n.name == name);
        }

        public static Column makeColumnByName(string name)
        {
            return Columns.Find(n => n.name == name);
        }

        public static List<Column> getColumnsNameAndType()
        {
            List<Column> columnsList = new List<Column>();

            SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["connectionStrings"].ConnectionString);
            string query = $"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY ORDINAL_POSITION;";
            SqlCommand command = new SqlCommand(query, connection);

            try
            {
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Column column = new Column();
                    column.name = reader.GetString(0);
                    column.type = reader.GetString(1);
                    column.isNullable = reader.GetString(2);
                    columnsList.Add(column);
                }
                reader.Close();
            }
            catch (Exception) { throw; }
            finally { connection.Close(); }

            return columnsList;
        }

        public static string mapFromSQLToCsharp(string sql)
        {
            switch (sql)
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                case "text":
                case "ntext": return "string";

                case "bigint": return "long";
                case "int": return "int";
                case "smallint": return "short";
                case "tinyint": return "byte";

                case "bit": return "bool";

                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": return "decimal";
                case "float": return "double";
                case "real": return "float";

                case "date":
                case "datetime":
                case "smalldatetime":
                case "datetime2":
                    return "DateTime";
                case "time": return "TimeSpan";

                case "uniqueidentifier": return "Guid";

                case "binary":
                case "varbinary":
                case "image":
                    return "byte[]";
                default: return "object";
            }

        }

        public static List<Column> mappingTheColumns()
        {
            List<Column> newList = new List<Column>();
            foreach (Column col in Columns)
            {
                Column c = new Column();
                c.name = col.name;
                c.isNullable = col.isNullable;
                c.composition = false; 

                string sqlType = col.type.ToLower();

                c.type = mapFromSQLToCsharp(sqlType);

                if (c.name.ToLower().EndsWith("id") && clsHelper.getColumnIndex(c.name) > 0)
                {
                    Console.Write($"Composition for {c.name} foreign key? (yes/no): ");
                    string answer = Console.ReadLine();
                    if (answer != null && (answer.ToLower() == "yes" || answer.ToLower() == "y"))
                    {
                        string cleanName = c.name.Substring(0, c.name.Length - 2);
                        cleanName = "cls" + char.ToUpper(cleanName[0]) + cleanName.Substring(1);

                        c.type = cleanName; // Update the type to composition
                        c.composition = true;
                    }
                }

                newList.Add(c);
            }
            return newList;
        }

        public static List<Column> getColumnsForCsharp()
        {
            List<Column> newList = new List<Column>();
            foreach (Column col in Columns)
            {
                Column c = new Column();
                c.name = col.name;
                c.isNullable = col.isNullable;
                c.composition = false;

                string sqlType = col.type.ToLower();
                c.type = mapFromSQLToCsharp(sqlType);
                newList.Add(c);
            }
            return newList;
        }

        public static int getColumnIndex(string columnName)
        {
            return Columns.FindIndex(c => c.name == columnName);
        }

        public static string writeParameters(int columnIndex = 0, bool withFirstColumn = true)
        {
            List<Column> newColumns = new List<Column>(getColumnsForCsharp());
            if (!withFirstColumn) newColumns.RemoveAt(0);
            if (newColumns.Count == 0) return "";
            return string.Join(", ", newColumns.Select(c => c.type + " " + c.name));
        }

        public static string writeParametersToSend(bool byRef = false, int withoutRefIndex = -1)
        {
            string parameters = "";
            List<Column> raw = getColumnsForCsharp(); // Else
            if (byRef)
            {
                for (int i = 0; i < raw.Count; ++i)
                {
                    if (i == withoutRefIndex) parameters += $@"{raw[withoutRefIndex].name}, ";
                    else parameters += $"ref {raw[i].name}, ";
                }
            }
            else
            {
                for (int i = 0; i < mappedColumns.Count; ++i)
                {
                    if (i == withoutRefIndex) continue;

                    if (mappedColumns[i].composition)
                    {
                        string cleanName = mappedColumns[i].name.Substring(0, mappedColumns[i].name.Length - 2);
                        cleanName = char.ToUpper(cleanName[0]) + cleanName.Substring(1);
                        parameters += $"this.{cleanName}.{raw[i].name}, ";
                    }
                    else
                    {
                        parameters += $"this.{raw[i].name}, ";
                    }
                }
            }

            string result = "";
            if (parameters.Length > 2)
            {
                result = parameters.Substring(0, parameters.Length - 2);
            }
            return result;
        }

        public static string getRawColumnNames()
        {
            return string.Join(", ", Columns.Select(c => c.name));
        }

        public static void GenerateArchitectureSolution(string targetDirectory, string solutionName)
        {
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);
            // making Libraries
            RunDotNetCommand(targetDirectory, $"new sln -n {solutionName}");
            RunDotNetCommand(targetDirectory, "new classlib -n DAL -f net10.0");
            RunDotNetCommand(targetDirectory, "new classlib -n BLL -f net10.0");
            RunDotNetCommand(targetDirectory, "new classlib -n Shared -f net10.0");
            RunDotNetCommand(targetDirectory, "new webapi -n WebAPI -f net10.0"); // PL

            RunDotNetCommand(targetDirectory, $"sln {solutionName}.sln add Shared/Shared.csproj DAL/DAL.csproj BLL/BLL.csproj WebAPI/WebAPI.csproj");

            // Main Folders/Libraries
            string dalFolder = Path.Combine(targetDirectory, "DAL");
            string bllFolder = Path.Combine(targetDirectory, "BLL");
            string webApiFolder = Path.Combine(targetDirectory, "WebAPI");
            string sharedFolder = Path.Combine(targetDirectory, "Shared");

            // Files to delete
            string dalClass1 = Path.Combine(targetDirectory, "DAL", "Class1.cs");
            string bllClass1 = Path.Combine(targetDirectory, "BLL", "Class1.cs");
            string sharedClass1 = Path.Combine(targetDirectory, "Shared", "Class1.cs");
            string webApiWeatherFile = Path.Combine(targetDirectory, "WebAPI", "WeatherForecast.cs");
            string webApiWeatherController = Path.Combine(targetDirectory, "WebAPI", "Controllers", "WeatherForecastController.cs");

            // adding refrences
            RunDotNetCommand(bllFolder, "add reference ../DAL/DAL.csproj");
            RunDotNetCommand(webApiFolder, "add reference ../BLL/BLL.csproj");
            RunDotNetCommand(dalFolder, "add reference ../Shared/Shared.csproj");
            RunDotNetCommand(bllFolder, "add reference ../Shared/Shared.csproj");
            RunDotNetCommand(webApiFolder, "add reference ../Shared/Shared.csproj");

            // Deleting extra files
            if (File.Exists(dalClass1)) File.Delete(dalClass1);
            if (File.Exists(bllClass1)) File.Delete(bllClass1);
            if (File.Exists(sharedClass1)) File.Delete(sharedClass1);
            if (File.Exists(webApiWeatherFile)) File.Delete(webApiWeatherFile);
            if (File.Exists(webApiWeatherController)) File.Delete(webApiWeatherController);

            // Separating DTOs into Brief and Full
            string briefDtoFolder = Path.Combine(sharedFolder, "DTOs", "Brief");
            string fullDtoFolder = Path.Combine(sharedFolder, "DTOs", "Full");

            if (!Directory.Exists(briefDtoFolder)) Directory.CreateDirectory(briefDtoFolder);
            if (!Directory.Exists(fullDtoFolder)) Directory.CreateDirectory(fullDtoFolder);

            // Adding AuthController to WebAPI project
            string controllersFolder = Path.Combine(webApiFolder, "Controllers");
            if (!Directory.Exists(controllersFolder)) Directory.CreateDirectory(controllersFolder);

            string authControllerPath = Path.Combine(controllersFolder, "AuthController.cs");
            string authControllerCode = @"using Microsoft.AspNetCore.Mvc;
namespace WebAPI.Controllers
{
    [ApiController]
    [Route(""api/[controller]"")]
    public class AuthController : ControllerBase
    {
        // TODO: Add Endpoints for authentication (login, register, etc.)
    }
}";
            File.WriteAllText(authControllerPath, authControllerCode);

            // Uppdating Program.cs File
            string programCsPath = Path.Combine(targetDirectory, "WebAPI", "Program.cs");

            string cleanProgramCode = @"WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Remember to Add needed CORS And Authentication
builder.Services.AddControllers(); 

var app = builder.Build();

// Forcing HTTPS
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();";
            File.WriteAllText(programCsPath, cleanProgramCode);

            // Adding clsSecurityHelper to Shared project
            string securityHelperPath = Path.Combine(sharedFolder, "clsSecurityHelper.cs");
            string securityHelperCode = @"using System;
using System.Security.Cryptography;

namespace Shared
{
    public static class clsSecurityHelper
    {
        public static string ComputeHash(string password, string salt, int iterations = 10000)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, iterations, HashAlgorithmName.SHA256))
            {
                byte[] hashBytes = pbkdf2.GetBytes(32);
                return Convert.ToBase64String(hashBytes);
            }
        }

        public static string GenerateSalt(int size = 16)
        {
            byte[] saltBytes = new byte[size];
            using (var provider = RandomNumberGenerator.Create())
            {
                provider.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }
    }
}";
            File.WriteAllText(securityHelperPath, securityHelperCode);
        }

        private static void RunDotNetCommand(string workingDirectory, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
            }
        }

        public static List<string> allSPs = new List<string>();

        public static void InjectAllToDB()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["connectionStrings"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (string spCode in allSPs)
                        {
                            using (SqlCommand cmd = new SqlCommand(spCode, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        Console.WriteLine("All Stored Procedures are injected in the database successfully!.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Error so no SPs are saved: {ex.Message}");
                    }
                }
            }
            allSPs.Clear();
        }

        public static List<string> GetAllTables()
        {
            List<string> tableNames = new List<string>();
            string connectionString = ConfigurationManager.ConnectionStrings["connectionStrings"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tableNames.Add(reader["TABLE_NAME"].ToString());
                        }
                    }
                }
            }
            return tableNames;
        }

        public static void debugThing(object obj)
            {
                Type type = obj.GetType();
                if(type != null)
                {
                    var Methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).OrderBy(method => method.Name);
                    Console.WriteLine($"Type: {type.FullName}");
                    Console.WriteLine($"Type: {type.FullName}");

                    foreach (var prop in type.GetProperties())
                    {
                        try
                        {
                            var value = prop.GetValue(obj);
                            Console.WriteLine($"  {prop.Name} = {value}");
                        }
                        catch
                        {
                            Console.WriteLine($"  {prop.Name} = [Cannot Read]");
                        }
                    }
                    foreach (var method in Methods)
                    {
                        Console.WriteLine($"Method: {method.Name}");
                    }
                    object myClass = Activator.CreateInstance(type);
                }

            }
        }
}

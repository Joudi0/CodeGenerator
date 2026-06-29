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
        public static string className => "cls" + objectName;

        public static List<Column> Columns;
        public static List<Column> mappedColumns;

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
            RunDotNetCommand(targetDirectory, $"new sln -n {solutionName}");
            RunDotNetCommand(targetDirectory, "new classlib -n DAL -f net8.0");
            RunDotNetCommand(targetDirectory, "new classlib -n BLL -f net8.0");
            RunDotNetCommand(targetDirectory, "new classlib -n Shared -f net8.0");
            RunDotNetCommand(targetDirectory, "new webapi -n WebAPI -f net8.0"); // PL

            RunDotNetCommand(targetDirectory, $"sln {solutionName}.sln add Shared/Shared.csproj DAL/DAL.csproj BLL/BLL.csproj WebAPI/WebAPI.csproj");
            string dalFolder = Path.Combine(targetDirectory, "DAL");
            string bllFolder = Path.Combine(targetDirectory, "BLL");
            string webApiFolder = Path.Combine(targetDirectory, "WebAPI");

            RunDotNetCommand(bllFolder, "add reference ../DAL/DAL.csproj");
            RunDotNetCommand(webApiFolder, "add reference ../BLL/BLL.csproj");
            string dalClass1 = Path.Combine(targetDirectory, "DAL", "Class1.cs");
            string bllClass1 = Path.Combine(targetDirectory, "BLL", "Class1.cs");
            string sharedClass1 = Path.Combine(targetDirectory, "Shared", "Class1.cs");

            RunDotNetCommand(dalFolder, "add reference ../Shared/Shared.csproj");
            RunDotNetCommand(bllFolder, "add reference ../Shared/Shared.csproj");
            RunDotNetCommand(webApiFolder, "add reference ../Shared/Shared.csproj");

            if (File.Exists(dalClass1)) File.Delete(dalClass1);
            if (File.Exists(bllClass1)) File.Delete(bllClass1);
            if (File.Exists(sharedClass1)) File.Delete(sharedClass1);
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

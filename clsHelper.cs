using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
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
            int index = clsHelper.getColumnIndex(name);
            if(index == -1)
            {
                Console.WriteLine($"Column with name '{name}' not found.");
                return new Column();
            }
            else
            {
                return mappedColumns[clsHelper.getColumnIndex(name)];
            }
        }
        public static Column makeColumnByName(string name)
        {
            int index = clsHelper.getColumnIndex(name);
            if (index == -1)
            {
                Console.WriteLine($"Column with name '{name}' not found.");
                return new Column();
            }
            else
            {
                return Columns[clsHelper.getColumnIndex(name)];
            }
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

        public static List<Column> mappingTheColumns(List<Column> ogList)
        {
            List<Column> newList = new List<Column>();
            foreach (Column col in ogList)
            {
                Column c = new Column();
                c.name = col.name;
                c.isNullable = col.isNullable;
                c.composition = false; 

                string sqlType = col.type.ToLower();

                switch (sqlType)
                {
                    case "varchar":
                    case "nvarchar":
                    case "char":
                    case "nchar":
                    case "text":
                    case "ntext": c.type = "string"; break;

                    case "bigint": c.type = "long"; break;
                    case "int": c.type = "int"; break;
                    case "smallint": c.type = "short"; break;
                    case "tinyint": c.type = "byte"; break;

                    case "bit": c.type = "bool"; break;

                    case "decimal":
                    case "numeric":
                    case "money":
                    case "smallmoney": c.type = "decimal"; break;
                    case "float": c.type = "double"; break;
                    case "real": c.type = "float"; break;

                    case "date":
                    case "datetime":
                    case "smalldatetime":
                    case "datetime2":
                        c.type = "DateTime";
                        break;
                    case "time": c.type = "TimeSpan"; break;

                    case "uniqueidentifier": c.type = "Guid"; break;

                    case "binary":
                    case "varbinary":
                    case "image":
                        c.type = "byte[]";
                        break;

                    default: c.type = "object"; break;
                }

                if (c.name.ToLower().EndsWith("id") && clsHelper.getColumnIndex(c.name) > 0)
                {
                    Console.WriteLine($"Composition for {c.name} foreign key? (yes/no): ");
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

        public static List<Column> getColumnsForCsharp(List<Column> ogList)
        {
            List<Column> newList = new List<Column>();
            foreach (Column col in ogList)
            {
                Column c = new Column();
                c.name = col.name;
                c.isNullable = col.isNullable;
                c.composition = false;

                string sqlType = col.type.ToLower();

                switch (sqlType)
                {
                    case "varchar":
                    case "nvarchar":
                    case "char":
                    case "nchar":
                    case "text":
                    case "ntext": c.type = "string"; break;

                    case "bigint": c.type = "long"; break;
                    case "int": c.type = "int"; break;
                    case "smallint": c.type = "short"; break;
                    case "tinyint": c.type = "byte"; break;

                    case "bit": c.type = "bool"; break;

                    case "decimal":
                    case "numeric":
                    case "money":
                    case "smallmoney": c.type = "decimal"; break;
                    case "float": c.type = "double"; break;
                    case "real": c.type = "float"; break;

                    case "date":
                    case "datetime":
                    case "smalldatetime":
                    case "datetime2":
                        c.type = "DateTime";
                        break;
                    case "time": c.type = "TimeSpan"; break;

                    case "uniqueidentifier": c.type = "Guid"; break;

                    case "binary":
                    case "varbinary":
                    case "image":
                        c.type = "byte[]";
                        break;

                    default: c.type = "object"; break;
                }

                newList.Add(c);
            }
            return newList;
        }

        public static int getColumnIndex(string columnName)
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                Column column = Columns[i];
                if (column.name == columnName)
                {
                    return i;
                }
            }
            return -1;
        }

        public static string writeParameters(int columnIndex = 0, bool withFirstColumn = true)
        {
            string parameters = "";
            List<Column> newColumns = new List<Column>(getColumnsForCsharp(Columns));
            if (!withFirstColumn) newColumns.RemoveAt(0);
            if (newColumns.Count == 0) return "";
            foreach (Column col in newColumns)
            {
                parameters += $"{col.type} {col.name}, ";
            }
            string result = "";
            if (parameters.Length > 2)
            {
                result = parameters.Substring(0, parameters.Length - 2);
            }
            return result;
        }
        public static string writeParametersToSend(bool byRef = false, int withoutRefIndex = -1)
        {
            string parameters = "";
            List<Column> mapped = new List<Column>(mappedColumns); //  For Composition
            List<Column> raw = new List<Column>(Columns);          // Else

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
                for (int i = 0; i < mapped.Count; ++i)
                {
                    if (i == withoutRefIndex) continue;

                    if (mapped[i].composition)
                    {
                        string cleanName = mapped[i].name.Substring(0, mapped[i].name.Length - 2);
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
            string parameters = "";
            foreach (Column col in Columns)
            {
                parameters += $"{col.name}, ";
            }
            string result = "";
            if (parameters.Length > 2)
            {
                result = parameters.Substring(0, parameters.Length - 2);
            }
            return result;
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

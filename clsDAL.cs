using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static CodeGenarator.clsHelper;

namespace CodeGenarator
{
    public class clsDAL
    {
        // Helpers

        public static string addWithValueAllScript(bool withoutFirst = true)
        {
            List<Column> newColumns = new List<Column>(getColumnsForCsharp());
            if (withoutFirst) newColumns.RemoveAt(0);
            string script = "";
            string tabs = "\n\t\t    ";
            foreach (Column col in newColumns)
            {
                script += tabs;
                if (col.isNullable == "NO") script += $@"command.Parameters.AddWithValue(@""{col.name}"", {col.name});";
                else if (col.isNullable == "YES")
                {
                    switch (col.type)
                    {
                        case "string": script += $@"command.Parameters.AddWithValue(@""{col.name}"", string.IsNullOrEmpty({col.name}) ? DBNull.Value : (object){col.name});"; break;
                        case "byte": script += $@"command.Parameters.AddWithValue(@""{col.name}"", ({col.name} == 0) ? DBNull.Value : (object){col.name});"; break;
                        case "decimal":
                        case "int": script += $@"command.Parameters.AddWithValue(@""{col.name}"", ({col.name} == -1) ? DBNull.Value : (object){col.name});"; break;
                        case "DateTime": script += $@"command.Parameters.AddWithValue(@""{col.name}"", ({col.name} == DateTime.MinValue) ? DBNull.Value : (object){col.name});"; break;
                        default: break;
                    }
                }
            }
            return script;
        }


        // Actual Functions

        public static string getRecordByColumnFunc(Column C)
        {
            if (Columns.Count == 0) return "Error in the lists";
            string FunctionName = "";
            if (getColumnIndex(C.name) == 0)
            {
                FunctionName = $@"get{objectName}ByID";
            }
            else FunctionName = $@"get{objectName}By{C.name}";
            string Function =
                $@"public static async Task<DataTable> {FunctionName}({C.type} {C.name})
                {{
                    DataTable dt = new DataTable();
                    using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
                    using SqlCommand command = new SqlCommand(""SP_{tableName}_SelectBy{C.name}"", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue(""@{C.name}"", {C.name});
                    try
                    {{
                        await connection.OpenAsync();
                        using SqlDataReader reader = await command.ExecuteReaderAsync();
                        if (reader.HasRows) dt.Load(reader);
                    }}
                     catch (Exception) {{ throw;}}

                    return dt;
                }}";

            return Function;
        }

        public static string updateFunc(Column C)
        {
            if (Columns.Count == 0) return "Error in the lists, The Column List is Empty";
            string Function =
                $@"        public static async Task<bool> update{objectName}({writeParameters()})
        {{
            int rowsAffected = 0;
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_Update"", connection);
            command.CommandType = CommandType.StoredProcedure;
            {addWithValueAllScript(false)}
            try
            {{
                await connection.OpenAsync();
                rowsAffected = await command.ExecuteNonQueryAsync();
            }}
            catch (Exception) {{ throw; }}
            return (rowsAffected > 0);
        }}";
            return Function;
        }

        public static string addFunc()
        {
            if (Columns.Count == 0) return "Error in the lists";
            string Function =
                $@"        public static async Task<int> add{objectName}({writeParameters(0, false)})
        {{
            int {objectName}ID = -1;
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_Insert"", connection);
            command.CommandType = CommandType.StoredProcedure;
            {addWithValueAllScript()}
            try
            {{
                await connection.OpenAsync();
                object result = await command.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int insertedID))
                {{
                    {objectName}ID = insertedID;
                }}
            }}
            catch (Exception) {{ throw; }}

            return {objectName}ID;
        }}";
            return Function;
        }

        public static string deleteFunc(Column C)
        {
            if (Columns.Count == 0) return "Error in the lists";

            string Function = $@"        public static async Task<bool> delete{objectName}({C.type} {C.name})
        {{
            int rowsAffected = 0;
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_Delete"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});
            try
            {{
                await connection.OpenAsync();
                rowsAffected = await command.ExecuteNonQueryAsync();
            }}
            catch (Exception) {{ throw; }}
            return (rowsAffected > 0);
        }}";
            return Function;
        }

        public static string getAllFunc()
        {
            string Function = $@"
        public static async Task<DataTable> getAll()
        {{
            DataTable dt = new DataTable();
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_SelectAll"", connection);
            command.CommandType = CommandType.StoredProcedure;

            try
            {{
                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows) dt.Load(reader);
            }}
            catch (Exception) {{ throw; }}

            return dt;
        }}";
            return Function;
        }

        public static string getAllByColumnFunc(Column C)
        {
            string FunctionName = "";
            if (getColumnIndex(C.name) == 0)
            {
                FunctionName = $@"getAllByID";
            }
            else FunctionName = $@"getAllBy{C.name}";
            if (Columns.Count == 0) return "Error in the lists";

            string Function = $@"
        public static async Task<DataTable> {FunctionName}({C.type} {C.name})
        {{
            DataTable dt = new DataTable();
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_SelectAllBy{C.name}"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});

            try
            {{
                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows) dt.Load(reader);
            }}
            catch (Exception) {{ throw; }}

            return dt;
        }}";
            return Function;
        }

        public static string isExistsFunc(Column C)
        {
            string FunctionName = "";
            if (getColumnIndex(C.name) == 0)
            {
                FunctionName = $@"is{objectName}ExistByID";
            }
            else FunctionName = $@"is{objectName}ExistBy{C.name}";
            string Function = $@"
        public static async Task<bool> {FunctionName}({C.type} {C.name})
        {{
            bool isFound = false;
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_IsExistBy{C.name}"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});

            try
            {{
                await connection.OpenAsync();
                object result = await command.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int res))
                {{
                    isFound = (res == 1);
                }}
            }}
            catch (Exception) {{ throw; }}

            return isFound;
        }}";
            return Function;
        }

        public static string PagingFunc()
        {
            
            if (Columns.Count == 0) return "Error in the lists";

            string Function = $@"
        public static async Task<DataTable> PagingDAL(int RowsPerPage, int PageNumber, string SortColumn, string Direction)
        {{
            DataTable dt = new DataTable();
            using SqlConnection connection = new SqlConnection(clsDataSettings.connectionString);
            using SqlCommand command = new SqlCommand(""SP_{tableName}_Paging"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@RowsPerPage"", RowsPerPage);
            command.Parameters.AddWithValue(""@PageNumber"", PageNumber);             
            command.Parameters.AddWithValue(""@SortColumn"", string.IsNullOrEmpty(SortColumn) ? (object)DBNull.Value : SortColumn);
            command.Parameters.AddWithValue(""@Direction"", string.IsNullOrEmpty(Direction) ? (object)DBNull.Value : Direction);    


            try
            {{
                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows) dt.Load(reader);
            }}
            catch (Exception) {{ throw; }}

            return dt;
        }}";
            return Function;
        }

        public static string classStructure(StringBuilder injectedString)
        {
            string structure = $@"using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
namespace DAL
{{
    public class cls{objectName}DAL
    {{  
{injectedString}  
    }}
}}";
            return structure;
        }
    }
}

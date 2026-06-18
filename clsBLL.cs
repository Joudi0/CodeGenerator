using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CodeGenarator.clsHelper;

namespace CodeGenarator
{
    public class clsBLL
    {
        public static string DALName = $@"cls{objectName}DAL";
        public static string tabs = "\t\t";
        // Helpers Functions:

        public static string giveInitialValue(Column c, bool newVar = false)
        {
            string line = "";
            if(newVar)
            {
                switch (c.type)
                {
                    case "byte": line += $@"{c.type} {c.name} = 0;"; break;
                    case "bool": line += $@"{c.type} {c.name} = false;"; break;
                    case "decimal":
                    case "int": line += $@"{c.type} {c.name} = -1;"; break;
                    case "string": line += $@"{c.type} {c.name} = """";"; break;
                    case "DateTime": line += $@"{c.type} {c.name} = DateTime.Now;"; break;
                    default: line += $@"this.{c.type.Substring(3)} = new {c.type}();"; break;


                }
            }
            else
            {
                switch (c.type)
                {
                    case "byte": line += $@"this.{c.name} = 0;"; break;
                    case "bool": line += $@"this.{c.name} = false;"; break;
                    case "decimal":
                    case "int": line += $@"this.{c.name} = -1;"; break;
                    case "string": line += $@"this.{c.name} = """";"; break;
                    case "DateTime": line += $@"this.{c.name} = DateTime.Now;"; break;
                    default: line += $@"this.{c.type.Substring(3)} = new {c.type}();"; break ;
                }

            }
            line += "\n";
            return line;
        }

        public static string getByValuesHelper(Column col)
        {
            string values = "";
            List<Column> columns = new List<Column>(Columns);
            int columnIndex = getColumnIndex(col.name);
            values += $@"{col.type} ";
        
            return values;
        }

        public static string initalVars(int withoutColumnIndex = -1)
        {
            string values = "";
            List<Column> cols = new List<Column>(mappedColumns);
            if (withoutColumnIndex > -1)
            {

                cols.RemoveAt(withoutColumnIndex);

            }
            foreach (Column c in cols)
            {
                
                values += tabs + giveInitialValue(c, true);
            }
            values += "\n";
            return values;
        }

        public static string initialThisValues()
        {
            string values = "";
            foreach (Column c in mappedColumns)
            {
                values += tabs + giveInitialValue(c);
            }
            values += tabs + "Mode = enMode.AddNew;\n" + tabs + "}\n";
            return values;
        }

        public static string thisValues()
        {
            string values = "";
            foreach (Column c in clsHelper.mappedColumns)
            {
                if(c.composition)
                {
                    continue;
                }
                else values += tabs + $@"this.{c.name} = {c.name};" + "\n";
            }
            values += tabs + "Mode = enMode.Update;\n";
        
            return values;
        }

        public static string asyncVariableValue()
        {
            string lines = "";
            foreach(clsHelper.Column C in clsHelper.mappedColumns)
            {
                if (C.composition)
                {
                    string cleanName = C.name.Substring(0, C.name.Length - 2);
                    cleanName = char.ToUpper(cleanName[0]) + cleanName.Substring(1);
                    lines += tabs + $@"obj.{cleanName} = await cls{cleanName}.get{cleanName}ByID({C.name});" + "\n";
                }
            }
            return lines;
        }

        public static string loadVarsFromRow()
        {
            string script = tabs + "DataRow row = dt.Rows[0];\n";

            foreach (Column col in getColumnsForCsharp(Columns))
            {
                if (col.isNullable == "NO")
                {
                    script += tabs + $@"{col.type} {col.name} = ({col.type})row[""{col.name}""];" + "\n";
                }
                else if (col.isNullable == "YES")
                {
                    script += tabs;
                    switch (col.type)
                    {
                        case "byte": script += $@"{col.type} {col.name} = (row[""{col.name}""] == DBNull.Value) ? (byte)0 : ({col.type})row[""{col.name}""];" + "\n"; break;
                        case "decimal":
                        case "int": script += $@"{col.type} {col.name} = (row[""{col.name}""] == DBNull.Value) ? -1 : ({col.type})row[""{col.name}""];" + "\n"; break;
                        case "string": script += $@"{col.type} {col.name} = (row[""{col.name}""] == DBNull.Value) ? """" : ({col.type})row[""{col.name}""];" + "\n"; break;
                        case "DateTime": script += $@"{col.type} {col.name} = (row[""{col.name}""] == DBNull.Value) ? DateTime.Now : ({col.type})row[""{col.name}""];" + "\n"; break;
                        case "bool": script += $@"{col.type} {col.name} = (row[""{col.name}""] == DBNull.Value) ? false : ({col.type})row[""{col.name}""];" + "\n"; break;
                        default: break;
                    }
                }
            }
            return script;
        }

        // Actual Functions:

        public static string writeProperties()
        {
            string Properties = "";
            foreach (Column c in mappedColumns)
            {
                if (c.composition)
                {
                    string cleanName = c.name.Substring(0, c.name.Length - 2);
                    cleanName = char.ToUpper(cleanName[0]) + cleanName.Substring(1);
                    Properties += $@"{tabs}public {c.type} {cleanName} {{ get; set; }}" + "\n";
                }
                else
                {
                    Properties += $@"{tabs}public {c.type} {c.name} {{ get; set; }}" + "\n";
                }
            }
            Properties += tabs + "public enum enMode {AddNew, Update}\n" + tabs + "public enMode Mode;\n";
            return Properties;
        }

        public static string writeConstructors(bool isEmpty)
        {
            string Constructors = "";
            if (isEmpty)
            {
                Constructors = $@"public {className}()
            {{
{initialThisValues()}";
            }
            else
            {
                Constructors = $@"private {className}({writeParameters(0, true)})
                {{
{thisValues()}
                }}";
            }
            return Constructors;
        }

        public static string isExistsFunc(Column C)
        {
            string FunctionName = "";
            if (getColumnIndex(C.name) == 0)
            {
                FunctionName = $@"is{objectName}ExistByID";
            }
            else FunctionName = $@"is{objectName}ExistBy{C.name}";
            string Function = $@"public static Task<bool> {FunctionName}({C.type} {C.name})
            {{
                return {DALName}.{FunctionName}({C.name});
                
            }}
";
            return Function;
        }

        public static string getByFunc(Column C)
        {
            int columnIndex = getColumnIndex(C.name);
            string FunctionName = "";
            if (columnIndex == 0)
            {
                FunctionName = $@"get{objectName}ByID";
            }
            else FunctionName = $@"get{objectName}By{C.name}";
            string Function = $@"
            public static async Task<cls{objectName}> {FunctionName}({C.type} {C.name})
            {{
                DataTable dt = await {DALName}.{FunctionName}({C.name});
                if (dt == null || dt.Rows.Count != 1) return null;
                {loadVarsFromRow()}
                var obj = new cls{objectName}({getRawColumnNames()});
                {asyncVariableValue()}
                return obj;
            }}
";
            return Function;
        }
         
        public static string addFunc()
        {
            string FunctionName = $@"_add{objectName}";
            string Function = $@"private async Task<bool> {FunctionName}()
            {{
                this.{Columns[0].name} = await {DALName}.add{objectName}({writeParametersToSend(false, 0)});
                return this.{Columns[0].name} > 0;
            }}";
            return Function;
        }

        public static string updateFunc()
        {
            string FunctionName = $@"_update{objectName}";

            string Function = $@"private Task<bool> {FunctionName}()
            {{
                return {DALName}.update{objectName}({writeParametersToSend(false)});
            }}";

            return Function;
        }
        
        public static string deleteFunc(Column C)
        {
            string FunctionName = $@"delete{objectName}";
            string secondFuncName = "";

            if (getColumnIndex(C.name) == 0) secondFuncName = $@"is{objectName}ExistByID";
            else secondFuncName = $@"is{objectName}ExistBy{C.name}";

            string Function = $@"
        public static async Task<bool> {FunctionName}({C.type} {C.name})
        {{
            if(await {secondFuncName}({C.name}))
            {{
                return {DALName}.delete{objectName}({C.name});
            }}
            else return false;
        }}
";
            return Function;
        }

        public static string getAllFunc()
        {
            string FunctionName = "getAll";
            string Function = $@"
            public static Task<DataTable> {FunctionName}()
            {{
                return {DALName}.{FunctionName}();
            }}

";
            return Function;
        }

        public static string PagingFunc()
        {
            string FunctionName = "Paging";
            string Function = $@"
            public static Task<DataTable> {FunctionName}(int RowsPerPage, int PageNumber, string SortColumn, string Direction)
            {{
                return {DALName}.{FunctionName}(RowsPerPage, PageNumber, SortColumn, Direction);
            }}

";
            return Function;
        }

        public static string getAllByFunc(Column C)
        {
            string FunctionName = "getAllBy" + C.name;
            string secondFuncName = "";
            if (getColumnIndex(C.name) == 0) secondFuncName = $@"is{objectName}ExistByID";
            else secondFuncName = $@"is{objectName}ExistBy{C.name}";

            string Function = $@"
            public static async Task<DataTable> {FunctionName}({C.type} {C.name})
            {{
                if(await {secondFuncName}({C.name}))
                {{
                    return {DALName}.{FunctionName}({C.name});
                }}
                return new DataTable();
            }}
";
            return Function;
        }

        public static string saveFunc()
        {
            string FunctionName = "Save";
            string Function = $@"
            public async Task<bool> {FunctionName}()
            {{
                switch (Mode)
                    {{
                        case enMode.AddNew:

                            if (await _add{objectName}())
                            {{

                                Mode = enMode.Update;
                                return true;
                            }}
                            else
                            {{
                                return false;
                            }}

                        case enMode.Update: return await _update{objectName}();
                    }}
                return true;        
            }}";
            return Function;
        }

        public static string classStructure(StringBuilder injectedString)
        {
            string classStructure = $@"using DAL;
using System;
using System.Data;
using System.Threading.Tasks;
namespace BLL
{{
    public class cls{objectName}
    {{

    {injectedString}

    }}
}}
";
            return classStructure;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenarator
{
    public class clsBLL
    {
        public static string DALName = $@"cls{clsHelper.objectName}DAL";
        public static string tabs = "        ";

        // Helpers Functions:
        public static string generateBriefMapping(string sourcePrefix = "item.")
        {
            string script = "";
            string indent = "                    ";

            List<clsHelper.Column> briefColumns = new List<clsHelper.Column>(clsHelper.getColumnsForCsharp());
            briefColumns.RemoveAll(c => clsAPIs.blackList.Contains(c.name.ToLower()));

            foreach (clsHelper.Column col in briefColumns)
            {
                script += $"{indent}{col.name} = {sourcePrefix}{col.name},\n";
            }
            return script.TrimEnd('\n', ',');
        }

        // Actual Functions:

        public static string isExistsFunc(clsHelper.Column C)
        {
            string functionName = (clsHelper.getColumnIndex(C.name) == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            return $@"        public static Task<bool> {functionName}({C.type} {C.name})
        {{
            return {DALName}.{functionName}({C.name});
        }}
";
        }

        public static string getByFunc(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string functionName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";

            return $@"
        public static async Task<{clsHelper.className}BriefDTO> {functionName}({C.type} {C.name})
        {{
            {clsHelper.className}FullDTO fullDto = await {DALName}.{functionName}({C.name});
            if (fullDto == null) return null;

            return new {clsHelper.className}BriefDTO
            {{
{generateBriefMapping("fullDto.")}
            }};
        }}
";
        }

        public static string addFunc()
        {
            return $@"
        public static Task<int> add{clsHelper.objectName}({clsHelper.className}FullDTO dto)
        {{
            return {DALName}.add{clsHelper.objectName}(dto);
        }}";
        }

        public static string updateFunc()
        {
            return $@"
        public static Task<bool> update{clsHelper.objectName}({clsHelper.className}FullDTO dto)
        {{
            return {DALName}.update{clsHelper.objectName}(dto);
        }}";
        }

        public static string deleteFunc(clsHelper.Column C)
        {
            string functionName = $"delete{clsHelper.objectName}";
            string existFuncName = (clsHelper.getColumnIndex(C.name) == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";

            return $@"
        public static async Task<bool> {functionName}({C.type} {C.name})
        {{
            if (await {existFuncName}({C.name}))
            {{
                return await {DALName}.delete{clsHelper.objectName}({C.name});
            }}
            return false;
        }}
";
        }

        public static string getAllFullBLLFunc()
        {
            return $@"
        public static Task<List<{clsHelper.className}FullDTO>> getAllFull()
        {{
            return {DALName}.getAll();
        }}
";
        }

        public static string getAllBriefBLLFunc()
        {
            return $@"
        public static async Task<List<{clsHelper.className}BriefDTO>> getAllBrief()
        {{
            List<{clsHelper.className}FullDTO> fullList = await {DALName}.getAll();
            List<{clsHelper.className}BriefDTO> briefList = new List<{clsHelper.className}BriefDTO>();
            
            foreach ({clsHelper.className}FullDTO item in fullList)
            {{
                briefList.Add(new {clsHelper.className}BriefDTO
                {{
{generateBriefMapping()}
                }});
            }}
            
            return briefList;
        }}
";
        }

        public static string PagingFunc()
        {
            return $@"
        public static async Task<List<{clsHelper.className}BriefDTO>> Paging(int rowsPerPage, int pageNumber, string sortColumn, string direction)
        {{
            List<{clsHelper.className}FullDTO> fullList = await {DALName}.PagingDAL(rowsPerPage, pageNumber, sortColumn, direction);
            List<{clsHelper.className}BriefDTO> briefList = new List<{clsHelper.className}BriefDTO>();
            
            foreach ({clsHelper.className}FullDTO item in fullList)
            {{
                briefList.Add(new {clsHelper.className}BriefDTO
                {{
{generateBriefMapping()}
                }});
            }}
            
            return briefList;
        }}
";
        }

        public static string getAllByFunc(clsHelper.Column C)
        {
            string functionName = "getAllBy" + C.name;
            string existFuncName = (clsHelper.getColumnIndex(C.name) == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";

            return $@"
        public static async Task<List<{clsHelper.className}BriefDTO>> {functionName}({C.type} {C.name})
        {{
            if (await {existFuncName}({C.name}))
            {{
                List<{clsHelper.className}FullDTO> fullList = await {DALName}.{functionName}({C.name});
                List<{clsHelper.className}BriefDTO> briefList = new List<{clsHelper.className}BriefDTO>();
                
                foreach ({clsHelper.className}FullDTO item in fullList)
                {{
                    briefList.Add(new {clsHelper.className}BriefDTO
                    {{
{generateBriefMapping()}
                    }});
                }}
                return briefList;
            }}
            return new List<{clsHelper.className}BriefDTO>();
        }}
";
        }

        public static string classStructure(StringBuilder injectedString)
        {
            return $@"using DAL;
using Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL
{{
    public class cls{clsHelper.objectName}
    {{
{injectedString}
    }}
}}
";
        }
    }
}
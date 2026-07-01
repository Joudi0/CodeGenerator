using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenarator
{
    public class clsAPIs
    {
        public static string tabs = "        ";

        public static string writeProperties(bool full = false)
        {
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.getColumnsForCsharp());
            string Properties = "";
            if (full)
            {
                foreach (clsHelper.Column c in columns)
                {
                    Properties += $@"{tabs}public {c.type} {c.name} {{ get; set; }}" + "\n";
                }
            }
            else
            {
                columns.RemoveAll(c => blackList.Contains(c.name.ToLower()));
                foreach (clsHelper.Column c in columns)
                {
                    Properties += $@"{tabs}public {c.type} {c.name} {{ get; set; }}" + "\n";
                }
            }
            return Properties;
        }

        public static List<string> blackList = new List<string> { "password",
        "ip", "secret", "salary", "balance", "privatekey"
        };

        public static string BriefDTO()
        {
            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}BriefDTO
    {{
        {writeProperties()}
    }}
}}
";
            return DTO;
        }

        public static string FullDTO()
        {
            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}FullDTO
    {{
        {writeProperties(true)}
    }}
}}
";
            return DTO;
        }

        // ==========================================
        //  Web API Controllers
        // ==========================================

        public static string getByAction(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";
            string actionName = (columnIndex == 0) ? "GetByID" : $"GetBy{C.name}";
            string route = (columnIndex == 0) ? "{id}" : $"{C.name}/{{{C.name}}}";
            string paramName = (columnIndex == 0) ? "id" : C.name;

            return $@"
        [HttpGet(""{route}"")]
        public async Task<IActionResult> {actionName}({C.type} {paramName})
        {{
            {clsHelper.className}FullDTO result = await cls{clsHelper.objectName}.{bllMethodName}({paramName});
            if (result == null) return NotFound($""{clsHelper.objectName} with {C.name} {{{paramName}}} not found."");
            return Ok(result);
        }}
";
        }

        public static string addAction()
        {
            return $@"
        [HttpPost]
        public async Task<IActionResult> Add({clsHelper.className}FullDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data."");
            int insertedID = await cls{clsHelper.objectName}.add{clsHelper.objectName}(dto);
            if (insertedID == -1) return StatusCode(500, ""An error occurred while adding the record."");
            return CreatedAtAction(nameof(GetByID), new {{ id = insertedID }}, dto);
        }}
";
        }

        public static string updateAction()
        {
            return $@"
        [HttpPut]
        public async Task<IActionResult> Update({clsHelper.className}FullDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data."");
            bool isUpdated = await cls{clsHelper.objectName}.update{clsHelper.objectName}(dto);
            if (!isUpdated) return NotFound($""{clsHelper.objectName} update failed or record not found."");
            return Ok(""Updated successfully."");
        }}
";
        }

        public static string deleteAction(clsHelper.Column C)
        {
            string bllMethodName = $"delete{clsHelper.objectName}";
            return $@"
        [HttpDelete(""{C.name}/{{{C.name}}}"")]
        public async Task<IActionResult> Delete({C.type} {C.name})
        {{
            bool isDeleted = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            if (!isDeleted) return NotFound($""{clsHelper.objectName} not found or couldn't be deleted."");
            return Ok(""Deleted successfully."");
        }}
";
        }

        public static string isExistAction(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            string actionName = (columnIndex == 0) ? "ExistsByID" : $"ExistsBy{C.name}";
            string route = $"exists/{C.name}/{{{C.name}}}";

            return $@"
        [HttpGet(""{route}"")]
        public async Task<IActionResult> {actionName}({C.type} {C.name})
        {{
            bool exists = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            return Ok(exists);
        }}
";
        }

        public static string pagingAction()
        {
            return $@"
        [HttpGet(""page"")]
        public async Task<IActionResult> GetPage([FromQuery] int rowsPerPage = 10, [FromQuery] int pageNumber = 1, [FromQuery] string sortColumn = ""{clsHelper.Columns[0].name}"", [FromQuery] string direction = ""ASC"")
        {{
            List<{clsHelper.className}BriefDTO> list = await cls{clsHelper.objectName}.Paging(rowsPerPage, pageNumber, sortColumn, direction);
            return Ok(list);
        }}
";
        }

        public static string getAllBriefByAction(clsHelper.Column C)
        {
            string bllMethodName = $"getAllBriefBy{C.name}";
            string actionName = $"GetAllBriefBy{C.name}";
            string route = $"all-brief/by/{C.name}/{{{C.name}}}";

            return $@"
        [HttpGet(""{route}"")]
        public async Task<IActionResult> {actionName}({C.type} {C.name})
        {{
            List<{clsHelper.className}BriefDTO> list = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            return Ok(list);
        }}
";
        }

        public static string getAllFullByAction(clsHelper.Column C)
        {
            string bllMethodName = $"getAllFullBy{C.name}";
            string actionName = $"GetAllFullBy{C.name}";
            string route = $"all-full/by/{C.name}/{{{C.name}}}";

            return $@"
        [HttpGet(""{route}"")]
        public async Task<IActionResult> {actionName}({C.type} {C.name})
        {{
            List<{clsHelper.className}FullDTO> list = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            return Ok(list);
        }}
";
        }

        public static string controllerStructure(StringBuilder injectedActions)
        {
            return $@"using BLL;
using Shared;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{{
    [ApiController]
    [Route(""api/[controller]"")]
    public class {clsHelper.objectName}Controller : ControllerBase
    {{
{injectedActions}
    }}
}}
";
        }
    }
}
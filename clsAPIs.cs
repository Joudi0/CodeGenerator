using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator
{
    public class clsAPIs
    {
        public static string tabs = "        ";

        public static string writeProperties(bool full = false)
        {
            List<clsHelper.Column> columns = full
        ? new List<clsHelper.Column>(clsHelper.mappedColumns)
        : new List<clsHelper.Column>(clsHelper.ColumnsForCsharp);
            string Properties = "";

            // Apply blacklist filter only if it's a Brief DTO
            if (!full)
            {
                columns.RemoveAll(c => blackList.Contains(c.name.ToLower()));
            }

            foreach (clsHelper.Column col in columns)
            {
                if (col.composition)
                {
                    // 1. Keep the primitive ID for database and DAL operations
                    Properties += $"{tabs}public {col.type} {col.name} {{ get; set; }}\n";

                    // 2. Dynamically construct and append the clean Nested Brief DTO property
                    string cleanName = col.name.Substring(0, col.name.Length - 2);
                    string dtoType = "cls" + char.ToUpper(cleanName[0]) + cleanName.Substring(1) + "BriefDTO";
                    string propName = char.ToUpper(cleanName[0]) + cleanName.Substring(1) + "Details";

                    Properties += $"{tabs}public {dtoType} {propName} {{ get; set; }}\n";
                }
                else
                {
                    // Normal database column mapping
                    Properties += $"{tabs}public {col.type} {col.name} {{ get; set; }}\n";
                }
            }
            return Properties;
        }

        public static List<string> blackList = new List<string>
{ 
    // Authentication & Cryptography
    "password", "pass", "pwd", "passwd",
    "passwordhash", "password_hash", "passwordsalt", "password_salt",
    "secret", "privatekey", "private_key", "publickey", "public_key",
    "key", "iv", "apikey", "api_key",
    "token", "authtoken", "auth_token", "refreshtoken", "refresh_token",
    "sessionid", "session_id", "pin", "pincode",

    // Financial & Sensitive Data
    "salary", "balance", "income", "revenue",
    "creditcard", "credit_card", "cvv", "cvc",
    "cardnumber", "card_number", "bankaccount", "bank_account",

    // Infrastructure & Device Info
    "ip", "ipaddress", "ip_address", "mac", "macaddress", "mac_address",

    // Personal Identifiers
    "ssn", "nationalid", "national_id", "passport", "passportnumber", "passport_number"
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
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string extraProperties = "";

            // Password property is only included in the Full DTO for user tables to facilitate registration and updates
            if (isUser)
            {
                extraProperties = "        public string Password { get; set; }\n";
            }

            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}FullDTO
    {{
        {writeProperties(true)}{extraProperties}
    }}
}}
";
            return DTO;
        }

        public static string SecurityDTO()
        {
            string DTO = $@"
using System;

namespace Shared
{{
    public class AuthDTO
    {{
        public int UserID {{ get; set; }}
        public string PasswordHash {{ get; set; }}
        public string PasswordSalt {{ get; set; }}
        public enRoles UserRoleID {{ get; set; }}
    }}
}}
";
            return DTO;
        }

        public static string RegisterRequestDTO()
        {
            StringBuilder dtoProperties = new StringBuilder();
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.ColumnsForCsharp);

            // Remove the first column (ID) since it is auto-generated and not provided during registration
            columns.RemoveAt(0);

            foreach (var col in columns)
            {
                // Skip cryptographic fields as they are generated on the server side, not sent by the client
                if (col.name.ToLower().Contains("hash") || col.name.ToLower().Contains("salt"))
                    continue;

                // NEW: Skip the role column to prevent Mass Assignment vulnerability
                if (col.name.ToLower().Contains("roleid") || col.name.ToLower().Contains("role"))
                    continue;

                dtoProperties.AppendLine($"        public {col.type} {col.name} {{ get; set; }}");
            }

            // Explicitly add the plain-text password property required to receive user credentials
            dtoProperties.AppendLine("        public string Password { get; set; }");

            return $@"using System;

namespace Shared
{{
    public class RegisterRequestDTO
    {{
{dtoProperties}    }}
}}";
        }

        // ==========================================
        //  Web API Controllers
        // ==========================================

        public static string loginAction()
        {
            return $@"
        /// <summary>
        /// Authenticates a user and generates a secure JWT token.
        /// </summary>
        /// <param name=""loginDto"">The user credentials container.</param>
        /// <param name=""tokenService"">The injected JWT token generation service.</param>
        /// <returns>An IActionResult containing the generated access token.</returns>
        /// <response code=""200"">Returns the secure JWT token upon successful authentication.</response>
        /// <response code=""400"">If the credentials are null or empty.</response>
        /// <response code=""401"">If the username or password is invalid.</response>
        [HttpPost(""login"")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO loginDto, [FromServices] clsTokenService tokenService)
        {{
            if (loginDto == null || string.IsNullOrEmpty(loginDto.Username) || string.IsNullOrEmpty(loginDto.Password))
                return BadRequest(""Username and Password are required."");

            // Verify credentials via BLL which now returns the AuthDTO object
            AuthDTO authData = await {clsHelper.className}.checkLogin(loginDto.Username, loginDto.Password);
            
            if (authData == null) 
                return Unauthorized(""Invalid username or password."");

            // FIXED: Using UserRoleID instead of Role to match AuthDTO properties
            var token = tokenService.GenerateJWTToken(authData.UserID, loginDto.Username, authData.UserRoleID.ToString());    

            return Ok(new {{ Token = token }});
        }}
";
        }

        public static string registerAction()
        {
            return $@"
        /// <summary>
        /// Registers a new user with automated password hashing and salting.
        /// </summary>
        /// <param name=""registerDto"">The registration data container.</param>
        /// <returns>An IActionResult containing the newly created user ID.</returns>
        /// <response code=""200"">Returns the ID of the newly registered user.</response>
        /// <response code=""400"">If the request body is null, password is missing, or registration fails.</response>
        [HttpPost(""register"")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO registerDto)
        {{
            if (registerDto == null || string.IsNullOrEmpty(registerDto.Password))
                return BadRequest(""Invalid registration data. Password is required."");

            try
            {{
                // Calling the dynamic dynamic BLL method we created earlier
                int insertedId = await {clsHelper.className}.RegisterUser(registerDto);
                
                if (insertedId == -1)
                    return BadRequest(""Registration failed. Could not create the user."");

                return Ok(new {{ Id = insertedId }});
            }}
            catch (Exception ex)
            {{
                return BadRequest(ex.Message);
            }}
        }}
";
        }

        public static string getByAction(clsHelper.Column C, string roles)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";
            string actionName = (columnIndex == 0) ? "GetByID" : $"GetBy{C.name}";
            string route = (columnIndex == 0) ? "{id}" : $"{C.name}/{{{C.name}}}";
            string paramName = (columnIndex == 0) ? "id" : C.name;

            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser && columnIndex == 0)
            {
                serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            var authResult = await authorizationService.AuthorizeAsync(User, {paramName}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
            }

            return $@"
        /// <summary>
        /// Retrieves a record by its {C.name}.
        /// </summary>
        /// <param name=""{paramName}"">The {C.name} parameter value.</param>
        /// <returns>An IActionResult containing the requested record details.</returns>
        /// <response code=""200"">Returns the found record details.</response>
        /// <response code=""404"">If no record matches the provided {C.name}.</response>
        {authAttribute}
        [HttpGet(""{route}"")]
        [ProducesResponseType(typeof({clsHelper.className}FullDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> {actionName}({C.type} {paramName}{serviceInjection})
        {{{ownershipCheck}            {clsHelper.className}FullDTO result = await cls{clsHelper.objectName}.{bllMethodName}({paramName});
            if (result == null) return NotFound($""{clsHelper.objectName} with {C.name} {{{paramName}}} not found."");
            return Ok(result);
        }}
";
        }

        public static string updateAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser)
            {
                string idFieldName = clsHelper.ColumnsForCsharp[0].name;
                serviceInjection = ", [FromServices] IAuthorizationService authorizationService";

                // 1. Centralized Policy-Based Resource Authorization Check
                ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            var authResult = await authorizationService.AuthorizeAsync(User, dto.{idFieldName}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
            }

            return $@"
        /// <summary>
        /// Updates an existing record in the database.
        /// </summary>
        /// <param name=""dto"">The updated data transfer object.</param>
        /// <returns>An IActionResult indicating the success of the update operation.</returns>
        /// <response code=""200"">If the record was updated successfully.</response>
        /// <response code=""400"">If the input payload is null.</response>
        /// <response code=""404"">If the record to update does not exist or modification failed.</response>
        {authAttribute}
        [HttpPut]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromBody] {clsHelper.className}FullDTO dto{serviceInjection})
        {{
            if (dto == null) return BadRequest(""Invalid data payload."");{ownershipCheck}
            bool isUpdated = await cls{clsHelper.objectName}.update{clsHelper.objectName}(dto);
            if (!isUpdated) return NotFound($""{clsHelper.objectName} update failed or record not found."");
            return Ok(""Updated successfully."");
        }}
";
        }

        public static string addAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            // if is it a user table, force the role to be the Highest available role (first in the list) to prevent privilege escalation during creation
            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }
            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Adds a new record to the database.
        /// </summary>
        /// <param name=""dto"">The full data transfer object for creation.</param>
        /// <returns>An IActionResult containing the created record details and location.</returns>
        /// <response code=""201"">Returns the newly created record along with its location.</response>
        /// <response code=""400"">If the input payload is null or invalid.</response>
        /// <response code=""500"">If an internal database error occurs during the operation.</response>
        {authAttribute}
        [HttpPost]
        [ProducesResponseType(typeof({clsHelper.className}FullDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Add([FromBody] {clsHelper.className}FullDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data payload."");
            int insertedID = await cls{clsHelper.objectName}.add{clsHelper.objectName}(dto);
            if (insertedID == -1) return StatusCode(500, ""An error occurred while adding the record."");
            return CreatedAtAction(""GetByID"", new {{ id = insertedID }}, dto);
        }}
";
        }

        public static string deleteAction(clsHelper.Column C, string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string bllMethodName = $"delete{clsHelper.objectName}";

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser)
            {
                serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            var authResult = await authorizationService.AuthorizeAsync(User, {C.name}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
            }

            return $@"
        /// <summary>
        /// Deletes a specific record using its unique identifier.
        /// </summary>
        /// <param name=""{C.name}"">The key value of the target record to delete.</param>
        /// <returns>An IActionResult confirming deletion.</returns>
        /// <response code=""200"">If the record was deleted successfully.</response>
        /// <response code=""404"">If the target record is not found or cannot be deleted.</response>
        {authAttribute}
        [HttpDelete(""{C.name}/{{{C.name}}}"")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete({C.type} {C.name}{serviceInjection})
        {{
{ownershipCheck}            bool isDeleted = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            if (!isDeleted) return NotFound($""{clsHelper.objectName} not found or couldn't be deleted."");
            return Ok(""Deleted successfully."");
        }}
";
        }

       

        public static string isExistAction(clsHelper.Column C, string roles)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            string actionName = (columnIndex == 0) ? "ExistsByID" : $"ExistsBy{C.name}";
            string route = $"exists/{C.name}/{{{C.name}}}";

            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser)
            {
                if (columnIndex == 0)
                {
                    // running centralized policy-based authorization check for user existence by ID
                    serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                    ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            var authResult = await authorizationService.AuthorizeAsync(User, {C.name}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
                }
                else if (clsHelper.AvailableRoles.Count > 0)
                {
                    // if it is a user table, force the role to be the Highest available role (first in the list) to prevent privilege escalation during existence checks
                    roles = clsHelper.AvailableRoles[0];
                }
            }

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Checks whether a record exists based on the provided criteria.
        /// </summary>
        /// <param name=""{C.name}"">The field value to look up.</param>
        /// <returns>An IActionResult containing a boolean flag indicating existence.</returns>
        /// <response code=""200"">Returns true if the record exists, otherwise false.</response>
        {authAttribute}
        [HttpGet(""{route}"")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({C.type} {C.name}{serviceInjection})
        {{{ownershipCheck}            bool exists = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            return Ok(exists);
        }}
";
        }

        public static string pagingAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            // if it is a user table, force the role to be the Highest available role (first in the list) to prevent privilege escalation during paging
            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Retrieves a paginated list of records based on query filters.
        /// </summary>
        /// <param name=""rowsPerPage"">The total number of rows per page.</param>
        /// <param name=""pageNumber"">The active page index.</param>
        /// <param name=""sortColumn"">The specific table column to sort by.</param>
        /// <param name=""direction"">The sorting direction constraint ('ASC' or 'DESC').</param>
        /// <returns>An IActionResult containing the filtered collection of Brief DTOs.</returns>
        /// <response code=""200"">Returns the paginated data collection matching criteria.</response>
        {authAttribute}
        [HttpGet(""page"")]
        [ProducesResponseType(typeof(List<{clsHelper.className}BriefDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPage([FromQuery] int rowsPerPage = 10, [FromQuery] int pageNumber = 1, [FromQuery] string sortColumn = ""{clsHelper.Columns[0].name}"", [FromQuery] string direction = ""ASC"")
        {{
            List<{clsHelper.className}BriefDTO> list = await cls{clsHelper.objectName}.Paging(rowsPerPage, pageNumber, sortColumn, direction);
            return Ok(list);
        }}
";
        }

        public static string getAllBriefByAction(clsHelper.Column C, string roles)
        {
            string bllMethodName = $"getAllBriefBy{C.name}";
            string actionName = $"GetAllBriefBy{C.name}";
            string route = $"all-brief/by/{C.name}/{{{C.name}}}";

            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            // if it is a user table, force the role to be the Highest available role (first in the list) to prevent privilege escalation during creation
            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Retrieves all matching records in brief format filtered by a specific column.
        /// </summary>
        /// <param name=""{C.name}"">The lookup criterion value.</param>
        /// <returns>An IActionResult containing a collection of Brief DTOs.</returns>
        /// <response code=""200"">Returns the list of brief format entries.</response>
        {authAttribute}
        [HttpGet(""{route}"")]
        [ProducesResponseType(typeof(List<{clsHelper.className}BriefDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({C.type} {C.name})
        {{
            List<{clsHelper.className}BriefDTO> list = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            return Ok(list);
        }}
";
        }

        public static string getAllFullByAction(clsHelper.Column C, string roles)
        {
            string bllMethodName = $"getAllFullBy{C.name}";
            string actionName = $"GetAllFullBy{C.name}";
            string route = $"all-full/by/{C.name}/{{{C.name}}}";

            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            // if it is a user table, restrict access to the highest available role (first in the list) to prevent unauthorized data exposure
            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }

            string authAttribute = (roles.ToLower() == "anonymous")
                ? "[AllowAnonymous]"
                : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Retrieves all matching records in full format filtered by a specific column.
        /// </summary>
        /// <param name=""{C.name}"">The lookup criterion value.</param>
        /// <returns>An IActionResult containing a collection of Full DTOs.</returns>
        /// <response code=""200"">Returns the list of full format entries.</response>
        {authAttribute}
        [HttpGet(""{route}"")]
        [ProducesResponseType(typeof(List<{clsHelper.className}FullDTO>), StatusCodes.Status200OK)]
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Shared;

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
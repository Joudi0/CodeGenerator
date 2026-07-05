using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
namespace CodeGenerator
{
    public class clsHelper
    {
        public static string tableName = "";
        public struct Column
        {
            public string name;
            public string type;
            public string isNullable;
            public bool composition;
            public int? length; // Optional
        };
        public static string objectName = "";
        public static string className = "";
        public static List<Column> Columns;
        public static List<Column> mappedColumns;
        public static List<Column> ColumnsForCsharp;
        public static List<string> AvailableRoles = new List<string>();

        // The global dopamine counter!
        public static int TotalLinesGenerated = 0;
        public static int TotalClasses = 0;
        public static int TotalDTOs = 0;
        public static int TotalSPs = 0;
        public static void TrackClass(int count = 1) => TotalClasses += count;
        public static void TrackDTO(int count = 1) => TotalDTOs += count;
        public static void TrackSPs(int count = 1) => TotalSPs += count;



        public static string connectionString = ConfigurationManager.ConnectionStrings["connectionStrings"].ConnectionString;

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
            string query = $"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY ORDINAL_POSITION;";

            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                {
                    connection.Open();
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Column column = new Column();
                            column.name = reader.GetString(0);
                            column.type = reader.GetString(1);
                            column.isNullable = reader.GetString(2);
                            columnsList.Add(column);
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return columnsList;
        }

        public static void LoadAvailableRoles()
        {
            if (AvailableRoles.Count > 0) return;

            string ensureRolesSql = @"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
        BEGIN
            CREATE TABLE Roles (
                RoleID INT IDENTITY(1,1) PRIMARY KEY,
                RoleName NVARCHAR(50) NOT NULL UNIQUE
            );
            INSERT INTO Roles (RoleName) VALUES ('Admin'), ('User');
        END";

            string fetchRolesSql = "SELECT RoleName FROM Roles ORDER BY RoleID;";

            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();

                    using (Microsoft.Data.SqlClient.SqlCommand cmdEnsure = new Microsoft.Data.SqlClient.SqlCommand(ensureRolesSql, conn))
                    {
                        cmdEnsure.ExecuteNonQuery();
                    }

                    using (Microsoft.Data.SqlClient.SqlCommand cmdFetch = new Microsoft.Data.SqlClient.SqlCommand(fetchRolesSql, conn))
                    {
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = cmdFetch.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AvailableRoles.Add(reader.GetString(0).Replace(" ", ""));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[red]Database Error during role initialization:[/] {ex.Message}");
                AvailableRoles = new List<string> { "Admin", "User" };
            }
        }
        public static string mapFromSQLToCsharp(string sql, string isNullable = "NO")
        {
            string suffix = (isNullable.ToUpper() == "YES") ? "?" : "";
            switch (sql)
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                case "text":
                case "ntext": return "string";

                case "bigint": return "long" + suffix;
                case "int": return "int" + suffix;
                case "smallint": return "short" + suffix;
                case "tinyint": return "byte" + suffix;

                case "bit": return "bool" + suffix;

                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": return "decimal" + suffix;
                case "float": return "double" + suffix;
                case "real": return "float" + suffix;

                case "date":
                case "datetime":
                case "smalldatetime":
                case "datetime2": return "DateTime" + suffix;
                case "time": return "TimeSpan" + suffix;

                case "uniqueidentifier": return "Guid" + suffix;

                case "binary":
                case "varbinary":
                case "image": return "byte[]";
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

                c.type = mapFromSQLToCsharp(sqlType, col.isNullable);

                if (c.name.ToLower().EndsWith("id") && clsHelper.getColumnIndex(c.name) > 0)
                {
                    Console.Write($"Composition for {c.name} foreign key? (yes/no): ");
                    string answer = Console.ReadLine();
                    if (answer != null && (answer.ToLower() == "yes" || answer.ToLower() == "y"))
                    {
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
                c.type = mapFromSQLToCsharp(sqlType, col.isNullable);
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

            RunDotNetCommand(targetDirectory, "new classlib -n DAL -f net10.0");
            RunDotNetCommand(targetDirectory, "new classlib -n BLL -f net10.0");
            RunDotNetCommand(targetDirectory, "new classlib -n Shared -f net10.0");
            RunDotNetCommand(targetDirectory, "new webapi -n WebAPI -f net10.0"); // PL

            string slnxPath = Path.Combine(targetDirectory, $"{solutionName}.slnx");
            string slnxContent = $@"<Solution>
  <Project Path=""WebAPI/WebAPI.csproj"" />
  <Project Path=""BLL/BLL.csproj"" />
  <Project Path=""DAL/DAL.csproj"" />
  <Project Path=""Shared/Shared.csproj"" />
</Solution>";

            File.WriteAllText(slnxPath, slnxContent);
            TrackLines(slnxContent);

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

            // adding references
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

            // Separating DTOs into Brief and Full and Auth
            string briefDtoFolder = Path.Combine(sharedFolder, "DTOs", "Brief");
            string fullDtoFolder = Path.Combine(sharedFolder, "DTOs", "Full");
            string AuthDtoFolder = Path.Combine(sharedFolder, "DTOs", "Auth");

            if (!Directory.Exists(briefDtoFolder)) Directory.CreateDirectory(briefDtoFolder);
            if (!Directory.Exists(fullDtoFolder)) Directory.CreateDirectory(fullDtoFolder);
            if (!Directory.Exists(AuthDtoFolder)) Directory.CreateDirectory(AuthDtoFolder);

            // Adding AuthController to WebAPI project
            string controllersFolder = Path.Combine(webApiFolder, "Controllers");
            if (!Directory.Exists(controllersFolder)) Directory.CreateDirectory(controllersFolder);

            // Updating Program.cs File
            string programCsPath = Path.Combine(targetDirectory, "WebAPI", "Program.cs");

            string cleanProgramCode = @"using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration[""JwtSettings:Issuer""],
            ValidAudience = builder.Configuration[""JwtSettings:Audience""],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration[""JwtSettings:SecretKey""]!))
        };
    });

// Configure IP-based rate limiting policies automatically with custom error responses
builder.Services.AddRateLimiter((Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options) =>
{
    options.RejectionStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests;
    
    // Handle rejected requests globally and return a clean JSON message
    options.OnRejected = async (Microsoft.AspNetCore.RateLimiting.OnRejectedContext context, System.Threading.CancellationToken token) =>
    {
        context.HttpContext.Response.ContentType = ""application/json"";
        string errorMessage = ""{\""error\"": \""Too many requests. Please try again later.\""}"";
        await context.HttpContext.Response.WriteAsync(errorMessage, System.Text.Encoding.UTF8, token);
    };

    // 1. Strict Policy for Authentication endpoints (5 requests per minute)
    options.AddFixedWindowLimiter(Shared.clsProjectPolicies.AuthPolicy, (System.Threading.RateLimiting.FixedWindowRateLimiterOptions fixedOptions) =>
    {
        fixedOptions.PermitLimit = 5;
        fixedOptions.Window = TimeSpan.FromMinutes(1);
        fixedOptions.QueueLimit = 0;
        fixedOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });

    // 2. Medium Policy for Write operations like Add, Update, Delete (30 requests per minute)
    options.AddFixedWindowLimiter(Shared.clsProjectPolicies.WritePolicy, (System.Threading.RateLimiting.FixedWindowRateLimiterOptions fixedOptions) =>
    {
        fixedOptions.PermitLimit = 30;
        fixedOptions.Window = TimeSpan.FromMinutes(1);
        fixedOptions.QueueLimit = 2;
        fixedOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });

    // 3. Loose Policy for Read operations like Get, Paging, GetAll (100 requests per minute)
    options.AddFixedWindowLimiter(Shared.clsProjectPolicies.ReadPolicy, (System.Threading.RateLimiting.FixedWindowRateLimiterOptions fixedOptions) =>
    {
        fixedOptions.PermitLimit = 100;
        fixedOptions.Window = TimeSpan.FromMinutes(1);
        fixedOptions.QueueLimit = 5;
        fixedOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddEndpointsApiExplorer();

// For .NET 10 OpenAPI native support
builder.Services.AddOpenApi();

// Add authorization services
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(""UserOwnerOrAdmin"", policy =>
        policy.Requirements.Add(new WebAPI.Authorization.UserOwnerOrAdminRequirement()));
});

System.Collections.Generic.IEnumerable<System.Type> handlerTypes = typeof(Program).Assembly.GetTypes()
    .Where((System.Type t) => typeof(IAuthorizationHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
foreach (System.Type handler in handlerTypes)
{
    builder.Services.AddSingleton(typeof(IAuthorizationHandler), handler);
}

builder.Services.AddControllers(); 

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); 
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter(); 
app.UseAuthorization();
app.MapControllers();
app.Run();";
            File.WriteAllText(programCsPath, cleanProgramCode);
            TrackLines(cleanProgramCode);

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
            TrackLines(securityHelperCode);

            string appSettingsPath = Path.Combine(targetDirectory, "WebAPI", "appsettings.json");
            string appSettingsCode = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*"",
  ""ConnectionStrings"": {
    ""DefaultConnection"": """ + ConfigurationManager.ConnectionStrings["connectionStrings"].ConnectionString + @"""
  },
  ""JwtSettings"": {
    ""SecretKey"": ""Your_Super_Secret_Key_That_Is_Long_Enough_To_Satisfy_Sha256_For_Jwt_Signing!"",
    ""Issuer"": ""DVLD_API"",
    ""Audience"": ""DVLD_Users"",
    ""ExpirationInHours"": 1
  }
}";
            File.WriteAllText(appSettingsPath, appSettingsCode);
            TrackLines(appSettingsCode);

            // Adding clsProjectPolicies to Shared project infrastructure
            string projectPoliciesPath = Path.Combine(sharedFolder, "clsProjectPolicies.cs");
            string projectPoliciesCode = clsAPIs.ProjectPolicies();
            File.WriteAllText(projectPoliciesPath, projectPoliciesCode);
            TrackLines(projectPoliciesCode);

            // Adding clsDataSettings to DAL project infrastructure
            string dataSettingsPath = Path.Combine(dalFolder, "clsDataSettings.cs");
            string dataSettingsCode = $@"namespace DAL
{{
    public static class clsDataSettings
    {{
        public static string connectionString = ""{ConfigurationManager.ConnectionStrings["connectionStrings"].ConnectionString}"";
    }}
}}";
            File.WriteAllText(dataSettingsPath, dataSettingsCode);
            TrackLines(dataSettingsCode);

            clsHelper.TrackClass(3);

            RunDotNetCommand(webApiFolder, "add package Microsoft.AspNetCore.Authentication.JwtBearer");
        }

        private static void RunDotNetCommand(string workingDirectory, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
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
                        TotalSPs += allSPs.Count;
                        foreach (string spCode in allSPs)
                        {
                            TrackLines(spCode);
                        }
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

        public static void TrackLines(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                TotalLinesGenerated += code.Split('\n').Length;
            }
        }

        public static string GetCleanClassName(string columnName)
        {
            string lower = columnName.ToLower();
            if (lower.Contains("user")) return "User";
            if (lower.Contains("person")) return "Person";
            if (lower.Contains("application")) return "Application";
            if (lower.Contains("license")) return "License";
            if (lower.Contains("country")) return "Country";
            string clean = columnName.EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                ? columnName.Substring(0, columnName.Length - 2)
                : columnName;
            return char.ToUpper(clean[0]) + clean.Substring(1);
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
        private static async Task EnsureTokenTableAsync()
        {
            string ensureTokensTableSql = @"
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserTokens')
    BEGIN
        CREATE TABLE UserTokens (
            TokenID INT IDENTITY(1,1) PRIMARY KEY,
            UserID INT NOT NULL,
            RefreshTokenHash NVARCHAR(256) NOT NULL UNIQUE,
            ExpiryDate DATETIME NOT NULL,
            RevokedAt DATETIME NULL
        );
    END";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(ensureTokensTableSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[red]Database Error during Token Table initialization:[/] {ex.Message}");
            }
        }

        private static void GenerateTokenService(string projectDirectory)
        {
            string webApiServicesFolder = Path.Combine(projectDirectory, "WebAPI", "Services");
            if (!Directory.Exists(webApiServicesFolder)) Directory.CreateDirectory(webApiServicesFolder);

            string clsTokenServicePath = Path.Combine(webApiServicesFolder, "clsTokenService.cs");
            string clsTokenService = @"using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace WebAPI.Services
{
    public class clsTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        
        public clsTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString(""DefaultConnection"");
        }

        public string GenerateAccessToken(int userId, string username, string RoleName)
        {
            IConfigurationSection jwtSettings = _configuration.GetSection(""JwtSettings"");
            string secretKey = jwtSettings[""SecretKey""];
            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            Claim[] claims = new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, RoleName)
            };

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: jwtSettings[""Issuer""],
                audience: jwtSettings[""Audience""],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<string> GenerateAndSaveRefreshTokenAsync(int userId)
        {
            byte[] randomNumber = new byte[64];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            string rawRefreshToken = Convert.ToBase64String(randomNumber);

            string tokenHash = ComputeSha256Hash(rawRefreshToken);

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = ""INSERT INTO UserTokens (UserID, RefreshTokenHash, ExpiryDate) VALUES (@UserID, @TokenHash, @ExpiryDate)"";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue(""@UserID"", userId);
                    cmd.Parameters.AddWithValue(""@TokenHash"", tokenHash);
                    cmd.Parameters.AddWithValue(""@ExpiryDate"", DateTime.UtcNow.AddDays(7));

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return rawRefreshToken;
        }

        public async Task<int> ValidateAndRevokeRefreshTokenAsync(int userId, string rawRefreshToken)
        {
            string tokenHash = ComputeSha256Hash(rawRefreshToken);

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = ""SELECT TokenID FROM UserTokens WHERE UserID = @UserID AND RefreshTokenHash = @TokenHash AND ExpiryDate > @Now AND RevokedAt IS NULL"";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue(""@UserID"", userId);
                    cmd.Parameters.AddWithValue(""@TokenHash"", tokenHash);
                    cmd.Parameters.AddWithValue(""@Now"", DateTime.UtcNow);

                    await conn.OpenAsync();
                    object result = await cmd.ExecuteScalarAsync();
                    
                    if (result != null)
                    {
                        int tokenId = Convert.ToInt32(result);
                        await RevokeTokenByIdAsync(tokenId);
                        return tokenId;
                    }
                }
            }

            return -1;
        }

        public async Task RevokeTokenByRawAsync(int userId, string rawRefreshToken)
        {
            string tokenHash = ComputeSha256Hash(rawRefreshToken);
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = ""UPDATE UserTokens SET RevokedAt = @Now WHERE UserID = @UserID AND RefreshTokenHash = @TokenHash AND RevokedAt IS NULL"";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue(""@UserID"", userId);
                    cmd.Parameters.AddWithValue(""@TokenHash"", tokenHash);
                    cmd.Parameters.AddWithValue(""@Now"", DateTime.UtcNow);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task RevokeTokenByIdAsync(int tokenId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = ""UPDATE UserTokens SET RevokedAt = @Now WHERE TokenID = @TokenID"";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue(""@TokenID"", tokenId);
                    cmd.Parameters.AddWithValue(""@Now"", DateTime.UtcNow);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString(""x2""));
                }
                return builder.ToString();
            }
        }
    }
}";
            File.WriteAllText(clsTokenServicePath, clsTokenService);
            TrackLines(clsTokenService);
            TrackClass();
        }

        private static async Task GenerateAuthDTOsAsync(string projectDirectory)
        {
            string authDtoFolder = Path.Combine(projectDirectory, "Shared", "DTOs", "Auth");
            if (!Directory.Exists(authDtoFolder)) Directory.CreateDirectory(authDtoFolder);

            string tokenResponseCode = clsAPIs.TokenResponseDTO();
            File.WriteAllText(Path.Combine(authDtoFolder, "TokenResponseDTO.cs"), tokenResponseCode);
            TrackLines(tokenResponseCode);

            string refreshRequestCode = clsAPIs.RefreshRequestDTO();
            File.WriteAllText(Path.Combine(authDtoFolder, "RefreshRequestDTO.cs"), refreshRequestCode);
            TrackLines(refreshRequestCode);

            string logoutRequestCode = clsAPIs.LogoutRequestDTO();
            File.WriteAllText(Path.Combine(authDtoFolder, "LogoutRequestDTO.cs"), logoutRequestCode);
            TrackLines(logoutRequestCode);

            string AuthDTOPath = Path.Combine(authDtoFolder, "AuthDTO.cs");
            string AuthDTOCode = clsAPIs.SecurityDTO();
            using (StreamWriter writer = new StreamWriter(AuthDTOPath)) { await writer.WriteAsync(AuthDTOCode); }
            TrackLines(AuthDTOCode);

            string RegisterRequestDTOPath = Path.Combine(authDtoFolder, "RegisterRequestDTO.cs");
            string RegisterRequestDTOCode = clsAPIs.RegisterRequestDTO();
            using (StreamWriter writer = new StreamWriter(RegisterRequestDTOPath)) { await writer.WriteAsync(RegisterRequestDTOCode); }
            TrackLines(RegisterRequestDTOCode);

            string loginRequestDTOPath = Path.Combine(authDtoFolder, "LoginRequestDTO.cs");
            string loginRequestDTOCode = "namespace Shared\n{\n    public class LoginRequestDTO\n    {\n        public string Username { get; set; }\n        public string Password { get; set; }\n    }\n}";
            File.WriteAllText(loginRequestDTOPath, loginRequestDTOCode);
            TrackLines(loginRequestDTOCode);

            string tokenRequestDTOPath = Path.Combine(authDtoFolder, "TokenRequestDTO.cs");
            string tokenRequestDTOCode = "namespace Shared\n{\n    public class TokenRequestDTO\n    {\n        public string AccessToken { get; set; }\n        public string RefreshToken { get; set; }\n    }\n}";
            File.WriteAllText(tokenRequestDTOPath, tokenRequestDTOCode);

            TrackLines(tokenRequestDTOCode);
            TrackDTO(7);
        }

        private static async Task GenerateAuthControllerAsync(string projectDirectory)
        {
            string controllersFolder = Path.Combine(projectDirectory, "WebAPI", "Controllers");
            if (!Directory.Exists(controllersFolder)) Directory.CreateDirectory(controllersFolder);
            string authControllerPath = Path.Combine(controllersFolder, "AuthController.cs");

            StringBuilder authActions = new StringBuilder();
            authActions.Append(clsAPIs.loginAction());
            authActions.Append(clsAPIs.registerAction());
            authActions.Append(clsAPIs.refreshAction());
            authActions.Append(clsAPIs.logoutAction());

            string fullAuthControllerCode = $@"using BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using WebAPI.Services;
using Shared;
using System;

namespace WebAPI.Controllers
{{
    [ApiController]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.AuthPolicy)]
    [Route(""api/[controller]"")]
    public class AuthController : ControllerBase
    {{
{authActions}
    }}
}}";
            File.WriteAllText(authControllerPath, fullAuthControllerCode);
            TrackLines(fullAuthControllerCode);
            TrackClass();
        }

        private static async Task GenerateRolesEnumAsync(string projectDirectory)
        {
            Console.Write("-> Making Dynamic Roles Enum... ");
            string enumsFolder = Path.Combine(projectDirectory, "Shared", "Enums");
            if (!Directory.Exists(enumsFolder)) Directory.CreateDirectory(enumsFolder);
            StringBuilder enumMembers = new StringBuilder();
            string fetchRolesSql = "SELECT RoleID, RoleName FROM Roles ORDER BY RoleID;";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(fetchRolesSql, conn))
                {
                    await conn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int roleId = reader.GetInt32(0);
                            string roleName = reader.GetString(1).Replace(" ", "");
                            enumMembers.AppendLine($"        {roleName} = {roleId},");
                        }
                    }
                }
            }

            string enumCode = $@"namespace Shared
{{
    public enum enRoles
    {{
{enumMembers.ToString().TrimEnd('\n', '\r', ',')}
    }}
}}";
            string enumPath = Path.Combine(enumsFolder, "enRoles.cs");
            File.WriteAllText(enumPath, enumCode);
            TrackLines(enumCode);
            Console.WriteLine("[Done]");
        }
        private static void GenerateAuthPolicies(string projectDirectory)
        {
            string authorizationFolder = Path.Combine(projectDirectory, "WebAPI", "Policies", "Authorization");
            if (!Directory.Exists(authorizationFolder)) Directory.CreateDirectory(authorizationFolder);

            string requirementCode = @"using Microsoft.AspNetCore.Authorization;

namespace WebAPI.Authorization
{
    public class UserOwnerOrAdminRequirement : IAuthorizationRequirement
    {
    }
}";
            File.WriteAllText(Path.Combine(authorizationFolder, "UserOwnerOrAdminRequirement.cs"), requirementCode);

            string handlerCode = @"using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebAPI.Authorization
{
    public class UserOwnerOrAdminHandler : AuthorizationHandler<UserOwnerOrAdminRequirement, int>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, 
            UserOwnerOrAdminRequirement requirement, 
            int resourceUserId)
        {
            if (context.User.IsInRole(""Admin""))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            string currentUserIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(currentUserIdClaim, out int authenticatedUserId) &&
                authenticatedUserId == resourceUserId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}";
            File.WriteAllText(Path.Combine(authorizationFolder, "UserOwnerOrAdminHandler.cs"), handlerCode);
            TrackClass(2);
        }

        public static async Task Auth()
        {
            string _projectDirectory = System.Configuration.ConfigurationManager.AppSettings["projectDirectory"];

            // Step 1: Initialize database components
            await EnsureTokenTableAsync();

            // Step 2: Generate token infrastructure services
            GenerateTokenService(_projectDirectory);

            // Step 3: Generate all dedicated authentication DTOs
            await GenerateAuthDTOsAsync(_projectDirectory);

            // Step 4: Build and inject the main Auth Controller
            await GenerateAuthControllerAsync(_projectDirectory);

            // Step 5: Fetch roles and build dynamic Enum
            await GenerateRolesEnumAsync(_projectDirectory);

            // Step 6: Create core policy requirements and handlers
            GenerateAuthPolicies(_projectDirectory);
        }


        public static void debugThing(object obj)
        {
            Type type = obj.GetType();
            if (type != null)
            {
                System.Linq.IOrderedEnumerable<System.Reflection.MethodInfo> Methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).OrderBy((System.Reflection.MethodInfo method) => method.Name);
                Console.WriteLine($"Type: {type.FullName}");
                Console.WriteLine($"Type: {type.FullName}");

                foreach (System.Reflection.PropertyInfo prop in type.GetProperties())
                {
                    try
                    {
                        object value = prop.GetValue(obj);
                        Console.WriteLine($"  {prop.Name} = {value}");
                    }
                    catch
                    {
                        Console.WriteLine($"  {prop.Name} = [Cannot Read]");
                    }
                }
                foreach (System.Reflection.MethodInfo method in Methods)
                {
                    Console.WriteLine($"Method: {method.Name}");
                }
                object myClass = Activator.CreateInstance(type);
            }
        }
    }
}

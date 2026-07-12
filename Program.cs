using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static CodeGenerator.clsHelper;

namespace CodeGenerator
{
    internal class Program
    {
        private static string _projectDirectory = ConfigurationManager.AppSettings["projectDirectory"];
        private static string _globalDefaultRole = "Admin";
        private static string prefix = "";

        // الـ Enum لتحديد نوع قاعدة البيانات
        public enum enDatabaseType { SqlServer, MySql, Postgres }
        private static enDatabaseType _selectedDbType;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            AnsiConsole.Write(
                                new FigletText("Code Generator")
                                    .Centered()
                                    .Color(Color.Green));

            AnsiConsole.Write(new Rule("[yellow]Welcome in Joudi's Code Generator v3.0 (Stable) [/]").Justify(Justify.Left));
            AnsiConsole.MarkupLine("[grey]This tool generates SPs, DAL, BLL, Controllers, and Security For you.[/]");
            AnsiConsole.MarkupLine("[red]Notice:[/] Please ensure database settings are configured in [cyan]clsHelper.connectionString[/].\n");

            // برومبت باستخدام Spectre.Console لاختيار الداتابيز
            _selectedDbType = AnsiConsole.Prompt(
                new SelectionPrompt<enDatabaseType>()
                    .Title("Select target [yellow]Database Type[/] for Code Generation:")
                    .PageSize(5)
                    .AddChoices(enDatabaseType.SqlServer, enDatabaseType.MySql, enDatabaseType.Postgres));

            AnsiConsole.MarkupLine($"[green]✔ Target Database Set To:[/] [bold cyan]{_selectedDbType}[/]\n");

            Console.Write("New Solution? (y/n): ");
            string answer = Console.ReadLine();
            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
            {
                Console.Write("solution Name: ");
                string sn = Console.ReadLine();

                clsHelper.GenerateArchitectureSolution(_projectDirectory, sn);
            }

            // Fetching all tables from the database using clsHelper.GetAllTables()
            List<string> databaseTables = clsHelper.GetAllTables();
            clsHelper.LoadAvailableRoles();
            AnsiConsole.MarkupLine($"[cyan]Found {databaseTables.Count} tables in the database.[/]\n");
            _globalDefaultRole = clsPresentation.PromptForActionRoles("Global Default CRUD Actions");
            Console.Write($"Enter The Prefix For all tables classes (Optional, e.g., 'cls' for Classes): ");
            prefix = Console.ReadLine();

            foreach (string tName in databaseTables)
            {
                while (Console.KeyAvailable) Console.ReadKey(true);

                if (AnsiConsole.Confirm($"Do you want to generate code for table [green]{tName}[/]?"))
                {
                    await Run(tName);
                    Console.Clear();
                    AnsiConsole.Write(new FigletText("ADO Gen Code").Centered().Color(Color.Green));
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine();
                }
            }

            // Finished generating code for all tables
            AnsiConsole.WriteLine();

            Panel signaturePanel = new Panel(
                new Markup(
                    $"[bold gold1]🚀 Total Code Generated:[/] [bold green]{TotalLinesGenerated:N0} lines of clean code![/]\n" +
                    $"[bold gold1]📦 Total Classes Generated:[/] [bold green]{clsHelper.TotalClasses} classes[/]\n" +
                    $"[bold gold1]📜 Total DTOs Generated:[/] [bold green]{clsHelper.TotalDTOs} DTOs[/]\n" +
                    $"[bold gold1]🔥 Total SPs Injected:[/] [bold green]{clsHelper.TotalSPs} Stored Procedures[/]\n\n" +
                    "[bold white]Developed with ❤️ by:[/] [bold green]Joudi[/]\n" +
                    "[bold white]Telegram:[/] [blue]@Joudi_Adeeb[/]\n" +
                    "[bold white]LinkedIn:[/] [blue]linkedin.com/in/joudi-adeeb[/]"
                )
            )
            .Header("[yellow]Generation Completed![/]")
            .BorderColor(Color.Gold1)
            .Padding(2, 1)
            .RoundedBorder()
            .Expand();

            AnsiConsole.Write(signaturePanel);
            AnsiConsole.MarkupLine("\n[grey]Press any key to exit...[/]");
            Console.ReadKey();
        }

        static bool isUserTable = false;
        public static async Task Run(string tableName)
        {
            clsHelper.tableName = tableName;
            clsHelper.Columns = clsHelper.getColumnsNameAndType();
            int count = clsHelper.Columns.Count;

            if (count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No Columns Found for table {tableName}, skipping...[/]");
                return;
            }

            StringBuilder DALFuncs = new StringBuilder();
            StringBuilder BLLFuncs = new StringBuilder();
            StringBuilder Controller = new StringBuilder();

            isUserTable = (tableName.ToLower() == "user" || tableName.ToLower() == "users");

            clsHelper.objectName = clsHelper.GetSingularEntityName(tableName);
            clsHelper.className = prefix + clsHelper.objectName;

            clsHelper.mappedColumns = clsHelper.mappingTheColumns();
            clsHelper.ColumnsForCsharp = clsHelper.getColumnsForCsharp();

            string defaultRole = _globalDefaultRole;
            clsBLL.DALName = $@"{clsHelper.className}DAL";

            // Inject Admin-specific architectural overloads for all tables globally
            BLLFuncs.Append(clsBLL.addAdminFunc());
            BLLFuncs.Append(clsBLL.updateFullFunc());

            // =========================================================
            // 1. Dictatoric way for User Table (Auto Generate All Actions)
            // =========================================================
            if (isUserTable)
            {
                AnsiConsole.MarkupLine("[yellow]-> Detecting User Table! Automatically generating complete secure infrastructure...[/]");

                // Basic Authentication and Authorization (Login and Register)
                clsHelper.allSPs.Add(GetLoginSP());
                DALFuncs.Append(GetAuthData());
                BLLFuncs.Append(clsBLL.checkLogin());
                BLLFuncs.Append(clsBLL.registerUser());

                // Add Standard & Admin
                clsHelper.allSPs.Add(GetAddSP());
                DALFuncs.Append(GetAddFunc());
                BLLFuncs.Append(clsBLL.addFunc());
                Controller.Append(clsAPIs.addAction(defaultRole));
                Controller.Append(clsAPIs.addAdminAction(defaultRole));

                // Forced GetByID method for User table with dynamic Ownership Policy
                clsHelper.Column idColumn = clsHelper.Columns[0];
                clsHelper.allSPs.Add(GetSelectByColumnSP(idColumn));
                DALFuncs.Append(GetRecordByColumnFunc(idColumn));
                BLLFuncs.Append(clsBLL.getByFunc(idColumn));
                BLLFuncs.Append(clsBLL.getBriefFunc(idColumn));
                Controller.Append(clsAPIs.getByAction(idColumn, defaultRole));

                // Forced GetByUsername method for User table restricted to Admin/Default role
                clsHelper.Column usernameColumn = clsHelper.Columns.Find(c => c.name.ToLower().Contains("username"));
                clsHelper.Column usernameColumnCsharp = clsHelper.ColumnsForCsharp.Find(c => c.name == usernameColumn.name);

                if (usernameColumnCsharp.name == null)
                {
                    usernameColumnCsharp = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("user")
                        && !c.name.ToLower().Contains("id") && !c.name.ToLower().Contains("role"));
                }

                if (!string.IsNullOrEmpty(usernameColumnCsharp.name))
                {
                    clsHelper.allSPs.Add(GetSelectByColumnSP(usernameColumn));
                    DALFuncs.Append(GetRecordByColumnFunc(usernameColumnCsharp));
                    BLLFuncs.Append(clsBLL.getByFunc(usernameColumnCsharp));
                    BLLFuncs.Append(clsBLL.getBriefFunc(usernameColumnCsharp));
                    Controller.Append(clsAPIs.getByAction(usernameColumnCsharp, defaultRole));
                }

                // Update Standard & Admin
                clsHelper.allSPs.Add(GetUpdateSP());
                DALFuncs.Append(GetUpdateFunc());
                BLLFuncs.Append(clsBLL.updateBriefFunc());
                Controller.Append(clsAPIs.updateAction(defaultRole));
                Controller.Append(clsAPIs.updateAdminAction(defaultRole));

                // Delete Forced for user table
                clsHelper.allSPs.Add(GetDeleteSP());
                DALFuncs.Append(GetDeleteFunc(idColumn));
                BLLFuncs.Append(clsBLL.deleteFunc(idColumn));
                Controller.Append(clsAPIs.deleteAction(idColumn, defaultRole));

                // IsExists Forced for user table
                clsHelper.allSPs.Add(GetIsExistByColumnSP(idColumn));
                DALFuncs.Append(GetIsExistsFunc(idColumn));
                BLLFuncs.Append(clsBLL.isExistsFunc(idColumn));
                Controller.Append(clsAPIs.isExistAction(idColumn, defaultRole));

                // Paging Forced for user table
                clsHelper.allSPs.Add(GetPagingSP());
                DALFuncs.Append(GetPagingFunc());
                BLLFuncs.Append(clsBLL.PagingFunc());
                Controller.Append(clsAPIs.pagingAction(defaultRole));

                // getAll Forced for user table
                DALFuncs.Append(GetAllFunc());
                BLLFuncs.Append(clsBLL.getAllBriefByFunc(idColumn));
                BLLFuncs.Append(clsBLL.getAllFullByFunc(idColumn));
                Controller.Append(clsAPIs.getAllBriefByAction(idColumn, defaultRole));
                Controller.Append(clsAPIs.getAllFullByAction(idColumn, defaultRole));
                clsHelper.allSPs.Add(GetSelectAllSP());
            }
            // =========================================================
            // 2. Automated / Democratic way for all other tables
            // =========================================================
            else
            {
                AnsiConsole.MarkupLine("[yellow]\n--- Generation Mode Selection ---[/]");
                Console.Write("Do you want to generate full CRUD automatically? (y/n): ");
                string modeAnswer = Console.ReadLine().ToLower();

                if (modeAnswer == "yes" || modeAnswer == "y")
                {
                    AnsiConsole.MarkupLine($"[green]-> Automatically generating full Secure CRUD using [/][bold yellow]{defaultRole}[/][green] role...[/]");
                    clsHelper.Column firstColumn = clsHelper.ColumnsForCsharp[0];

                    // Add Standard & Admin
                    clsHelper.allSPs.Add(GetAddSP());
                    DALFuncs.Append(GetAddFunc());
                    BLLFuncs.Append(clsBLL.addFunc());
                    Controller.Append(clsAPIs.addAction(defaultRole));
                    Controller.Append(clsAPIs.addAdminAction(defaultRole));

                    // GetByID
                    clsHelper.allSPs.Add(GetSelectByColumnSP(firstColumn));
                    DALFuncs.Append(GetRecordByColumnFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.getByFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.getBriefFunc(firstColumn));
                    Controller.Append(clsAPIs.getByAction(firstColumn, defaultRole));

                    // Update Standard & Admin
                    clsHelper.allSPs.Add(GetUpdateSP());
                    DALFuncs.Append(GetUpdateFunc());
                    BLLFuncs.Append(clsBLL.updateBriefFunc());
                    Controller.Append(clsAPIs.updateAction(defaultRole));
                    Controller.Append(clsAPIs.updateAdminAction(defaultRole));

                    // Delete
                    clsHelper.allSPs.Add(GetDeleteSP());
                    DALFuncs.Append(GetDeleteFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.deleteFunc(firstColumn));
                    Controller.Append(clsAPIs.deleteAction(firstColumn, defaultRole));

                    // IsExist
                    clsHelper.allSPs.Add(GetIsExistByColumnSP(firstColumn));
                    DALFuncs.Append(GetIsExistsFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.isExistsFunc(firstColumn));
                    Controller.Append(clsAPIs.isExistAction(firstColumn, defaultRole));

                    // Paging
                    clsHelper.allSPs.Add(GetPagingSP());
                    DALFuncs.Append(GetPagingFunc());
                    BLLFuncs.Append(clsBLL.PagingFunc());
                    Controller.Append(clsAPIs.pagingAction(defaultRole));

                    // GetAll
                    DALFuncs.Append(GetAllFunc());
                    BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));
                    Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, defaultRole));
                    Controller.Append(clsAPIs.getAllFullByAction(firstColumn, defaultRole));
                    clsHelper.allSPs.Add(GetSelectAllSP());

                    // Custom "By" filters extensions
                    AnsiConsole.MarkupLine("[cyan]\n-> CRUD generated. Now let's add custom 'By' columns filters...[/]");

                    // Custom Get By
                    Console.Write("Do you want to add custom 'Get By' methods? (yes/no): ");
                    string askgetBy = Console.ReadLine().ToLower();
                    if (askgetBy == "yes" || askgetBy == "y")
                    {
                        List<string> getByColumns = clsPresentation.getBy();
                        foreach (string colName in getByColumns)
                        {
                            clsHelper.Column columnSql = clsHelper.makeColumnByName(colName);
                            clsHelper.Column columnCsharp = clsHelper.mappedColumns.Find(c => c.name == colName);

                            clsHelper.allSPs.Add(GetSelectByColumnSP(columnSql));
                            DALFuncs.Append(GetRecordByColumnFunc(columnCsharp));
                            BLLFuncs.Append(clsBLL.getByFunc(columnCsharp));
                            BLLFuncs.Append(clsBLL.getBriefFunc(columnCsharp));

                            string getByRoles = clsPresentation.PromptForActionRoles($"GetBy{colName}");
                            Controller.Append(clsAPIs.getByAction(columnCsharp, getByRoles));
                        }
                    }

                    // Custom Exist By
                    Console.Write("Do you want to add custom 'isExist By' methods? (yes/no): ");
                    string askExistBy = Console.ReadLine().ToLower();
                    if (askExistBy == "yes" || askExistBy == "y")
                    {
                        List<string> existColumns = clsPresentation.existBy();
                        foreach (string colName in existColumns)
                        {
                            clsHelper.Column columnSql = clsHelper.makeColumnByName(colName);
                            clsHelper.Column columnCsharp = clsHelper.mappedColumns.Find(c => c.name == colName);

                            clsHelper.allSPs.Add(GetIsExistByColumnSP(columnSql));
                            DALFuncs.Append(GetIsExistsFunc(columnCsharp));
                            BLLFuncs.Append(clsBLL.isExistsFunc(columnCsharp));

                            string existRoles = clsPresentation.PromptForActionRoles($"ExistsBy{colName}");
                            Controller.Append(clsAPIs.isExistAction(columnCsharp, existRoles));
                        }
                    }

                    // Custom GetAll By
                    Console.Write("Do you want to add custom 'GetAll By' methods? (yes/no): ");
                    string askGetAllBy = Console.ReadLine().ToLower();
                    if (askGetAllBy == "yes" || askGetAllBy == "y")
                    {
                        List<string> getAllByColumns = clsPresentation.getAllBy();
                        foreach (string colName in getAllByColumns)
                        {
                            clsHelper.Column columnSql = clsHelper.makeColumnByName(colName);
                            clsHelper.Column columnCsharp = clsHelper.mappedColumns.Find(c => c.name == colName);

                            clsHelper.allSPs.Add(GetSelectAllBySP(columnSql));
                            DALFuncs.Append(GetAllByColumnFunc(columnCsharp));
                            BLLFuncs.Append(clsBLL.getAllBriefByFunc(columnCsharp));
                            BLLFuncs.Append(clsBLL.getAllFullByFunc(columnCsharp));

                            string getAllByRoles = clsPresentation.PromptForActionRoles($"GetAllBy{colName}");
                            Controller.Append(clsAPIs.getAllBriefByAction(columnCsharp, getAllByRoles));
                            Controller.Append(clsAPIs.getAllFullByAction(columnCsharp, getAllByRoles));
                        }
                    }
                }
                else
                {
                    Console.Write("Is this a Lookup table (Static/Read-Only data)? (y/n): ");
                    string lookupAnswer = Console.ReadLine().ToLower();

                    if (lookupAnswer == "yes" || lookupAnswer == "y")
                    {
                        AnsiConsole.MarkupLine($"[green]-> Generating Secure Lookup configuration using [/][bold yellow]{defaultRole}[/][green] role...[/]");
                        clsHelper.Column firstColumn = clsHelper.ColumnsForCsharp[0];

                        // GetByID
                        clsHelper.allSPs.Add(GetSelectByColumnSP(firstColumn));
                        DALFuncs.Append(GetRecordByColumnFunc(firstColumn));
                        BLLFuncs.Append(clsBLL.getByFunc(firstColumn));
                        BLLFuncs.Append(clsBLL.getBriefFunc(firstColumn));
                        Controller.Append(clsAPIs.getByAction(firstColumn, defaultRole));

                        // GetAll
                        DALFuncs.Append(GetAllFunc());
                        BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                        BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));
                        Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, defaultRole));
                        Controller.Append(clsAPIs.getAllFullByAction(firstColumn, defaultRole));
                        clsHelper.allSPs.Add(GetSelectAllSP());
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[blue]-> Falling back to manual customizable setup...[/]");
                        string answer = "yes";
                        Console.Write("\nFor DAL, BLL, And Stored Procedures:\n");

                        // Get By:
                        List<string> getByColumns = clsPresentation.getBy();
                        foreach (string colName in getByColumns)
                        {
                            clsHelper.Column column = clsHelper.makeColumnByName(colName);
                            clsHelper.allSPs.Add(GetSelectByColumnSP(column));
                            DALFuncs.Append(GetRecordByColumnFunc(column));
                            BLLFuncs.Append(clsBLL.getByFunc(column));
                            BLLFuncs.Append(clsBLL.getBriefFunc(column));

                            string getByRoles = clsPresentation.PromptForActionRoles($"GetBy{colName}");
                            Controller.Append(clsAPIs.getByAction(column, getByRoles));
                        }

                        // Update Standard & Admin:
                        Console.Write("update? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.allSPs.Add(GetUpdateSP());
                            DALFuncs.Append(GetUpdateFunc());
                            BLLFuncs.Append(clsBLL.updateBriefFunc());

                            string updateRoles = clsPresentation.PromptForActionRoles("Update");
                            Controller.Append(clsAPIs.updateAction(updateRoles));
                            Controller.Append(clsAPIs.updateAdminAction(clsPresentation.PromptForActionRoles("UpdateAdmin")));
                        }

                        // Delete:
                        Console.Write("delete? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.Column C = clsHelper.mappedColumns[0];
                            clsHelper.allSPs.Add(GetDeleteSP());
                            DALFuncs.Append(GetDeleteFunc(C));
                            BLLFuncs.Append(clsBLL.deleteFunc(C));

                            string deleteRoles = clsPresentation.PromptForActionRoles("Delete");
                            Controller.Append(clsAPIs.deleteAction(C, deleteRoles));
                        }

                        // Add Standard & Admin:
                        Console.Write("add? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.allSPs.Add(GetAddSP());
                            DALFuncs.Append(GetAddFunc());
                            BLLFuncs.Append(clsBLL.addFunc());

                            string addRoles = clsPresentation.PromptForActionRoles("Add");
                            Controller.Append(clsAPIs.addAction(addRoles));
                            Controller.Append(clsAPIs.addAdminAction(clsPresentation.PromptForActionRoles("AddAdmin")));
                        }

                        // isExist:
                        Console.Write("isExist? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            List<string> Columns = clsPresentation.existBy();
                            foreach (string colName in Columns)
                            {
                                clsHelper.Column column = clsHelper.makeColumnByName(colName);
                                clsHelper.allSPs.Add(GetIsExistByColumnSP(column));
                                DALFuncs.Append(GetIsExistsFunc(column));
                                BLLFuncs.Append(clsBLL.isExistsFunc(column));

                                string existRoles = clsPresentation.PromptForActionRoles($"ExistsBy{colName}");
                                Controller.Append(clsAPIs.isExistAction(column, existRoles));
                            }
                        }

                        // Paging:
                        Console.Write("Paging? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.allSPs.Add(GetPagingSP());
                            DALFuncs.Append(GetPagingFunc());
                            BLLFuncs.Append(clsBLL.PagingFunc());

                            string pagingRoles = clsPresentation.PromptForActionRoles("GetPage (Paging)");
                            Controller.Append(clsAPIs.pagingAction(pagingRoles));
                        }

                        // getAll:
                        Console.Write("getAll? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.Column firstColumn = clsHelper.ColumnsForCsharp[0];
                            DALFuncs.Append(GetAllFunc());

                            BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                            BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));

                            string getAllRoles = clsPresentation.PromptForActionRoles("GetAll");
                            Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, getAllRoles));
                            Controller.Append(clsAPIs.getAllFullByAction(firstColumn, getAllRoles));

                            clsHelper.allSPs.Add(GetSelectAllSP());
                            Console.Write("GetAll Method Generated, do you want 'GetAll By' ? (yes/no): ");
                            answer = Console.ReadLine();
                            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                            {
                                List<string> Columns = clsPresentation.getAllBy();
                                foreach (string colName in Columns)
                                {
                                    clsHelper.Column column = clsHelper.makeColumnByName(colName);
                                    clsHelper.allSPs.Add(GetSelectAllBySP(column));
                                    DALFuncs.Append(GetAllByColumnFunc(column));

                                    BLLFuncs.Append(clsBLL.getAllBriefByFunc(column));
                                    BLLFuncs.Append(clsBLL.getAllFullByFunc(column));

                                    string getAllByRoles = clsPresentation.PromptForActionRoles($"GetAllBy{colName}");
                                    Controller.Append(clsAPIs.getAllBriefByAction(column, getAllByRoles));
                                    Controller.Append(clsAPIs.getAllFullByAction(column, getAllByRoles));
                                }
                            }
                        }
                    }
                }
            }

            await saveFilesAsync(DALFuncs, BLLFuncs, Controller);
        }

        public static async Task saveFilesAsync(StringBuilder DALFuncs, StringBuilder BLLFuncs, StringBuilder Controller)
        {
            try
            {
                if (!Directory.Exists(_projectDirectory))
                {
                    Directory.CreateDirectory(_projectDirectory);
                }

                // Check for Folders
                string dal = Path.Combine(_projectDirectory, "DAL");
                string bll = Path.Combine(_projectDirectory, "BLL");
                string dto = Path.Combine(_projectDirectory, "Shared", "DTOs");

                string inputsDto = Path.Combine(dto, "Inputs");
                string outputsDto = Path.Combine(dto, "Outputs");
                string controllersFolder = Path.Combine(_projectDirectory, "WebAPI", "Controllers");

                if (!Directory.Exists(dal)) Directory.CreateDirectory(dal);
                if (!Directory.Exists(bll)) Directory.CreateDirectory(bll);
                if (!Directory.Exists(dto)) Directory.CreateDirectory(dto);
                if (!Directory.Exists(inputsDto)) Directory.CreateDirectory(inputsDto);
                if (!Directory.Exists(outputsDto)) Directory.CreateDirectory(outputsDto);

                if (isUserTable)
                {
                    await clsHelper.Auth();
                }
                if (!Directory.Exists(controllersFolder)) Directory.CreateDirectory(controllersFolder);

                // Exact file paths
                string dalPath = Path.Combine(dal, $"{clsHelper.className}DAL.cs");
                string bllPath = Path.Combine(bll, $"{clsHelper.className}.cs");
                string controllerPath = Path.Combine(controllersFolder, $"{clsHelper.objectName}Controller.cs");

                string briefInputDTOPath = Path.Combine(inputsDto, $"{clsHelper.className}BriefInputDTO.cs");
                string fullInputDTOPath = Path.Combine(inputsDto, $"{clsHelper.className}FullInputDTO.cs");
                string briefOutputDTOPath = Path.Combine(outputsDto, $"{clsHelper.className}BriefOutputDTO.cs");
                string fullOutputDTOPath = Path.Combine(outputsDto, $"{clsHelper.className}FullOutputDTO.cs");

                // Save files
                Console.Write("-> Making Stored Procedures... ");
                clsHelper.InjectAllToDB();
                Console.WriteLine("[Done]");

                Console.Write("-> Making DAL Class... ");
                string dalCode = GetClassStructure(DALFuncs);
                using (StreamWriter writer = new StreamWriter(dalPath))
                {
                    await writer.WriteAsync(dalCode);
                }
                TrackLines(dalCode);
                Console.WriteLine("[Done]");

                Console.Write("-> Making BLL Class... ");
                string bllCode = clsBLL.classStructure(BLLFuncs);
                using (StreamWriter writer = new StreamWriter(bllPath))
                {
                    await writer.WriteAsync(bllCode);
                }
                TrackLines(bllCode);
                Console.WriteLine("[Done]");

                Console.Write("-> Making 4 DTO Classes (Inputs/Outputs)... ");
                string briefInputCode = clsAPIs.BriefInputDTO();
                string fullInputCode = clsAPIs.FullInputDTO();
                string briefOutputCode = clsAPIs.BriefOutputDTO();
                string fullOutputCode = clsAPIs.FullOutputDTO();

                using (StreamWriter writer = new StreamWriter(briefInputDTOPath)) await writer.WriteAsync(briefInputCode);
                using (StreamWriter writer = new StreamWriter(fullInputDTOPath)) await writer.WriteAsync(fullInputCode);
                using (StreamWriter writer = new StreamWriter(briefOutputDTOPath)) await writer.WriteAsync(briefOutputCode);
                using (StreamWriter writer = new StreamWriter(fullOutputDTOPath)) await writer.WriteAsync(fullOutputCode);

                TrackLines(briefInputCode);
                TrackLines(fullInputCode);
                TrackLines(briefOutputCode);
                TrackLines(fullOutputCode);
                Console.WriteLine("[Done]");

                Console.Write("-> Making Web API Controller... ");
                string controllerCode = clsAPIs.controllerStructure(Controller);
                using (StreamWriter writer = new StreamWriter(controllerPath))
                {
                    await writer.WriteAsync(controllerCode);
                }
                TrackLines(controllerCode);

                clsHelper.TrackClass(3);
                clsHelper.TrackDTO(4);
                Console.WriteLine("[Done]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]Error while saving files:[/] {ex.Message}");
            }
        }

        // =========================================================================
        // 🛠️ دالات مساعدة (Wrappers) متوافقة 100% مع C# 7.3
        // =========================================================================
        private static string GetAuthData()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.getAuthData();
                case enDatabaseType.MySql: return clsMysqlDAL.getAuthData();
                case enDatabaseType.Postgres: return clsPostgresDAL.getAuthData();
                default: return "";
            }
        }

        private static string GetAddFunc()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.addFunc();
                case enDatabaseType.MySql: return clsMysqlDAL.addFunc();
                case enDatabaseType.Postgres: return clsPostgresDAL.addFunc();
                default: return "";
            }
        }

        private static string GetUpdateFunc()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.updateFunc();
                case enDatabaseType.MySql: return clsMysqlDAL.updateFunc();
                case enDatabaseType.Postgres: return clsPostgresDAL.updateFunc();
                default: return "";
            }
        }

        private static string GetDeleteFunc(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.deleteFunc(c);
                case enDatabaseType.MySql: return clsMysqlDAL.deleteFunc(c);
                case enDatabaseType.Postgres: return clsPostgresDAL.deleteFunc(c);
                default: return "";
            }
        }

        private static string GetRecordByColumnFunc(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.getRecordByColumnFunc(c);
                case enDatabaseType.MySql: return clsMysqlDAL.getRecordByColumnFunc(c);
                case enDatabaseType.Postgres: return clsPostgresDAL.getRecordByColumnFunc(c);
                default: return "";
            }
        }

        private static string GetIsExistsFunc(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.isExistsFunc(c);
                case enDatabaseType.MySql: return clsMysqlDAL.isExistsFunc(c);
                case enDatabaseType.Postgres: return clsPostgresDAL.isExistsFunc(c);
                default: return "";
            }
        }

        private static string GetPagingFunc()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.PagingFunc();
                case enDatabaseType.MySql: return clsMysqlDAL.PagingFunc();
                case enDatabaseType.Postgres: return clsPostgresDAL.PagingFunc();
                default: return "";
            }
        }

        private static string GetAllFunc()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.getAllFunc();
                case enDatabaseType.MySql: return clsMysqlDAL.getAllFunc();
                case enDatabaseType.Postgres: return clsPostgresDAL.getAllFunc();
                default: return "";
            }
        }

        private static string GetAllByColumnFunc(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.getAllByColumnFunc(c);
                case enDatabaseType.MySql: return clsMysqlDAL.getAllByColumnFunc(c);
                case enDatabaseType.Postgres: return clsPostgresDAL.getAllByColumnFunc(c);
                default: return "";
            }
        }

        private static string GetClassStructure(StringBuilder injectedString)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlDAL.classStructure(injectedString);
                case enDatabaseType.MySql: return clsMysqlDAL.classStructure(injectedString);
                case enDatabaseType.Postgres: return clsPostgresDAL.classStructure(injectedString);
                default: return "";
            }
        }

        private static string GetAddSP()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.addSP();
                case enDatabaseType.MySql: return clsMysqlSPs.addSP();
                case enDatabaseType.Postgres: return clsPostgresSPs.addSP();
                default: return "";
            }
        }

        private static string GetUpdateSP()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.updateSP();
                case enDatabaseType.MySql: return clsMysqlSPs.updateSP();
                case enDatabaseType.Postgres: return clsPostgresSPs.updateSP();
                default: return "";
            }
        }

        private static string GetDeleteSP()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.deleteSP();
                case enDatabaseType.MySql: return clsMysqlSPs.deleteSP();
                case enDatabaseType.Postgres: return clsPostgresSPs.deleteSP();
                default: return "";
            }
        }

        private static string GetSelectAllSP()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.selectAllSP();
                case enDatabaseType.MySql: return clsMysqlSPs.selectAllSP();
                case enDatabaseType.Postgres: return clsPostgresSPs.selectAllSP();
                default: return "";
            }
        }

        private static string GetSelectAllBySP(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.selectAllBySP(c);
                case enDatabaseType.MySql: return clsMysqlSPs.selectAllBySP(c);
                case enDatabaseType.Postgres: return clsPostgresSPs.selectAllBySP(c);
                default: return "";
            }
        }

        private static string GetSelectByColumnSP(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.selectByColumnSP(c);
                case enDatabaseType.MySql: return clsMysqlSPs.selectByColumnSP(c);
                case enDatabaseType.Postgres: return clsPostgresSPs.selectByColumnSP(c);
                default: return "";
            }
        }

        private static string GetPagingSP()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.PagingSP();
                case enDatabaseType.MySql: return clsMysqlSPs.PagingSP();
                case enDatabaseType.Postgres: return clsPostgresSPs.PagingSP();
                default: return "";
            }
        }

        private static string GetIsExistByColumnSP(Column c)
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.isExistByColumnSP(c);
                case enDatabaseType.MySql: return clsMysqlSPs.isExistByColumnSP(c);
                case enDatabaseType.Postgres: return clsPostgresSPs.isExistByColumnSP(c);
                default: return "";
            }
        }

        private static string GetLoginSP()
        {
            switch (_selectedDbType)
            {
                case enDatabaseType.SqlServer: return clsMssqlSPs.loginSP();
                case enDatabaseType.MySql: return clsMysqlSPs.loginSP();
                case enDatabaseType.Postgres: return clsPostgresSPs.loginSP();
                default: return "";
            }
        }
    }
}

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
            prefix = Console.ReadLine(); // Enter cls always, working on it to be optional in the future
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
            // =========================================================
            // 1. Dictatoric way for User Table (Auto Generate All Actions)
            // =========================================================
            if (isUserTable)
            {
                AnsiConsole.MarkupLine("[yellow]-> Detecting User Table! Automatically generating complete secure infrastructure...[/]");

                // Basic Authentication and Authorization (Login and Register)
                clsHelper.allSPs.Add(clsSPs.loginSP());
                DALFuncs.Append(clsDAL.getAuthData());
                BLLFuncs.Append(clsBLL.checkLogin());
                BLLFuncs.Append(clsBLL.registerUser());

                // Add Forced for User Table
                clsHelper.allSPs.Add(clsSPs.addSP());
                DALFuncs.Append(clsDAL.addFunc());
                BLLFuncs.Append(clsBLL.addFunc());
                Controller.Append(clsAPIs.addAction(defaultRole));

                // Forced GetByID method for User table with dynamic Ownership Policy
                clsHelper.Column idColumn = clsHelper.Columns[0];
                clsHelper.allSPs.Add(clsSPs.selectByColumnSP(idColumn));
                DALFuncs.Append(clsDAL.getRecordByColumnFunc(idColumn));
                BLLFuncs.Append(clsBLL.getByFunc(idColumn));
                BLLFuncs.Append(clsBLL.getBriefFunc(idColumn));
                Controller.Append(clsAPIs.getByAction(idColumn, defaultRole));

                // Forced GetByUsername method for User table restricted to Admin/Default role
                clsHelper.Column usernameColumn = clsHelper.Columns.Find(c => c.name.ToLower().Contains("username"));
                clsHelper.Column usernameColumnCsharp = clsHelper.ColumnsForCsharp.Find(c => c.name == usernameColumn.name);

                if (usernameColumnCsharp.name == null)
                {
                    usernameColumnCsharp = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("user")
                        && !c.name.ToLower().Contains("id")
                        && !c.name.ToLower().Contains("role"));
                }

                if (!string.IsNullOrEmpty(usernameColumnCsharp.name))
                {
                    clsHelper.allSPs.Add(clsSPs.selectByColumnSP(usernameColumn));
                    DALFuncs.Append(clsDAL.getRecordByColumnFunc(usernameColumnCsharp));
                    BLLFuncs.Append(clsBLL.getByFunc(usernameColumnCsharp));
                    BLLFuncs.Append(clsBLL.getBriefFunc(usernameColumnCsharp));
                    Controller.Append(clsAPIs.getByAction(usernameColumnCsharp, defaultRole));
                }

                // Update is forced for user table and ownership policy
                clsHelper.allSPs.Add(clsSPs.updateSP());
                DALFuncs.Append(clsDAL.updateFunc());
                BLLFuncs.Append(clsBLL.updateFunc());
                Controller.Append(clsAPIs.updateAction(defaultRole));

                // Delete Forced for user table
                clsHelper.allSPs.Add(clsSPs.deleteSP());
                DALFuncs.Append(clsDAL.deleteFunc(idColumn));
                BLLFuncs.Append(clsBLL.deleteFunc(idColumn));
                Controller.Append(clsAPIs.deleteAction(idColumn, defaultRole));

                // IsExists Forced for user table
                clsHelper.allSPs.Add(clsSPs.isExistByColumnSP(idColumn));
                DALFuncs.Append(clsDAL.isExistsFunc(idColumn));
                BLLFuncs.Append(clsBLL.isExistsFunc(idColumn));
                Controller.Append(clsAPIs.isExistAction(idColumn, defaultRole));

                // Paging Forced for user table
                clsHelper.allSPs.Add(clsSPs.PagingSP());
                DALFuncs.Append(clsDAL.PagingFunc());
                BLLFuncs.Append(clsBLL.PagingFunc());
                Controller.Append(clsAPIs.pagingAction(defaultRole));

                // getAll Forced for user table
                DALFuncs.Append(clsDAL.getAllFunc());
                BLLFuncs.Append(clsBLL.getAllBriefByFunc(idColumn));
                BLLFuncs.Append(clsBLL.getAllFullByFunc(idColumn));
                Controller.Append(clsAPIs.getAllBriefByAction(idColumn, defaultRole));
                Controller.Append(clsAPIs.getAllFullByAction(idColumn, defaultRole));
                clsHelper.allSPs.Add(clsSPs.selectAllSP());
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

                    // Add
                    clsHelper.allSPs.Add(clsSPs.addSP());
                    DALFuncs.Append(clsDAL.addFunc());
                    BLLFuncs.Append(clsBLL.addFunc());
                    Controller.Append(clsAPIs.addAction(defaultRole));

                    // GetByID
                    clsHelper.allSPs.Add(clsSPs.selectByColumnSP(firstColumn));
                    DALFuncs.Append(clsDAL.getRecordByColumnFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.getByFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.getBriefFunc(firstColumn));
                    Controller.Append(clsAPIs.getByAction(firstColumn, defaultRole));

                    // Update
                    clsHelper.allSPs.Add(clsSPs.updateSP());
                    DALFuncs.Append(clsDAL.updateFunc());
                    BLLFuncs.Append(clsBLL.updateFunc());
                    Controller.Append(clsAPIs.updateAction(defaultRole));

                    // Delete
                    clsHelper.allSPs.Add(clsSPs.deleteSP());
                    DALFuncs.Append(clsDAL.deleteFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.deleteFunc(firstColumn));
                    Controller.Append(clsAPIs.deleteAction(firstColumn, defaultRole));

                    // IsExist
                    clsHelper.allSPs.Add(clsSPs.isExistByColumnSP(firstColumn));
                    DALFuncs.Append(clsDAL.isExistsFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.isExistsFunc(firstColumn));
                    Controller.Append(clsAPIs.isExistAction(firstColumn, defaultRole));

                    // Paging
                    clsHelper.allSPs.Add(clsSPs.PagingSP());
                    DALFuncs.Append(clsDAL.PagingFunc());
                    BLLFuncs.Append(clsBLL.PagingFunc());
                    Controller.Append(clsAPIs.pagingAction(defaultRole));

                    // GetAll
                    DALFuncs.Append(clsDAL.getAllFunc());
                    BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                    BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));
                    Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, defaultRole));
                    Controller.Append(clsAPIs.getAllFullByAction(firstColumn, defaultRole));
                    clsHelper.allSPs.Add(clsSPs.selectAllSP());

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

                            clsHelper.allSPs.Add(clsSPs.selectByColumnSP(columnSql));
                            DALFuncs.Append(clsDAL.getRecordByColumnFunc(columnCsharp));
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

                            clsHelper.allSPs.Add(clsSPs.isExistByColumnSP(columnSql));
                            DALFuncs.Append(clsDAL.isExistsFunc(columnCsharp));
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

                            clsHelper.allSPs.Add(clsSPs.selectAllBySP(columnSql));
                            DALFuncs.Append(clsDAL.getAllByColumnFunc(columnCsharp));
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
                        clsHelper.allSPs.Add(clsSPs.selectByColumnSP(firstColumn));
                        DALFuncs.Append(clsDAL.getRecordByColumnFunc(firstColumn));
                        BLLFuncs.Append(clsBLL.getByFunc(firstColumn));
                        BLLFuncs.Append(clsBLL.getBriefFunc(firstColumn));
                        Controller.Append(clsAPIs.getByAction(firstColumn, defaultRole));

                        // GetAll
                        DALFuncs.Append(clsDAL.getAllFunc());
                        BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                        BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));
                        Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, defaultRole));
                        Controller.Append(clsAPIs.getAllFullByAction(firstColumn, defaultRole));
                        clsHelper.allSPs.Add(clsSPs.selectAllSP());
                    }
                    else
                    {
                        // Fallback to the fully manual democratic way (Original detailed prompts)
                        AnsiConsole.MarkupLine("[blue]-> Falling back to manual customizable setup...[/]");
                        string answer = "yes";
                        Console.Write("\nFor DAL, BLL, And Stored Procedures:\n");

                        // Get By:
                        List<string> getByColumns = clsPresentation.getBy();
                        foreach (string colName in getByColumns)
                        {
                            clsHelper.Column column = clsHelper.makeColumnByName(colName);
                            clsHelper.allSPs.Add(clsSPs.selectByColumnSP(column));
                            DALFuncs.Append(clsDAL.getRecordByColumnFunc(column));
                            BLLFuncs.Append(clsBLL.getByFunc(column));
                            BLLFuncs.Append(clsBLL.getBriefFunc(column));

                            string getByRoles = clsPresentation.PromptForActionRoles($"GetBy{colName}");
                            Controller.Append(clsAPIs.getByAction(column, getByRoles));
                        }

                        // Update:
                        Console.Write("update? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.allSPs.Add(clsSPs.updateSP());
                            DALFuncs.Append(clsDAL.updateFunc());
                            BLLFuncs.Append(clsBLL.updateFunc());

                            string updateRoles = clsPresentation.PromptForActionRoles("Update");
                            Controller.Append(clsAPIs.updateAction(updateRoles));
                        }

                        // Delete:
                        Console.Write("delete? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.Column C = clsHelper.mappedColumns[0];
                            clsHelper.allSPs.Add(clsSPs.deleteSP());
                            DALFuncs.Append(clsDAL.deleteFunc(C));
                            BLLFuncs.Append(clsBLL.deleteFunc(C));

                            string deleteRoles = clsPresentation.PromptForActionRoles("Delete");
                            Controller.Append(clsAPIs.deleteAction(C, deleteRoles));

                        }

                        // Add:
                        Console.Write("add? yes/no: ");
                        answer = Console.ReadLine();
                        if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                        {
                            clsHelper.allSPs.Add(clsSPs.addSP());
                            DALFuncs.Append(clsDAL.addFunc());
                            BLLFuncs.Append(clsBLL.addFunc());

                            string addRoles = clsPresentation.PromptForActionRoles("Add");
                            Controller.Append(clsAPIs.addAction(addRoles));
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
                                clsHelper.allSPs.Add(clsSPs.isExistByColumnSP(column));
                                DALFuncs.Append(clsDAL.isExistsFunc(column));
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
                            clsHelper.allSPs.Add(clsSPs.PagingSP());
                            DALFuncs.Append(clsDAL.PagingFunc());
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
                            DALFuncs.Append(clsDAL.getAllFunc());

                            BLLFuncs.Append(clsBLL.getAllBriefByFunc(firstColumn));
                            BLLFuncs.Append(clsBLL.getAllFullByFunc(firstColumn));

                            string getAllRoles = clsPresentation.PromptForActionRoles("GetAll");
                            Controller.Append(clsAPIs.getAllBriefByAction(firstColumn, getAllRoles));
                            Controller.Append(clsAPIs.getAllFullByAction(firstColumn, getAllRoles));

                            clsHelper.allSPs.Add(clsSPs.selectAllSP());
                            Console.Write("GetAll Method Generated, do you want 'GetAll By' ? (yes/no): ");
                            answer = Console.ReadLine();
                            if (answer.ToLower() == "yes" || answer.ToLower() == "y")
                            {
                                List<string> Columns = clsPresentation.getAllBy();
                                foreach (string colName in Columns)
                                {
                                    clsHelper.Column column = clsHelper.makeColumnByName(colName);
                                    clsHelper.allSPs.Add(clsSPs.selectAllBySP(column));
                                    DALFuncs.Append(clsDAL.getAllByColumnFunc(column));

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
                string briefDto = Path.Combine(dto, "Brief");
                string fullDto = Path.Combine(dto, "Full");
                string controllersFolder = Path.Combine(_projectDirectory, "WebAPI", "Controllers");

                if (!Directory.Exists(dal)) Directory.CreateDirectory(dal);
                if (!Directory.Exists(bll)) Directory.CreateDirectory(bll);
                if (!Directory.Exists(dto)) Directory.CreateDirectory(dto);
                if (!Directory.Exists(briefDto)) Directory.CreateDirectory(briefDto);
                if (!Directory.Exists(fullDto)) Directory.CreateDirectory(fullDto);

                if (isUserTable)
                {
                    // Call the Auth logic now relocated inside clsHelper
                    await clsHelper.Auth();
                }
                if (!Directory.Exists(controllersFolder)) Directory.CreateDirectory(controllersFolder);

                // Exact file paths
                string dalPath = Path.Combine(dal, $"{clsHelper.className}DAL.cs");
                string bllPath = Path.Combine(bll, $"{clsHelper.className}.cs");
                string BriefDTOPath = Path.Combine(briefDto, $"{clsHelper.className}BriefDTO.cs");
                string FullDTOPath = Path.Combine(fullDto, $"{clsHelper.className}FullDTO.cs");
                string controllerPath = Path.Combine(controllersFolder, $"{clsHelper.objectName}Controller.cs");

                // Save files
                Console.Write("-> Making Stored Procedures... ");
                clsHelper.InjectAllToDB();
                Console.WriteLine("[Done]");

                Console.Write("-> Making DAL Class... ");
                string dalCode = clsDAL.classStructure(DALFuncs);
                using (StreamWriter writer = new StreamWriter(dalPath))
                {
                    await writer.WriteAsync(dalCode);
                }
                TrackLines(dalCode); // Catch and count!
                Console.WriteLine("[Done]");

                Console.Write("-> Making BLL Class... ");
                string bllCode = clsBLL.classStructure(BLLFuncs);
                using (StreamWriter writer = new StreamWriter(bllPath))
                {
                    await writer.WriteAsync(bllCode);
                }
                TrackLines(bllCode); // Catch and count!
                Console.WriteLine("[Done]");

                Console.Write("-> Making DTO Class... ");
                string BriefDTOCode = clsAPIs.BriefDTO();
                using (StreamWriter writer = new StreamWriter(BriefDTOPath))
                {
                    await writer.WriteAsync(BriefDTOCode);
                }

                string FullDTOCode = clsAPIs.FullDTO();
                using (StreamWriter writer = new StreamWriter(FullDTOPath))
                {
                    await writer.WriteAsync(FullDTOCode);
                }

                TrackLines(BriefDTOCode); // Catch and count!
                TrackLines(FullDTOCode); // Catch and count!
                Console.WriteLine("[Done]");

                Console.Write("-> Making Web API Controller... ");
                string controllerCode = clsAPIs.controllerStructure(Controller);
                using (StreamWriter writer = new StreamWriter(controllerPath))
                {
                    await writer.WriteAsync(controllerCode);
                }
                TrackLines(controllerCode); // Catch and count!
                clsHelper.TrackClass(3);
                clsHelper.TrackDTO(2);
                Console.WriteLine("[Done]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]Error while saving files:[/] {ex.Message}");
            }
        }
    }
}

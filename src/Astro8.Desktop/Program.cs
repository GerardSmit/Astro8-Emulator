﻿using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO.Compression;
using Astro8;
using Astro8.Devices;
using Astro8.Instructions;
using Astro8.Utils;
using Astro8.Yabal;
using static SDL2.SDL;
using static SDL2.SDL.SDL_EventType;

var filePathOption = new Argument<FileInfo?>(
    name: "path",
    description: "File to compile.",
    parse: result =>
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var filePath = Path.GetFullPath(string.Join(" ", result.Tokens.Select(i => i.Value)));

        if (!File.Exists(filePath))
        {
            result.ErrorMessage = $"File '{filePath}' does not exist";
            return null;
        }

        return new FileInfo(filePath);
    })
{
    Arity = ArgumentArity.OneOrMore
};

var disableScreenOption = new Option<bool>(
    name: "--disable-screen",
    description: "Disable the screen.");

disableScreenOption.AddAlias("-xS");

var disableCharactersOption = new Option<bool>(
    name: "--disable-characters",
    description: "Disable the character set.");

disableCharactersOption.AddAlias("--disable-chars");
disableCharactersOption.AddAlias("-xC");

var charactersToConsoleOption = new Option<bool>(
    name: "--console",
    description: "Redirect character set to the console.");

charactersToConsoleOption.AddAlias("-c");

var stateOption = new Option<string>(
    name: "--state",
    description: "Path of the state.");

charactersToConsoleOption.AddAlias("-s");

var run = new Command(
    name: "run",
    description: "Run the given file")
{
    filePathOption,
    disableScreenOption,
    disableCharactersOption,
    charactersToConsoleOption,
    stateOption
};

run.SetHandler(Execute);

var outOption = new Option<string>(
    name: "--out",
    description: "Output path of the assembly.");

outOption.AddAlias("-o");

var formatOption = new Option<List<OutputFormat>?>(
    name: "--format",
    description: "Change the output format.",
    parseArgument: EnumHelper.ParseEnum<OutputFormat>);

formatOption.AddAlias("-f");

var build = new Command(
    name: "build",
    description: "Compiles the given file to an assembly file")
{
    filePathOption,
    outOption,
    formatOption
};

build.SetHandler(Build);

var rootCommand = new RootCommand
{
    run,
    build
};

async Task Build(InvocationContext ctx)
{
    var path = ctx.ParseResult.GetValueForArgument(filePathOption);

    if (path == null)
    {
        return;
    }

    var outPath = ctx.ParseResult.GetValueForOption(outOption);
    var formats = ctx.ParseResult.GetValueForOption(formatOption) ?? new List<OutputFormat>();

    if (formats.Count == 0)
    {
        formats.Add(OutputFormat.Assembly);
    }

    var builder = new YabalBuilder();
    var code = File.ReadAllText(path.FullName);
    await builder.CompileCodeAsync(code, file: path.Name);
    PrintErrors(builder.Errors, code);

    if (string.IsNullOrEmpty(outPath))
    {
        outPath = path.FullName;
    }
    else if (Path.EndsInDirectorySeparator(outPath) || Directory.Exists(outPath))
    {
        outPath = Path.Combine(outPath, Path.GetFileName(path.FullName));
    }

    foreach (var format in formats)
    {
        var filePath = Path.ChangeExtension(outPath, format switch
        {
            OutputFormat.Assembly => ".asm",
            OutputFormat.AstroExecutable => ".aexe",
            OutputFormat.Logisim => ".hex",
            OutputFormat.AssemblyWithComments => ".asmc",
            _ => throw new NotSupportedException()
        });

        using var file = File.Open(filePath, FileMode.Create);
        using var writer = new StreamWriter(file);

        switch (format)
        {
            case OutputFormat.Assembly:
                builder.ToAssembly(writer);
                Console.WriteLine($"Assembly file written to {filePath}");
                break;
            case OutputFormat.AssemblyWithComments:
                builder.ToAssembly(writer, true);
                Console.WriteLine($"Assembly with comments file written to {filePath}");
                break;
            case OutputFormat.AstroExecutable:
                builder.ToHex(writer);
                Console.WriteLine($"Astro Executable written to {filePath}");
                break;
            case OutputFormat.Logisim:
                builder.ToLogisimFile(writer, 0xFFFF);
                Console.WriteLine($"Logisim Evolution file written to {filePath}");
                break;
            default:
                throw new NotSupportedException();
        }
    }
}

async Task Execute(InvocationContext ctx)
{
    var path = ctx.ParseResult.GetValueForArgument(filePathOption);

    if (path == null)
    {
        return;
    }

    var builder = new YabalBuilder();
    var code = File.ReadAllText(path.FullName);
    await builder.CompileCodeAsync(code, file: path.Name);

    var disableScreen = ctx.ParseResult.GetValueForOption(disableScreenOption);
    var disableCharacters = ctx.ParseResult.GetValueForOption(disableCharactersOption);
    var statePath = ctx.ParseResult.GetValueForOption(stateOption);
    var charactersToConsole = ctx.ParseResult.GetValueForOption(charactersToConsoleOption);

    var config = ConfigContext.Load();
    using var handler = new DesktopHandler(
        config.Screen.Width,
        config.Screen.Height,
        config.Screen.Scale
    );

    var cpuBuilder = CpuBuilder.Create(handler, config);

    cpuBuilder.WithMemory();
    cpuBuilder.WithKeyboard();
    cpuBuilder.WithMouse();

    if (!disableScreen)
    {
        cpuBuilder.WithScreen();

        if (!handler.Init())
        {
            ctx.Console.WriteLine("Failed to initialize SDL2.");
            return;
        }
    }

    if (!disableCharacters)
    {
        cpuBuilder.WithCharacter(writeToConsole: charactersToConsole);

        if (charactersToConsole)
        {
            Console.CursorVisible = false;
            Console.Clear();
        }
    }

    var program = builder.Build();
    PrintErrors(builder.Errors, code);

    Console.WriteLine(program.ToAssembly(addComments: true));

    cpuBuilder.WithProgram(program);

    var cpu = cpuBuilder.Create();

    if (!string.IsNullOrEmpty(statePath) && File.Exists(statePath))
    {
        LoadState();
    }

    cpu.RunThread(
        config.Cpu.CycleDuration,
        config.Cpu.InstructionsPerCycle
    );

    Console.CancelKeyPress += (_, _) =>
    {
        Console.CursorVisible = true;
    };

    try
    {
        // Console screen
        if (disableScreen)
        {
            while (cpu.Running)
            {
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.F11 when statePath != null:
                        SaveState();
                        break;
                    case ConsoleKey.F12 when statePath != null:
                        LoadState();
                        break;
                    default:
                    {
                        if (Character.CharToInt.TryGetValue(key.KeyChar, out var value))
                        {
                            cpu.SetKeyboard(value);
                        }

                        break;
                    }
                }
            }

            return;
        }

        // SDL2 screen
        while (cpu.Running)
        {
            handler.Update();

            if (SDL_PollEvent(out var e) != 1)
            {
                continue;
            }

            if (e.type is SDL_APP_TERMINATING or SDL_QUIT)
            {
                cpu.Halt();
                break;
            }

            switch (e.type)
            {
                case SDL_KEYDOWN:
                {
                    switch (e.key.keysym.scancode)
                    {
                        case SDL_Scancode.SDL_SCANCODE_F11 when statePath != null:
                            SaveState();
                            break;
                        case SDL_Scancode.SDL_SCANCODE_F12 when statePath != null:
                            LoadState();
                            break;
                    }

                    cpu.SetKeyboard(
                        Keyboard.Table.TryGetValue((int) e.key.keysym.scancode, out var keyCode)
                            ? keyCode
                            : 168
                    );

                    break;
                }
                case SDL_KEYUP:
                {
                    cpu.SetKeyboard(168);
                    break;
                }
                case SDL_MOUSEMOTION:
                    cpu.SetMousePosition(e.motion.x, e.motion.y);
                    break;
                case SDL_MOUSEBUTTONDOWN when e.button.button == SDL_BUTTON_LEFT:
                    cpu.SetMouseButton(MouseButton.Left, true);
                    break;
                case SDL_MOUSEBUTTONUP when e.button.button == SDL_BUTTON_LEFT:
                    cpu.SetMouseButton(MouseButton.Left, false);
                    break;
                case SDL_MOUSEBUTTONDOWN when e.button.button == SDL_BUTTON_RIGHT:
                    cpu.SetMouseButton(MouseButton.Right, true);
                    break;
                case SDL_MOUSEBUTTONUP when e.button.button == SDL_BUTTON_RIGHT:
                    cpu.SetMouseButton(MouseButton.Right, false);
                    break;
            }
        }
    }
    finally
    {
        Console.CursorVisible = true;
    }

    void SaveState()
    {
        using var file = File.Open(statePath, FileMode.Create);
        using var stream = new GZipStream(file, CompressionMode.Compress);
        cpu.Save(stream);
    }

    void LoadState()
    {
        if (!File.Exists(statePath))
        {
            return;
        }

        using var file = File.Open(statePath, FileMode.Open);
        using var stream = new GZipStream(file, CompressionMode.Decompress);
        cpu.Load(stream);
    }
}

return await rootCommand.InvokeAsync(args, console: new SystemConsole());

void PrintErrors(IReadOnlyDictionary<SourceRange, List<CompileError>> errorByRange, string code)
{
    if (errorByRange.Count == 0)
    {
        return;
    }

    Console.WriteLine("Compilation errors:");

    foreach (var (range, errors) in errorByRange.OrderBy(i => i.Key))
    {
        Console.WriteLine();
        Console.WriteLine($"In '{range.File}' at line {range.StartLine}, column {range.StartColumn}:");

        if (errors.Count == 1)
        {
            Console.Write("   ");
            WriteError(errors[0]);
        }
        else
        {
            foreach (var error in errors)
            {
                Console.Write(" - ");
                WriteError(error);
            }
        }

        Console.WriteLine();
        Console.WriteLine(code.GetPeek(range));
    }
}

void WriteError(CompileError compileError)
{
    var color = Console.ForegroundColor;

    Console.ForegroundColor = compileError.Level switch
    {
        ErrorLevel.Error => ConsoleColor.DarkRed,
        ErrorLevel.Warning => ConsoleColor.DarkYellow,
        ErrorLevel.Debug => ConsoleColor.DarkGray,
        _ => color
    };

    Console.Write(compileError.Level);

    Console.ForegroundColor = color;

    Console.Write(": ");
    Console.WriteLine(compileError.Message);
}

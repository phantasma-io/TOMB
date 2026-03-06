using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;

using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Validation;

using Module = Phantasma.Tomb.CodeGen.Module;
using PhantasmaPhoenix.Cryptography;
using DomainSettings = PhantasmaPhoenix.Protocol.DomainSettings;

namespace Phantasma.Tomb
{
    class Program
    {
        static void ExportLibraryInfo()
        {
            var sb = new StringBuilder();
            foreach (var libraryName in Contract.AvailableLibraries)
            {
                var library = Contract.LoadLibrary(libraryName, null, libraryName == Module.FormatLibraryName ? ModuleKind.Description : ModuleKind.Contract);

                sb.AppendLine("### "+libraryName);
                sb.AppendLine("| Method | Return type | Description|");
                sb.AppendLine("| ------------- | ------------- |------------- |");
                foreach (var method in library.methods.Values)
                {
                    var parameters = new StringBuilder();
                    int index = -1;
                    foreach (var entry in method.Parameters)
                    {
                        index++;

                        if (parameters.Length > 0)
                        {
                            parameters.Append(", ");
                        }

                        parameters.Append(entry.Name + ":" + entry.Type + "");
                    }

                    sb.AppendLine($"| {libraryName}.{method.Name}({parameters}) | {method.ReturnType} | TODO|");
                }
                sb.AppendLine("");
            }

            File.WriteAllText("libs.txt", sb.ToString());
        }

        static void ExportModule(Module module, string outputPath)
        {
            if (module.asm != null)
            {
                File.WriteAllText(Path.Combine(outputPath, module.Name + ".asm"), module.asm);
            }

            if (module.script != null)
            {
                var extension = module.Kind == ModuleKind.Script ? ".tx" : ".pvm";
                File.WriteAllBytes(Path.Combine(outputPath, module.Name + extension), module.script);

                var hex = Base16.Encode(module.script);
                File.WriteAllText(Path.Combine(outputPath, module.Name + extension + ".hex"), hex);
            }

            if (module.debugInfo != null)
            {
                File.WriteAllText(Path.Combine(outputPath, module.Name + ".debug"), module.debugInfo.ToJSON());
            }

            if (module.abi != null)
            {
                var abiBytes = module.abi.ToByteArray();
                File.WriteAllBytes(Path.Combine(outputPath, module.Name + ".abi"), abiBytes);

                var hex = Base16.Encode(abiBytes);
                File.WriteAllText(Path.Combine(outputPath, module.Name + ".abi.hex"), hex);
            }
        }

        static public void ShowWarning(string warning)
        {
            var temp = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warning);
            Console.ForegroundColor = temp;
        }

        static IEnumerable<KeyValuePair<string, Type>> GetTypesWithHelpAttribute(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttributes(typeof(CompilerAttribute), true);
                if (attr.Length > 0)
                {
                    var myAttribute = attr[0] as CompilerAttribute;

                    yield return new KeyValuePair<string, Type>(myAttribute.Extension, type);
                }
            }
        }

        static Compiler FindCompilerForFile(string fileName, int targetProtocolVersion)
        {
            var extension = Path.GetExtension(fileName);

            var compilerType = typeof(Compiler);

            var compilerTypes = GetTypesWithHelpAttribute(compilerType.Assembly);

            foreach (var entry in compilerTypes)
            {
                if (entry.Key == extension)
                {
                    var targetCompilerType = entry.Value;
                    return (Compiler) Activator.CreateInstance(targetCompilerType, new object[] { targetProtocolVersion });
                }
            }

            return null;
        }

        static bool TryParseNativeCheckMode(string value, out NativeCheckMode mode)
        {
            if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                mode = NativeCheckMode.Off;
                return true;
            }

            if (string.Equals(value, "warn", StringComparison.OrdinalIgnoreCase))
            {
                mode = NativeCheckMode.Warn;
                return true;
            }

            if (string.Equals(value, "error", StringComparison.OrdinalIgnoreCase))
            {
                mode = NativeCheckMode.Error;
                return true;
            }

            mode = NativeCheckMode.Error;
            return false;
        }

        static void Main(string[] args)
        {
            string sourceFileName = null;
            string outputPath = null;

            Compiler.WarningHandler = ShowWarning;

            int targetProtocolVersion = DomainSettings.LatestKnownProtocol;

            for (int i=0; i<args.Length; i++)
            {
                if (i == args.Length - 1)
                {
                    sourceFileName = args[i];
                    break;
                }

                var tmp = args[i].Split(':', 2);

                var tag = tmp[0];
                string value = tmp.Length == 2 ? tmp[1] : null;

                switch (tag)
                {
                    case "protocol":
                        int version = -1;
                        if (int.TryParse(value, out version) && version > 0)
                        {
                            targetProtocolVersion = version;
                        }
                        else
                        {
                            ShowWarning("Invalid protocol version: " + value);
                        }
                        break;

                    case "output":
                        {
                            outputPath = value;
                            break;
                        }

                    case "libpath":
                        {
                            Module.AddLibraryPath(value);
                            break;
                        }

                    case "debug":
                        {
                            Compiler.DebugMode = true;
                            break;
                        }

                    case "nativecheck":
                        {
                            NativeCheckMode mode;
                            if (TryParseNativeCheckMode(value, out mode))
                            {
                                Compiler.NativeCheckMode = mode;
                            }
                            else
                            {
                                ShowWarning("Invalid nativecheck mode: " + value + " (expected off|warn|error)");
                            }
                            break;
                        }

                    default:
                        ShowWarning("Unknown option: " + tag);
                        break;
                }
            }

            bool compilingBuiltins = false;

#if DEBUG
            if (string.IsNullOrEmpty(sourceFileName))
            {
                compilingBuiltins = true;
                sourceFileName = @"../../../builtins.tomb";
            }
#else
            if (string.IsNullOrEmpty(sourceFileName))
            {
                sourceFileName = @"my_contract.tomb";
            }
#endif

            sourceFileName = Path.GetFullPath(sourceFileName);

            if (!File.Exists(sourceFileName))
            {
                Console.WriteLine("File not found:" + sourceFileName);
                return;
            }

            if (outputPath == null)
            {
                outputPath = Path.GetDirectoryName(sourceFileName);

                if (string.IsNullOrEmpty(outputPath) || compilingBuiltins)
                {
                    outputPath = "./";
                }

                outputPath = Path.GetFullPath(outputPath);
            }

            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("Directory not found:" + outputPath);
            }

            outputPath = Path.Combine(outputPath, "Output");
            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("Creating output dir :" + outputPath);
                Directory.CreateDirectory(outputPath);
            }

            Module.AddLibraryPath(outputPath);
            Console.WriteLine("Output path: " + outputPath);

            var sourceCode = File.ReadAllText(sourceFileName);

            Console.WriteLine("Compiling " + sourceFileName);
            Console.WriteLine("Target protocol version: " + targetProtocolVersion);

            var compiler = FindCompilerForFile(sourceFileName, targetProtocolVersion);
            if (compiler == null)
            {
                Console.WriteLine("No compiler found for file: " + sourceFileName);
                return;
            }

            try
            {
                var modules = compiler.Process(sourceCode);

                foreach (var module in modules)
                {
                    ExportModule(module, outputPath);

                    foreach (var subModule in module.SubModules)
                    {
                        ExportModule(subModule, outputPath);
                    }
                }
            }
            catch (CompilerException ex) 
            {
                Console.WriteLine(ex.Message);
                System.Environment.Exit(-1);
            }

            Console.WriteLine("Success!");

#if DEBUG
            //ExportLibraryInfo();
#endif

        }
    }
}

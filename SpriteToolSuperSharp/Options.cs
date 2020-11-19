﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpriteToolSuperSharp {
    class CommandLineOptions {
        private string RomFile;
        readonly List<string> Opts = new List<string>();
        public Dictionary<Defines.FileType, string> Paths = new();
        public Dictionary<Defines.ExtType, string> ExtPaths = new();
        public string OutFile = null;
        public string AsmDir = string.Empty;
        public string AsmDirPath = string.Empty;

        public bool Debug { get; private set; } = false;
        public bool PerLevel { get; private set; }  = false;
        public bool Disable255SpritePerLevel { get; private set; }  = false;
        public bool KeepTmpFile { get; private set; } = false;
        public bool ExtMod { get; private set; } = true;
        public bool DisableMeiMei { get; private set; } = false;

        public bool AlwaysRemap { get; private set; } = false;
        public bool MeiMeiDebug { get; private set; } = false;
        public bool MeiMeiKeepTemp { get; private set; } = false;

        public IntPtr WindowHandle = new IntPtr();
        public UInt16 VerificationCode = 0;

        public CommandLineOptions(string[] args) {
            Opts = args.ToList();
            RomFile = Parse();
            if (!File.Exists(RomFile)) {
                Mixins.WaitAndExit($"Couldn't find {RomFile}, please make sure the name is correct");
            }
        }

        public string GetRomFile() {
            return RomFile;
        }

        public void PrintHelp() {
            var paths = new ToolPaths().ASMPaths;
            Console.Out.WriteLine($"Version 1.{Defines.ToolVersion:X02}");
            Console.Out.WriteLine("Usage: pixi <options> <ROM>\nOptions are:");
            Console.Out.WriteLine("-d\t\tEnable debug output, the following flag <-out> only works when this is set");
            Console.Out.WriteLine("-out <filename>\t\tTo be used IMMEDIATELY after -d, will redirect the debug output to the specified file, if omitted, the output will default to prompt");
            Console.Out.WriteLine("-k\t\tKeep debug files\n");
            Console.Out.WriteLine($"-l  <listpath>\tSpecify a custom list file (Default: {paths[Defines.FileType.List]})");
            Console.Out.WriteLine("-pl\t\tPer level sprites - will insert perlevel sprite code");
            Console.Out.WriteLine("-npl\t\tSame as the current default, no sprite per level will be inserted, left dangling for compatibility reasons");
            Console.Out.WriteLine("-d255spl\t\tDisable 255 sprite per level support (won't do the 1938 remap)");
            Console.Out.WriteLine("\n");

            Console.Out.WriteLine($"-a  <asm>\tSpecify a custom asm directory (Default {paths[Defines.FileType.Asm]})");
            Console.Out.WriteLine($"-sp <sprites>\tSpecify a custom sprites directory (Default {paths[Defines.FileType.Sprites]})");
            Console.Out.WriteLine($"-sh <shooters>\tSpecify a custom shooters directory (Default {paths[Defines.FileType.Shooters]})");
            Console.Out.WriteLine($"-g  <generators>\tSpecify a custom generators directory (Default {paths[Defines.FileType.Generators]})");
            Console.Out.WriteLine($"-e  <extended>\tSpecify a custom extended sprites directory (Default {paths[Defines.FileType.Extended]})");
            Console.Out.WriteLine($"-c  <cluster>\tSpecify a custom cluster sprites directory (Default {paths[Defines.FileType.Cluster]})");
            Console.Out.WriteLine("\n");

            Console.Out.WriteLine($"-r   <routines>\tSpecify a shared routine directory (Default {paths[Defines.FileType.List]})");
            Console.Out.WriteLine("\n");

            Console.Out.WriteLine("-ext-off\t Disables extmod file logging (check LM's readme for more info on what extmod is)");
            Console.Out.WriteLine("-ssc <append ssc>\tSpecify ssc file to be copied into <romname>.ssc");
            Console.Out.WriteLine("-mwt <append mwt>\tSpecify mwt file to be copied into <romname>.mwt");
            Console.Out.WriteLine("-mw2 <append mw2>\tSpecify mw2 file to be copied into <romname>.mw2, the provided file is assumed to have 0x00 first byte sprite header and the 0xFF end byte");
            Console.Out.WriteLine("-s16 <base s16>\tSpecify s16 file to be used as a base for <romname>.s16");
            Console.Out.WriteLine("     Do not use <romname>.xxx as an argument as the file will be overwriten");

            Console.Out.WriteLine("\nMeiMei flags:");
            Console.Out.WriteLine("-meimei-off\t\tShuts down MeiMei completely");
            Console.Out.WriteLine("-meimei-a\t\tEnables always remap sprite data");
            Console.Out.WriteLine("-meimei-k\t\tEnables keep temp patches files");
            Console.Out.WriteLine("-meimei-d\t\tEnables debug for MeiMei patches\n");
            Environment.Exit(0);
        }
        private string Parse() {
            if (Opts.Count == 0) {
                Console.Out.WriteLine("Insert the name of your ROM (or drag and drop the rom on the window): ");
                return Console.ReadLine();
            }
            if (Opts.Count == 1 && (Opts[0] == "-h" || Opts[0] == "--help"))
                PrintHelp();
            for (int i = 0; i < Opts.Count - 1; i++) {
                switch (Opts[i]) {
                    case "-h":
                    case "--help":
                        PrintHelp();
                        break;
                    case "-d":
                    case "--debug":
                        if (Opts[i + 1] == "-out") {
                            OutFile = Opts[i + 2];
                            i += 2;
                        }
                        Debug = true;
                        break;
                    case "-k":
                        KeepTmpFile = true;
                        break;
                    case "-pl":
                        PerLevel = true;
                        break;
                    case "-npl":
                        PerLevel = false;
                        break;
                    case "-d255spl":
                        Disable255SpritePerLevel = true;
                        break;
                    case "-meimei-a":
                        AlwaysRemap = true;
                        break;
                    case "-meimei-d":
                        MeiMeiDebug = true;
                        break;
                    case "-meimei-k":
                        MeiMeiKeepTemp = true;
                        break;
                    case "-meimei-off":
                        DisableMeiMei = true;
                        break;
                    case "-ext-off":
                        ExtMod = false;
                        break;
                    case "-r":
                        Paths.Add(Defines.FileType.Routines, Opts[++i]);
                        break;
                    case "-a":
                        Paths.Add(Defines.FileType.Asm, Opts[++i]);
                        break;
                    case "-sp":
                        Paths.Add(Defines.FileType.Sprites, Opts[++i]);
                        break;
                    case "-g":
                        Paths.Add(Defines.FileType.Generators, Opts[++i]);
                        break;
                    case "-l":
                        Paths.Add(Defines.FileType.List, Opts[++i]);
                        break;
                    case "-e":
                        Paths.Add(Defines.FileType.Extended, Opts[++i]);
                        break;
                    case "-c":
                        Paths.Add(Defines.FileType.Cluster, Opts[++i]);
                        break;
                    case "-ssc":
                        ExtPaths.Add(Defines.ExtType.ExtSSC, Opts[++i]);
                        break;
                    case "-mwt":
                        ExtPaths.Add(Defines.ExtType.ExtMWT, Opts[++i]);
                        break;
                    case "-mw2":
                        ExtPaths.Add(Defines.ExtType.ExtMW2, Opts[++i]);
                        break;
                    case "-s16":
                        ExtPaths.Add(Defines.ExtType.ExtS16, Opts[++i]);
                        break;
                    case "-lm-handle":
                        WindowHandle = new IntPtr(Convert.ToUInt32(Opts[++i].Split(':')[0], 16));
                        VerificationCode = Convert.ToUInt16(Opts[i].Split(':')[1], 16);
                        break;
                    default:
                        throw new Exception($"Invalid command line argument/option {Opts[i]}");
                }
            }
            return Opts[^1];
        }
    }
    class ToolOptions {
        //options
        public bool Debug = false;
        public bool PerLevel = false;
        public bool Disable255SpritePerLevel = false;
        public bool KeepTmpFile = false;
        public bool ExtMod = false;
        public bool DisableMeiMei = false;

        //paths
        public string AsmDir = string.Empty;
        public string AsmDirPath = string.Empty;

        //optional output
        public TextWriter Output = null;
        public FileStream OptionalOutFile = null;

        //lunar magic handle
        public bool LMHandle = false;
        public IntPtr WindowHandle;
        public UInt16 VerificationCode = 0;


        public ToolOptions(CommandLineOptions cmd, out MeiMeiInstance meimei, out ToolPaths Paths) {
            meimei = new MeiMeiInstance(new MeiMei());
            meimei.SetOptions(cmd.AlwaysRemap, cmd.MeiMeiDebug, cmd.MeiMeiKeepTemp);
            Debug = cmd.Debug;
            PerLevel = cmd.PerLevel;
            Disable255SpritePerLevel = cmd.Disable255SpritePerLevel;
            KeepTmpFile = cmd.KeepTmpFile;
            ExtMod = cmd.ExtMod;
            DisableMeiMei = cmd.DisableMeiMei;
            AsmDir = cmd.AsmDir;
            AsmDirPath = cmd.AsmDirPath;
            WindowHandle = cmd.WindowHandle;
            VerificationCode = cmd.VerificationCode;
            if (VerificationCode != 0)
                LMHandle = true;
            Paths = new ToolPaths(cmd.Paths, cmd.ExtPaths);
            if (cmd.OutFile is not null) {
                OptionalOutFile = new FileStream(cmd.OutFile, FileMode.OpenOrCreate);
                Output = new StreamWriter(OptionalOutFile);
            } else if (Debug) {
                Output = Console.Out;
            }
        }

    }
}

using AsarCLR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SpriteSharp {
    class Pixi {
        private readonly ToolData Data;
        private readonly CommandLineOptions CmdOpts;
        private readonly ToolOptions Opts;
        private readonly ToolPaths Paths;
        private readonly Rom Rom;
        private readonly MeiMeiInstance MeiMei;

        public Pixi(string[] args) {
            Data = new ToolData();
            CmdOpts = new CommandLineOptions(args);
            Opts = new ToolOptions(CmdOpts, out MeiMei, out Paths);
            if (Opts.Backup) {
                File.Delete(CmdOpts.GetRomFile() + ".bak");
                File.Copy(CmdOpts.GetRomFile(), CmdOpts.GetRomFile() + ".bak");
            }
            Rom = new Rom(CmdOpts.GetRomFile());
        }
        async public Task Run() {
            if (!Rom.RunChecks(out string errors)) {
                throw new CheckFailedException(errors);
            }
            if (!Opts.DisableMeiMei) {
                MeiMei.Instance.Initialize(Rom.Filename);
            }

            // set paths
            SetPaths(Environment.GetCommandLineArgs()[0]);

            // create config files + extra defines
            CreateConfigFile(Opts.AsmDirPath + "/config.asm");
            Data.ExtraDefines = ListExtraASM(Opts.AsmDirPath + "/ExtraDefines");

            // populate sprite list
            await PopulateSpriteList(Paths.ASMPaths, Paths.ASMPaths[Defines.FileType.List], Opts.Output);

            // clean old rom
            await CleanPreviousRuns(Paths.ASMPaths[Defines.FileType.Asm], Opts.AsmDir);

            // create shared patch
            await CreateSharedPatch(Paths.ASMPaths[Defines.FileType.Routines]);

            // patch all types of sprites
            PatchSprites(Data.ExtraDefines, Data.SprLists[Defines.ListType.Sprite], Opts.PerLevel ? Defines.MaxSprCount : 0x100);
            PatchSprites(Data.ExtraDefines, Data.SprLists[Defines.ListType.Cluster], Defines.SprCount);
            PatchSprites(Data.ExtraDefines, Data.SprLists[Defines.ListType.Extended], Defines.SprCount);

            // create binary tables
            await CreateBinaryTables();

            // create mwt, mw2, ssc, s16
            await CreateExtFiles();

            // apply main.asm, cluster.asm, extended.asm
            Patch(Paths.ASMPaths[Defines.FileType.Asm] + "main.asm");
            Patch(Paths.ASMPaths[Defines.FileType.Asm] + "cluster.asm");
            Patch(Paths.ASMPaths[Defines.FileType.Asm] + "extended.asm");

            // apply extra asm
            ListExtraASM(Opts.AsmDirPath + "/ExtraHijacks").ForEach(x => Patch(x));

            // cleanup temp files, create restore file for lm
            if (!Opts.KeepTmpFile)
                CleanTempFiles(Paths.ASMPaths[Defines.FileType.Asm], Opts.PerLevel);
            if (Opts.ExtMod)
                CreateLMRestore(Rom.Filename);

            Console.WriteLine("\nAll sprites applied successfully!");

            Rom.Close();
            Asar.close();
            if (!Opts.DisableMeiMei) {
                MeiMei.Instance.ConfigureSa1Def(Opts.AsmDirPath + "/sa1def.asm");
                await MeiMei.Instance.Run();
            }

            if (Opts.Output != null)
                await Opts.Output.FlushAsync();

            if (Opts.LMHandle) {
                if (!NativeMethods.PostMessageWrap(Opts.WindowHandle, Opts.VerificationCode)) {
                    throw new LunarMagicException();
                }
                Environment.Exit(0);
            }
        }
        private void Patch(string patchname) {
            if (!Asar.patch(patchname, ref Rom.RomData)) {
                var errors = Asar.geterrors();
                throw new AsarErrorException("An error has been detected\n" + errors.Aggregate("", (x, b) => x += b.Fullerrdata + "\n"));
            }
            Rom.RomSize = Rom.RomData.Length;
        }
        private void SetPaths(string arg) {
            foreach (var (type, path) in Paths.ASMPaths) {
                if (type == Defines.FileType.List) {
                    Paths.ASMPaths[type] = path.SetPathsRelativeTo(Rom.Filename);
                } else {
                    Paths.ASMPaths[type] = path.SetPathsRelativeTo(arg);
                }
            }
            Opts.AsmDir = Paths.ASMPaths[Defines.FileType.Asm];
            Opts.AsmDirPath = Opts.AsmDir.CleanPathTrail();
            foreach (var (type, path) in Paths.Extensions) {
                Paths.Extensions[type] = path.SetPathsRelativeTo(Rom.Filename);
            }
        }
        private void CreateConfigFile(string configpath) {
            // if (opts.PerLevel || opts.Disable255SpritePerLevel) { return;  }
            string tofile = $"!PerLevel = {(Opts.PerLevel ? 1 : 0)}\n!Disable255SpritesPerLevel = {(Opts.Disable255SpritePerLevel ? 1 : 0)}";
            File.WriteAllText(configpath, tofile);
        }
        private async Task PopulateSpriteList(Dictionary<Defines.FileType, string> paths, string listName, TextWriter output) {
            Defines.ListType type = Defines.ListType.Sprite;
            List<string> lines = (await File.ReadAllLinesAsync(listName)).Select(x => x.Trim()).ToList();
            lines.RemoveAll(x => string.IsNullOrWhiteSpace(x));
            foreach (var (line, i) in lines.WithIndex()) {
                int sprid = 0;
                int level = 0x200;
                if (line == "SPRITE:") {
                    type = Defines.ListType.Sprite; continue;
                } else if (line == "EXTENDED:") {
                    type = Defines.ListType.Extended; continue;
                } else if (line == "CLUSTER:") {
                    type = Defines.ListType.Cluster; continue;
                }
                List<string> parts = new();
                StringBuilder sr = new StringBuilder();
                foreach (char c in line) {
                    if (c == ':' && parts.Count == 0 && Opts.PerLevel) {
                        parts.Add(sr.ToString().Trim());
                        sr.Clear();
                    } else if (char.IsWhiteSpace(c) && parts.Count < (Opts.PerLevel ? 2 : 1)) {
                        parts.Add(sr.ToString().Trim());
                        sr.Clear();
                    } else
                        sr.Append(c);
                }
                parts.Add(sr.ToString().Trim());
                if (parts.Count != (Opts.PerLevel ? 3 : 2))
                    throw new ListParsingException($"Error on line {i}, malformed line");
                try {
                    sprid = Convert.ToInt32(parts[0], 16);
                } catch (Exception e) {
                    throw new ListParsingException($"Error on line {i}: {e.Message}");
                }
                if (type == Defines.ListType.Sprite) {
                    if (parts.Count > 2) {
                        try {
                            level = Convert.ToInt32(parts[0], 16);
                            sprid = Convert.ToInt32(parts[1], 16);
                        } catch (Exception e) {
                            throw new ListParsingException($"Error on line {i}: {e.Message}");
                        }
                    }
                    if (parts.Count > 2 && level != 0x200 && !Opts.PerLevel) {
                        throw new ListParsingException($"Error on line {i}: Trying to insert per level sprites without the -pl flag");
                    }
                    int n = VerifySprite(level, sprid, Opts.PerLevel);
                    if (n == -1) {
                        if (sprid >= 0x100) throw new ListParsingException($"Error on line {i}: Sprite number must be less than 0x100");
                        if (level > 0x200) throw new ListParsingException($"Error on line {i}: Level must range from 000-1FF");
                        if (sprid >= 0xB0 && sprid < 0xC0) throw new ListParsingException($"Error on line {i}: Only sprite B0-BF can be assigned a level");
                    }
                } else {
                    if (sprid > Defines.SprCount)
                        throw new ListParsingException($"Error on line {i}: Sprite number must be less than 0x80");
                }
                if (Data.SprLists[type][sprid] is not null)
                    throw new ListParsingException($"Error on line {i}: Sprite number already used");
                Data.SprLists[type][sprid] = new();
                Data.SprLists[type][sprid].Line = i;
                Data.SprLists[type][sprid].Level = level;
                Data.SprLists[type][sprid].Number = sprid;
                Data.SprLists[type][sprid].SprType = (int)type;
                string dir = "";
                if (type != Defines.ListType.Sprite) {
                    dir = paths[(Defines.FileType)((int)Defines.FileType.Extended - 1 + (int)type)];
                } else {
                    if (sprid < 0xC0)
                        dir = paths[Defines.FileType.Sprites];
                    else if (sprid > 0xD0)
                        dir = paths[Defines.FileType.Shooters];
                    else
                        dir = paths[Defines.FileType.Generators];
                }
                Data.SprLists[type][sprid].Directory = dir;
                string filename = dir + parts[^1];
                if (type != Defines.ListType.Sprite) {
                    if (!filename.EndsWith(".asm"))
                        throw new ListParsingException($"Error on line {i}, not an asm file");
                    Data.SprLists[type][sprid].AsmFile = filename;
                } else {
                    Data.SprLists[type][sprid].CfgFile = filename;
                    var ext = Path.GetExtension(filename).ToLower();
                    if (ext == ".json") {
                        Data.SprLists[type][sprid].ReadJson(output);
                    } else if (ext == ".cfg") {
                        Data.SprLists[type][sprid].ReadCfg(output);
                    } else
                        throw new ListParsingException($"Error on line {i}: Unknown filetype");
                }
                if (output != null) {
                    output?.WriteLine($"Read from line {i}");
                    if (level != 0x200)
                        output?.WriteLine($"Number {sprid:X02} for level {level:X03}");
                    else
                        output?.WriteLine($"Number {sprid:X02}");
                    output?.Flush();
                    Data.SprLists[type][sprid].PrintSprite(output);
                    output?.Write("\n--------------------------------------\n");
                    output?.Flush();
                }
                if (Data.SprLists[type][sprid].Table.Type == 0) {
                    Data.SprLists[type][sprid].Table.Init = new Pointer(Defines.InitPtr + 2 * sprid);
                    Data.SprLists[type][sprid].Table.Main = new Pointer(Defines.MainPtr + 2 * sprid);
                }
            }
        }
        private async Task CleanPreviousRuns(string pathname, string asmdir) {
            int pixi = Rom.SnesToPc(0x02FFE2);
            int sprtool = Rom.SnesToPc(Rom.PointerFromSnes(0x02A963).Addr() - 3);
            if (Encoding.UTF8.GetString(Rom.RomData[pixi..(pixi + 4)]) == "STSD") {
                string cleanpatch = asmdir + "_cleanup.asm";
                StringBuilder sr = new();
                int version = Rom.RomData[Rom.SnesToPc(0x02FFE6)];
                int flags = Rom.RomData[Rom.SnesToPc(0x02FFE7)];
                bool perlevel = ((flags & 0x01) == 1) || (version < 2);
                if (perlevel) {
                    if (version >= 30) {
                        sr.Append(";Per-Level sprites\n");
                        int leveltableaddr = Rom.PointerFromSnes(0x02FFF1).Addr();
                        if (leveltableaddr != 0xFFFFFF && leveltableaddr != 0x000000) {
                            int plsaddr = Rom.SnesToPc(leveltableaddr);
                            for (int level = 0; level < 0x400; level += 2) {
                                int plslvaddr = (Rom.RomData[plsaddr + level] + (Rom.RomData[plsaddr + level + 1] << 8));
                                if (plslvaddr == 0)
                                    continue;
                                plslvaddr = Rom.SnesToPc(plslvaddr + leveltableaddr);
                                for (int i = 0; i < 0x20; i += 2) {
                                    int plsdataaddr = (Rom.RomData[plslvaddr + i] + (Rom.RomData[plslvaddr + i + 1] << 8));
                                    if (plsdataaddr == 0)
                                        continue;
                                    Pointer mainptr = Rom.PointerFromSnes(plsdataaddr + leveltableaddr + 0x0B);
                                    if (mainptr.Addr() == 0xFFFFFF)
                                        continue;
                                    if (!mainptr.IsEmpty())
                                        sr.Append($"autoclean ${mainptr.Addr():X06}\t;{level >> 1:X03}:{0xB0 + (i >> 1):X02}\n");
                                }
                            }
                        }
                    }
                } else {
                    for (int bank = 0; bank < 4; bank++) {
                        int leveltableaddr = (Rom.RomData[Rom.SnesToPc(0x02FFEA + bank)] << 16) + 0x8000;
                        if (leveltableaddr == 0xFF8000)
                            continue;
                        sr.Append($";Per level sprites for levels {bank * 0x80:X03} - {((bank + 1) * 0x80) - 1:X03}\n");
                        for (int tableoff = 0x0B; tableoff < 0x8000; tableoff += 0x10) {
                            Pointer mainptr = Rom.PointerFromSnes(leveltableaddr + tableoff);
                            if (mainptr.Addr() == 0xFFFFFF) {
                                break;
                            }
                            if (!mainptr.IsEmpty()) {
                                sr.Append($"autoclean ${mainptr.Addr():X06}");
                            }
                        }
                        sr.Append('\n');
                    }
                }
                int limit = version >= 30 ? 0x1000 : (perlevel ? 0xF00 : 0x1000);
                sr.Append(";Global sprites: \n");
                int globaltableaddr = Rom.PointerFromSnes(0x02FFEE).Addr();
                if (Rom.PointerFromSnes(globaltableaddr).Addr() != 0xFFFFFF) {
                    for (int tableoffset = 0x0B; tableoffset < limit; tableoffset += 0x10) {
                        Pointer mainptr = Rom.PointerFromSnes(globaltableaddr + tableoffset);
                        if (!mainptr.IsEmpty()) {
                            sr.Append($"autoclean ${mainptr.Addr():X06}\n");
                        }
                    }
                }
                sr.Append("\n\n; Routines: \n");
                for (int i = 0; i < 100; i++) {
                    int routineptr = Rom.PointerFromSnes(0x03E05C + i * 3).Addr();
                    if (routineptr != 0xFFFFFF) {
                        sr.Append($"autoclean ${routineptr:X06}\n\torg ${0x03E05C + (i * 3):X06}\n\tdl $FFFFFF\n");
                    }
                }
                if (version >= 1) {
                    sr.Append("\n\n; Cluster:\n");
                    int clustertable = Rom.PointerFromSnes(0x00A68A).Addr();
                    if (clustertable != 0x9C1498)
                        for (int i = 0; i < Defines.SprCount; i++) {
                            Pointer clusterptr = Rom.PointerFromSnes(clustertable + (i * 3));
                            if (!clusterptr.IsEmpty())
                                sr.Append($"autoclean ${clusterptr.Addr():X06}\n");
                        }
                    sr.Append("\n\n; Extended:\n");
                    int extendedtable = Rom.PointerFromSnes(0x029B1F).Addr();
                    if (extendedtable != 0x176FBC)
                        for (int i = 0; i < Defines.SprCount; i++) {
                            Pointer extendedptr = Rom.PointerFromSnes(extendedtable + (i * 3));
                            if (!extendedptr.IsEmpty())
                                sr.Append($"autoclean ${extendedptr.Addr():X06}\n");
                        }
                }
                await File.WriteAllTextAsync(cleanpatch, sr.ToString());
                Patch(cleanpatch);
            } else if (Encoding.UTF8.GetString(Rom.RomData[sprtool..(sprtool + 3)]) == "MDK") {
                Patch(pathname + "spritetool_clean.asm");
                string mdk = "MDK";
                int nob = Rom.RomSize / 0x8000;
                for (int i = 0x10; i < nob; ++i) {
                    byte[] bank = Rom.RomData[(i * 0x8000)..];
                    int bankoff = 8;
                    while (true) {
                        int offset = bankoff;
                        int j = 0;
                        for (; offset < 0x8000; offset++) {
                            if (bank[offset] != mdk[j++])
                                j = 0;
                            if (j == mdk.Length) {
                                offset -= mdk.Length - 1;
                                break;
                            }
                        }
                        if (offset >= 0x8000)
                            break;
                        bankoff = offset + mdk.Length;
                        if (Encoding.UTF8.GetString(bank[(offset - 8)..(offset - 4)]) != "STAR")
                            continue;
                        int size = (bank[offset - 3] << 8) + bank[offset - 4] + 8;
                        int inverted = (bank[offset - 1] << 8) + bank[offset - 4];
                        if ((size - 8 + inverted) == 0x0FFFF)
                            size++;
                        if ((size - 8 + inverted) <= 0x10000) {
                            int pc = i * 0x8000 + offset - 8;
                            Console.WriteLine($"Size: {size - 8:X04}, inverted {inverted:X04}");
                            Console.WriteLine($"Bad SpriteTool RATS tag detected at ${Rom.PcToSnes(pc):X06}, 0x{pc:X05}. Remove anyway? (y/n)");
                            var key = Console.ReadKey();
                            if (key.KeyChar != 'Y' && key.KeyChar != 'y') {
                                continue;
                            }
                        }
                        Array.Fill<byte>(bank, 0, offset - 8, size);
                        Array.Copy(bank, 0, Rom.RomData, i * 0x8000, bank.Length);
                    }
                }
            }
        }
        private void PatchSprites(List<string> extraDefines, Sprite[] sprites, int size) {
            for (int i = 0; i < size; i++) {
                Sprite spr = sprites[i];
                if (spr is null)
                    continue;
                if (spr.AsmFile == string.Empty || spr.AsmFile == null)
                    continue;
                bool duplicate = false;
                for (int j = i - 1; j >= 0; j--) {
                    if (sprites[j].AsmFile is not null && spr.AsmFile != string.Empty) {
                        if (sprites[j].AsmFile == spr.AsmFile) {
                            spr.Table.Init = sprites[j].Table.Init;
                            spr.Table.Main = sprites[j].Table.Main;
                            spr.ExtCapePtr = sprites[j].ExtCapePtr;
                            spr.StatusPointers = sprites[j].StatusPointers;
                            sprites[i] = spr;
                            duplicate = true;
                            break;
                        }
                    }
                }
                if (!duplicate) {
                    PatchSprite(extraDefines, ref spr, Opts.Output);
                }


                if (spr.Level < 0x200 && spr.Number >= 0xB0 && spr.Number < 0xC0) {
                    int plslvaddr = Data.PlsLevelPtrs[spr.Level * 2] + (Data.PlsLevelPtrs[spr.Level * 2 + 1] << 8);
                    if (plslvaddr == 0x0000) {
                        plslvaddr = Data.PlsSpritePtrAddr + 1;
                        Data.PlsLevelPtrs[spr.Level * 2] = (byte)plslvaddr;
                        Data.PlsLevelPtrs[spr.Level * 2 + 1] = (byte)(plslvaddr >> 8);
                        Data.PlsSpritePtrAddr += 0x20;
                    }
                    plslvaddr--;
                    plslvaddr += (spr.Number - 0xB0) * 2;
                    if (Data.PlsDataAddr >= 0x8000) {
                        throw new SpriteFailureException("Too many Per-Level sprites. Please remove some");
                    }
                    Data.PlsSpritePtr[plslvaddr] = (byte)(Data.PlsDataAddr + 1);
                    Data.PlsSpritePtr[plslvaddr + 1] = (byte)((Data.PlsDataAddr + 1) >> 8);
                    List<byte> statptrs = new();
                    foreach (var ptr in spr.StatusPointers.Values) {
                        statptrs.AddRange(spr.StatusPtrsToBytes());
                    }
                    Array.Copy(spr.Table, 0, Data.PlsData, Data.PlsDataAddr, 0x10);
                    Array.Copy(statptrs.ToArray(), 0, Data.PlsPointers, Data.PlsDataAddr, 15);
                    Data.PlsPointers[Data.PlsDataAddr + 0x0F] = 0xFF;
                    Data.PlsDataAddr += 0x10;
                }
            }
        }
        private async Task CreateBinaryTables() {
            string asmpath = Paths.ASMPaths[Defines.FileType.Asm];
            await File.WriteAllBytesAsync(asmpath + "_versionflag.bin", Data.VersionFlag);
            if (Opts.PerLevel) {
                await File.WriteAllBytesAsync(asmpath + "_PerLevelLvlPtrs.bin", Data.PlsLevelPtrs);
                if (Data.PlsDataAddr == 0) {
                    byte[] dummy = new byte[] { 0xFF };
                    await File.WriteAllBytesAsync(asmpath + "_PerLevelSprPtrs.bin", dummy);
                    await File.WriteAllBytesAsync(asmpath + "_PerLevelT.bin", dummy);
                    await File.WriteAllBytesAsync(asmpath + "_PerLevelCustomPtrTable.bin", dummy);
                } else {
                    await File.WriteAllBytesAsync(asmpath + "_PerLevelSprPtrs.bin", Data.PlsSpritePtr[0..Data.PlsSpritePtrAddr]);
                    await File.WriteAllBytesAsync(asmpath + "_PerLevelT.bin", Data.PlsData[0..Data.PlsDataAddr]);
                    await File.WriteAllBytesAsync(asmpath + "_PerLevelCustomPtrTable.bin", Data.PlsPointers[0..Data.PlsDataAddr]);
                }
                Data.SprLists[Defines.ListType.Sprite][0x2000..0x2100].WriteLongTable(asmpath + "_DefaultTables.bin");
            } else {
                Data.SprLists[Defines.ListType.Sprite][..0x100].WriteLongTable(asmpath + "_DefaultTables.bin");
            }
            List<byte> customstatusptrs = new();
            int start = Opts.PerLevel ? 0x2000 : 0;
            int end = start + 0x100;
            byte[] dummyTable = { 0x21, 0x80, 0x01, 0x21, 0x80, 0x01, 0x21, 0x80, 0x01, 0x21, 0x80, 0x01, 0x21, 0x80, 0x01 };
            byte[] dummyPtr = { 0x21, 0x80, 0x01 };
            foreach (var spr in Data.SprLists[Defines.ListType.Sprite][start..end]) {
                if (spr is null)
                    customstatusptrs.AddRange(dummyTable);
                else
                    customstatusptrs.AddRange(spr.StatusPtrsToBytes());
            }
            while (customstatusptrs.Count < 0x100 * 15) {
                for (int i = 0; i < 5; i++) customstatusptrs.AddRange(new Pointer(0x018021).ToBytes());
            }
            await File.WriteAllBytesAsync(asmpath + "_CustomStatusPtr.bin", customstatusptrs.ToArray());

            List<byte> otherptrs = new();
            foreach (var spr in Data.SprLists[Defines.ListType.Cluster]) {
                if (spr is null)
                    otherptrs.AddRange(dummyPtr);
                else
                    otherptrs.AddRange((byte[])spr.Table.Main);
            }
            await File.WriteAllBytesAsync(asmpath + "_ClusterPtr.bin", otherptrs.ToArray());

            otherptrs.Clear();
            foreach (var spr in Data.SprLists[Defines.ListType.Extended]) {
                if (spr is null)
                    otherptrs.AddRange(dummyPtr);
                else
                    otherptrs.AddRange((byte[])spr.Table.Main);
            }
            await File.WriteAllBytesAsync(asmpath + "_ExtendedPtr.bin", otherptrs.ToArray());

            otherptrs.Clear();
            foreach (var spr in Data.SprLists[Defines.ListType.Extended]) {
                if (spr is null)
                    otherptrs.AddRange(dummyPtr);
                else
                    otherptrs.AddRange((byte[])spr.ExtCapePtr);
            }
            await File.WriteAllBytesAsync(asmpath + "_ExtendedCapePtr.bin", otherptrs.ToArray());

            byte[] extrabytes = new byte[0x200];
            for (int i = 0; i < 0x100; i++) {
                int num = VerifySprite(0x200, i, Opts.PerLevel);
                Sprite spr = Data.SprLists[Defines.ListType.Sprite][num];
                if (num == -1 || (Opts.PerLevel && i >= 0xB0 && i < 0xC0)) {
                    extrabytes[i] = 7;
                    extrabytes[i + 0x100] = 7;
                } else {
                    if (spr is not null) {
                        extrabytes[i] = (byte)(3 + spr.ByteCount);
                        extrabytes[i + 0x100] = (byte)(3 + spr.ExtraByteCount);
                    } else {
                        extrabytes[i] = 3;
                        extrabytes[i + 0x100] = 3;
                    }
                }
            }
            await File.WriteAllBytesAsync(asmpath + "_CustomSize.bin", extrabytes);

        }
        private async Task CreateExtFiles() {
            Data.Map.AddRange(new Map16[Defines.Map16Size]);
            Dictionary<Defines.ExtType, FileStream> streams = new() {
                { Defines.ExtType.ExtMW2, Rom.OpenSubfile("mw2") },
                { Defines.ExtType.ExtMWT, Rom.OpenSubfile("mwt") },
                { Defines.ExtType.ExtS16, Rom.OpenSubfile("s16") },
                { Defines.ExtType.ExtSSC, Rom.OpenSubfile("ssc") }
            };
            foreach (var (type, ext) in Paths.Extensions) {
                if (ext != string.Empty) {
                    if (type == Defines.ExtType.ExtS16) {
                        Data.Map.AddRange(Map16.FromBytes(File.ReadAllBytes(ext)));
                    } else if (type == Defines.ExtType.ExtMW2) {
                        await streams[type].WriteAsync((await File.ReadAllBytesAsync(ext)).AsMemory()[0..^1]); // to avoid copying over the 0xFF
                    } else {
                        await streams[type].WriteAsync((await File.ReadAllBytesAsync(ext)));
                    }
                } else if (type == Defines.ExtType.ExtMW2) {
                    streams[type].WriteByte(0);
                }
            }
            for (int i = 0; i < 0x100; i++) {
                int n = VerifySprite(0x200, i, Opts.PerLevel);
                if (!(n == -1 || (Opts.PerLevel && i >= 0xB0 && i < 0xC0))) {
                    Sprite spr = Data.SprLists[Defines.ListType.Sprite][n];
                    if (spr is null)
                        continue;
                    if (spr.Line != -1) {
                        int ntile = Map16.FindFree(Data.Map, spr.MapData.Count);
                        if (ntile == -1) {
                            throw new SpriteFailureException("Too much Map16 data to fit inside your s16 file");
                        }
                        Data.Map.RemoveRange(ntile, spr.MapData.Count);
                        Data.Map.InsertRange(ntile, spr.MapData);           // replace the 00s with our data

                        foreach (var dis in spr.Displays) {
                            StringBuilder ssc = new StringBuilder();
                            int refd = (dis.Y * 0x1000) + (dis.X * 0x100) + 0x20 + (dis.ExtraBit ? 0x10 : 0);
                            if (dis.Description != null) {
                                ssc.Append($"{i:X02} {refd:X04} {dis.Description}\n");
                            } else {
                                ssc.Append($"{i:X02} {refd:X04} {spr.AsmFile}\n");
                            }
                            ssc.Append($"{i:X02} {refd + 2:X04}");
                            foreach (Tile t in dis.Tiles) {
                                if (t.Text is not null) {
                                    ssc.Append($" 0,0,*{t.Text}*");
                                    break;
                                } else {
                                    int tnum = t.TileNumber;
                                    if (tnum >= 0x300) tnum += 0x100 + ntile;
                                    ssc.Append($" {t.XOff},{t.YOff},{tnum:X}");
                                }
                            }
                            ssc.Append('\n');
                            await streams[Defines.ExtType.ExtSSC].WriteAsync(Encoding.UTF8.GetBytes(ssc.ToString()));
                        }
                        foreach (var (coll, index) in spr.Collections.WithIndex()) {
                            Defines.ExtType mw2 = Defines.ExtType.ExtMW2;
                            Defines.ExtType mwt = Defines.ExtType.ExtMWT;
                            byte c = (byte)(0x79 + (coll.ExtraBit ? 0x04 : 0));
                            streams[mw2].WriteByte(c);
                            streams[mw2].WriteByte(0x70);
                            streams[mw2].WriteByte((byte)spr.Number);
                            int bcount = (coll.ExtraBit ? spr.ExtraByteCount : spr.ByteCount);
                            await streams[mw2].WriteAsync(coll.Prop.AsMemory(0, bcount));
                            if (index == 0) {
                                await streams[mwt].WriteAsync(Encoding.UTF8.GetBytes($"{spr.Number:X02}\t{coll.Name}\n"));
                            } else {
                                await streams[mwt].WriteAsync(Encoding.UTF8.GetBytes($"\t{coll.Name}\n"));
                            }
                        }
                    }
                }
            }
            var count = Data.Map.Count * Unsafe.SizeOf<Map16>();
            var buffer = new byte[count];
            CollectionsMarshal.AsSpan(Data.Map).CopyTo(MemoryMarshal.Cast<byte, Map16>(buffer));
            await streams[Defines.ExtType.ExtS16].WriteAsync(buffer);
            streams[Defines.ExtType.ExtMW2].WriteByte(0xFF);
            foreach (var stream in streams.Values) {
                stream.Flush();
                stream.Dispose();
            }
        }
        private void PatchSprite(List<string> extraDefines, ref Sprite spr, TextWriter output) {
            StringBuilder spritePatch = new();
            string escapedAsmDir = Opts.AsmDir.Replace("!", @"\!");
            string escapedDirectory = spr.Directory.Replace("!", @"\!");
            string escapedAsmFile = spr.AsmFile.Replace("!", @"\!");
            spritePatch.Append("namespace nested on\n");
            spritePatch.Append($"incsrc \"{escapedAsmDir}sa1def.asm\"\n");
            extraDefines.ForEach(x => spritePatch.Append($"incsrc \"{x}\"\n"));
            spritePatch.Append("incsrc \"shared.asm\"\n");
            spritePatch.Append($"SPRITE_ENTRY_{spr.Number}:\n");
            spritePatch.Append($"incsrc \"{escapedDirectory}_header.asm\"\n");
            spritePatch.Append("freecode cleaned\n");
            spritePatch.Append($"\tincsrc \"{(Path.IsPathRooted(spr.AsmFile) ? "" : escapedDirectory)}{escapedAsmFile}\"\n");
            spritePatch.Append("namespace nested off\n");
            File.WriteAllText(Defines.TempSprFile, spritePatch.ToString());
            Patch(Defines.TempSprFile);
            spr.StatusPointers = new Dictionary<string, Pointer>{
                {"init", new Pointer(0x018021) },
                {"main", new Pointer(0x018021) },
                {"cape", new Pointer(0x000000) },
                {"mouth", new Pointer(0x000000) },
                {"kicked", new Pointer(0x000000) },
                {"carriable", new Pointer(0x000000) },
                {"carried", new Pointer(0x000000) },
                {"goal", new Pointer(0x000000) },
            };
            var prints = Asar.getprints();
            var low_prints = prints.Select(x => x.Trim().ToLower()).ToArray();
            output?.WriteLine($"__________________________________\n{spr.AsmFile}");
            if (prints.Length > 2)
                output?.WriteLine("Prints:\n");
            foreach (var (print, i) in low_prints.WithIndex()) {
                string k = spr.StatusPointers.Keys.FirstOrDefault(x => print.StartsWith(x));
                if (k == default) {
                    if (print.StartsWith("VERG")) {
                        if (Defines.ToolVersion < Convert.ToInt32(print[4..].Trim(), 16)) {
                            throw new VersionGuardFailed(spr.AsmFile);
                        }
                    } else
                        output?.WriteLine($"{prints[i]}");
                } else {
                    string digits = print[k.Length..].Trim();
                    spr.StatusPointers[k] = new Pointer(Convert.ToInt32(digits, 16));
                }
            }
            spr.StatusPointers.Remove("init", out spr.Table.Init);
            spr.StatusPointers.Remove("main", out spr.Table.Main);
            if (spr.Table.Init.IsEmpty() && spr.Table.Main.IsEmpty()) {
                throw new SpriteFailureException($"Sprite {spr.AsmFile} had neither INIT nor MAIN defined in its file, insertion has been aborted");
            }
            if (spr.SprType == 1) {
                spr.ExtCapePtr = spr.StatusPointers["cape"];
                spr.StatusPointers.Clear();
            } else if (spr.SprType == 0) {
                spr.StatusPointers.Remove("cape");
            } else {
                spr.StatusPointers.Clear();
            }
            output?.WriteLine($"\tINIT: ${spr.Table.Init.Addr():X06}\n\tMAIN: ${spr.Table.Main.Addr():X06}");
            if (spr.SprType == 0) {
                spr.StatusPointers.ToList().ForEach(v => output?.WriteLine($"\t{v.Key.ToUpper()}: ${v.Value.Addr():X06}"));
            } else if (spr.SprType == 1) {
                output?.WriteLine($"\tCAPE: ${spr.ExtCapePtr.Addr():X06}");
            }
        }
        private static void CleanTempFiles(string path, bool perlevel) {
            File.Delete(path + "_versionflag.bin");
            File.Delete(path + "_DefaultTables.bin");
            File.Delete(path + "_CustomStatusPtr.bin");
            File.Delete(path + "_ClusterPtr.bin");
            File.Delete(path + "_ExtendedPtr.bin");
            File.Delete(path + "_ExtendedCapePtr.bin");
            File.Delete(path + "_CustomSize.bin");
            File.Delete(path + "_cleanup.asm");
            File.Delete(Defines.TempSprFile);
            File.Delete("shared.asm");
            if (perlevel) {
                File.Delete(path + "_PerLevelLvlPtrs.bin");
                File.Delete(path + "_PerLevelSprPtrs.bin");
                File.Delete(path + "_PerLevelT.bin");
                File.Delete(path + "_PerLevelCustomPtrTable.bin");
            }
        }
        private static void CreateLMRestore(string filename) {
            string extmod = Path.GetFileNameWithoutExtension(filename) + ".extmod";
            string toappend = $"Pixi v1.{Defines.ToolVersion:X02}\t";
            string contents = "";
            if (File.Exists(extmod))
                contents = File.ReadAllText(extmod);

            if (!contents.EndsWith(toappend)) {
                File.WriteAllText(extmod, contents + toappend);
            }
        }
        private static async Task CreateSharedPatch(string routinepath) {
            StringBuilder sr = new StringBuilder();
            sr.Append("macro include_once(target, base, offset)\n\tif !<base> != 1\n\t\t!<base> = 1\n\t\tpushpc\n\t\tif read3(<offset>+$03E05C) != $FFFFFF\n");
            sr.Append("\t\t\t<base> = read3(<offset>+$03E05C)\n\t\telse\n\t\t\tfreecode cleaned\n\t\t\t\t#<base>:\n\t\t\t\t");
            sr.Append("print \"\tRoutine: <base> inserted at $\",pc\n\t\t\t\tnamespace <base>\n\t\t\t\tincsrc \"<target>\"\n\t\t\t\tnamespace off\n");
            sr.Append("\t\t\tORG <offset>+$03E05C\n\t\t\t\tdl <base>\n\t\tendif\n\t\tpullpc\n\tendif\nendmacro\n");
            if (!Directory.Exists(routinepath)) {
                throw new MissingFileException(routinepath);
            }
            var routines = Directory.GetFiles(routinepath, "*.asm", SearchOption.AllDirectories);
            if (routines.Length > 100) {
                throw new SpriteFailureException("More than 100 routines, please remove some");
            }
            string escapedroutinepath = routinepath.Replace("!", @"\\\!");
            foreach (var (routine, count) in routines.WithIndex()) {
                string name = Path.GetFileNameWithoutExtension(routine);
                sr.Append($"!{name} = 0\nmacro {name}()\n\t%include_once(\"{escapedroutinepath}{name}.asm\", {name}, ${(count * 3):X02})\n\tJSL {name}\nendmacro\n");
            }
            Console.WriteLine($"{routines.Length} Shared routines registered in \"{routinepath}\"");
            await File.WriteAllTextAsync("shared.asm", sr.ToString());
        }
        private static List<string> ListExtraASM(string asmpath) {
            List<string> files = new();
            if (Directory.Exists(asmpath)) {
                foreach (var file in Directory.GetFiles(asmpath, "*.asm", SearchOption.AllDirectories)) {
                    files.Add(file);
                }
            }
            return files;
        }
        private static int VerifySprite(int level, int number, bool perlevel) {
            if (!perlevel)
                return number;

            if (level > 0x200 || number > 0xFF)
                return -1;
            if (level == 0x200)
                return 0x2000 + number;
            else if (number >= 0xB0 && number > 0xc0)
                return (level * 0x10) + (number - 0xB0);
            return -1;
        }
    }
}

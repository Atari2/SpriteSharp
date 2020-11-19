using AsarCLR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpriteToolSuperSharp {
    class MeiMei {
        const int SprAddrLimit = 0x800;
        public bool AlwaysRemap { get; set; } = false;
        public bool Debug { get; set; } = false;
        public bool KeepTemp { get; set; } = false;

        public string Name;
        public MeiMeiRom PrevRom;
        public MeiMeiRom NowRom;
        public byte[] PrevEx = new byte[0x400];
        public byte[] NowEx = new byte[0x400];
        public string Sa1DefPath;

        public bool Patch(string patchname, ref MeiMeiRom rom) {
            if (!Asar.patch(patchname, ref rom.RomData)) {
                Asarerror[] errors = Asar.geterrors();
                Console.Out.WriteLine("An error has been detected: \n");
                for (int i = 0; i < errors.Length; i++) {
                    Console.Out.WriteLine(errors[i].Fullerrdata);
                }
                return false;
            }
            if (Debug) {
                var prints = Asar.getprints();
                foreach (var print in prints) {
                    Console.Out.WriteLine($"\t{print}");
                }
            }
            rom.RomSize = rom.RomData.Length;
            return true;
        }

        public void Initialize(string n) {
            Name = n;
            Array.Fill<byte>(PrevEx, 0x03);
            Array.Fill<byte>(NowEx, 0x03);
            PrevRom = new MeiMeiRom(Name);
            if (ReadByte(PrevRom, 0x07730F) == 0x42) {
                int addr = SNEStoPC((int)ReadLong(PrevRom, 0x07730C), PrevRom.Sa1());
                PrevRom.ReadData(out PrevEx, 0x400, addr);
            }
        }

        public void Run() {
            if (!Asar.init()) {
                Mixins.WaitAndExit("Error: Asar library is missing or couldn't be initialized, please redownload the tool or the dll.");
            }
            NowRom = new MeiMeiRom(Name);           // rom to use for reading values
            MeiMeiRom rom = new MeiMeiRom(Name);    // actual rom to patch
            bool returnvalue = ResizeData(out string ErrMsg, ref rom);
            Asar.close();

            if (!returnvalue) {
                Console.Out.WriteLine(ErrMsg);
                Console.Out.WriteLine($"\n\nError occurred in MeiMei\n{ErrMsg}\nYour rom has reverted before Pixi insert");
                rom.RomData = PrevRom.RomData;
                rom.RomSize = PrevRom.RomSize;
            }
            rom.Close();
        }
        public bool ResizeData(out string ErrMsg, ref MeiMeiRom rom) {
            ErrMsg = "";
            if (ReadByte(PrevRom, 0x07730F) == 0x42) {
                int addr = SNEStoPC((int)ReadLong(NowRom, 0x07730C), NowRom.Sa1());
                NowRom.ReadData(out NowEx, 0x400, addr);
            }
            bool changeEx = false;
            for (int i = 0; i < 0x400; i++) {
                if (PrevEx[i] != NowEx[i]) {
                    changeEx = true;
                    break;
                }
            }
            bool revert = changeEx || AlwaysRemap;
            if (changeEx) {
                Console.Out.WriteLine("\nExtra bytes change detected");
            }
            if (revert) {
                byte[] sprAllData = new byte[SprAddrLimit];
                bool[] remapped = new bool[0x200];
                Array.Fill(remapped, false);
                for (int lv = 0; lv < 0x200; lv++) {
                    if (remapped[lv]) continue;
                    int sprAddrSNES = (ReadByte(NowRom, 0x077100 + lv) << 16) + ReadWord(NowRom, 0x02EC00 + (lv * 2));
                    if ((sprAddrSNES & 0x8000) == 0) {
                        ErrMsg = "Sprite Data has invalid address";
                        return !revert;
                    }
                    int sprAddrPC = SNEStoPC(sprAddrSNES, PrevRom.Sa1());
                    Array.Fill<byte>(sprAllData, 0);
                    sprAllData[0] = ReadByte(NowRom, sprAddrPC);
                    int prevOfs = 1;
                    int nowOfs = 1;
                    bool exlevelflag = (sprAllData[0] & 0x20) != 0;
                    bool changeData = false;
                    while (true) {
                        NowRom.ReadData(out byte[] sprCommonData, 3, sprAddrPC + prevOfs);
                        if (nowOfs >= SprAddrLimit - 3) {
                            ErrMsg = "Sprite data is too large!";
                            return false;
                        }
                        if (sprCommonData[0] == 0xFF) {
                            sprAllData[nowOfs++] = 0xFF;
                            if (!exlevelflag) break;
                            sprAllData[nowOfs++] = sprCommonData[1];
                            if (sprCommonData[1] == 0xFE) break;
                            else {
                                prevOfs += 2;
                                NowRom.ReadData(out sprCommonData, 3, sprAddrPC + prevOfs);
                            }
                        }
                        sprAllData[nowOfs++] = sprCommonData[0];    // YYYYEEsy
                        sprAllData[nowOfs++] = sprCommonData[1];    // XXXXSSSS
                        sprAllData[nowOfs++] = sprCommonData[2];	// NNNNNNNN

                        uint sprNum = ((uint)(sprCommonData[0] & 0x0C) << 6) | (sprCommonData[2]);

                        if (NowEx[sprNum] > PrevEx[sprNum]) {
                            changeData = true;
                            int i;
                            for (i = 3; i < PrevEx[sprNum]; i++) {
                                sprAllData[nowOfs++] = ReadByte(NowRom, sprAddrPC + prevOfs + i);
                                if (nowOfs >= SprAddrLimit) { ErrMsg = "Sprite data is too large!"; return !revert; }
                            }
                            for (; i < NowEx[sprNum]; i++) {
                                sprAllData[nowOfs++] = 0x00;
                                if (nowOfs >= SprAddrLimit) { ErrMsg = "Sprite data is too large!"; return !revert; }
                            }
                        } else if (NowEx[sprNum] < PrevEx[sprNum]) {
                            changeData = true;
                            for (int i = 3; i < NowEx[sprNum]; i++) {
                                sprAllData[nowOfs++] = ReadByte(NowRom, sprAddrPC + prevOfs + i);
                                if (nowOfs >= SprAddrLimit) { ErrMsg = "Sprite data is too large!"; return !revert; }
                            }
                        } else {
                            for (int i = 3; i < NowEx[sprNum]; i++) {
                                sprAllData[nowOfs++] = ReadByte(NowRom, sprAddrPC + prevOfs + i);
                                if (nowOfs >= SprAddrLimit) { ErrMsg = "Sprite data is too large!"; return !revert; }
                            }
                        }
                        prevOfs += PrevEx[sprNum];
                    }
                    if (changeData) {
                        string binFilename = $"_tmp_bin_{lv:X}.bin";
                        List<byte> bindata = new();
                        if (sprAllData != null && sprAllData.Length > 0) {
                            for (int a = 0; a <= nowOfs; a++) {
                                bindata.Add(sprAllData[a]);
                            }
                        }
                        File.WriteAllBytes(binFilename, bindata.ToArray());
                        StringBuilder sr = new StringBuilder();
                        string binaryLabel = $"SpriteData{lv:X}";
                        string levelBankAddress = $"{PCtoSNES(0x077100 + lv, PrevRom.Sa1()):X06}";
                        string levelWordAddress = $"{PCtoSNES(0x02EC00 + lv * 2, PrevRom.Sa1()):X06}";
                        sr.Append($"incsrc \"{Sa1DefPath}\"\n\n");
                        sr.Append($"!oldDataPointer = read2(${levelWordAddress})|(read1(${levelBankAddress})<<16)\n");
                        sr.Append($"!oldDataSize = read2(pctosnes(snestopc(!oldDataPointer)-4))+1\n");
                        sr.Append("autoclean !oldDataPointer\n\n");

                        sr.Append($"org ${levelBankAddress}\n");
                        sr.Append($"\tdb {binaryLabel}>>16\n\n");

                        sr.Append($"org ${levelWordAddress}\n");
                        sr.Append($"\tdw {binaryLabel}\n\n");

                        sr.Append($"freedata cleaned\n{binaryLabel}:\n");
                        sr.Append($"\t!newDataPointer = {binaryLabel}\n\tincbin {binFilename}\n{binaryLabel}_end:\n");

                        sr.Append($"\tprint \"Data pointer $\",hex(!oldDataPointer), \" : $\",hex(!newDataPointer)\n");
                        sr.Append($"\tprint \"Data size    $\",hex(!oldDataSize),\" : $\",hex({binaryLabel}_end-{binaryLabel}-1)\n");
                        File.WriteAllText($"_tmp_{lv:X}.asm", sr.ToString());
                        sr.Clear();
                        if (Debug) {
                            Console.Out.WriteLine($"Fixing sprite data for level {lv:X}");
                        }
                        if (!Patch($"_tmp_{lv:X}.asm", ref rom)) {
                            ErrMsg = "An error occurred when patching sprite data with asar.";
                            return false;
                        }
                        if (Debug) {
                            Console.Out.WriteLine("Done!");
                        }
                        if (!KeepTemp) {
                            File.Delete(binFilename);
                            File.Delete($"_tmp_{lv:X}.asm");
                        }
                        remapped[lv] = true;
                    }
                }
                Console.Out.WriteLine("Sprite data remapped successfully");
                revert = false;
            }
            return !revert;
        }
        public void ConfigureSa1Def(string pathToSa1Def) {
            Sa1DefPath = pathToSa1Def;
        }

        byte ReadByte(MeiMeiRom rom, int addr) {
            rom.ReadData(out byte[] data, 1, addr);
            return data[0];
        }

        ushort ReadWord(MeiMeiRom rom, int addr) {
            rom.ReadData(out byte[] data, 2, addr);
            return (ushort)(data[0] | (data[1] << 8));
        }

        uint ReadLong(MeiMeiRom rom, int addr) {
            rom.ReadData(out byte[] data, 3, addr);
            return (uint)(data[0] | (data[1] << 8) | (data[2] << 16));
        }

        void WriteByte(MeiMeiRom rom, int addr, byte data) {
            rom.WriteData(new byte[] { data }, 1, addr);
        }

        void WriteWord(MeiMeiRom rom, int addr, ushort data) {
            rom.WriteData(new byte[] { (byte)data, (byte)(data >> 8) }, 2, addr);
        }

        void WriteLong(MeiMeiRom rom, int addr, uint data) {
            rom.WriteData(new byte[] { (byte)data, (byte)(data >> 8), (byte)(data >> 16) }, 3, addr);
        }

        int SNEStoPC(int addr, bool isSa1) {
            if (!isSa1) {
                return (addr & 0x7FFF) + ((addr & 0x7F0000) >> 1);
            } else {
                if (addr >= 0xC00000) {
                    return addr - 0x800000;
                } else {
                    int newAddr = addr;
                    if (newAddr >= 0x800000) {
                        newAddr -= 0x400000;
                    }

                    return (newAddr & 0x7FFF) | ((newAddr >> 1) & 0x7F8000);
                }
            }
        }
        int PCtoSNES(int addr, bool isSa1) {
            if (!isSa1) {
                return ((addr & 0x7FFF) + 0x8000) + ((addr & 0x3F8000) << 1);
            } else {
                if (addr >= 0x400000) {
                    return addr + 0x800000;
                } else {
                    int newAddr = (addr & 0x7FFF) | ((addr & ~0x7FFF) << 1) | 0x8000;
                    if (newAddr >= 0x400000) {
                        return newAddr + 0x400000;
                    } else {
                        return newAddr;
                    }
                }
            }
        }

        public class MeiMeiRom : Rom {
            public MeiMeiRom(string filename) : base(filename) {

            }
            public int GetRomData(int addr) {
                if (addr < 0 || addr >= RomSize) return -1;
                return RomData[addr];
            }
            public int WriteData(byte[] data, int size, int addr) {
                if (!CheckAddr(addr, size)) return 0;

                for (int i = 0; i < size;) {
                    RomData[addr++] = data[i++];
                }
                return 1;
            }
            public int ReadData(out byte[] data, int size, int addr) {
                data = new byte[size];
                if (!CheckAddr(addr, size)) return 0;
                for (int i = 0; i < size;) {
                    data[i++] = RomData[addr++];
                }
                return 1;
            }
            private bool CheckAddr(int addr, int size) {
                return (addr >= 0 && addr + size < RomSize);
            }
            public bool Sa1() {
                return GetRomData(0x81D5) == 35;
            }
        }
    }

}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpriteToolSuperSharp {

    class Rom {
        public string Filename { get; set; }
        public int HeaderSize;
        public int RomSize;
        public byte[] RomData;
        public byte[] HeaderData;

        public Rom() { }
        public Rom(string name) {
            Filename = name;
            RomSize = (int)new FileInfo(Filename).Length;
            HeaderSize = RomSize & 0x7FFF;
            RomSize -= HeaderSize;
            RomData = new byte[RomSize];
            HeaderData = new byte[HeaderSize];
            using var sr = new FileStream(Filename, FileMode.Open);
            sr.Read(HeaderData, 0, HeaderSize);
            sr.Read(RomData, 0, RomSize);
        }

        public void Close() {
            byte[] data = new byte[HeaderSize + RomSize];
            Array.Copy(HeaderData, data, HeaderSize);
            Array.Copy(RomData, 0, data, HeaderSize, RomSize);
            File.WriteAllBytes(Filename, data);
        }

        public int PcToSnes(int address) {
            return ((address << 1) & 0x7F0000) | (address & 0x7FFF) | 0x8000;
        }
        public int SnesToPc(int address) {
            return ((address & 0x7F0000) >> 1 | (address & 0x7FFF));
        }
        private static int GetPointer(byte[] data, int address, int size = 3, int bank = 0x00) {
            address = (data[address])
                      | (data[address + 1] << 8)
                      | ((data[address + 2] << 16) * (size - 2));
            return address | (bank << 16);
        }

        public Pointer PointerFromSnes(int address, int size = 3, int bank = 0x00) {
            return new Pointer(GetPointer(RomData, SnesToPc(address), size, bank));
        }

        public Pointer PointerFromPc(int address, int size = 3, int bank = 0x00) {
            return new Pointer(GetPointer(RomData, address, size, bank));
        }

        public bool RunChecks(out string ErrMsg) {
            ErrMsg = "";
            StringBuilder sr = new StringBuilder();
            int lm_edit_ptr = GetPointer(RomData, SnesToPc(0x06F624));
            if (lm_edit_ptr == 0xFFFFFF) {
                sr.Append("You're inserting Pixi without having modified a level in Lunar Magic, this will cause bugs\n");
                sr.Append("Please save a level before applying Pixi");
                Close();
                ErrMsg = sr.ToString();
                return false;
            }
            byte vram_jump = RomData[SnesToPc(0x00F6E4)];
            if (vram_jump != 0x5C) {
                sr.Append("You haven't installed the VRAM optimization patch in Lunar Magic, ");
                sr.Append("this will cause many features of Pixi to work incorrectly, insertion was aborted...");
                Close();
                ErrMsg = sr.ToString();
                return false;
            }
            byte ver = RomData[SnesToPc(0x02FFE2 + 4)];
            if (ver > Defines.ToolVersion && ver != 0xFF) {
                sr.Append($"The ROM has been patched with a newer version of PIXI (1.{ver:X02}) already.\n");
                sr.Append($"This is version 1.{Defines.ToolVersion:X02}\nPlease get a newer version.");
                Close();
                ErrMsg = sr.ToString();
                return false;
            }
            return true;
        }

    }
    class Pointer {
        public byte LowByte { get; private set; } = Defines.RTLLow;
        public byte HighByte { get; private set; } = Defines.RTLHigh;
        public byte BankByte { get; private set; } = Defines.RTLBank;

        public Pointer() { }
        public Pointer(int snes) {
            LowByte = (byte)(snes & 0xFF);
            HighByte = (byte)((snes >> 8) & 0xFF);
            BankByte = (byte)((snes >> 16) & 0xFF);
        }

        public bool IsEmpty() {
            return LowByte == Defines.RTLLow && HighByte == Defines.RTLHigh && BankByte == Defines.RTLBank;
        }

        public int Addr() {
            return (BankByte << 16) + (HighByte << 8) + LowByte;
        }

        public void SetPointer(int address) {
            LowByte = (byte)(address & 0xFF);
            HighByte = (byte)((address >> 8) & 0xFF);
            BankByte = (byte)((address >> 16) & 0xFF);
        }

        public static implicit operator byte[](Pointer ptr) {
            byte[] ret = new byte[3];
            ret[0] = ptr.LowByte;
            ret[1] = ptr.HighByte;
            ret[2] = ptr.BankByte;
            return ret;
        }

        public byte[] ToBytes() {
            byte[] ret = this;
            return ret;
        }
    }

    public class Tile {
        [JsonProperty("X offset")]
        public int XOff = 0;
        [JsonProperty("Y offset")]
        public int YOff = 0;
        [JsonProperty("map16 tile")]
        public int TileNumber = 0;
        [JsonProperty("Text")]
        public string Text = string.Empty;
    }

    public class Display {
        [JsonProperty]
        public string Description = string.Empty;
        [JsonProperty]
        public List<Tile> Tiles = new();
        [JsonProperty]
        public bool ExtraBit = false;
        [JsonProperty]
        public int X = 0;
        [JsonProperty]
        public int Y = 0;
        [JsonProperty]
        public string DisplayText = string.Empty;
        [JsonProperty]
        public bool UseText = false;
    }

    public class Collection {
        public string Name = string.Empty;
        public bool ExtraBit = false;
        [JsonIgnore]
        public byte[] Prop = new byte[12];

        [JsonProperty("Extra Property Byte 1")]
        byte prop1 { get => Prop[0]; set => Prop[0] = value; }
        [JsonProperty("Extra Property Byte 2")]
        byte prop2 { get => Prop[1]; set => Prop[1] = value; }
        [JsonProperty("Extra Property Byte 3")]
        byte prop3 { get => Prop[2]; set => Prop[2] = value; }
        [JsonProperty("Extra Property Byte 4")]
        byte prop4 { get => Prop[3]; set => Prop[3] = value; }
        [JsonProperty("Extra Property Byte 5")]
        byte prop5 { get => Prop[4]; set => Prop[4] = value; }
        [JsonProperty("Extra Property Byte 6")]
        byte prop6 { get => Prop[5]; set => Prop[5] = value; }
        [JsonProperty("Extra Property Byte 7")]
        byte prop7 { get => Prop[6]; set => Prop[6] = value; }
        [JsonProperty("Extra Property Byte 8")]
        byte prop8 { get => Prop[7]; set => Prop[7] = value; }
        [JsonProperty("Extra Property Byte 9")]
        byte prop9 { get => Prop[8]; set => Prop[8] = value; }
        [JsonProperty("Extra Property Byte 10")]
        byte prop10 { get => Prop[9]; set => Prop[9] = value; }
        [JsonProperty("Extra Property Byte 11")]
        byte prop11 { get => Prop[10]; set => Prop[10] = value; }
        [JsonProperty("Extra Property Byte 12")]
        byte prop12 { get => Prop[11]; set => Prop[11] = value; }

    }

    class Map8x8 {
        public byte Tile = 0;
        public byte Prop = 0;

        public Map8x8() { }
        public Map8x8(byte tile, byte prop) {
            Tile = tile;
            Prop = prop;
        }
        public bool IsEmpty() {
            return Tile == 0 && Prop == 0;
        }
    }

    class Map16 {
        public Map8x8 TopLeft = new();
        public Map8x8 BotLeft = new();
        public Map8x8 TopRight = new();
        public Map8x8 BotRight = new();

        public Map16() { }
        public Map16(byte[] values) {
            TopLeft = new Map8x8(values[0], values[1]);
            BotLeft = new Map8x8(values[2], values[3]);
            TopRight = new Map8x8(values[4], values[5]);
            BotRight = new Map8x8(values[6], values[7]);
        }
        public static List<Map16> FromBytes(byte[] data) {
            List<Map16> maps = new List<Map16>(data.Length / 8);
            for (int i = 0; i < data.Length; i += 8) {
                maps.Add(new Map16(data[i..(i + 8)]));
            }
            return maps;
        }

        public byte[] ToBytes() {
            byte[] bytes = {
                TopLeft.Tile,
                TopLeft.Prop,
                BotLeft.Tile,
                BotLeft.Prop,
                TopRight.Tile,
                TopRight.Prop,
                BotRight.Tile,
                BotRight.Prop
            };
            return bytes;
        }

        private bool IsEmpty() {
            return TopLeft.IsEmpty() && BotLeft.IsEmpty() && TopRight.IsEmpty() && BotRight.IsEmpty();
        }

        public static int FindFree(List<Map16> maps, int count) {
            int i = 0;
            if (count == 0)
                return 0;
            foreach (var map in maps) {
                if (map.IsEmpty())
                    return i;
                i++;
            }
            return -1;
        }

        public static List<Map16> Read(string filename) {
            List<Map16> maps = new();
            byte[] raw = new byte[Defines.Map16Size * 8];
            File.ReadAllBytes(filename).CopyTo(raw, 0);
            for (int i = 0; i < raw.Length; i += 8) {
                Map16 m = new();
                m.TopLeft.Tile = raw[i];
                m.TopLeft.Prop = raw[i + 1];
                m.BotLeft.Tile = raw[i + 2];
                m.BotLeft.Prop = raw[i + 3];
                m.TopRight.Tile = raw[i + 4];
                m.TopRight.Prop = raw[i + 5];
                m.BotRight.Tile = raw[i + 6];
                m.BotRight.Prop = raw[i + 7];
                maps.Add(m);
            }
            return maps;
        }

    }

    class SpriteTable {
        public byte Type = 0;
        public byte ActLike = 0;
        public byte[] Tweak = new byte[6];
        public Pointer Init = new();
        public Pointer Main = new();
        public byte[] Extra = new byte[2];

        public static bool IsEmpty(List<Sprite> sprites) {
            return sprites.All(x => x.Table.Init.IsEmpty() && x.Table.Main.IsEmpty());
        }
        public static implicit operator byte[](SpriteTable sp) {
            byte[] tab = new byte[0x10];
            tab[0] = sp.Type;
            tab[1] = sp.ActLike;
            Array.Copy(sp.Tweak, 0, tab, 2, 6);
            tab[8] = sp.Init.LowByte;
            tab[9] = sp.Init.HighByte;
            tab[10] = sp.Init.BankByte;
            tab[11] = sp.Main.LowByte;
            tab[12] = sp.Main.HighByte;
            tab[13] = sp.Main.BankByte;
            tab[14] = sp.Extra[0];
            tab[15] = sp.Extra[1];
            return tab;
        }
        public byte[] ToBytes() {
            byte[] ret = this;
            return ret;
        }
    }

    public class JsonSprite {
        public class J1656 {
            [JsonProperty("Object Clipping")]
            public byte objclip;
            [JsonProperty("Can be jumped on")]
            public bool canbejumped;
            [JsonProperty("Dies when jumped on")]
            public bool diesjumped;
            [JsonProperty("Hop in/kick shell")]
            public bool hopin;
            [JsonProperty("Disappears in cloud of smoke")]
            public bool disapp;

            public byte ToByte() {
                byte tweak = 0;
                tweak |= (byte)((objclip & 0x0F) << 0);
                tweak |= canbejumped ? 0x10 : 0;
                tweak |= diesjumped ? 0x20 : 0;
                tweak |= hopin ? 0x40 : 0;
                tweak |= disapp ? 0x80 : 0;
                return tweak;

            }
        }

        public class J1662 {
            [JsonProperty("Sprite Clipping")]
            public byte sprclip;
            [JsonProperty("USe shell as death frame")]
            public bool deathframe;
            [JsonProperty("Fall straight down when killed")]
            public bool strdown;

            public byte ToByte() {
                byte tweak = 0;
                tweak |= (byte)((sprclip & 0x3F) << 0);
                tweak |= deathframe ? 0x40 : 0;
                tweak |= strdown ? 0x80 : 0;
                return tweak;
            }
        }

        public class J166e {
            [JsonProperty("Use second graphics page")]
            public bool secondpage;
            [JsonProperty("Palette")]
            public byte palette;
            [JsonProperty("Disable fireball killing")]
            public bool fireball;
            [JsonProperty("Disable cape killing")]
            public bool cape;
            [JsonProperty("Disable water splash")]
            public bool splash;
            [JsonProperty("Don't interact with Layer 2")]
            public bool lay2;

            public byte ToByte() {
                byte tweak = 0;
                tweak |= secondpage ? 0x01 : 0;
                tweak |= (byte)((palette & 0x07) << 1);
                tweak |= fireball ? 0x10 : 0;
                tweak |= cape ? 0x20 : 0;
                tweak |= splash ? 0x40 : 0;
                tweak |= lay2 ? 0x80 : 0;
                return tweak;
            }
        }

        public class J167a {
            [JsonProperty("Don't disable cliping when starkilled")]
            public bool star;
            [JsonProperty("Invincible to star/cape/fire/bounce blk.")]
            public bool blk;
            [JsonProperty("Process when off screen")]
            public bool offscr;
            [JsonProperty("Don't change into shell when stunned")]
            public bool stunn;
            [JsonProperty("Can't be kicked like shell")]
            public bool kick;
            [JsonProperty("Process interaction with Mario every frame")]
            public bool everyframe;
            [JsonProperty("Gives power-up when eaten by yoshi")]
            public bool powerup;
            [JsonProperty("Don't use default interaction with Mario")]
            public bool defaultint;

            public byte ToByte() {
                byte tweak = 0;
                tweak |= star ? 0x01 : 0;
                tweak |= blk ? 0x02 : 0;
                tweak |= offscr ? 0x04 : 0;
                tweak |= stunn ? 0x08 : 0;
                tweak |= kick ? 0x10 : 0;
                tweak |= everyframe ? 0x20 : 0;
                tweak |= powerup ? 0x40 : 0;
                tweak |= defaultint ? 0x80 : 0;
                return tweak;
            }
        }

        public class J1686 {
            [JsonProperty("Inedible")]
            public bool inedible;
            [JsonProperty("Stay in Yoshi's mouth")]
            public bool mouth;
            [JsonProperty("Weird ground behaviour")]
            public bool ground;
            [JsonProperty("Don't interact with other sprites")]
            public bool nosprint;
            [JsonProperty("Don't change direction if touched")]
            public bool direc;
            [JsonProperty("Don't turn into coin when goal passed")]
            public bool goalpass;
            [JsonProperty("Spawn a new sprite")]
            public bool newspr;
            [JsonProperty("Don't interact with objects")]
            public bool noobjint;

            public byte ToByte() {
                byte tweak = 0;
                tweak |= inedible ? 0x01 : 0;
                tweak |= mouth ? 0x02 : 0;
                tweak |= ground ? 0x04 : 0;
                tweak |= nosprint ? 0x08 : 0;
                tweak |= direc ? 0x10 : 0;
                tweak |= goalpass ? 0x20 : 0;
                tweak |= newspr ? 0x40 : 0;
                tweak |= noobjint ? 0x80 : 0;
                return tweak;
            }
        }

        public class J190F {
            [JsonProperty("Make platform passable from below")]
            public bool below;
            [JsonProperty("Don't erase when goal passed")]
            public bool goal;
            [JsonProperty("Can't be killed by sliding")]
            public bool slidekill;
            [JsonProperty("Takes 5 fireballs to kill")]
            public bool fivefire;
            [JsonProperty("Can be jumped on with upwards Y speed")]
            public bool yupsp;
            [JsonProperty("Death frame two tiles high")]
            public bool deathframe;
            [JsonProperty("Don't turn into a coin with silver POW")]
            public bool nosilver;
            [JsonProperty("Don't get stuck in walls (carryable sprites)")]
            public bool nostuck;

            public byte ToByte() {
                byte tweak = 0;
                tweak |= below ? 0x01 : 0;
                tweak |= goal ? 0x02 : 0;
                tweak |= slidekill ? 0x04 : 0;
                tweak |= fivefire ? 0x08 : 0;
                tweak |= yupsp ? 0x10 : 0;
                tweak |= deathframe ? 0x20 : 0;
                tweak |= nosilver ? 0x40 : 0;
                tweak |= nostuck ? 0x80 : 0;
                return tweak;
            }
        }

        [JsonProperty("$1656")]
        public J1656 t1656;

        [JsonProperty("$1662")]
        public J1662 t1662;

        [JsonProperty("$166E")]
        public J166e t166e;

        [JsonProperty("$167A")]
        public J167a t167a;

        [JsonProperty("$1686")]
        public J1686 t1686;

        [JsonProperty("$190F")]
        public J190F t190f;

        [JsonProperty("AsmFile")]
        public string asmfile;

        [JsonProperty("ActLike")]
        public byte actlike;

        [JsonProperty("Type")]
        public byte type;

        [JsonProperty("Extra Property Byte 1")]
        public byte extraprop1;

        [JsonProperty("Extra Property Byte 2")]
        public byte extraprop2;

        [JsonProperty("Additional Byte Count (extra bit clear)")]
        public int addbcountclear;

        [JsonProperty("Additional Byte Count (extra bit set)")]
        public int addbcountset;

        [JsonProperty("Map16")]
        public string map16;

        [JsonProperty("Displays")]
        public List<Display> displays;

        [JsonProperty("Collection")]
        public List<Collection> collections;

    }

    class Sprite {
        public int Line = -1;
        public int Number = 0;
        public int Level = 0x200;
        public SpriteTable Table = new();
        public Dictionary<string, Pointer> StatusPointers = new();
        public Pointer ExtCapePtr = new();
        public int ByteCount = 0;
        public int ExtraByteCount = 0;

        public string Directory = null;
        public string AsmFile = null;
        public string CfgFile = null;

        public List<Map16> MapData = new();
        public List<Display> Displays = new();
        public List<Collection> Collections = new();

        public byte[] StatusPtrsToBytes() {
            List<byte> bytes = new();
            StatusPointers.Values.ToList().ForEach(x => bytes.AddRange(x.ToBytes()));
            return bytes.ToArray();
        }

        public int SprType = 0;
        // TODO
        public void PrintSprite(TextWriter stream) {

            if (stream == null)
                return;

            stream.WriteLine($"Type:        {Table.Type:X02}");
            stream.WriteLine($"ActLike:     {Table.ActLike:X02}");
            stream.WriteLine($"Tweak:       {Table.Tweak[0]:X02}, {Table.Tweak[1]:X02}, {Table.Tweak[2]:X02}, {Table.Tweak[3]:X02}, " +
                                            $"{Table.Tweak[4]:X02}, {Table.Tweak[5]:X02}");
            if (Table.Type != 0) {
                stream.WriteLine($"Extra:       {Table.Extra[0]:X02}, {Table.Extra[1]:X02}");
                stream.WriteLine($"ASM File:    {AsmFile}");
                stream.WriteLine($"Byte Count:       {ByteCount}, {ExtraByteCount}");
            }

            if (MapData.Count > 0) {
                stream.WriteLine("Map16:");
                foreach (var map in MapData) {
                    stream.WriteLine($"{map.TopLeft.Tile:X02}, {map.TopLeft.Prop:X02}, " +
                        $"{map.BotLeft.Tile:X02}, {map.BotLeft.Prop:X02}, " +
                        $"{map.TopRight.Tile:X02}, {map.TopRight.Prop:X02}, " +
                        $"{map.BotRight.Tile:X02}, {map.BotRight.Prop:X02}");
                }
            }

            if (Displays.Count > 0) {
                stream.WriteLine("Displays:");
                foreach (var dis in Displays) {
                    stream.WriteLine($"\tX: {dis.X}, Y: {dis.Y}, Extra-Bit: {dis.ExtraBit}");
                    stream.WriteLine($"\tDescription: {dis.Description}");
                    foreach (var til in dis.Tiles) {
                        if (til.Text != string.Empty)
                            stream.WriteLine($"\t\t{til.XOff},{til.YOff},*{til.Text}*");
                        else
                            stream.WriteLine($"\t\t{til.XOff},{til.YOff},{til.TileNumber:X}");
                    }
                }
                stream.WriteLine("Collections:");
                foreach (var coll in Collections) {
                    stream.Write($"\tExtra-Bit: {coll.ExtraBit}, Property Bytes: ( ");
                    for (int i = 0; i < (coll.ExtraBit ? ExtraByteCount : ByteCount); i++) {
                        stream.Write($"{coll.Prop[i]:X02} ");
                    }
                    stream.Write($") Name: {coll.Name}");
                }
            }
            stream.Flush();
        }

        public void ReadCfg(TextWriter stream) {
            if (!File.Exists(CfgFile)) {
                throw new Exception($"File {CfgFile} not found");
            }
            List<string> cfg = File.ReadAllText(CfgFile).Split('\n').ToList();
            cfg.RemoveAll(x => string.IsNullOrWhiteSpace(x));
            byte[] tweaks = cfg[2].Split('\t', ' ').Select(x => Convert.ToByte(x.Trim(), 16)).ToArray();
            byte[] prop = cfg[3].Split(' ', '\t').Select(x => Convert.ToByte(x.Trim(), 16)).ToArray();
            Regex exRe = new Regex(@"([\da-fA-F]{1,2}):([\da-fA-F]{1,2})", RegexOptions.Compiled);
            Match exM = exRe.Match(cfg.Count > 5 ? cfg[5] : "");
            if ((cfg.Count != 5 && cfg.Count != 6) || tweaks.Length != 6 || prop.Length != 2)
                throw new Exception($"CFG file had wrong format {CfgFile}");
            for (int i = 0; i < cfg.Count; i++) {
                switch (i) {
                    case 0:
                        Table.Type = Convert.ToByte(cfg[0].Trim(), 16);
                        break;
                    case 1:
                        Table.ActLike = Convert.ToByte(cfg[1].Trim(), 16);
                        break;
                    case 2:
                        Table.Tweak = tweaks;
                        break;
                    case 3:
                        Table.Extra = prop;
                        break;
                    case 4:
                        AsmFile = Mixins.AppendToDir(CfgFile, cfg[i]);
                        break;
                    case 5:
                        if (!exM.Success) {
                            ByteCount = 0;
                            ExtraByteCount = 0;
                        } else {
                            ByteCount = Convert.ToByte(exM.Groups[1].Value, 16);
                            ExtraByteCount = Convert.ToByte(exM.Groups[2].Value, 16);
                            if (ByteCount > 12) ByteCount = 12;
                            if (ExtraByteCount > 12) ExtraByteCount = 12;
                        }
                        break;
                    default:
                        throw new Exception($"CFG file had wrong format {CfgFile}");
                }
            }
            if (stream is not null) {
                stream.WriteLine($"Parsed: {CfgFile}, {Line - 1} lines");
                stream.Flush();
            }
        }

        public void ReadJson(TextWriter stream) {
            try {
                JsonSprite root = JsonConvert.DeserializeObject<JsonSprite>(File.ReadAllText(CfgFile));
                AsmFile = Mixins.AppendToDir(CfgFile, root.asmfile);
                Table.ActLike = root.actlike;
                Table.Type = root.type;
                if (Table.Type != 0) {
                    // do the thing with the asm file
                    Table.Extra[0] = root.extraprop1;
                    Table.Extra[1] = root.extraprop2;
                    ByteCount = root.addbcountclear;
                    ExtraByteCount = root.addbcountset;
                    if (ByteCount > 12)
                        ByteCount = 12;
                    if (ExtraByteCount > 12)
                        ExtraByteCount = 12;

                    // do the things with the tweaks here
                    Table.Tweak[0] = root.t1656.ToByte();
                    Table.Tweak[1] = root.t1662.ToByte();
                    Table.Tweak[2] = root.t166e.ToByte();
                    Table.Tweak[3] = root.t167a.ToByte();
                    Table.Tweak[4] = root.t1686.ToByte();
                    Table.Tweak[5] = root.t190f.ToByte();


                    MapData = Map16.FromBytes(Convert.FromBase64String(root.map16));
                    Displays = root.displays;
                    Collections = root.collections;
                    if (stream is not null) {
                        stream.WriteLine($"Parsed {CfgFile}");
                        stream.Flush();
                    }

                }
            } catch (Exception e) {
                Console.Out.WriteLine(e.Message);
            }
        }
    }
    class ToolPaths {
        readonly public Dictionary<Defines.FileType, string> ASMPaths = new() {
            { Defines.FileType.List, "list.txt" },
            { Defines.FileType.Sprites, "sprites/" },
            { Defines.FileType.Shooters, "shooters/" },
            { Defines.FileType.Generators, "generators/" },
            { Defines.FileType.Routines, "routines/" },
            { Defines.FileType.Asm, "asm/" },
            { Defines.FileType.Extended, "extended/" },
            { Defines.FileType.Cluster, "cluster/" },
            { Defines.FileType.Overworld, "overworld/" }
        };
        readonly public Dictionary<Defines.ExtType, string> Extensions = new() {
            { Defines.ExtType.ExtSSC, string.Empty },
            { Defines.ExtType.ExtMWT, string.Empty },
            { Defines.ExtType.ExtMW2, string.Empty },
            { Defines.ExtType.ExtS16, string.Empty }
        };

        public ToolPaths() { }

        public ToolPaths(Dictionary<Defines.FileType, string> paths, Dictionary<Defines.ExtType, string> extensions) {
            foreach ((var type, var value) in paths) {
                ASMPaths[type] = value;
            }
            foreach ((var type, var value) in extensions) {
                Extensions[type] = value;
            }
        }

    }
    class MeiMeiInstance {
        public MeiMei Instance = null;
        public MeiMeiInstance(MeiMei instance) {
            Instance = instance;
        }
        public void SetOptions(bool alwaysremap = false, bool debug = false, bool keeptemp = false) {
            Instance.AlwaysRemap = alwaysremap;
            Instance.Debug = debug;
            Instance.KeepTemp = keeptemp;
        }
    }
}

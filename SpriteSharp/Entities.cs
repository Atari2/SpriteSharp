using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace SpriteSharp {

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
            using var sr = new FileStream(Filename, FileMode.Truncate);
            sr.Write(HeaderData);
            sr.Write(RomData);
        }

        public static int PcToSnes(int address) {
            return ((address << 1) & 0x7F0000) | (address & 0x7FFF) | 0x8000;
        }
        public static int SnesToPc(int address) {
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
    struct Pointer {
        public byte LowByte { get; private set; }
        public byte HighByte { get; private set; }
        public byte BankByte { get; private set; }
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

    public struct Tile {
        [JsonInclude, JsonPropertyName("X offset")]
        public int XOff;
        [JsonInclude, JsonPropertyName("Y offset")]
        public int YOff;
        [JsonInclude, JsonPropertyName("map16 tile")]
        public int TileNumber;
        [JsonInclude, JsonPropertyName("Text")]
        public string Text;
    }

    public struct Display {
        [JsonInclude]
        public string Description;
        [JsonInclude]
        public List<Tile> Tiles;
        [JsonInclude]
        public bool ExtraBit;
        [JsonInclude]
        public int X;
        [JsonInclude]
        public int Y;
        [JsonInclude]
        public string DisplayText;
        [JsonInclude]
        public bool UseText;
    }

    public class Collection {
        [JsonInclude]
        public string Name = string.Empty;
        [JsonInclude]
        public bool ExtraBit = false;
        [JsonIgnore]
        public byte[] Prop = new byte[12];

        [JsonInclude, JsonPropertyName("Extra Property Byte 1")]
        public byte Prop1 { get => Prop[0]; set => Prop[0] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 2")]
        public byte Prop2 { get => Prop[1]; set => Prop[1] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 3")]
        public byte Prop3 { get => Prop[2]; set => Prop[2] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 4")]
        public byte Prop4 { get => Prop[3]; set => Prop[3] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 5")]
        public byte Prop5 { get => Prop[4]; set => Prop[4] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 6")]
        public byte Prop6 { get => Prop[5]; set => Prop[5] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 7")]
        public byte Prop7 { get => Prop[6]; set => Prop[6] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 8")]
        public byte Prop8 { get => Prop[7]; set => Prop[7] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 9")]
        public byte Prop9 { get => Prop[8]; set => Prop[8] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 10")]
        public byte Prop10 { get => Prop[9]; set => Prop[9] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 11")]
        public byte Prop11 { get => Prop[10]; set => Prop[10] = value; }
        [JsonInclude, JsonPropertyName("Extra Property Byte 12")]
        public byte Prop12 { get => Prop[11]; set => Prop[11] = value; }

    }

    struct Map8x8 {
        public byte Tile;
        public byte Prop;
        public Map8x8(byte tile, byte prop) {
            Tile = tile;
            Prop = prop;
        }
        public bool IsEmpty() {
            return Tile == 0 && Prop == 0;
        }
    }

    struct Map16 {
        public Map8x8 TopLeft;
        public Map8x8 BotLeft;
        public Map8x8 TopRight;
        public Map8x8 BotRight;
        public Map16(ReadOnlySpan<byte> values) {
            this = MemoryMarshal.Read<Map16>(values);
        }
        public static List<Map16> FromBytes(ReadOnlySpan<byte> data) {
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
        public Pointer Init = new(0x018021);
        public Pointer Main = new(0x018021);
        public byte[] Extra = new byte[2];

        public static bool IsEmpty(IEnumerable<Sprite> sprites) {
            return sprites.Where(x => x is not null).All(x => x.Table.Init.IsEmpty() && x.Table.Main.IsEmpty());
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
            [JsonPropertyName("Object Clipping")]
            public byte objclip;
            [JsonPropertyName("Can be jumped on")]
            public bool canbejumped;
            [JsonPropertyName("Dies when jumped on")]
            public bool diesjumped;
            [JsonPropertyName("Hop in/kick shell")]
            public bool hopin;
            [JsonPropertyName("Disappears in cloud of smoke")]
            public bool disapp;

            public static implicit operator byte(J1656 spr) {
                byte tweak = 0;
                tweak |= (byte)((spr.objclip & 0x0F) << 0);
                tweak |= spr.canbejumped ? 0x10 : 0;
                tweak |= spr.diesjumped ? 0x20 : 0;
                tweak |= spr.hopin ? 0x40 : 0;
                tweak |= spr.disapp ? 0x80 : 0;
                return tweak;
            }
        }

        public class J1662 {
            [JsonPropertyName("Sprite Clipping")]
            public byte sprclip;
            [JsonPropertyName("USe shell as death frame")]
            public bool deathframe;
            [JsonPropertyName("Fall straight down when killed")]
            public bool strdown;

            public static implicit operator byte(J1662 spr) {
                byte tweak = 0;
                tweak |= (byte)((spr.sprclip & 0x3F) << 0);
                tweak |= spr.deathframe ? 0x40 : 0;
                tweak |= spr.strdown ? 0x80 : 0;
                return tweak;
            }
        }

        public class J166e {
            [JsonPropertyName("Use second graphics page")]
            public bool secondpage;
            [JsonPropertyName("Palette")]
            public byte palette;
            [JsonPropertyName("Disable fireball killing")]
            public bool fireball;
            [JsonPropertyName("Disable cape killing")]
            public bool cape;
            [JsonPropertyName("Disable water splash")]
            public bool splash;
            [JsonPropertyName("Don't interact with Layer 2")]
            public bool lay2;

            public static implicit operator byte(J166e spr) {
                byte tweak = 0;
                tweak |= spr.secondpage ? 0x01 : 0;
                tweak |= (byte)((spr.palette & 0x07) << 1);
                tweak |= spr.fireball ? 0x10 : 0;
                tweak |= spr.cape ? 0x20 : 0;
                tweak |= spr.splash ? 0x40 : 0;
                tweak |= spr.lay2 ? 0x80 : 0;
                return tweak;
            }
        }

        public class J167a {
            [JsonPropertyName("Don't disable cliping when starkilled")]
            public bool star;
            [JsonPropertyName("Invincible to star/cape/fire/bounce blk.")]
            public bool blk;
            [JsonPropertyName("Process when off screen")]
            public bool offscr;
            [JsonPropertyName("Don't change into shell when stunned")]
            public bool stunn;
            [JsonPropertyName("Can't be kicked like shell")]
            public bool kick;
            [JsonPropertyName("Process interaction with Mario every frame")]
            public bool everyframe;
            [JsonPropertyName("Gives power-up when eaten by yoshi")]
            public bool powerup;
            [JsonPropertyName("Don't use default interaction with Mario")]
            public bool defaultint;

            public static implicit operator byte(J167a spr) {
                byte tweak = 0;
                tweak |= spr.star ? 0x01 : 0;
                tweak |= spr.blk ? 0x02 : 0;
                tweak |= spr.offscr ? 0x04 : 0;
                tweak |= spr.stunn ? 0x08 : 0;
                tweak |= spr.kick ? 0x10 : 0;
                tweak |= spr.everyframe ? 0x20 : 0;
                tweak |= spr.powerup ? 0x40 : 0;
                tweak |= spr.defaultint ? 0x80 : 0;
                return tweak;
            }
        }

        public class J1686 {
            [JsonPropertyName("Inedible")]
            public bool inedible;
            [JsonPropertyName("Stay in Yoshi's mouth")]
            public bool mouth;
            [JsonPropertyName("Weird ground behaviour")]
            public bool ground;
            [JsonPropertyName("Don't interact with other sprites")]
            public bool nosprint;
            [JsonPropertyName("Don't change direction if touched")]
            public bool direc;
            [JsonPropertyName("Don't turn into coin when goal passed")]
            public bool goalpass;
            [JsonPropertyName("Spawn a new sprite")]
            public bool newspr;
            [JsonPropertyName("Don't interact with objects")]
            public bool noobjint;

            public static implicit operator byte(J1686 spr) {
                byte tweak = 0;
                tweak |= spr.inedible ? 0x01 : 0;
                tweak |= spr.mouth ? 0x02 : 0;
                tweak |= spr.ground ? 0x04 : 0;
                tweak |= spr.nosprint ? 0x08 : 0;
                tweak |= spr.direc ? 0x10 : 0;
                tweak |= spr.goalpass ? 0x20 : 0;
                tweak |= spr.newspr ? 0x40 : 0;
                tweak |= spr.noobjint ? 0x80 : 0;
                return tweak;
            }
        }

        public class J190F {
            [JsonPropertyName("Make platform passable from below")]
            public bool below;
            [JsonPropertyName("Don't erase when goal passed")]
            public bool goal;
            [JsonPropertyName("Can't be killed by sliding")]
            public bool slidekill;
            [JsonPropertyName("Takes 5 fireballs to kill")]
            public bool fivefire;
            [JsonPropertyName("Can be jumped on with upwards Y speed")]
            public bool yupsp;
            [JsonPropertyName("Death frame two tiles high")]
            public bool deathframe;
            [JsonPropertyName("Don't turn into a coin with silver POW")]
            public bool nosilver;
            [JsonPropertyName("Don't get stuck in walls (carryable sprites)")]
            public bool nostuck;

            public static implicit operator byte(J190F spr) {
                byte tweak = 0;
                tweak |= spr.below ? 0x01 : 0;
                tweak |= spr.goal ? 0x02 : 0;
                tweak |= spr.slidekill ? 0x04 : 0;
                tweak |= spr.fivefire ? 0x08 : 0;
                tweak |= spr.yupsp ? 0x10 : 0;
                tweak |= spr.deathframe ? 0x20 : 0;
                tweak |= spr.nosilver ? 0x40 : 0;
                tweak |= spr.nostuck ? 0x80 : 0;
                return tweak;
            }
        }

        [JsonInclude, JsonPropertyName("$1656")]
        public J1656 t1656;

        [JsonInclude, JsonPropertyName("$1662")]
        public J1662 t1662;

        [JsonInclude, JsonPropertyName("$166E")]
        public J166e t166e;

        [JsonInclude, JsonPropertyName("$167A")]
        public J167a t167a;

        [JsonInclude, JsonPropertyName("$1686")]
        public J1686 t1686;

        [JsonPropertyName("$190F")]
        public J190F t190f;

        [JsonInclude, JsonPropertyName("AsmFile")]
        public string asmfile;

        [JsonInclude, JsonPropertyName("ActLike")]
        public byte actlike;

        [JsonInclude, JsonPropertyName("Type")]
        public byte type;

        [JsonInclude, JsonPropertyName("Extra Property Byte 1")]
        public byte extraProp1;

        [JsonInclude, JsonPropertyName("Extra Property Byte 2")]
        public byte extraProp2;

        [JsonInclude, JsonPropertyName("Additional Byte Count (extra bit clear)")]
        public int addbcountclear;

        [JsonInclude, JsonPropertyName("Additional Byte Count (extra bit set)")]
        public int addbcountset;

        [JsonInclude, JsonPropertyName("Map16")]
        public string map16;

        [JsonInclude, JsonPropertyName("Displays")]
        public List<Display> displays;

        [JsonInclude, JsonPropertyName("Collection")]
        public List<Collection> collections;

    }

    class Sprite {
        public int Line = -1;
        public int Number = 0;
        public int Level = 0x200;
        public SpriteTable Table = new();
        public Dictionary<string, Pointer> StatusPointers = new();
        public Pointer ExtCapePtr = new(0x018021);
        public int ByteCount = 0;
        public int ExtraByteCount = 0;

        public string Directory = null;
        public string AsmFile = null;
        public string CfgFile = null;

        public List<Map16> MapData = new();
        public List<Display> Displays = new();
        public List<Collection> Collections = new();

        public Sprite() { }

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
            try {
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
                            AsmFile = CfgFile.AppendToDir(cfg[i]);
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
            } catch (Exception e) {
                throw new CFGParsingException(e.Message);
            }
        }

        public void ReadJson(TextWriter stream) {
            try {
                var options = new JsonSerializerOptions() {
                    IncludeFields = true,
                };
                var readOnlySpan = new ReadOnlySpan<byte>(File.ReadAllBytes(CfgFile));
                JsonSprite root = JsonSerializer.Deserialize<JsonSprite>(readOnlySpan, options);
                AsmFile = CfgFile.AppendToDir(root.asmfile);
                Table.ActLike = root.actlike;
                Table.Type = root.type;
                if (Table.Type != 0) {
                    // do the thing with the asm file
                    Table.Extra[0] = root.extraProp1;
                    Table.Extra[1] = root.extraProp2;
                    ByteCount = root.addbcountclear;
                    ExtraByteCount = root.addbcountset;
                    if (ByteCount > 12)
                        ByteCount = 12;
                    if (ExtraByteCount > 12)
                        ExtraByteCount = 12;

                    // do the things with the tweaks here
                    Table.Tweak[0] = root.t1656;
                    Table.Tweak[1] = root.t1662;
                    Table.Tweak[2] = root.t166e;
                    Table.Tweak[3] = root.t167a;
                    Table.Tweak[4] = root.t1686;
                    Table.Tweak[5] = root.t190f;


                    MapData = Map16.FromBytes(Convert.FromBase64String(root.map16));
                    Displays = root.displays;
                    Collections = root.collections;
                    if (stream is not null) {
                        stream.WriteLine($"Parsed {CfgFile}");
                        stream.Flush();
                    }

                }
            } catch (Exception e) {
                throw new JSONParsingException(e.Message);
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

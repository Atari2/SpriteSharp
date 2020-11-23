using System.Collections.Generic;

namespace SpriteSharp {
    class ToolData {
        public Dictionary<Defines.ListType, Sprite[]> SprLists = new() {
            { Defines.ListType.Sprite, new Sprite[Defines.MaxSprCount] },
            { Defines.ListType.Cluster, new Sprite[Defines.SprCount] },
            { Defines.ListType.Extended, new Sprite[Defines.SprCount] },
            { Defines.ListType.Overworld, new Sprite[Defines.SprCount] }
        };
        public byte[] PlsLevelPtrs = new byte[0x400];
        public byte[] PlsSpritePtr = new byte[0x4000];
        public byte[] PlsData = new byte[0x8000];
        public byte[] PlsPointers = new byte[0x8000];
        public int PlsDataAddr = 0;
        public int PlsSpritePtrAddr = 0;
        public byte[] VersionFlag = { (byte)Defines.ToolVersion, 0x00, 0x00, 0x00 };
        public List<string> ExtraDefines = new();
        public List<Map16> Map = new(Defines.Map16Size);
    }
    static class Defines {
        public enum FileType {
            Routines,
            Sprites,
            Generators,
            Shooters,
            List,
            Asm,
            Extended,
            Cluster,
            Overworld
        }
        public enum ExtType {
            ExtSSC,
            ExtMWT,
            ExtMW2,
            ExtS16
        }

        public enum ListType {
            Sprite = 0,
            Extended = 1,
            Cluster = 2,
            Overworld = 3
        }

        public static readonly int InitPtr = 0x01817D;
        public static readonly int MainPtr = 0x0185CC;
        public static readonly string TempSprFile = "spr_temp.asm";
        public static readonly int SprCount = 0x80;
        public static readonly int ToolVersion = 0x31;
        public static readonly int MaxRomSize = 16 * 1024 * 1024;
        public static readonly byte RTLBank = 0x01;
        public static readonly byte RTLHigh = 0x80;
        public static readonly byte RTLLow = 0x21;
        public static readonly int MaxSprCount = 0x2100;
        public static readonly int Map16Size = 0x3800;
    }
}

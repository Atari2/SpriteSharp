using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpriteSharp {
    static class Extensions {

        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self) => self.Select((item, index) => (item, index));

        public static FileStream OpenSubfile(this Rom rom, string ext) {
            string filename = Path.GetFileNameWithoutExtension(rom.Filename) + "." + ext;
            FileStream str = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
            str.SetLength(0);
            str.Seek(0, SeekOrigin.Begin);
            return str;
        }

        public static void WriteLongTable(this Sprite[] sprites, string filename) {
            byte[] dummy = new byte[0x10];
            List<byte> bytes = new();
            Array.Fill<byte>(dummy, 0xFF);
            if (SpriteTable.IsEmpty(sprites.ToList())) {
                File.WriteAllBytes(filename, dummy);
            } else {
                sprites.ToList().ForEach(x => bytes.AddRange(x.Table.ToBytes()));
                File.WriteAllBytes(filename, bytes.ToArray());
            }
        }

        public static string SetPathsRelativeTo(this string path, string arg0) {
            if (path is null || path == string.Empty)
                return path;
            int count = 0;
            int pos = arg0.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (pos != -1)
                count = pos + 1;
            StringBuilder newpath = new StringBuilder();
            newpath.Append(count != 0 ? arg0.Substring(0, count) : "./");
            newpath.Append(path);
            newpath.Replace('\\', '/');
            return newpath.ToString();
        }

        public static string CleanPathTrail(this string path) {
            return path[0..^1];
        }

        public static string AppendToDir(this string path, string file) {
            file = Path.GetFileName(file);
            int end = path.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (end == -1) end = path.Length - 1;
            end++;
            string newpath = path[0..end] + file;
            return newpath.Trim();
        }
    }
}

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
            byte[] dummy = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            if (SpriteTable.IsEmpty(sprites)) {
                File.WriteAllBytes(filename, dummy);
            } else {
                byte[] emptyTable = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x21, 0x80, 0x01, 0x21, 0x80, 0x01, 0x00, 0x00 };
                var size = 0x10;
                var tableSize = sprites.Length * size;
                var buffer = new byte[tableSize];
                foreach (var (spr, i) in sprites.WithIndex()) {
                    if (spr is null)
                        Array.Copy(emptyTable, 0, buffer, i * size, size);
                    else
                        Array.Copy(spr.Table, 0, buffer, i * size, size);
                }
                File.WriteAllBytes(filename, buffer);
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

using System;
using System.Reflection;
using AsarCLR;

namespace SpriteToolSuperSharp {
    class Program {
        static void Main(string[] args) {
            try {
                if (!System.IO.File.Exists("asar.dll")) {
                    string resourceName = "SpriteToolSuperSharp.asar.dll";
                    string libraryName = "asar.dll";
                    string tempDllPath = ResourceExtractor.LoadUnmanagedLibraryFromResource(Assembly.GetExecutingAssembly(), resourceName, libraryName);
                }
                if (!Asar.init()) {
                    Mixins.WaitAndExit("Error: Asar library is missing or couldn't be initialized, please redownload the tool or add the dll.");
                }
                Pixi pixi = new Pixi(args);
                pixi.Run();
            } catch (Exception e) {
                Mixins.WaitAndExit($"Unexpected error occurred: {e.Message}\n\t{e.StackTrace}");
            }
        }
    }
}

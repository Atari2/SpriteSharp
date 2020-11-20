using AsarCLR;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace SpriteToolSuperSharp {
    class Program {
        static async Task Main(string[] args) {
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
                await pixi.Run();
            } catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) {
                Mixins.WaitAndExit($"A file or directory wasn't found. Please make sure that the executable is in the correct folder. " +
                    $"More details below: \n{ex.Message}");
            } catch (Exception ex) when (ex is NullReferenceException) {
                Mixins.WaitAndExit($"A null ref was thrown, please contact the developer because this should happen. More details below:\n" +
                    $"{ex.Message}\n\t{ex.StackTrace}");
            } catch (Exception e) {
                Mixins.WaitAndExit($"Uncaught error occurred, please contact the developer: {e.Message}\n\t{e.StackTrace}");
            }
        }
    }
}

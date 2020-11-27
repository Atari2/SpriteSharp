using AsarCLR;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SpriteSharp {

    class Program {
        static async Task Main(string[] args) {
            try {
                string tempDllPath = ResourceExtractor.OSDependantLoad();
                if (!Asar.init()) {
                    throw new MissingAsarDLLException();
                }
                Pixi pixi = new Pixi(args);
                await pixi.Run();
            } catch (Exception ex) when (ex is ToolException) {
                Console.WriteLine($"{ex.Message}");
            } catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) {
                Console.WriteLine($"A file or directory wasn't found. Please make sure that the executable is in the correct folder. " +
                    $"More details below: \n{ex.Message}");
            } catch (Exception ex) when (ex is NullReferenceException) {
                Console.WriteLine($"A null ref was thrown, please contact the developer because this shouldn't happen. More details below:\n" +
                    $"{ex.Message}\n\t{ex.StackTrace}");
            } catch (Exception e) {
                Console.WriteLine($"Uncaught error occurred, please contact the developer: {e.Message}\n\t{e.StackTrace}");
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}

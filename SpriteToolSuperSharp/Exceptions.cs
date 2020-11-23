using System;
namespace SpriteToolSuperSharp {
    public class ToolException : Exception {
        public ToolException() { }
        public ToolException(string message) : base(message) { }
    }

    public class InvalidCmdParameterException : ToolException {
        public InvalidCmdParameterException(string message) : base(message) { }
    }

    public class MissingAsarDLLException : ToolException {
        public MissingAsarDLLException() : base("Error: Asar library is missing or couldn't be initialized, please redownload the tool or the dll.") { }
    }

    public class MissingFileException : ToolException {
        public MissingFileException(string filename) : base($"{filename} wasn't found, please make sure to have entered the correct name.") { }
    }

    public class CheckFailedException : ToolException {
        public CheckFailedException(string message) : base(message) { }
    }

    public class JSONParsingException : ToolException {
        public JSONParsingException(string message) : base($"Error was thrown while parsing JSON file, error was:\n\t{message}") { }
    }

    public class CFGParsingException : ToolException { 
        public CFGParsingException(string message) : base($"Error was thrown while parsing CFG file, error was:\n\t{message}") { }
    }

    public class VersionGuardFailed : ToolException {
        public VersionGuardFailed(string sprite) : base($"Version guard failed for sprite {sprite}") { }
    }

    public class SpriteFailureException : ToolException {
        public SpriteFailureException(string message) : base(message) { }
    }

    public class AsarErrorException : ToolException {
        public AsarErrorException(string message) : base(message) { }
    }

    public class LunarMagicException : ToolException {
        public LunarMagicException() : base("Something went wrong while posting the reload rom message to Lunar Magic, reload the ROM manually") { }
    }

    public class ListParsingException : ToolException {
        public ListParsingException(string message) : base(message) { }
    }
}

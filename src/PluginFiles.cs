using System.IO;
using IPA.Utilities;

namespace LipSync
{
    internal class PluginFiles
    {
        public static DirectoryInfo DataDir => new DirectoryInfo(Path.Combine(UnityGame.UserDataPath, "LipSync"));
        public static FileInfo ModelFile => new FileInfo(Path.Combine(DataDir.FullName, "model.onnx"));
        public static FileInfo ConfigFile => new FileInfo(Path.Combine(DataDir.FullName, "config.json"));
    }
}
using System.IO;

namespace VoxelMaterialLimit
{
    // A shader file served from memory after this plugin rewrote its text. It remembers the file it came from so that the
    // files it includes can still be found next to the original.
    public sealed class PatchedIncludeStream : MemoryStream
    {
        public string OriginalPath { get; }

        public PatchedIncludeStream(byte[] content, string originalPath) : base(content, false)
        {
            OriginalPath = originalPath;
        }
    }
}

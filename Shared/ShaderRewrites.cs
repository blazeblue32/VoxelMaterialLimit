using System;
using System.Text.RegularExpressions;

namespace VoxelMaterialLimit
{
    // The text changes made to the player's own shader files as they are read for compilation. Each is a pattern on identifiers, not a copy of game code.
    public static class ShaderRewrites
    {
        public const string LimitFile = "Common.hlsli";
        public const string VertexFile = "VertexTemplateBase.hlsli";

        // Number of entries in the voxel material table.
        static readonly Regex LimitDefine = new Regex(@"(#[ \t]*define[ \t]+MAX_VOXEL_MATERIALS[ \t]+)(\d+)", RegexOptions.Compiled);

        // Voxel vertices hold the material numbers as signed bytes, so numbers 128 and above arrive negative and the float-to-unsigned copy turns them into 0.
        // Reading them back through an integer and masking to a byte restores 128 to 255.
        static readonly Regex MaterialInfoCopy = new Regex(@"(\btriplanar_mat_info\s*=\s*)(__triplanar_mat_info\b)(\s*;)", RegexOptions.Compiled);

        // True when the file is one this plugin rewrites (by file name).
        public static bool Wants(string fileName)
        {
            return fileName != null && (EndsWith(fileName, LimitFile) || EndsWith(fileName, VertexFile));
        }

        // True for a rewrite that only matters to the voxel material shaders (so other shaders are left as the game has them).
        public static bool VoxelOnly(string fileName)
        {
            return fileName != null && EndsWith(fileName, VertexFile);
        }

        // Returns the rewritten text; the text itself when the file has no match (the caller logs that).
        public static string Rewrite(string fileName, string text)
        {
            if (EndsWith(fileName, LimitFile)) return LimitDefine.Replace(text, m => m.Groups[1].Value + ShaderLimitPatch.NewLimit);
            if (EndsWith(fileName, VertexFile)) return MaterialInfoCopy.Replace(text, "$1(uint4)(((int4)$2) & 255)$3");
            return text;
        }

        // Whole file name only, so DecalsCommon.hlsli and the like are not mistaken for Common.hlsli.
        static bool EndsWith(string path, string name)
        {
            string file = path.Substring(path.LastIndexOfAny(new[] { '/', '\\' }) + 1);
            return file.Equals(name, StringComparison.OrdinalIgnoreCase);
        }
    }
}

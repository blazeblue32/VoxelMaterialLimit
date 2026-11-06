using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace VoxelMaterialLimit
{
    // The renderer reads voxel materials from a constant buffer declared in the shaders with a fixed number of entries.
    // This patch raises that number while the shader source is being read for compilation. The game's own buffer
    // and material index space already hold 256 entries, so 256 is the highest value that is meaningful.
    public static class ShaderLimitPatch
    {
        public const int NewLimit = 256;

        static readonly System.Collections.Generic.HashSet<string> reported = new System.Collections.Generic.HashSet<string>();

        public static void Apply(Harmony harmony)
        {
            Type includeType = FindType("VRageRender.MyShaderCompiler+MyIncludeProcessor");
            MethodInfo open = includeType == null ? null : AccessTools.Method(includeType, "Open");
            if (open == null)
            {
                Log("shader include reader not found; the game has changed, nothing was patched");
                return;
            }
            harmony.Patch(open, postfix: new HarmonyMethod(typeof(ShaderLimitPatch), nameof(OpenPostfix)));
            Log("installed; voxel material limit in shaders will be " + NewLimit);
        }

        // Harmony postfix for the include reader: arg 1 is the requested file name, the result is the file's stream.
        public static void OpenPostfix(string __1, ref Stream __result)
        {
            try
            {
                if (__result == null || !ShaderRewrites.Wants(__1)) return;
                string text;
                using (var reader = new StreamReader(__result, new UTF8Encoding(false), true))
                    text = reader.ReadToEnd();

                string patched = ShaderRewrites.Rewrite(__1, text);
                if (reported.Add(__1))
                    Log(patched != text ? "rewrote " + __1 + " as it was read" : "WARNING: nothing to rewrite in " + __1 + "; the game has changed");
                __result = new MemoryStream(new UTF8Encoding(false).GetBytes(patched), false);
            }
            catch (Exception e)
            {
                Log("failed to patch the shader include: " + e);
            }
        }

        public static string ShadersPath()
        {
            // The folder the game itself compiles from (its compiler class exposes it), falling back to the content folder's shader directory.
            Type compiler = FindType("VRageRender.MyShaderCompiler");
            var prop = compiler == null ? null : AccessTools.Property(compiler, "ShadersPath");
            string path = prop?.GetValue(null) as string;
            return !string.IsNullOrEmpty(path) ? path : Path.Combine(VRage.FileSystem.MyFileSystem.ShadersBasePath, "Shaders");
        }

        public static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null)
                   ?? AccessTools.TypeByName(fullName);
        }

        public static void Log(string message)
        {
            string line = "[VoxelMaterialLimit] " + message;
            try
            {
                if (VRage.Utils.MyLog.Default != null) VRage.Utils.MyLog.Default.WriteLineAndConsole(line);
                else Console.WriteLine(line);
            }
            catch { Console.WriteLine(line); }
        }
    }
}

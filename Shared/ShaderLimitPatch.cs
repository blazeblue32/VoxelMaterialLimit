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

        // Messages already logged once; shader compiles run on several threads, so every access takes the lock.
        static readonly System.Collections.Generic.HashSet<string> reported = new System.Collections.Generic.HashSet<string>();

        static FieldInfo basePathField;

        public static void Apply(Harmony harmony)
        {
            Type includeType = FindType("VRageRender.MyShaderCompiler+MyIncludeProcessor");
            MethodInfo open = includeType == null ? null : AccessTools.Method(includeType, "Open");
            if (open == null)
            {
                Log("shader include reader not found; the game has changed, nothing was patched");
                return;
            }
            basePathField = AccessTools.Field(includeType, "m_basePath");
            harmony.Patch(open, prefix: new HarmonyMethod(typeof(ShaderLimitPatch), nameof(OpenPrefix)),
                                postfix: new HarmonyMethod(typeof(ShaderLimitPatch), nameof(OpenPostfix)));
            Log("installed; voxel material limit in shaders will be " + NewLimit);
        }

        // Harmony prefix for the include reader (arguments: 0 = include kind, 1 = requested name, 2 = the including file's stream).
        // A file this plugin rewrote is served from memory, and the game's reader finds an including file's folder only when that
        // file's stream is a real file stream. So for a quoted ("local") include made from inside a rewritten file, look next to the
        // original file ourselves; anything else, and anything not found there, is left to the game's reader.
        public static bool OpenPrefix(object __0, string __1, Stream __2, ref Stream __result)
        {
            try
            {
                if (!(__2 is PatchedIncludeStream parent) || Convert.ToInt32(__0) != 0 || __1 == null) return true;
                string folder = Path.GetDirectoryName(parent.OriginalPath);
                string candidate = folder == null ? null : Path.Combine(folder, __1);
                if (candidate == null || !VRage.FileSystem.MyFileSystem.FileExists(candidate)) return true;
                __result = new FileStream(candidate, FileMode.Open, FileAccess.Read);
                return false;
            }
            catch (Exception e)
            {
                Log("could not resolve an include from a rewritten file: " + e.Message);
                return true;
            }
        }

        // Harmony postfix for the include reader: arg 1 is the requested file name, the result is the file's stream.
        // Only a stream the game opened from a real file is rewritten; a stream another plugin has substituted is left alone.
        public static void OpenPostfix(object __instance, string __1, ref Stream __result)
        {
            try
            {
                var file = __result as FileStream;
                if (file == null || !ShaderRewrites.Wants(__1)) return;
                if (ShaderRewrites.VoxelOnly(__1) && !IsVoxelMaterialShader(__instance)) return;

                string original = file.Name, text;
                using (var reader = new StreamReader(file, new UTF8Encoding(false), true))
                    text = reader.ReadToEnd();

                string patched = ShaderRewrites.Rewrite(__1, text);
                if (ReportOnce(__1))
                    Log(patched != text ? "rewrote " + __1 + " as it was read" : "WARNING: nothing to rewrite in " + __1 + "; the game has changed");
                __result = new PatchedIncludeStream(new UTF8Encoding(false).GetBytes(patched), original);
            }
            catch (Exception e)
            {
                Log("failed to patch the shader include: " + e);
            }
        }

        // The vertex rewrite is only needed for the voxel material shaders. The include reader remembers the folder of the shader being
        // compiled, so use that to leave every other material's shaders (and their cached bytecode) exactly as the game has them.
        // If the folder cannot be read, rewrite anyway: slower to compile, but correct.
        static bool IsVoxelMaterialShader(object reader)
        {
            string folder = null;
            try { folder = reader == null || basePathField == null ? null : basePathField.GetValue(reader) as string; }
            catch (Exception) { }
            if (folder == null) { if (ReportOnce("basepath")) Log("could not tell which shader is being compiled; rewriting for all materials"); return true; }
            return folder.Replace('\\', '/').IndexOf("/Materials/Triplanar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ReportOnce(string key)
        {
            lock (reported) return reported.Add(key);
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

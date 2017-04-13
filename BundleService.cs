using System.IO;
using NUglify;

namespace ModuleLoader
{
    public class BundleService
    {
        private readonly string[] _paths;
        public BundleService(string[] paths)
        {
            _paths = paths;
        }

        public BundlerResult Bundle()
        {
            var result = new BundlerResult();

            foreach (var path in _paths)
            {
                if (path.Contains("**"))
                {
                    ResolveRelativePath(path, result);
                }
                else if (path.Contains("*"))
                {
                    ResolvePatternPath(path, result);
                }
                else
                {
                    ProcessFilePath(path, result);
                }
            }

            return result;
        }

        private void ProcessFilePath(string path, BundlerResult result)
        {
            if (Path.GetExtension(path) == ".js")
            {
                result.Js += "<script>" + Uglify.Js(File.ReadAllText(path)) + "</script>" + "\n";
            }
            else
            {
                result.Css += File.ReadAllText(path) + "\n";
            }
        }

        private void ResolvePatternPath(string path, BundlerResult result)
        {
            var contextDirectory = path.Substring(0, path.IndexOf("*"));
            var pattern = path.Substring(path.IndexOf("*"));

            var files = Directory.GetFiles(contextDirectory, pattern);

            foreach (var item in files)
            {
                ProcessFilePath(item, result);
            }
        }

        private void ResolveRelativePath(string path, BundlerResult result)
        {
            var contextDirectory = path.Substring(0, path.IndexOf("*"));
            var pattern = path.Substring(path.IndexOf("*"));

            var files = Directory.GetFiles(contextDirectory, pattern);

            foreach (var item in files)
            {
                ProcessFilePath(path, result);
            }
        }
    }
}

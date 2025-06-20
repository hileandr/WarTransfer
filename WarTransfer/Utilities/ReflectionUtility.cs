using System.IO;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace WarTransfer
{
    public static class ReflectionUtility
    {
        public static PropertyInfo GetProperty<TDelegate>(Expression<TDelegate> e)
        {
            var expression = e.Body;
            MemberExpression memberExpression = expression as MemberExpression;

            if (memberExpression != null)
            {
                return (PropertyInfo)memberExpression.Member;
            }

            return null;
        }

        public static string GetAssemblyDirectory(Assembly assembly)
        {
            string codeBase = assembly.CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }
    }
}

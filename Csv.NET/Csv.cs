using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;

namespace Csv.NET
{
    public class CsvFormatAttribute : Attribute
    {
        public string Format { get; }

        public CsvFormatAttribute(string format)
        {
            Format = format;
        }
    }

    public static class Csv
    {
        public static T[] ReadFile<T>(string path, char delim = ',')
        {
            var props = typeof(T).GetProperties();

            T CreateInstanceFromRow(string[] row)
            {
                if (props.Length != row.Length)
                {
                    throw new Exception("Unequivalent count of properties and rows");
                }

                var ret = (T)FormatterServices.GetSafeUninitializedObject(typeof(T));

                for (int i = 0; i < props.Length; i++)
                {
                    props[i].SetValue(ret, Convert.ChangeType(row[i], props[i].PropertyType));
                }

                return ret;
            }


            return File.ReadAllLines(path)
                .Select(l => l.Split(delim))
                .Select(r => CreateInstanceFromRow(r))
                .ToArray();
        }

        public static void WriteFile<T>(T[] rows, string path, char delim = ',')
        {
            var props = typeof(T).GetProperties();
            var stringFormatVals = new List<string>();

            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].GetCustomAttributes(typeof(CsvFormatAttribute), true).FirstOrDefault() is CsvFormatAttribute cf)
                {
                    stringFormatVals.Add($"{{{i}:{cf.Format}}}");
                }
                else
                {
                    stringFormatVals.Add($"{{{i}}}");
                }
            }

            var fmi = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object[]) });
            var format = string.Join(delim, stringFormatVals);
            var erow = Expression.Parameter(typeof(T));
            var eprops = props.Select(p => Expression.Property(erow, p)).ToArray();
            var epropsArray = Expression.NewArrayInit(typeof(object), eprops.Select(ep => Expression.Convert(ep, typeof(object))));
            var cfc = Expression.Call(fmi, Expression.Constant(format), epropsArray);
            var toStringLambda = Expression.Lambda<Func<T, string>>(cfc, erow);
            var toString = toStringLambda.Compile();

            using (var f = File.OpenWrite(path))
            using (var bs = new BufferedStream(f))
            using (var sw = new StreamWriter(bs))
            {
                foreach (var row in rows)
                {
                    sw.WriteLine(toString(row));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Slamby.Common.Mapper
{
    public class DynamicMapper
    {
        internal class Mapping
        {
            public string Source { get; set; }

            public Func<object, object> SourcePropertyGetterFunc { get; set; }

            public string Destination { get; set; }

            public List<string> DestinationPaths { get; set; } = new List<string>();

            public string DestinationFieldName { get; set; }
        }

        readonly List<Mapping> mappings;

        private DynamicMapper(Dictionary<string, string> mappings)
        {
            this.mappings = mappings.Select(map =>
                new Mapping()
                {
                    Source = map.Value,
                    Destination = map.Key
                })
                .ToList();
        }

        public static DynamicMapper CreateMap(Type sourceType, Dictionary<string, string> mappings)
        {
            var mapper = new DynamicMapper(mappings);

            try
            {
                mapper.PrepareMappings(sourceType);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return mapper;
        }

        private void PrepareMappings(Type sourceType)
        {
            foreach (var mapping in mappings)
            {
                mapping.SourcePropertyGetterFunc = GetCompiledFunc(sourceType, mapping.Source);

                var splits = mapping.Destination.Split(new[] { "." }, StringSplitOptions.None);

                mapping.DestinationPaths = splits.Take(Math.Max(splits.Count() - 1, 0)).ToList();

                mapping.DestinationFieldName = mapping.Destination;
                var dotIndex = mapping.Destination.LastIndexOf(".", StringComparison.Ordinal);
                if (dotIndex > -1)
                {
                    mapping.DestinationFieldName = mapping.DestinationFieldName.Substring(dotIndex + 1);
                }
            }
        }

        private Func<object, object> GetCompiledFunc(Type type, string property)
        {
            var arg = Expression.Parameter(typeof(object), "source");
            Expression expr = Expression.Convert(arg, type);

            var propType = type;
            foreach (var propertyName in property.Split('.'))
            {
                var propertyInfo = propType.GetProperty(propertyName);
                if (propertyInfo == null)
                {
                    throw new ArgumentException(string.Format("Field {0} not found.", propertyName));
                }
                expr = Expression.Property(expr, propertyInfo);
                propType = propertyInfo.PropertyType;
            }

            expr = Expression.Convert(expr, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(expr, arg);

            return lambda.Compile();
        }

        public object Map(object source)
        {
            return Map(new[] { source }).First();
        }

        public IEnumerable<object> Map(IEnumerable<object> source)
        {
            var res = new List<object>(source.Count());
            var lockObject = new object();

            Parallel.ForEach(source, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, sourceObject =>
            {
                var createdObject = new ExpandoObject() as IDictionary<string, object>;

                foreach (var mapping in mappings)
                {
                    CreateHierarchy(createdObject, mapping);

                    var currentObject = createdObject;
                    foreach (var split in mapping.DestinationPaths)
                    {
                        currentObject = currentObject[split] as IDictionary<string, object>;
                    }

                    currentObject[mapping.DestinationFieldName] = mapping.SourcePropertyGetterFunc(sourceObject);
                }

                lock (lockObject)
                {
                    res.Add(createdObject);
                }
            });

            return res;
        }

        private void CreateHierarchy(IDictionary<string, object> createdObject, Mapping mappingObject)
        {
            var currentObject = createdObject;

            foreach (var split in mappingObject.DestinationPaths)
            {
                if (!currentObject.ContainsKey(split))
                {
                    currentObject.Add(split, new ExpandoObject());
                }

                currentObject = currentObject[split] as IDictionary<string, object>;
            }
        }
    }
}

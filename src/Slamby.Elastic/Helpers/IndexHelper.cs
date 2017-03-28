using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Nest;
using Newtonsoft.Json.Linq;
using Slamby.Common.Helpers;

namespace Slamby.Elastic.Helpers
{
    public static class IndexHelper
    {
        public static IPromise<IProperties> MapProperties(JToken node, PropertiesDescriptor<object> descriptor, PropertiesDescriptor<object> analyzers, List<string> interpretedFields, string tagField)
        {
            if (node.Type != JTokenType.Object)
            {
                return descriptor;
            }

            var properties = node.Children<JProperty>().ToList();

            if (node.Parent == null)
            {
                // Process root object
                var type = properties.FirstOrDefault(c => c.Name == SchemaHelper.Elements.Type)?.Value.Value<string>();
                if (type == SchemaHelper.Types.Object)
                {
                    var propertiesProperty = properties.FirstOrDefault(c => c.Name == SchemaHelper.Elements.Properties).Value;
                    return MapProperties(propertiesProperty, descriptor, analyzers, interpretedFields, tagField, string.Empty);
                }
            }

            return descriptor;
        }

        public static IPromise<IProperties> MapProperties(JToken node, PropertiesDescriptor<object> descriptor, PropertiesDescriptor<object> analyzers, List<string> interpretedFields, string tagField, string path)
        {
            var properties = node.Children<JProperty>().ToList();

            foreach (JProperty property in properties)
            {
                var name = property.Name;
                var fullPath = path.AppendSeparator(".") + name;
                var isInterpreted = interpretedFields.Contains(fullPath, StringComparer.OrdinalIgnoreCase);
                Func<PropertiesDescriptor<object>, IPromise<IProperties>> applyAnalyzerFields = field => isInterpreted ? analyzers : field;
                var tokenPrefix = string.Empty;

                var type = property.Value.SelectToken(SchemaHelper.Elements.Type)?.Value<string>();
                var format = property.Value.SelectToken(SchemaHelper.Elements.Format)?.Value<string>();

                if (string.IsNullOrEmpty(type))
                {
                    // Json schema validator should catch this
                    throw new ArgumentNullException($"Elastic type is not provided");
                }

                // Elastic nem kezel külön array-t, ezért ha így tartalmazza a schema az eleme típusát használjuk fel
                if (type == SchemaHelper.Types.Array)
                {
                    tokenPrefix = $"{SchemaHelper.Elements.Items}.";
                    type = property.Value.SelectToken($"{tokenPrefix}{SchemaHelper.Elements.Type}")?.Value<string>();
                    format = property.Value.SelectToken($"{tokenPrefix}{SchemaHelper.Elements.Format}")?.Value<string>();
                }

                switch (type)
                {
                    case SchemaHelper.Types.String:
                        {
                            if (tagField.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                            {
                                descriptor.String(desc => desc
                                .Name(name)
                                .Fields(applyAnalyzerFields)
                                .Index(FieldIndexOption.NotAnalyzed));
                                
                            } else
                            {
                                descriptor.String(desc => desc
                                .Name(name)
                                .Fields(applyAnalyzerFields));
                            }
                            break;
                        }
                    case SchemaHelper.Types.Long:
                    case SchemaHelper.Types.Integer:
                    case SchemaHelper.Types.Short:
                    case SchemaHelper.Types.Byte:
                    case SchemaHelper.Types.Double:
                    case SchemaHelper.Types.Float:
                        {
                            var numberType = (NumberType)Enum.Parse(typeof(NumberType), type, true);
                            descriptor.Number(desc => desc
                                .Name(name)
                                .Type(numberType)
                                .Fields(applyAnalyzerFields));
                            break;
                        }
                    case SchemaHelper.Types.Boolean:
                        {
                            descriptor.Boolean(desc => desc
                                .Name(name)
                                .Fields(applyAnalyzerFields));
                            break;
                        }
                    case SchemaHelper.Types.Date:
                        {
                            if (string.IsNullOrWhiteSpace(format))
                            {
                                format = "strict_date_optional_time||epoch_millis";
                            }

                            descriptor.Date(desc => desc
                                .Name(name)
                                .Format(format)
                                .Fields(applyAnalyzerFields));
                        }
                        break;
                    case SchemaHelper.Types.Attachment:
                        {
                            descriptor.Attachment(desc => desc
                                .Name(name)
                                .FileField(d => d //ContentField
                                    .Store(true)
                                    .Fields(applyAnalyzerFields)) 
                                .ContentTypeField(d => d.Store(true))
                                .ContentLengthField(d => (d as NumberPropertyDescriptor<object>).Store(true))
                                .LanguageField(d => (d as StringPropertyDescriptor<object>).Store(true))
                                .KeywordsField(d => d.Store(true))
                                .AuthorField(d => d.Store(true))
                                .DateField(d => d.Store(true))
                                .TitleField(d => d.Store(true))
                            );
                            break;
                        }
                    case SchemaHelper.Types.Object:
                        {
                            descriptor.Object<object>(desc => desc
                                .Name(name)
                                .Properties(propDesc =>
                                    MapProperties(property.Value.SelectToken($"{tokenPrefix}{SchemaHelper.Elements.Properties}"),
                                                  propDesc,
                                                  analyzers,
                                                  interpretedFields,
                                                  tagField,
                                                  fullPath)));
                            break;
                        }
                    default:
                        {
                            throw new NotImplementedException($"Elastic type '{type}' is not implemented");
                        }
                }
            }

            return descriptor;
        }

        public static IPromise<IProperties> SetInterpretedFields(PropertiesDescriptor<object> descriptor, PropertiesDescriptor<object> fieldsDescriptor, string[][] segmentArray)
        {
            var levelSegments = GetLevelSegments(segmentArray, 0);
            var segmentGroups = levelSegments.GroupAdjacent(l => l).Select(g => new { Segment = g.Key, Count = g.Count() });

            foreach (var group in segmentGroups)
            {
                if (group.Count == 1)
                {
                    SetInterpretedFields(descriptor, fieldsDescriptor, segmentArray.First(s => s.First() == group.Segment));
                    continue;
                }

                var nseg = segmentArray.Where(s => s.FirstOrDefault() == group.Segment)
                                   .Select(x => x.Skip(1).ToArray())
                                   .ToArray();
                descriptor.Object<object>(s => s
                    .Name(group.Segment)
                    .Properties(pDesc => SetInterpretedFields(pDesc, fieldsDescriptor, nseg)));
            }

            return descriptor;
        }

        public static IPromise<IProperties> SetInterpretedFields(PropertiesDescriptor<object> descriptor, PropertiesDescriptor<object> fieldsDescriptor, IEnumerable<string> segmentList)
        {
            var segment = segmentList.First();
            var restSegments = segmentList.Skip(1);

            if (restSegments.Count() == 0)
            {
                descriptor.String(strDesc => strDesc
                                    .Name(segment)
                                    .Fields(f => fieldsDescriptor));
                return descriptor;
            }

            descriptor.Object<object>(desc => desc
                    .Name(segment)
                    .Properties(pDesc => SetInterpretedFields(pDesc, fieldsDescriptor, restSegments)));

            return descriptor;
        }

        public static string[] GetLevelSegments(string[][] segments, int index)
        {
            return segments.Select(segment => segment.Skip(index).FirstOrDefault()).ToArray();
        }
    }
}

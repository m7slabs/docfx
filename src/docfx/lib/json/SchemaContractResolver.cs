// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Docs.Build
{
    internal class SchemaContractResolver : JsonContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            var converter = GetConverter(member);

            if (converter != null)
            {
                prop.Converter = converter;
            }
            return prop;
        }

        private SchemaValidationAndTransformConverter GetConverter(MemberInfo member)
        {
            var validators = member.GetCustomAttributes<ValidationAttribute>(false);
            var contentTypeAttributes = member.GetCustomAttributes<DataTypeAttribute>(false);
            if (contentTypeAttributes.Any() || validators.Any())
            {
                return new SchemaValidationAndTransformConverter(contentTypeAttributes, validators, member.Name);
            }
            return null;
        }

        private sealed class SchemaValidationAndTransformConverter : JsonConverter
        {
            private readonly IEnumerable<ValidationAttribute> _validators;
            private readonly IEnumerable<DataTypeAttribute> _attributes;
            private readonly string _fieldName;

            public SchemaValidationAndTransformConverter(IEnumerable<DataTypeAttribute> attributes, IEnumerable<ValidationAttribute> validators, string fieldName)
            {
                _attributes = attributes;
                _validators = validators;
                _fieldName = fieldName;
            }

            public override bool CanConvert(Type objectType) => true;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => new NotSupportedException();

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var range = JsonUtility.ToRange((IJsonLineInfo)reader);
                var value = serializer.Deserialize(reader, objectType);
                if (value is null)
                {
                    return null;
                }

                foreach (var validator in _validators)
                {
                    try
                    {
                        validator.Validate(value, new ValidationContext(value) { DisplayName = _fieldName });
                    }
                    catch (Exception e)
                    {
                        JsonUtility.State.Errors.Add(Errors.ViolateSchema(range, e.Message));
                    }
                }

                var transform = JsonUtility.State.Transform;
                return transform != null ? transform(_attributes, value, reader.Path) : value;
            }
        }
    }
}
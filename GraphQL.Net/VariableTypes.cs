using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Parser;

namespace GraphQL.Net
{
    public class VariableTypes
    {
        private readonly List<Func<ITypeHandler, ITypeHandler>> _customHandlers =
            new List<Func<ITypeHandler, ITypeHandler>>();
        private RootTypeHandler _rootTypeHandler;

        private ITypeHandler TypeHandler => _rootTypeHandler;

        private IReadOnlyDictionary<string, CoreVariableType> _typeDictionaryWithEnums;
        private IReadOnlyDictionary<string, CoreVariableType> _definedTypesDictionary;
        private IReadOnlyDictionary<string, CoreVariableType> _enumTypesDictionary;

        public void AddType(Func<ITypeHandler, ITypeHandler> customHandler)
        {
            if (_rootTypeHandler != null) throw new Exception("Can't add types after completing.");
            _customHandlers.Add(customHandler);
        }

        private class MetaTypeHandler : IMetaTypeHandler
        {
            private readonly VariableTypes _variableTypes;

            public MetaTypeHandler(VariableTypes variableTypes)
            {
                _variableTypes = variableTypes;
            }


            public IEnumerable<ITypeHandler> Handlers(ITypeHandler rootHandler)
                => _variableTypes._customHandlers.Select(h => h(rootHandler));
        }

        public void Complete()
        {
            if (_rootTypeHandler != null) throw new Exception("Variable types already complete.");
            _rootTypeHandler = new RootTypeHandler(new MetaTypeHandler(this));

            var rootTypeDictionary = _rootTypeHandler.TypeDictionary;

            var definedTypes = TypeHandler.DefinedTypes;
            var enumTypes = definedTypes.Where(t => t.IsEnumType && !IsReservedName(GetEnumOrTypeName(t)));

            _definedTypesDictionary = DictionaryFromTypeList(definedTypes);
            _enumTypesDictionary = DictionaryFromTypeList(enumTypes);

            _typeDictionaryWithEnums = DictionaryFromTypeList(rootTypeDictionary.Values.Concat(enumTypes));
        }

        public IReadOnlyDictionary<string, CoreVariableType> TypeDictionary => _typeDictionaryWithEnums;

        public IEnumerable<CoreVariableType> DefinedTypes => _definedTypesDictionary.Values;
        public IEnumerable<CoreVariableType> DefinedEnumTypes => _enumTypesDictionary.Values;

        private bool IsReservedName(string name)
        {
            return name.StartsWith("__");
        }

        private Dictionary<string, CoreVariableType> DictionaryFromTypeList(IEnumerable<CoreVariableType> types)
        {
            return types.ToDictionary(t => GetEnumOrTypeName(t));
        }

        private string GetEnumOrTypeName(CoreVariableType t)
        {
            if (t is CoreVariableType.EnumType enumType)
            {
                return enumType.Item.EnumName;
            }
            if (t is CoreVariableType.NamedType namedType)
            {
                return namedType.Item.TypeName;
            }

            throw new InvalidOperationException($"GraphQL type does not have a name!");
        }

        public EnumValue ResolveEnumValue(string name)
        {
            return _rootTypeHandler.ResolveEnumValueByName(name).Value;
        }

        /// <summary>
        /// Return the schema variable type used to represent values of type <paramref name="clrType"/>.
        /// </summary>
        /// <param name="clrType"></param>
        /// <returns></returns>
        public VariableType VariableTypeOf(Type clrType)
            => TypeHandler.GetMapping(clrType)?.Value.VariableType;

        /// <summary>
        /// Get a CLR object of type <paramref name="desiredCLRType"/> from the value <paramref name="graphQLValue"/>.
        /// </summary>
        /// <param name="graphQLValue"></param>
        /// <param name="desiredCLRType"></param>
        /// <returns></returns>
        public object TranslateValue(Value graphQLValue, Type desiredCLRType)
            => TypeHandler.GetMapping(desiredCLRType)?.Value.Translate.Invoke(graphQLValue);
    }
}

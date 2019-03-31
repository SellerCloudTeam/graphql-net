using GraphQL.Net.SchemaAdapters;
using GraphQL.Parser;
using GraphQL.Parser.Execution;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraphQL.Net
{
    public class GraphQL<TContext>
    {
        private readonly GraphQLSchema<TContext> _schema;
        public GraphQL(GraphQLSchema<TContext> schema)
        {
            _schema = schema;
        }

        public IDictionary<string, object> ExecuteQuery(string queryStr)
        {
            var execContext = DefaultExecContext.Instance;
            return ExecuteQuery(queryStr, execContext);
        }

        public IDictionary<string, object> ExecuteQuery(string queryStr, TContext queryContext)
        {
            var execContext = DefaultExecContext.Instance;
            return ExecuteQuery(queryStr, queryContext, execContext);
        }

        public IDictionary<string, object> ExecuteQuery(string queryStr, IExecContext execContext)
        {
            if (_schema.ContextCreator == null)
                throw new InvalidOperationException("No context creator specified. Either pass a context " +
                    "creator to the schema's constructor or call overloaded method 'ExecuteQuery(string query, TContext context)' " +
                    "and pass a context.");
            var context = _schema.ContextCreator();
            var result = ExecuteQuery(queryStr, context, execContext);
            (context as IDisposable)?.Dispose();
            return result;
        }

        public IDictionary<string, object> ExecuteQuery(string queryStr, TContext queryContext, IExecContext execContext)
        {
            if (!_schema.Completed)
                throw new InvalidOperationException("Schema must be Completed before executing a query. Try calling the schema's Complete method.");

            if (queryContext == null)
                throw new ArgumentException("Context must not be null.");

            var document = GraphQLDocument<Info>.Parse(_schema.Adapter, queryStr);
            var operation = document.Operations.Single(); // TODO support multiple operations per document, look up by name
            var execSelections = execContext.ToExecSelections(operation.Value);

            var outputs = new Dictionary<string, object>();
            foreach (var execSelection in execSelections.Select(s => s.Value))
            {
                var field = execSelection.SchemaField.Field();
                outputs[execSelection.Name] = Executor<TContext>.Execute(_schema, queryContext, field, execSelection);
            }
            return outputs;
        }
    }
}

/**
 * “Commons Clause” License Condition v1.0
 * 
 * The Software is provided to you by the Licensor under the License, as defined below, subject to the following condition.
 * 
 * Without limiting other conditions in the License, the grant of rights under the License will not include, and the License 
 * does not grant to you, the right to Sell the Software.
 * 
 * For purposes of the foregoing, “Sell” means practicing any or all of the rights granted to you under the License to provide 
 * to third parties, for a fee or other consideration (including without limitation fees for hosting or consulting/ support 
 * services related to the Software), a product or service whose value derives, entirely or substantially, from the 
 * functionality of the Software. Any license notice or attribution required by the License must also include this Commons 
 * Clause License Condition notice.
 * 
 * Software: PeerColab Engine
 * License: Apache 2.0
 * Licensor: New Horizon Invest AS
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PeerColabEngine
{
    /// <summary>
    /// The operation verb is not to confuse with HTTP verbs. The operation verbs is to mark the operation
    /// with what type of data processing it is doing.
    /// </summary>
    public enum OperationVerb
    {
        GET,
        CREATE,
        ADD,
        UPDATE,
        PATCH,
        REMOVE,
        DELETE,
        START,
        STOP,
        PROCESS,
        SEARCH,
        NAVIGATETO
    }

    public static class GlobalSerializer
    {
        private static TransportSerializer _serializer = new DefaultTransportSerializer();

        public static TransportSerializer GetSerializer()
        {
            return _serializer;
        }

        public static void SetSerializer(TransportSerializer serializer)
        {
            _serializer = serializer;
        }
    }

    public static class OperationVerbs
    {
        public static readonly OperationVerb[] All = new[]
        {
            OperationVerb.GET,
            OperationVerb.CREATE,
            OperationVerb.ADD,
            OperationVerb.UPDATE,
            OperationVerb.PATCH,
            OperationVerb.REMOVE,
            OperationVerb.DELETE,
            OperationVerb.START,
            OperationVerb.STOP,
            OperationVerb.PROCESS
        };
    }

    public class OutOfContextOperationPathParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class OutOfContextOperation
    {
        public string UsageId { get; set; } = string.Empty;
        public string OperationId { get; set; } = string.Empty;
        public string OperationVerb { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public object RequestJson { get; set; }
        public List<OutOfContextOperationPathParameter> PathParameters { get; set; } = new List<OutOfContextOperationPathParameter>();

    }

    public interface ContextCache
    {
        Task<bool> Put(Guid transactionId, CallInformation ctx);
        Task<CallInformation> Get(Guid transactionId);
    }

    public class InMemoryContextCache : ContextCache
    {
        private class CacheEntry
        {
            public CallInformation Ctx { get; set; }
            public long ExpiresAt { get; set; }
        }

        private readonly Dictionary<Guid, CacheEntry> _cache = new Dictionary<Guid, CacheEntry>();
        private readonly long _maxLifetimeMs;

        public InMemoryContextCache(long maxLifetimeMs = 3000 * 1000)
        {
            _maxLifetimeMs = maxLifetimeMs;
        }

        public Task<bool> Put(Guid transactionId, CallInformation ctx)
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _maxLifetimeMs;
            lock (_cache) {
                _cache[transactionId] = new CacheEntry { Ctx = ctx, ExpiresAt = expiresAt };
            }
            return Task.FromResult(true);
        }

        public Task<CallInformation> Get(Guid transactionId)
        {
            lock (_cache) {
                if (!_cache.TryGetValue(transactionId, out var entry))
                    return Task.FromResult<CallInformation>(null);

                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > entry.ExpiresAt) {
                    _cache.Remove(transactionId);
                    return Task.FromResult<CallInformation>(null);
                }
                return Task.FromResult(entry.Ctx);
            }
        }
    }

    public class Transport
    {
        public static TransportSessionBuilder Session(string identifier)
        {
            return new TransportSessionBuilder(identifier);
        }
    }

    public class TransportSessionBuilder
    {
        private TransportSessionConfiguration config;

        public TransportSessionBuilder(string identifier)
        {
            config = new TransportSessionConfiguration
            {
                Locale = "en-GB",
                Interceptors = new TransportDispatcher(
                    identifier,
                    new InMemoryContextCache(),
                    false
                ),
                Serializer = GlobalSerializer.GetSerializer()
            };
        }

        public TransportSessionBuilder SetupOutboundContextCache(ContextCache cache)
        {
            config.Interceptors.ContextCache = cache;
            return this;
        }

        public TransportSessionBuilder AssignSerializer(TransportSerializer serializer)
        {
            config.Serializer = serializer;
            return this;
        }

        public TransportSessionBuilder Intercept<T, R>(OperationHandler<T, R> handler)
        {
            if (handler is RequestOperationHandler<T, R> reqHandler)
            {
                config.Interceptors.AddRequestHandler(
                    reqHandler.Operation.Id,
                    async (input, ctx) =>
                    {
                        T convertedInput = default(T);
                        if (input is JsonElement)
                            convertedInput = config.Serializer.Deserialize<T>(((JsonElement)input).GetRawText());
                        else
                            convertedInput = (T)input;
                        var result = await reqHandler.Handler(convertedInput, ctx);
                        return result.Convert<object>();
                    }
                );
            }
            else if (handler is MessageOperationHandler<T> msgHandler)
            {
                config.Interceptors.AddMessageHandler(
                    msgHandler.Operation.Id,
                    async (input, ctx) =>
                    {
                        T convertedInput = default(T);
                        if (input is JsonElement)
                            convertedInput = config.Serializer.Deserialize<T>(((JsonElement)input).GetRawText());
                        else
                            convertedInput = (T)input;
                        return await msgHandler.Handler(convertedInput, ctx);
                    }
                );
            }
            return this;
        }

        public TransportSessionBuilder InterceptPattern(string pattern, RequestInterceptor<object, object> handler)
        {
            config.Interceptors.AddPatternHandler(pattern, handler);
            return this;
        }

        public TransportSessionBuilder InspectRequest(RequestInspector inspector)
        {
            config.Interceptors.RequestsInspector = inspector;
            return this;
        }

        public TransportSessionBuilder InspectResponse(ResponseInspector inspector)
        {
            config.Interceptors.ResponsesInspector = inspector;
            return this;
        }

        public OutboundSessionBuilder OutboundSessionBuilder(string clientIdentifier)
        {
            return new OutboundSessionBuilder(clientIdentifier, config.Interceptors.ContextCache, config.Serializer);
        }

        public TransportSession Build()
        {
            return new TransportSession(config);
        }

        public TransportSessionBuilder OnLogMessage(TransportAbstractionLogger logger)
        {
            Logger.AssignLogger(logger);
            return this;
        }
    }

    public class OutboundSessionBuilder
    {
        private string serviceId;
        private TransportSessionConfiguration config;

        public OutboundSessionBuilder(string serviceId, ContextCache contextCache, TransportSerializer serializer)
        {
            this.serviceId = serviceId;
            config = new TransportSessionConfiguration
            {
                Locale = "en-GB",
                Interceptors = new TransportDispatcher(serviceId, contextCache, true),
                Serializer = serializer
            };
        }

        public OutboundSessionBuilder Intercept<T, R>(OperationHandler<T, R> handler)
        {
            if (handler is RequestOperationHandler<T, R> reqHandler)
            {
                config.Interceptors.AddRequestHandler(
                    reqHandler.Operation.Id,
                    async (input, ctx) =>
                    {
                        var result = await reqHandler.Handler.Invoke((T)input, ctx);
                        return result.Convert<object>();
                    }
                );
            }
            else if (handler is MessageOperationHandler<T> msgHandler)
            {
                config.Interceptors.AddMessageHandler(
                    msgHandler.Operation.Id,
                    async (input, ctx) =>
                    {
                        return await msgHandler.Handler.Invoke((T)input, ctx);
                    }
                );
            }
            return this;
        }

        public OutboundSessionBuilder InterceptPattern(string pattern, RequestInterceptor<object, object> handler)
        {
            config.Interceptors.AddPatternHandler(pattern, handler);
            return this;
        }

        public OutboundSessionBuilder InspectRequest(RequestInspector inspector)
        {
            config.Interceptors.RequestsInspector = inspector;
            return this;
        }

        public OutboundSessionBuilder InspectResponse(ResponseInspector inspector)
        {
            config.Interceptors.ResponsesInspector = inspector;
            return this;
        }

        public OutboundClientFactory Build()
        {
            return new OutboundClientFactory(serviceId, config);
        }
    }

    public class OutboundClientFactory
    {
        private string serviceId;
        private TransportSessionConfiguration config;

        public OutboundClientFactory(string serviceId, TransportSessionConfiguration config)
        {
            this.serviceId = serviceId;
            this.config = config;
        }

        public async Task<TransportClient> ForIncomingRequest(Guid transactionId)
        {
            return await new TransportSession(config, true)
                .CreateClient(serviceId)
                .WithTransactionId(transactionId);
        }

        public TransportClient AsIndependentRequests()
        {
            return new TransportSession(config)
                .CreateClient(serviceId);
        }
    }

    public interface TransportSerializer
    {
        string Serialize<T>(T obj);
        T Deserialize<T>(string serialized);
    }

    public class DefaultTransportSerializer : TransportSerializer
    {
        private static readonly System.Text.Json.JsonSerializerOptions CamelCaseOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        public string Serialize<T>(T obj)
        {
            return System.Text.Json.JsonSerializer.Serialize(obj, CamelCaseOptions);
        }

        public T Deserialize<T>(string serialized)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(serialized, CamelCaseOptions);
        }
    }

    public class TransportSession
    {
        private TransportSessionConfiguration config;
        private bool matchSessions;

        public TransportSession(TransportSessionConfiguration config, bool matchSessions = false)
        {
            this.config = config;
            this.matchSessions = matchSessions;
        }

        public TransportSession WithLocale(string locale)
        {
            config.Locale = locale;
            return this;
        }

        public Task<Result<object>> AcceptIncomingRequest(string json, List<Attribute> customAttributes = null)
        {
            return AcceptIncomingRequest(
                TransportRequest<object>.FromSerialized(config.Serializer, json),
                customAttributes
            );
        }

        public async Task<Result<object>> AcceptIncomingRequest(TransportRequest<object> tr, List<Attribute> customAttributes = null)
        {
            var ctx = TransportContext.From(tr);

            if (customAttributes != null)
            {
                foreach (var attribute in customAttributes)
                {
                    if (ctx.HasAttribute(attribute.Name))
                        continue;
                    ctx.Call.Attributes.Add(attribute);
                }
            }

            if (ctx.Operation.Type == "request")
            {
                var result = await config.Interceptors.HandleAsRequest(tr.RequestJson, ctx, matchSessions);
                return result.AssignSerializer(config.Serializer);
            }
            else
            {
                var result = await config.Interceptors.HandleAsMessage(tr.RequestJson, ctx, matchSessions);
                return result.AssignSerializer(config.Serializer);
            }
        }

        public TransportSerializer GetSerializer()
        {
            return config.Serializer ?? GlobalSerializer.GetSerializer();
        }

        public TransportClient CreateClient(string clientIdentifier, string dataTenant = null)
        {
            var info = CallInformation.New(config.Locale, dataTenant);
            return new TransportClient(clientIdentifier, config, info, matchSessions);
        }
    }

    public class TransportClient
    {
        private string clientIdentifier;
        private TransportSessionConfiguration config;
        private CallInformation callInfo;
        private bool matchSessions;

        public TransportClient(string clientIdentifier, TransportSessionConfiguration config, CallInformation callInformation, bool matchSessions = false)
        {
            this.clientIdentifier = clientIdentifier;
            this.config = config;
            this.callInfo = callInformation;
            this.matchSessions = matchSessions;
        }

        public async Task<TransportClient> WithTransactionId(Guid transactionId)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = await config.Interceptors.GetCallInfoFromCache(transactionId, callInfo, matchSessions);
            newCallInfo.TransactionId = transactionId;
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient WithLocale(string locale)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            newCallInfo.Locale = locale;
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient WithDataTenant(string tenant)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            newCallInfo.DataTenant = tenant;
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient WithCharacters(ICharacters characters)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            newCallInfo.Characters = characters;
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient AddAttribute<T>(string name, T value)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            var attr = newCallInfo.Attributes.Find(x => x.Name == name);
            if (attr != null)
                attr.Value = value;
            else
                newCallInfo.Attributes.Add(new Attribute(name, value));
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient RemoveAttribute(string name)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            newCallInfo.Attributes = newCallInfo.Attributes.Where(a => a.Name != name).ToList();
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient AddPathParam<T>(string name, T value)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            var param = newCallInfo.PathParams.Find(x => x.Name == name);
            if (param != null)
                param.Value = value;
            else
                newCallInfo.PathParams.Add(new Attribute(name, value));
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public TransportClient RemovePathParam(string name)
        {
            // Clone to avoid state sharing issues
            var newCallInfo = callInfo.Clone();
            newCallInfo.PathParams = newCallInfo.PathParams.Where(a => a.Name != name).ToList();
            return new TransportClient(clientIdentifier, config, newCallInfo, matchSessions);
        }

        public async Task<Result<R>> Request<T, R>(OperationRequest<T, R> call)
        {
            // Clone before sending to avoid state sharing issues
            var requestCallInfo = callInfo.Clone();
            if (requestCallInfo.TransactionId == Guid.Empty)
                requestCallInfo.TransactionId = Guid.NewGuid();

            var ctx = new TransportContext(
                call.AsOperationInformation(clientIdentifier),
                requestCallInfo,
                config.Serializer
            );

            if (call is RequestOperationRequest<T, R>)
            {
                var result = await config.Interceptors.HandleAsRequest((object)call.Input, ctx, matchSessions);
                return result.Convert<R>();
            }
            else
            {
                var result = await config.Interceptors.HandleAsMessage((object)call.Input, ctx, matchSessions);
                return result.Convert<R>();
            }
        }

        public async Task<Result<object>> AcceptOperationAsync(OutOfContextOperation operation, List<Attribute> customAttributes = null)
        {
            if (customAttributes == null)
                customAttributes = new List<Attribute>();

            OperationRequest<object, object> call = null;
            if (operation.OperationType == "request")
            {
                call = new RequestOperationRequest<object, object>(
                    operation.UsageId,
                    new TransportOperation<object, object>(
                        operation.OperationType,
                        operation.OperationId,
                        operation.OperationVerb,
                        new List<string>(),
                        new TransportOperationSettings { RequiresTenant = false, CharacterSetup = new TransportOperationCharacterSetup() }
                    ),
                    operation.RequestJson);
            }
            else
            {
                call = new MessageOperationRequest<object>(
                        operation.UsageId,
                        new TransportOperation<object, object>(
                            operation.OperationType,
                            operation.OperationId,
                            operation.OperationVerb,
                            new List<string>(),
                            new TransportOperationSettings { RequiresTenant = false, CharacterSetup = new TransportOperationCharacterSetup() }
                        ),
                        operation.RequestJson);
            }

            // Clone call info
            var requestCallInfo = this.callInfo.Clone();

            if (Guid.Empty == requestCallInfo.TransactionId)
                requestCallInfo.TransactionId = Guid.NewGuid();

            var ctx = new TransportContext(
                call.AsOperationInformation(this.clientIdentifier),
                requestCallInfo,
                this.config.Serializer
            );

            // Add path parameters from operation
            var operationPathParameters = operation.PathParameters?
                .Select(param => new Attribute(
                    param.Name,
                    param.Value
                ))
                .ToList() ?? new List<Attribute>();

            foreach (var param in operationPathParameters)
            {
                if (ctx.HasPathParameter(param.Name))
                    continue;

                ctx.Call.PathParams.Add(param);
            }

            // Append missing attributes
            foreach (var attribute in customAttributes)
            {
                if (ctx.HasAttribute(attribute.Name))
                    continue;

                ctx.Call.Attributes.Add(attribute);
            }

            // Handle based on operation type
            Result<object> result;

            if (ctx.Operation.Type == "request")
            {
                result = await this.config.Interceptors.HandleAsRequest(operation.RequestJson, ctx) as Result<object>;
            }
            else
            {
                result = await this.config.Interceptors.HandleAsMessage(operation.RequestJson, ctx) as Result<object>;
            }

            return result.AssignSerializer(this.config.Serializer);
        }
    }

    // You may need to implement or adjust the following types for your codebase:
    // - TransportSessionConfiguration
    // - CallInformation (with a Clone() method)
    // - Attribute
    // - ICharacters
    // - OperationRequest<T, R>
    // - RequestOperationRequest<T, R>
    // - TransportContext
    // - Result<R>
    // Delegate definitions for interceptors and inspectors
    public delegate Task<Result<R>> RequestInterceptor<T, R>(T input, TransportContext ctx);
    public delegate Task<Result<object>> MessageInterceptor<T>(T input, TransportContext ctx);

    public delegate Task<Result<object>> RequestInspector(object input, TransportContext ctx);
    public delegate Task<Result<object>> ResponseInspector(Result<object> result, object input, TransportContext ctx);

    public class Attribute
    {
        public string Name { get; set; }
        public object Value { get; set; }

        public Attribute(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    public class TransportRequest<T>
    {
        public string OperationId { get; }
        public string OperationVerb { get; }
        public string OperationType { get; }
        public string CallingClient { get; }
        public string UsageId { get; }
        public Guid TransactionId { get; }
        public string DataTenant { get; }
        public string Locale { get; }
        public Characters Characters { get; }
        public List<Attribute> Attributes { get; }
        public List<Attribute> PathParams { get; }
        public T RequestJson { get; }
        public string Raw { get; }
        public TransportSerializer Serializer { get; private set; }

        public TransportRequest(
            string operationId,
            string operationVerb,
            string operationType,
            string callingClient,
            string usageId,
            Guid transactionId,
            string dataTenant,
            string locale,
            Characters characters,
            List<Attribute> attributes,
            List<Attribute> pathParams,
            T requestJson,
            string raw = null)
        {
            OperationId = operationId;
            OperationVerb = operationVerb;
            OperationType = operationType;
            CallingClient = callingClient;
            UsageId = usageId;
            TransactionId = transactionId;
            DataTenant = dataTenant;
            Locale = locale;
            Characters = characters;
            Attributes = attributes;
            PathParams = pathParams;
            RequestJson = requestJson;
            Raw = raw;
        }

        public static TransportRequest<T> FromSerialized(TransportSerializer serializer, string serialized)
        {
            var req = new TransportRequest<T>(
                "", "", "", "", "", Guid.Empty, "", "",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                default,
                null
            ).AssignSerializer(serializer);

            return req.Deserialize<T>(serialized);
        }

        public static TransportRequest<T> From(T input, TransportContext ctx)
        {
            return new TransportRequest<T>(
                ctx.Operation.Id,
                ctx.Operation.Verb,
                ctx.Operation.Type,
                ctx.Operation.CallingClient,
                ctx.Operation.UsageId,
                ctx.Call.TransactionId == Guid.Empty ? Guid.NewGuid() : ctx.Call.TransactionId,
                ctx.Call.DataTenant ?? "",
                ctx.Call.Locale,
                new Characters(ctx.Call.Characters),
                ctx.Call.Attributes,
                ctx.Call.PathParams,
                input
            ).AssignSerializer(ctx.Serializer);
        }

        public TransportRequest<T> AssignSerializer(TransportSerializer serializer)
        {
            Serializer = serializer;
            return this;
        }

        public string Serialize()
        {
            if (Serializer == null)
                throw new Exception("No serializer assigned to TransportRequest");
            return Serializer.Serialize(this);
        }

        public TransportRequest<TOut> Deserialize<TOut>(string serialized)
        {
            if (Serializer == null)
                throw new Exception("No serializer assigned to TransportRequest");
            var deserialized = Serializer.Deserialize<TransportRequest<TOut>>(serialized);
            var newFromDeserialized = new TransportRequest<TOut>(
                deserialized.OperationId,
                deserialized.OperationVerb,
                deserialized.OperationType,
                deserialized.CallingClient,
                deserialized.UsageId,
                deserialized.TransactionId,
                deserialized.DataTenant,
                deserialized.Locale,
                deserialized.Characters,
                deserialized.Attributes,
                deserialized.PathParams,
                deserialized.RequestJson,
                serialized
            );
            newFromDeserialized.AssignSerializer(Serializer);
            return newFromDeserialized;
        }
    }

    public class OperationInformation
    {
        public string Id { get; }
        public string Verb { get; }
        public string Type { get; }
        public string CallingClient { get; }
        public string UsageId { get; }

        public OperationInformation(string id, string verb, string type, string callingClient, string usageId)
        {
            Id = id;
            Verb = verb;
            Type = type;
            CallingClient = callingClient;
            UsageId = usageId;
        }
    }

    public class CallInformation
    {
        public string Locale { get; set; }
        public string DataTenant { get; set; }
        public ICharacters Characters { get; set; }
        public List<Attribute> Attributes { get; set; }
        public List<Attribute> PathParams { get; set; }
        public Guid TransactionId { get; set; }

        public CallInformation(
            string locale,
            string dataTenant,
            ICharacters characters,
            List<Attribute> attributes,
            List<Attribute> pathParams,
            Guid transactionId)
        {
            Locale = locale;
            DataTenant = dataTenant;
            Characters = characters;
            Attributes = attributes;
            PathParams = pathParams;
            TransactionId = transactionId;
        }

        public static CallInformation New(string locale, string dataTenant = null, Guid? transactionId = null)
        {
            return new CallInformation(
                locale,
                dataTenant ?? "",
                new CharacterMetaValues(),
                new List<Attribute>(),
                new List<Attribute>(),
                transactionId == null ? Guid.NewGuid() : (Guid)transactionId
            );
        }

        public CallInformation Clone()
        {
            return new CallInformation(
                Locale,
                DataTenant,
                Characters is CharacterMetaValues charMeta ? new CharacterMetaValues
                {
                    Subject = charMeta.Subject != null ? new Identifier(charMeta.Subject.Type, charMeta.Subject.Id) : null,
                    Responsible = charMeta.Responsible != null ? new Identifier(charMeta.Responsible.Type, charMeta.Responsible.Id) : null,
                    Performer = charMeta.Performer != null ? new Identifier(charMeta.Performer.Type, charMeta.Performer.Id) : null,
                    Timestamp = charMeta.Timestamp
                } : Characters,
                Attributes.Select(a => new Attribute(a.Name, a.Value)).ToList(),
                PathParams.Select(p => new Attribute(p.Name, p.Value)).ToList(),
                TransactionId
            );
        }
    }

    class AttributeDeserializer
    {
        public static T SafeExtract<T>(object value, TransportSerializer serializer = null)
        {
            if (typeof(T) == typeof(Guid) && value is string strValue) {
                return (T)(object)Guid.Parse(strValue);
            } else if (value is JsonValue jsonValue) {
                var raw = jsonValue.ToJsonString();
                return serializer.Deserialize<T>(raw);
            } else if (value is JsonElement jsonElement) {
                if (serializer == null)
                    throw new Exception("Serializer required to extract from JsonElement");
                return serializer.Deserialize<T>(jsonElement.GetRawText());
            }
            return (T)value;
        }
    }

    public class TransportContext
    {
        public OperationInformation Operation { get; }
        public CallInformation Call { get; }
        public TransportSerializer Serializer { get; }

        public TransportContext(OperationInformation operation, CallInformation call, TransportSerializer serializer)
        {
            Operation = operation;
            Call = call;
            Serializer = serializer;
        }

        public bool HasAttribute(string name)
        {
            return Call.Attributes.Exists(item => item.Name == name);
        }

        public T GetAttribute<T>(string name)
        {
            var item = Call.Attributes.Find(i => i.Name == name) as Attribute;
            if (item == null)
                throw new Exception("Missing attribute, use HasAttribute first if you are unsure if it exists");
            return AttributeDeserializer.SafeExtract<T>(item.Value, Serializer);
        }

        public bool HasPathParameter(string name)
        {
            return Call.PathParams.Exists(item => item.Name == name);
        }

        public T GetPathParameter<T>(string name)
        {
            var item = Call.PathParams.Find(i => i.Name == name);
            if (item == null)
                throw new Exception("Missing path param, use HasPathParam first if you are unsure if it exists");
            return AttributeDeserializer.SafeExtract<T>(item.Value, Serializer);
        }

        public static TransportContext From(TransportRequest<object> gatewayRequest)
        {
            if (gatewayRequest.Serializer == null)
                throw new Exception("Serializer required to convert from gateway request");
            return new TransportContext(
                new OperationInformation(
                    gatewayRequest.OperationId,
                    gatewayRequest.OperationVerb,
                    gatewayRequest.OperationType,
                    gatewayRequest.CallingClient,
                    gatewayRequest.UsageId
                ),
                new CallInformation(
                    gatewayRequest.Locale,
                    gatewayRequest.DataTenant,
                    gatewayRequest.Characters,
                    gatewayRequest.Attributes,
                    gatewayRequest.PathParams,
                    gatewayRequest.TransactionId
                ),
                gatewayRequest.Serializer
            );
        }

        public Result<T> DeserializeResult<T>(string data)
        {
            var result = this.Serializer.Deserialize<Result<T>>(data);
            result.AssignSerializer(this.Serializer);
            return result;
        }

        public string SerializeRequest<T>(T input)
        {
            return TransportRequest<T>.From(input, this).Serialize();
        }
    }

    public class Result : Result<object>
    {
    }

    // Result<T> class
    public class Result<T>
    {
        public T Value { get; set; }
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public Metavalues Meta { get; set; }
        public TransportError Error { get; set; }
        private TransportSerializer serializer;

        public Result()
        {
            Meta = new Metavalues();
        }

        public Result(Result<T> other)
        {
            Value = other.Value;
            StatusCode = other.StatusCode != 0 ? other.StatusCode : (other.Error != null ? 500 : 200);
            Success = other.Success || IsStatusCodeSuccess(StatusCode);
            Meta = other.Meta == null ? new Metavalues() : other.Meta;
            Error = other.Error ?? (!IsStatusCodeSuccess(StatusCode) ? new TransportError(StatusCode.ToString()) : null);
        }

        private bool IsStatusCodeSuccess(int statusCode)
        {
            return statusCode >= 200 && statusCode <= 308;
        }

        public bool IsSuccess() => Success;
        public bool HasError() => Error != null;

        public Result<T> AssignSerializer(TransportSerializer serializer)
        {
            this.serializer = serializer;
            return this;
        }

        public string Serialize()
        {
            if (serializer == null)
                throw new System.Exception("No serializer assigned to Result");
            // Implement serialization logic as needed
            return serializer.Serialize(this);
        }

        public Result<TOut> Deserialize<TOut>(string serialized)
        {
            if (serializer == null)
                throw new System.Exception("No serializer assigned to Result");
            var deserialized = serializer.Deserialize<Result<TOut>>(serialized);
            return deserialized.AssignSerializer(serializer);
        }

        public static Result<object> Ok()
        {
            return new Result<object>
            {
                Success = true,
                Value = null,
                StatusCode = 200,
                Meta = new Metavalues()
            };
        }

        public static Result<V> Ok<V>(V value = default, Metavalues meta = null)
        {
            return new Result<V>
            {
                Success = true,
                Value = value,
                Meta = meta == null ? new Metavalues() : meta,
                StatusCode = 200
            };
        }

        public static Result<object> OkStatus(int code)
        {
            return new Result<object>
            {
                Success = true,
                Value = null,
                StatusCode = code
            };
        }

        public static Result<T> NotFound(string errorCode, string technicalError = null, string userError = null)
        {
            return Failed(404, errorCode, technicalError, userError);
        }

        public static Result<T> BadRequest(string errorCode, string technicalError = null, string userError = null)
        {
            return Failed(400, errorCode, technicalError, userError);
        }

        public static Result<T> InternalServerError(string errorCode, string technicalError = null, string userError = null)
        {
            return Failed(500, errorCode, technicalError, userError);
        }

        public static Result<T> Failed(int statusCode, string errorCode, string technicalError = null, string userError = null)
        {
            return new Result<T>
            {
                Value = default,
                StatusCode = statusCode,
                Success = false,
                Error = new TransportError(
                    errorCode,
                    new TransportErrorDetails
                    {
                        TechnicalError = technicalError ?? "",
                        SessionIdentifier = "",
                        UserError = userError ?? ""
                    }
                )
            };
        }

        public Result<T> SetMeta(Metavalues value)
        {
            Meta = value;
            return this;
        }

        public Result<T> WithMeta(Action<Metavalues> meta)
        {
            if (Meta == null)
                Meta = new Metavalues();
            meta(Meta);
            return this;
        }

        public Result<T> AddMetaValue(Metavalue value)
        {
            if (Meta == null)
                Meta = new Metavalues();
            Meta.Add(value);
            return this;
        }

        public Result<T> AddMetaValues(IEnumerable<Metavalue> values)
        {
            if (Meta == null)
                Meta = new Metavalues();
            Meta.Add(values);
            return this;
        }

        public Result ConvertToEmpty()
        {
            return new Result
            {
                Success = Success,
                StatusCode = StatusCode,
                Meta = Meta,
                Error = Error
            };
        }

        public Result<TOut> Convert<TOut>()
        {
            try
            {
                if (Error != null)
                {
                    return new Result<TOut>
                    {
                        Success = Success,
                        Value = default(TOut),
                        StatusCode = StatusCode,
                        Meta = Meta,
                        Error = Error
                    };
                }

                if (Value != null && Value.GetType().Equals(typeof(JsonElement))) {
                    var jsonElement = (JsonElement)(object)Value;
                    var srlzr = serializer;
                    if (srlzr == null)
                        srlzr = GlobalSerializer.GetSerializer();
                    TOut jsonConverted = srlzr.Deserialize<TOut>(jsonElement.GetRawText());
                    return new Result<TOut>
                    {
                        Success = Success,
                        Value = jsonConverted,
                        StatusCode = StatusCode,
                        Meta = Meta,
                        Error = Error
                    };
                } else if (Value != null && Value.GetType().Equals(typeof(JsonValue))) {
                    var jsonValue = (JsonValue)(object)Value;
                    var srlzr = serializer;
                    if (srlzr == null)
                        srlzr = GlobalSerializer.GetSerializer();
                    TOut jsonConverted = srlzr.Deserialize<TOut>(jsonValue.ToJsonString());
                    return new Result<TOut>
                    {
                        Success = Success,
                        Value = jsonConverted,
                        StatusCode = StatusCode,
                        Meta = Meta,
                        Error = Error
                    };
                } 

                TOut value = default(TOut);
                if (Value != null)
                    value = (TOut)(object)Value;
                return new Result<TOut>
                {
                    Success = Success,
                    Value = value,
                    StatusCode = StatusCode,
                    Meta = Meta,
                    Error = Error
                };
            }
            catch (System.Exception e)
            {
                return Result<TOut>.Failed(
                    500,
                    "TransportAbstraction.Serialization.DeserializeError",
                    e.Message + ": " + e.GetType().Name + (e.StackTrace != null ? "\n" + e.StackTrace : ""));
            }
        }

        public Result<TOut> Maybe<TOut>(System.Func<T, Metavalues, Result<TOut>> onSuccess, bool throwErrors = false)
        {
            if (throwErrors)
            {
                if (!Success)
                    return Convert<TOut>();
                return onSuccess(Value, Meta);
            }
            try
            {
                if (!Success)
                    return Convert<TOut>();
                return onSuccess(Value, Meta);
            }
            catch (System.Exception e)
            {
                return MaybeError<TOut>(e);
            }
        }

        public Result<T> MaybePassThrough(System.Func<T, Metavalues, Result<object>> onSuccess, bool throwErrors = false)
        {
            if (throwErrors)
            {
                if (!Success)
                    return this;
                var result = onSuccess(Value, Meta);
                if (!result.Success)
                    return result.Convert<T>();
                return this;
            }
            try
            {
                if (!Success)
                    return this;
                var result = onSuccess(Value, Meta);
                if (!result.Success)
                    return result.Convert<T>();
                return this;
            }
            catch (System.Exception e)
            {
                return MaybeError<T>(e);
            }
        }

        public async Task<Result<TOut>> MaybeAsync<TOut>(System.Func<T, Metavalues, Task<Result<TOut>>> onSuccess, bool throwErrors = false)
        {
            if (throwErrors)
            {
                if (!Success)
                    return Convert<TOut>();
                return await onSuccess(Value, Meta);
            }
            try
            {
                if (!Success)
                    return Convert<TOut>();
                return await onSuccess(Value, Meta);
            }
            catch (System.Exception e)
            {
                return MaybeError<TOut>(e);
            }
        }

        public async Task<Result<T>> MaybePassThroughAsync(System.Func<T, Metavalues, Task<Result<object>>> onSuccess, bool throwErrors = false)
        {
            if (throwErrors)
            {
                if (!Success)
                    return this;
                var result = await onSuccess(Value, Meta);
                if (!result.Success)
                    return result.Convert<T>();
                return this;
            }
            try
            {
                if (!Success)
                    return this;
                var result = await onSuccess(Value, Meta);
                if (!result.Success)
                    return result.Convert<T>();
                return this;
            }
            catch (System.Exception e)
            {
                return MaybeError<T>(e);
            }
        }

        private Result<TOut> MaybeError<TOut>(System.Exception e)
        {
            if (e != null)
            {
                Logger.Error("MaybeException: " + e.Message);
                return Result<TOut>.Failed(500, "TransportAbstraction.MaybeException", e.Message + ": " + e.GetType().Name + (e.StackTrace != null ? "\n" + e.StackTrace : ""));
            }
            Logger.Error("MaybeException: Unknown error");
            return Result<TOut>.Failed(500, "TransportAbstraction.MaybeException", "Unknown error");
        }
    }

    public class Metavalues
    {
        public bool HasMoreValues { get; set; }
        public List<Metavalue> Values { get; set; } = new List<Metavalue>();
        public int? TotalValueCount { get; set; }
        public List<Attribute> Attributes { get; set; } = new List<Attribute>();

        public Metavalues() { }

        public bool HasMetaValue(string valueId)
        {
            return Values.Exists(i => i.ValueId == valueId);
        }

        public Metavalue GetMetaValue(string valueId)
        {
            return Values.Find(i => i.ValueId == valueId);
        }

        public Metavalues SetHasMoreValues(bool? hasMore = null)
        {
            HasMoreValues = hasMore ?? true;
            return this;
        }

        public Metavalues SetTotalValueCount(int? count = null)
        {
            TotalValueCount = count;
            return this;
        }

        public Metavalues Add(Metavalue value)
        {
            Values.Add(value);
            return this;
        }

        public Metavalues Add(IEnumerable<Metavalue> values)
        {
            Values.AddRange(values);
            return this;
        }

        public Metavalues WithAttribute(string name, object value)
        {
            if (Attributes == null)
                Attributes = new List<Attribute>();
            var attr = Attributes.FirstOrDefault(a => a.Name == name);
            if (attr != null)
                attr.Value = value;
            else
                Attributes.Add(new Attribute(name, value));
            return this;
        }

        public bool HasAttribute(string name)
        {
            return Attributes != null && Attributes.Any(a => a.Name == name);
        }

        public T GetAttribute<T>(string name)
        {
            if (Attributes == null)
                return default;
            var item = Attributes.FirstOrDefault(a => a.Name == name);
            return item != null ? AttributeDeserializer.SafeExtract<T>(item.Value, GlobalSerializer.GetSerializer()) : default;
        }
    }

    public class Metavalue
    {
        public string ValueId { get; set; }
        public string DataTenant { get; set; }
        public CharacterMetaValues InitialCharacters { get; set; }
        public CharacterMetaValues CurrentCharacters { get; set; }
        public List<Attribute> Attributes { get; set; } = new List<Attribute>();

        public bool KnowsInitialCharacters() => InitialCharacters != null;
        public bool KnowsCurrentCharacters() => CurrentCharacters != null;

        public Metavalue WithInitialCharacters(CharacterMetaValues characters)
        {
            InitialCharacters = characters;
            return this;
        }

        public Metavalue WithCurrentCharacters(CharacterMetaValues characters)
        {
            CurrentCharacters = characters;
            return this;
        }

        public Metavalue WithAttribute(string name, object value) {
            var attr = this.Attributes.FirstOrDefault(a => a.Name == name);
            if (attr != null)
                attr.Value = value;
            else
                this.Attributes.Add(new Attribute(name, value));
            return this;
        }

        public bool HasAttribute(string name)
        {
            return this.Attributes.Any(a => a.Name == name);
        }

        public T GetAttribute<T>(string name)
        {
            var item = this.Attributes.FirstOrDefault(a => a.Name == name);
            return item != null ? AttributeDeserializer.SafeExtract<T>(item.Value, GlobalSerializer.GetSerializer()) : default;
        }

        public static Metavalue WithAttribute(string valueId, string name, object value)
        {
            var ret = new Metavalue();
            ret.ValueId = valueId;
            ret.DataTenant = null;
            ret.WithAttribute(name, value);
            return ret;
        }

        public static Metavalue With(
            string valueId,
            string dataTenant = null,
            Identifier initialPerformer = null,
            System.DateTime? createdAt = null,
            Identifier currentPerformer = null,
            System.DateTime? updatedAt = null)
        {
            var ret = new Metavalue()
                .WithInitialCharacters(CharacterMetaValues.FromPerformer(initialPerformer).WithTimestamp(createdAt))
                .WithCurrentCharacters(CharacterMetaValues.FromPerformer(currentPerformer).WithTimestamp(updatedAt));
            ret.ValueId = valueId;
            ret.DataTenant = dataTenant;
            return ret;
        }
    }

    public class Identifier
    {
        public string Type { get; set; }
        public string Id { get; set; }

        public Identifier() { }
        public Identifier(string type, string id)
        {
            Type = type;
            Id = id;
        }
    }

    public class CharacterMetaValues : ICharacters
    {
        public Identifier Subject { get; set; }
        public Identifier Responsible { get; set; }
        public Identifier Performer { get; set; }
        public System.DateTime? Timestamp { get; set; }

        public bool HasSubject() => Subject != null;
        public bool HasResponsible() => Responsible != null;
        public bool HasPerformer() => Performer != null;
        public bool HasTimestamp() => Timestamp != null;

        public static CharacterMetaValues FromSubject(Identifier subjectOrTerm)
        {
            return new CharacterMetaValues().WithSubject(subjectOrTerm);
        }

        public static CharacterMetaValues FromSubject(string terminology, string id)
        {
            return new CharacterMetaValues().WithSubject(terminology, id);
        }

        public static CharacterMetaValues FromResponsible(Identifier responsibleOrTerm)
        {
            return new CharacterMetaValues().WithResponsible(responsibleOrTerm);
        }

        public static CharacterMetaValues FromResponsible(string terminology, string id)
        {
            return new CharacterMetaValues().WithResponsible(terminology, id);
        }

        public static CharacterMetaValues FromPerformer(Identifier performerOrTerm)
        {
            return new CharacterMetaValues().WithPerformer(performerOrTerm);
        }

        public static CharacterMetaValues FromPerformer(string terminology, string id)
        {
            return new CharacterMetaValues().WithPerformer(terminology, id);
        }

        public static CharacterMetaValues FromTimestamp(System.DateTime? timestamp)
        {
            return new CharacterMetaValues().WithTimestamp(timestamp);
        }

        public CharacterMetaValues WithSubject(Identifier subject)
        {
            Subject = subject;
            return this;
        }

        public CharacterMetaValues WithSubject(string terminology, string id)
        {
            Subject = new Identifier(terminology, id);
            return this;
        }

        public CharacterMetaValues WithResponsible(Identifier responsible)
        {
            Responsible = responsible;
            return this;
        }

        public CharacterMetaValues WithResponsible(string terminology, string id)
        {
            Responsible = new Identifier(terminology, id);
            return this;
        }

        public CharacterMetaValues WithPerformer(Identifier performer)
        {
            Performer = performer;
            return this;
        }

        public CharacterMetaValues WithPerformer(string terminology, string id)
        {
            Performer = new Identifier(terminology, id);
            return this;
        }

        public CharacterMetaValues WithTimestamp(System.DateTime? timestamp)
        {
            Timestamp = timestamp;
            return this;
        }
    }

    public class Characters : ICharacters
    {
        public Characters()
        {

        }

        public Characters(ICharacters characters)
        {
            Performer = characters.Performer;
            Responsible = characters.Responsible;
            Subject = characters.Subject;
        }

        public Identifier Subject { get; set; }
        public Identifier Responsible { get; set; }
        public Identifier Performer { get; set; }
    }

    public interface ICharacters
    {
        Identifier Subject { get; set; }
        Identifier Responsible { get; set; }
        Identifier Performer { get; set; }
    }

    public class TransportErrorDetails
    {
        public string TechnicalError { get; set; }
        public string UserError { get; set; }
        public string SessionIdentifier { get; set; }
        public string CallingClient { get; set; }
        public string CalledOperation { get; set; }
        public string TransactionId { get; set; }
    }

    public class TransportError
    {
        public string Code { get; set; }
        public TransportErrorDetails Details { get; set; }
        public List<TransportError> Related { get; set; }
        public TransportError Parent { get; set; }

        public TransportError() { }

        public TransportError(string code, TransportErrorDetails details = null, List<TransportError> related = null, TransportError parent = null)
        {
            Code = code;
            Details = details;
            Related = related ?? new List<TransportError>();
            Parent = parent;
        }

        public TransportError(string code, string details)
            : this(code, new TransportErrorDetails { TechnicalError = details }) { }

        public string ToShortString()
        {
            var tech = Details?.TechnicalError;
            if (string.IsNullOrWhiteSpace(tech))
                return $"{Code}";
            return $"{Code} - {tech}";
        }

        public override string ToString()
        {
            var sb = ToShortString();
            if (Related == null || Related.Count == 0)
                return sb;
            sb += "\n    Related errors:";
            foreach (var r in Related)
                sb += $"\n        {r.ToShortString()}";
            return sb;
        }

        public string ToLongString()
        {
            var sb = "";
            if (Details != null)
            {
                if (!string.IsNullOrEmpty(Details.TransactionId))
                    sb += $"\nTransactionId: {Details.TransactionId}";
                if (!string.IsNullOrEmpty(Details.CalledOperation))
                    sb += $"\nOperation: {Details.CalledOperation}";
                if (!string.IsNullOrEmpty(Details.SessionIdentifier))
                    sb += $"\nSession: {Details.SessionIdentifier}";
                if (!string.IsNullOrEmpty(Details.CallingClient))
                    sb += $"\nClient: {Details.CallingClient}";
            }
            sb += $"\n{ToString()}";
            if (Parent == null)
                return sb.TrimStart('\n');
            sb += "\nParent error:";
            var parentStr = Parent.ToLongString();
            parentStr = string.Join("\n", parentStr.Split('\n').Select(line => line.Length > 0 ? "   " + line : line));
            sb += $"\n{parentStr}";
            return sb.TrimStart('\n');
        }
    }

    public class TransportSessionConfiguration
    {
        public string Locale { get; set; }
        public TransportDispatcher Interceptors { get; set; }
        public TransportSerializer Serializer { get; set; }
    }

    public class TransportDispatcher
    {
        private bool sortPatterns;
        private List<string> sortedPatterns = new List<string>();

        public RequestInspector RequestsInspector { get; set; }
        public ResponseInspector ResponsesInspector { get; set; }

        private readonly Dictionary<string, RequestInterceptor<object, object>> requestHandlers = new Dictionary<string, RequestInterceptor<object, object>>();
        private readonly Dictionary<string, MessageInterceptor<object>> messageHandlers = new Dictionary<string, MessageInterceptor<object>>();
        private readonly Dictionary<string, RequestInterceptor<object, object>> patternHandlers = new Dictionary<string, RequestInterceptor<object, object>>();

        public string SessionIdentifier { get; }
        public ContextCache ContextCache { get; set; }
        public bool CacheReads { get; }

        public TransportDispatcher(
            string sessionIdentifier,
            ContextCache contextCache,
            bool cacheReads)
        {
            SessionIdentifier = sessionIdentifier;
            ContextCache = contextCache;
            CacheReads = cacheReads;
            sortPatterns = false;
        }
        public void AddRequestHandler(string operationId, RequestInterceptor<object, object> handler)
        {
            ValidateUniqueHandler(operationId);
            requestHandlers[operationId] = handler;
        }

        public void AddMessageHandler(string operationId, MessageInterceptor<object> handler)
        {
            ValidateUniqueHandler(operationId);
            messageHandlers[operationId] = handler;
        }

        public void AddPatternHandler(string pattern, RequestInterceptor<object, object> handler)
        {
            ValidateUniqueHandler(pattern);
            patternHandlers[pattern] = handler;
            sortPatterns = true;
        }

        public async Task<Result<object>> RouteFromGatewayRequest(object input, TransportContext ctx)
        {
            if (ctx.Operation.Type == "request")
                return await HandleAsRequest(input, ctx);
            else
                return (await HandleAsMessage(input, ctx)).ConvertToEmpty();
        }

        public async Task<Result<object>> HandleAsMessage(object input, TransportContext ctx, bool matchSessions = false)
        {
            var inspectionResult = await InspectRequest(input, ctx);
            var cacheResult = await HandleCache(ctx, matchSessions);
            if (!cacheResult.Success)
                return cacheResult.Convert<object>();
            if (inspectionResult != null)
                return inspectionResult;
            if (messageHandlers.ContainsKey(cacheResult.Value.Operation.Id))
            {
                if (messageHandlers.TryGetValue(cacheResult.Value.Operation.Id, out var handler))
                    return await RunMessageHandler(handler, input, cacheResult.Value);
            }
            return await RunPatternHandler(input, cacheResult.Value);
        }

        public async Task<Result<object>> HandleAsRequest(object input, TransportContext ctx, bool matchSessions = false)
        {
            var inspectionResult = await InspectRequest(input, ctx);
            if (inspectionResult != null)
                return inspectionResult;
            var cacheResult = await HandleCache(ctx, matchSessions);
            if (!cacheResult.Success)
                return cacheResult.Convert<object>();
            if (requestHandlers.ContainsKey(cacheResult.Value.Operation.Id))
            {
                if (requestHandlers.TryGetValue(cacheResult.Value.Operation.Id, out var handler))
                    return await RunRequestHandler(handler, input, cacheResult.Value);
            }
            return await RunPatternHandler(input, cacheResult.Value);
        }

        private void ValidateUniqueHandler(string id)
        {
            if (requestHandlers.ContainsKey(id) ||
                messageHandlers.ContainsKey(id) ||
                patternHandlers.ContainsKey(id))
            {
                throw new Exception("The path " + id + " already has a handler");
            }
        }

        private async Task<Result<TransportContext>> HandleCache(TransportContext ctx, bool matchSessions)
        {
            if (CacheReads)
                return Result<TransportContext>.Ok(ctx);

            try
            {
                var result = await ContextCache.Put(ctx.Call.TransactionId, ctx.Call);
                if (!result)
                    return Result<TransportContext>.Failed(500, "TransportAbstraction.ContextCachePersistance", "The incoming context could not be persisted for transaction " + ctx.Call.TransactionId);
                return Result<TransportContext>.Ok(ctx);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return GenericError<TransportContext>(e);
            }
        }

        public async Task<CallInformation> GetCallInfoFromCache(Guid newTransactionId, CallInformation callInfo, bool matchSessions)
        {
            if (!CacheReads)
                return callInfo;
            if (!matchSessions)
                return callInfo;

            try
            {
                var result = await ContextCache.Get(newTransactionId);
                if (result == null)
                {
                    Logger.Error("Failed to read context cache for record " + callInfo.TransactionId);
                    return callInfo;
                }
                return result;
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return callInfo;
            }
        }

        private async Task<Result<object>> RunPatternHandler(object input, TransportContext ctx)
        {
            var matchingPattern = FindMatchingPattern(ctx.Operation.Id);
            if (matchingPattern != null)
            {
                if (patternHandlers.TryGetValue(matchingPattern, out var patternHandler))
                    return await RunRequestHandler(patternHandler, input, ctx);
                return await InspectResponse(HandlerNotFound(ctx.Operation.Id).ConvertToEmpty(), input, ctx);
            }
            return await InspectResponse(HandlerNotFound(ctx.Operation.Id).ConvertToEmpty(), input, ctx);
        }

        private async Task<Result<object>> RunMessageHandler(MessageInterceptor<object> handler, object input, TransportContext ctx)
        {
            Result<object> result;
            try
            {
                result = (await handler(input, ctx)).Convert<object>();
            }
            catch (Exception e)
            {
                result = GenericError<object>(e);
            }
            return await InspectResponse(result, input, ctx);
        }

        private async Task<Result<object>> RunRequestHandler(RequestInterceptor<object, object> handler, object input, TransportContext ctx)
        {
            Result<object> result;
            try
            {
                result = await handler(input, ctx);
            }
            catch (Exception e)
            {
                result = GenericError<object>(e);
            }
            return await InspectResponse(result, input, ctx);
        }

        private string FindMatchingPattern(string featureId)
        {
            if (sortPatterns)
                ReSortPatterns();
            foreach (var key in sortedPatterns)
            {
                if (featureId.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    return key;
            }
            return null;
        }

        private void ReSortPatterns()
        {
            var keys = patternHandlers.Keys.ToList();
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            sortedPatterns = keys;
            sortPatterns = false;
        }

        private async Task<Result<object>> InspectRequest(object cinput, TransportContext ctx)
        {
            if (RequestsInspector == null)
                return null;

            try
            {
                return await RequestsInspector(cinput, ctx);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return null;
            }
        }

        public async Task<Result<object>> InspectMessageResponse(Result<object> result, object cinput, TransportContext ctx)
        {
            if (ResponsesInspector == null)
                return result;

            try
            {
                return await ResponsesInspector(result, cinput, ctx);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
            }

            return result;
        }

        public async Task<Result<object>> InspectResponse(Result<object> result, object cinput, TransportContext ctx)
        {
            if (ResponsesInspector == null)
                return result;

            try
            {
                return await ResponsesInspector(result, cinput, ctx);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
            }
            return result;
        }

        private Result<object> HandlerNotFound(string operationId)
        {
            return Result<object>.BadRequest("TransportAbstraction.HandlerNotFound", "There are no matching handlers for the operation: " + operationId);
        }

        private Result<T> GenericError<T>(Exception e)
        {
            if (e != null)
            {
                return Result<T>.Failed(500, "TransportAbstraction.UnhandledError", $"{e.Message}: {e.GetType().Name}\n{e.StackTrace}");
            }
            return Result<T>.Failed(500, "TransportAbstraction.UnhandledError", "Unknown error");
        }
    }

    public enum LogLevel
    {
        Fatal = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
        Trace = 5
    }

    public class LogMessage
    {
        public string Source { get; }
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Message { get; }
        public Exception Error { get; }

        public LogMessage(string source, DateTime timestamp, LogLevel level, string message, Exception error = null)
        {
            Source = source;
            Timestamp = timestamp;
            Level = level;
            Message = message;
            Error = error;
        }

        public bool IsWithin(LogLevel level)
        {
            return this.Level <= level;
        }

        public override string ToString()
        {
            var s = $"{Timestamp:HH:mm:ss.fff} {Level} - {Message}\n";
            if (Error != null)
            {
                s += $"{Timestamp:HH:mm:ss.fff} {Level} - {Error.Message}\n";
            }
            return s;
        }

        public string ToJson()
        {
            return ToString();
        }
    }

    public interface TransportAbstractionLogger
    {
        LogLevel LogLevel { get; set; }
        void Write(LogMessage message);
    }

    public class DefaultLogger : TransportAbstractionLogger
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Debug;

        public void Write(LogMessage message)
        {
            if (message.IsWithin(LogLevel))
            {
                Console.WriteLine(message.ToString());
            }
        }
    }

    public static class Logger
    {
        private static string _source = string.Empty;
        private static TransportAbstractionLogger _logger = new DefaultLogger();

        public static void AssignLogger(TransportAbstractionLogger logger)
        {
            _logger = logger;
        }

        public static void UpdateSource(string source)
        {
            _source = source;
        }

        public static void Write(string message, LogLevel level, Exception error = null)
        {
            if (_logger == null) throw new Exception("Logger has not been assigned");
            _logger.Write(new LogMessage(_source, DateTime.Now, level, message, error));
        }

        public static void Trace(string message, Exception error = null) => Write(message, LogLevel.Trace, error);
        public static void Info(string message, Exception error = null) => Write(message, LogLevel.Info, error);
        public static void Debug(string message, Exception error = null) => Write(message, LogLevel.Debug, error);
        public static void Warning(string message, Exception error = null) => Write(message, LogLevel.Warning, error);
        public static void Error(string message, Exception error = null) => Write(message, LogLevel.Error, error);
        public static void Fatal(string message, Exception error = null) => Write(message, LogLevel.Fatal, error);
    }

    public class TransportOperationCharacter
    {
        public bool Required { get; set; }
        public List<string> ValidTypes { get; set; }
    }

    public class TransportOperationCharacterSetup
    {
        public TransportOperationCharacter Performer { get; set; }
        public TransportOperationCharacter Responsible { get; set; }
        public TransportOperationCharacter Subject { get; set; }
    }

    public class TransportOperationSettings
    {
        public bool RequiresTenant { get; set; }
        public TransportOperationCharacterSetup CharacterSetup { get; set; }
    }

    public class TransportOperation<T, R>
    {
        public string Id { get; }
        public string Type { get; }
        public string Verb { get; }
        public List<string> PathParameters { get; }
        public TransportOperationSettings Settings { get; }

        public TransportOperation(string type, string id, string verb, List<string> pathParameters = null, TransportOperationSettings settings = null)
        {
            Id = id;
            Type = type;
            Verb = verb;
            PathParameters = pathParameters;
            Settings = settings;
        }
    }

    public abstract class OperationHandler<T, R>
    {
        public TransportOperation<T, R> Operation { get; }

        protected OperationHandler(TransportOperation<T, R> operation)
        {
            Operation = operation;
        }
    }

    public class RequestOperationHandler<T, R> : OperationHandler<T, R>
    {
        public Func<T, TransportContext, System.Threading.Tasks.Task<Result<R>>> Handler { get; }

        public RequestOperationHandler(RequestOperation<T, R> operation, Func<T, TransportContext, System.Threading.Tasks.Task<Result<R>>> handler)
            : base(operation)
        {
            Handler = handler;
        }
    }

    public class MessageOperationHandler<T> : OperationHandler<T, object>
    {
        public Func<T, TransportContext, System.Threading.Tasks.Task<Result<object>>> Handler { get; }

        public MessageOperationHandler(MessageOperation<T> operation, Func<T, TransportContext, System.Threading.Tasks.Task<Result<object>>> handler)
            : base(operation)
        {
            Handler = handler;
        }
    }

    public abstract class OperationRequest<T, R>
    {
        public string UsageId { get; }
        public TransportOperation<T, R> Operation { get; }
        public T Input { get; }

        protected OperationRequest(string usageId, TransportOperation<T, R> operation, T input)
        {
            UsageId = usageId;
            Operation = operation;
            Input = input;
        }

        public OperationInformation AsOperationInformation(string callingClient)
        {
            return new OperationInformation(
                Operation.Id,
                Operation.Verb,
                Operation.Type,
                callingClient,
                UsageId
            );
        }
    }

    public class RequestOperationRequest<T, R> : OperationRequest<T, R>
    {
        public RequestOperationRequest(string usageId, TransportOperation<T, R> operation, T input) : base(usageId, operation, input) { }
    }

    public class MessageOperationRequest<T> : OperationRequest<T, object>
    {
        public MessageOperationRequest(string usageId, TransportOperation<T, object> operation, T input) : base(usageId, operation, input) { }
    }

    public abstract class RequestOperation<T, R> : TransportOperation<T, R>
    {
        protected RequestOperation(string id, string verb, List<string> pathParameters = null, TransportOperationSettings settings = null)
            : base("request", id, verb, pathParameters, settings)
        {
        }

        protected RequestOperationHandler<T, R> CreateHandler(RequestOperation<T, R> instance, Func<T, TransportContext, System.Threading.Tasks.Task<Result<R>>> interceptor)
        {
            return new RequestOperationHandler<T, R>(instance, interceptor);
        }

        public RequestOperationHandler<T, R> Handle(Func<T, TransportContext, System.Threading.Tasks.Task<Result<R>>> interceptor)
        {
            return CreateHandler(this, interceptor);
        }
    }

    public abstract class MessageOperation<T> : TransportOperation<T, object>
    {
        protected MessageOperation(string id, string verb, List<string> pathParameters = null, TransportOperationSettings settings = null)
            : base("message", id, verb, pathParameters, settings)
        {
        }

        protected MessageOperationHandler<T> CreateHandler(MessageOperation<T> instance, Func<T, TransportContext, System.Threading.Tasks.Task<Result<object>>> interceptor)
        {
            // No need to wrap, just pass through
            return new MessageOperationHandler<T>(
                instance,
                interceptor
            );
        }

        public MessageOperationHandler<T> Handle(Func<T, TransportContext, System.Threading.Tasks.Task<Result<object>>> interceptor)
        {
            return CreateHandler(this, interceptor);
        }
    }
}

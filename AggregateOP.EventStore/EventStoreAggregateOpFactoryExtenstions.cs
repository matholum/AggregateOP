﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AggregateOP.Base;
using AggregateOP.EventStore.Deserializers;
using AggregateOP.EventStore.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AggregateOP.EventStore
{
    public static class EventStoreAggregateOpFactoryExtentions
    {
        public static void AddEventStore(this AggregateOPFactory factory)
        {
            var eventModelType = factory.EventModelType;

            var eventDeserializers = factory._assemblies
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IEvent).IsAssignableFrom(p) && p != typeof(IEvent))
                .ToDictionary(
                t => t.Name,
                t =>
                {
                    var deserializer = typeof(JsonEventDeserializer<>);
                    deserializer = deserializer.MakeGenericType(t);

                    var method = deserializer.GetMethod("Deserialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod);

                    IEvent factory(string json) => method.Invoke(null, new[] { json }).CastToReflected(t);

                    Func<string, object, long, long, EventModel> makeModel = (string json, object metadata, long position, long version) =>
                    {
                        var genericModel = eventModelType;
                        genericModel = genericModel.MakeGenericType(t);

                        var e = factory(json);

                        var model = Activator.CreateInstance(genericModel, e, metadata, position, version);

                        return model as EventModel;
                    };

                    return makeModel;
                });

            factory._services.AddSingleton<IEventStoreClient, EventStoreClient>();

            factory.AddEventRepository(sp =>
            {
                var store = sp.GetRequiredService<IEventStoreClient>();
                var eventStoreOptions = sp.GetRequiredService<IOptionsMonitor<EventStoreConfig>>();

                return new EventRepository(store, eventStoreOptions, eventDeserializers);
            });
        }
    }
}

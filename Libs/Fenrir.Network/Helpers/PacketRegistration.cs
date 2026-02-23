using System.Diagnostics;
using Fenrir.Network.Collections;
using Fenrir.Network.Framing;
using Fenrir.Network.Transport;

namespace Fenrir.Network.Helpers;

using System;
using System.Linq;
using System.Reflection;
using Fenrir.Network.Dispatcher;

public static class PacketRegistration
{
    // TODO: A way to define ID, MessageType, Size?
    private static Func<IClient, ValueTask> CreateHandlerDelegateWithNoPacket<TMessage>(MethodInfo methodInfo)
    {
        if (methodInfo == null)
            throw new ArgumentNullException(nameof(methodInfo));

        // Ensure the method has the correct signature
        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 1 || 
            parameters[0].ParameterType != typeof(IClient) || 
            methodInfo.ReturnType != typeof(ValueTask))
        {
            throw new ArgumentException($"Method {methodInfo.Name} does not match the required signature.");
        }

        // Create the delegate
        return (Func<IClient, ValueTask>)Delegate.CreateDelegate(typeof(Func<IClient, ValueTask>), methodInfo);
    }
    
    private static Func<IClient, TMessage, ValueTask> CreateHandlerDelegateWithPacket<TMessage>(MethodInfo methodInfo)
    {
        if (methodInfo == null)
            throw new ArgumentNullException(nameof(methodInfo));

        // Ensure the method has the correct signature
        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 2 || 
            parameters[0].ParameterType != typeof(IClient) || 
            !typeof(TMessage).IsAssignableFrom(parameters[1].ParameterType) || 
            methodInfo.ReturnType != typeof(ValueTask))
        {
            throw new ArgumentException($"Method {methodInfo.Name} does not match the required signature.");
        }

        // Create the delegate
        return (Func<IClient, TMessage, ValueTask>)Delegate.CreateDelegate(typeof(Func<IClient, TMessage, ValueTask>), methodInfo);
    }

    public static void RegisterPacketsToCollection(PacketCollection packetCollection, String packetNamespace)
    {
        var packetTypes = Assembly.GetEntryAssembly()
            .GetTypes()
            .Where(t => t.Namespace == packetNamespace && t.GetCustomAttribute<PacketAttribute>() != null && typeof(IPacket).IsAssignableFrom(t));

        // TODO: Logging would be appreciated.
        foreach (var packetType in packetTypes)
        {
            var packetAttribute = packetType.GetCustomAttribute<PacketAttribute>();
            if (packetAttribute is { PacketId: not null })
            {
                packetCollection.RegisterPacket(new PacketInfo(packetType));
            }
        }
    }
    
    // public static void RegisterPacketsToDispatcher<TPacketType, TMessage>(IMessageDispatcher<TPacketType, TMessage> dispatcher, String handlersNamespace, String packetNamespace)
    // {
    //     // TODO: Must handler be public and static?
    //     var packetHandlers = Assembly.GetEntryAssembly()
    //         .GetTypes()
    //         .Where(t => t.Namespace == handlersNamespace)
    //         .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
    //             .Where(m => m.GetCustomAttribute<PacketHandlerAttribute>() != null)
    //             .Select(m => new { Method = m, Attribute = m.GetCustomAttribute<PacketHandlerAttribute>() }));
    //     
    //     foreach (var compositePacketHandler in packetHandlers)
    //     {
    //         var packetHandlerMethod = compositePacketHandler.Method;
    //         var packetHandlerAttribute = compositePacketHandler.Attribute;
    //         
    //         // Get the PacketId from the PacketHandlerAttribute.
    //         Debug.Assert(packetHandlerMethod != null, nameof(packetHandlerMethod) + " != null");
    //         Debug.Assert(packetHandlerAttribute != null, nameof(packetHandlerAttribute) + " != null");
    //         
    //         // TODO: Check if packetHandlerMethod is Func<IClient, TMessage, ValueTask>
    //         
    //         // For a handler with no Packet just id.
    //         if (packetHandlerMethod.GetParameters().Length == 2)
    //         {
    //
    //             // We must get the id from the PacketHandlerAttribute.
    //             if (packetHandlerAttribute.PacketId != null)
    //             {
    //                 // Ensure that PacketId is a type of TPacketType.
    //                 // TODO: Throw exceptions.
    //                 if (packetHandlerAttribute.PacketId.GetType() != typeof(TPacketType))
    //                     continue;
    //
    //                 // TODO: Message Metadata?
    //                 // TODO: Server Context?
    //                 TPacketType packetId = (TPacketType)packetHandlerAttribute.PacketId;
    //                 var handlerDelegate = CreateHandlerDelegateWithNoPacket<TMessage>(packetHandlerMethod);
    //                 dispatcher.RegisterHandler(packetId, handlerDelegate);
    //             }
    //             else
    //             {
    //                 throw new ArgumentException("PacketId is required for this method signature.");
    //             }
    //         } else if (packetHandlerMethod.GetParameters().Length == 3)
    //         {
    //             // We must get the id from the PacketHandlerAttribute.
    //             if (packetHandlerAttribute.PacketId != null)
    //             {
    //                 // Ensure that PacketId is a type of TPacketType.
    //                 // TODO: Throw exceptions.
    //                 if (packetHandlerAttribute.PacketId.GetType() != typeof(TPacketType))
    //                     continue;
    //                 
    //                 TPacketType packetId2 = (TPacketType)packetHandlerAttribute.PacketId;
    //                 var handlerDelegate2 = CreateHandlerDelegateWithPacket<TMessage>(packetHandlerMethod);
    //                 dispatcher.RegisterHandler(packetId2, handlerDelegate2);
    //             }
    //             else
    //             {
    //                 throw new ArgumentException("PacketId is required for this method signature.");
    //             }
    //             // Get the first argument that is a subtype of TMessage.
    //             // var handlerDelegate2 = CreateHandlerDelegateWithPacket<TMessage>(packetHandlerMethod);
    //             // var packetType = handlerDelegate2.Method.GetParameters()[1].ParameterType;
    //             // var packetId2 = packetType.GetCustomAttribute<PacketAttribute>()?.PacketId;
    //             // if (packetId2 == null)
    //             // {
    //             //     // Instansiate.
    //             //     var packet = (IPacket)Activator.CreateInstance(packetType);
    //             //     packetId2 = packet.GetPacketType();
    //             // }
    //             // dispatcher.RegisterHandler(packetId2, handlerDelegate2);
    //         }
    //         else
    //         {
    //             throw new ArgumentException("Invalid method signature.");
    //         }
    //     }
    //     
    //     
    //     // var packetTypes = Assembly.GetExecutingAssembly()
    //     //     .GetTypes()
    //     //     .Where(t => t.Namespace == packetNamespace && t.GetCustomAttribute<PacketAttribute>() != null);
    //     //
    //     // foreach (var packetType in packetTypes)
    //     // {
    //     //     var packetAttribute = packetType.GetCustomAttribute<PacketAttribute>();
    //     //     if (packetAttribute != null)
    //     //     {
    //     //         var packetId = packetAttribute.PacketId;
    //     //         var handlerMethod = typeof(PacketRegistrar).GetMethod(nameof(HandlePacket), BindingFlags.NonPublic | BindingFlags.Static)
    //     //             ?.MakeGenericMethod(packetType);
    //     //
    //     //         if (handlerMethod != null)
    //     //         {
    //     //             var handlerDelegate = Delegate.CreateDelegate(typeof(Func<IClient, object, ValueTask>), handlerMethod);
    //     //             dispatcher.RegisterHandler(packetId, (Func<IClient, object, ValueTask>)handlerDelegate);
    //     //         }
    //     //     }
    //     // }
    // }
}

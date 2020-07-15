﻿using RockLib.Messaging.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RockLib.Messaging.CloudEvents
{
    /// <summary>
    /// Extension methods related to CloudEvents.
    /// </summary>
    public static partial class CloudEventExtensions
    {
        private static readonly ConcurrentDictionary<Type, CopyConstructor> _copyConstructors = new ConcurrentDictionary<Type, CopyConstructor>();
        private static readonly ConcurrentDictionary<Type, MessageConstructor> _messageConstructors = new ConcurrentDictionary<Type, MessageConstructor>();
        private static readonly ConcurrentDictionary<Type, ValidateMethod> _validateMethods = new ConcurrentDictionary<Type, ValidateMethod>();

        /// <summary>
        /// Creates a new instance of the <typeparamref name="TCloudEvent"/> type and copies all
        /// cloud event attributes except for <see cref="CloudEvent.Id"/> and <see cref=
        /// "CloudEvent.Time"/> to the new instance. Note that neither the source's <see cref=
        /// "CloudEvent.StringData"/>, nor its <see cref="CloudEvent.BinaryData"/>, nor any of its
        /// <see cref="CloudEvent.AdditionalAttributes"/> are copied to the new instance.
        /// <para>
        /// The <typeparamref name="TCloudEvent"/> type <em>must</em> define a public copy
        /// constructor - one with a single parameter of type <typeparamref name="TCloudEvent"/>.
        /// A <see cref="MissingMemberException"/> is immediately thrown if the class does not
        /// define such a constructor.
        /// </para>
        /// </summary>
        /// <typeparam name="TCloudEvent">
        /// The type of <see cref="CloudEvent"/> to create a copy of.
        /// </typeparam>
        /// <param name="cloudEvent">The <see cref="CloudEvent"/> to create a copy of.</param>
        /// <returns>A copy of the <paramref name="cloudEvent"/> parameter.</returns>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="cloudEvent"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="MissingMemberException">
        /// If the <typeparamref name="TCloudEvent"/> class does not define a public copy
        /// constructor - one with a single parameter of type <typeparamref name="TCloudEvent"/>.
        /// with the exact parameters <c>(<see cref="IReceiverMessage"/>, <see cref=
        /// "IProtocolBinding"/>)</c>.
        /// </exception>
        public static TCloudEvent Copy<TCloudEvent>(this TCloudEvent cloudEvent)
            where TCloudEvent : CloudEvent
        {
            if (cloudEvent is null)
                throw new ArgumentNullException(nameof(cloudEvent));

            var copyConstructor = _copyConstructors.GetOrAdd(typeof(TCloudEvent), CopyConstructor.Create)
                ?? throw MissingCopyConstructor(typeof(TCloudEvent));

            return (TCloudEvent)copyConstructor.Invoke(cloudEvent);
        }

        /// <summary>
        /// Creates an instance of <typeparamref name="TCloudEvent"/> with properties mapped from
        /// the headers of <paramref name="receiverMessage"/>.
        /// <para>
        /// The <typeparamref name="TCloudEvent"/> type <em>must</em> define a public constructor
        /// with the exact parameters <c>(<see cref="IReceiverMessage"/>, <see cref=
        /// "IProtocolBinding"/>)</c>. A <see cref="MissingMemberException"/> is immediately
        /// thrown if the class does not define such a constructor.
        /// </para>
        /// </summary>
        /// <typeparam name="TCloudEvent">The type of <see cref="CloudEvent"/> to create.</typeparam>
        /// <param name="receiverMessage">
        /// The <see cref="IReceiverMessage"/> to be mapped to the new <typeparamref name="TCloudEvent"/>.
        /// </param>
        /// <param name="protocolBinding">
        /// The <see cref="IProtocolBinding"/> used to map <see cref="IReceiverMessage"/> headers to
        /// CloudEvent attributes.
        /// </param>
        /// <returns>
        /// A new <typeparamref name="TCloudEvent"/> with properties mapped from the headers of the <see cref="IReceiverMessage"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="receiverMessage"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="MissingMemberException">
        /// If the <typeparamref name="TCloudEvent"/> class does not define a public constructor
        /// with the exact parameters <c>(<see cref="IReceiverMessage"/>, <see cref=
        /// "IProtocolBinding"/>)</c>.
        /// </exception>
        public static TCloudEvent To<TCloudEvent>(this IReceiverMessage receiverMessage, IProtocolBinding protocolBinding = null)
            where TCloudEvent : CloudEvent
        {
            if (receiverMessage is null)
                throw new ArgumentNullException(nameof(receiverMessage));

            var messageConstructor = _messageConstructors.GetOrAdd(typeof(TCloudEvent), MessageConstructor.Create)
                ?? throw MissingReceiverConstructor(typeof(TCloudEvent));

            return (TCloudEvent)messageConstructor.Invoke(receiverMessage, protocolBinding);
        }

        /// <summary>
        /// Start listening for CloudEvents and handle them using the specified callback function.
        /// <para>
        /// The <typeparamref name="TCloudEvent"/> type <em>must</em> define a constructor with the
        /// exact parameters <c>(<see cref="IReceiverMessage"/>, <see cref="IProtocolBinding"/>)
        /// </c>. A <see cref="MissingMemberException"/> is immediately thrown if the class does
        /// not define such a constructor.
        /// </para>
        /// </summary>
        /// <param name="receiver">The receiver to start.</param>
        /// <param name="onEventReceivedAsync">
        /// A function that is invoked when a CloudEvent is received.
        /// </param>
        /// <param name="protocolBinding">
        /// The <see cref="IProtocolBinding"/> used to map <see cref="IReceiverMessage"/> headers to
        /// CloudEvent attributes.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="receiver"/> or <paramref name="onEventReceivedAsync"/> is <see
        /// langword="null"/>.
        /// </exception>
        /// <exception cref="MissingMemberException">
        /// If the <typeparamref name="TCloudEvent"/> class does not define a public constructor
        /// with the exact parameters <c>(<see cref="IReceiverMessage"/>, <see cref=
        /// "IProtocolBinding"/>)</c>.
        /// </exception>
        public static void Start<TCloudEvent>(this IReceiver receiver,
            Func<TCloudEvent, IReceiverMessage, Task> onEventReceivedAsync, IProtocolBinding protocolBinding = null)
            where TCloudEvent : CloudEvent
        {
            if (receiver is null)
                throw new ArgumentNullException(nameof(receiver));
            if (onEventReceivedAsync is null)
                throw new ArgumentNullException(nameof(onEventReceivedAsync));

            if (!MessageConstructor.Exists(typeof(TCloudEvent)))
                throw MissingReceiverConstructor(typeof(TCloudEvent));

            receiver.Start(message => onEventReceivedAsync(message.To<TCloudEvent>(protocolBinding), message));
        }

        /// <summary>
        /// Adds a <see cref="ValidatingSender"/> decorator that ensures messages are valid
        /// CloudEvents.
        /// <para>
        /// The <typeparamref name="TCloudEvent"/> type <em>must</em> define a public static method
        /// named "Validate" with the exact parameters <c>(<see cref="SenderMessage"/>, <see cref=
        /// "IProtocolBinding"/>)</c>. A <see cref="MissingMemberException"/> is immediately thrown if
        /// the class does not define such a method.
        ///  </para>
        /// </summary>
        /// <typeparam name="TCloudEvent">The type of CloudEvent used to apply validation.</typeparam>
        /// <param name="builder">The <see cref="ISenderBuilder"/>.</param>
        /// <param name="protocolBinding">
        /// The <see cref="IProtocolBinding"/> used to map CloudEvent attributes to <see cref="SenderMessage"/>
        /// headers.
        /// </param>
        /// <returns>The same <see cref="ISenderBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="MissingMemberException">
        /// If the <typeparamref name="TCloudEvent"/> class does not define a public static method
        /// named "Validate" with the exact parameters <c>(<see cref="SenderMessage"/>, <see cref=
        /// "IProtocolBinding"/>)</c>.
        /// </exception>
        public static ISenderBuilder AddValidation<TCloudEvent>(this ISenderBuilder builder, IProtocolBinding protocolBinding = null)
            where TCloudEvent : CloudEvent
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            var validateMethod = _validateMethods.GetOrAdd(typeof(TCloudEvent), ValidateMethod.Create)
                ?? throw MissingValidateMethod(typeof(TCloudEvent));

            return builder.AddValidation(message => validateMethod.Invoke(message, protocolBinding));
        }

        private static MissingMemberException MissingReceiverConstructor(Type cloudEventType) =>
            new MissingMemberException($"CloudEvent type '{cloudEventType.Name}' must have a public constructor"
                + $" with the exact parameters ({nameof(IReceiverMessage)}, {nameof(IProtocolBinding)}).");

        private static MissingMemberException MissingCopyConstructor(Type cloudEventType) =>
            new MissingMemberException($"CloudEvent type '{cloudEventType.Name}' must have a public copy constructor" +
                $" (a constructor with a single parameter of type '{cloudEventType.Name}').");

        private static MissingMemberException MissingValidateMethod(Type cloudEventType) =>
            new MissingMemberException($"CloudEvent type '{cloudEventType.Name}' must have a public static method" +
                $" named '{nameof(CloudEvent.Validate)}' with the exact parameters ({nameof(SenderMessage)}, {nameof(IProtocolBinding)}).");
    }
}
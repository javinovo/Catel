﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SerializationContext.cs" company="Catel development team">
//   Copyright (c) 2008 - 2015 Catel development team. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Runtime.Serialization;
using Catel.Reflection;

namespace Catel.Runtime.Serialization
{
    using System;
    using System.Collections.Generic;
    using Catel.Scoping;

    /// <summary>
    /// The serialization context used to serialize and deserialize models.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    public class SerializationContext<TContext> : ISerializationContext<TContext>
        where TContext : class
    {
        private IDisposable _serializableToken;
        private ScopeManager<Stack<Type>> _typeStackScopeManager;
        private ScopeManager<ReferenceManager> _referenceManagerScopeManager;
        private int? _depth;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationContext{TContext}" /> class.
        /// </summary>
        /// <param name="model">The model, can be <c>null</c> for value types.</param>
        /// <param name="modelType">Type of the model.</param>
        /// <param name="context">The context.</param>
        /// <param name="contextMode">The context mode.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="modelType" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">The <paramref name="context" /> is <c>null</c>.</exception>
        public SerializationContext(object model, Type modelType, TContext context, SerializationContextMode contextMode)
        {
            Argument.IsNotNull("modelType", modelType);
            Argument.IsNotNull("context", context);

            Model = model;
            ModelType = modelType;
            ModelTypeName = modelType.GetSafeFullName(false);
            Context = context;
            ContextMode = contextMode;
            TypeStack = new Stack<Type>();

            var scopeName = SerializationContextHelper.GetSerializationReferenceManagerScopeName();

            _typeStackScopeManager = ScopeManager<Stack<Type>>.GetScopeManager(scopeName, () => new Stack<Type>());
            TypeStack = _typeStackScopeManager.ScopeObject;

            _referenceManagerScopeManager = ScopeManager<ReferenceManager>.GetScopeManager(scopeName);
            ReferenceManager = _referenceManagerScopeManager.ScopeObject;

            _serializableToken = CreateSerializableToken();
        }

        /// <summary>
        /// Gets or sets the model that needs serialization or deserialization.
        /// </summary>
        /// <value>The model.</value>
        /// <remarks>
        /// Only set the model if you know what you are doing. In most (99.9%), you want to serializer to take care of this.
        /// </remarks>
        public object Model { get; set; }

        /// <summary>
        /// Gets the type of the model.
        /// </summary>
        /// <value>The type of the model.</value>
        public Type ModelType { get; private set; }

        /// <summary>
        /// Gets the name of the model type, which should be a cached version of <c>ModelType.GetSafeFullName(false);</c>.
        /// </summary>
        /// <value>The name of the model type.</value>
        public string ModelTypeName { get; private set; }

        /// <summary>
        /// Gets the depth of the current element being processed.
        /// </summary>
        /// <value>The depth.</value>
        public int Depth
        {
            get
            {
                if (!_depth.HasValue)
                {
                    _depth = ReferenceManager.Count;
                }

                return _depth.Value;
            }
        }

        /// <summary>
        /// Gets the type stack inside the current scope.
        /// </summary>
        public Stack<Type> TypeStack { get; private set; } 

        /// <summary>
        /// Gets the context mode.
        /// </summary>
        /// <value>The context mode.</value>
        public SerializationContextMode ContextMode { get; private set; }

        /// <summary>
        /// Gets the context.
        /// </summary>
        /// <value>The context.</value>
        public TContext Context { get; private set; }

        /// <summary>
        /// Gets the reference manager.
        /// </summary>
        /// <value>The reference manager.</value>
        public ReferenceManager ReferenceManager { get; private set; }

#if NET
        /// <summary>
        /// Gets or sets the serialization information.
        /// </summary>
        /// <value>The serialization information.</value>
        public SerializationInfo SerializationInfo { get; set; }
#endif

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_typeStackScopeManager != null)
            {
                _typeStackScopeManager.Dispose();
                _typeStackScopeManager = null;
            }

            if (_referenceManagerScopeManager != null)
            {
                _referenceManagerScopeManager.Dispose();
                _referenceManagerScopeManager = null;
            }

            if (_serializableToken != null)
            {
                _serializableToken.Dispose();
                _serializableToken = null;
            }
        }

        private IDisposable CreateSerializableToken()
        {
            return new DisposableToken<SerializationContext<TContext>>(this,
                x =>
                {
                    x.Instance.TypeStack.Push(x.Instance.ModelType);

                    var serializable = x.Instance.Model as ISerializable;
                    if (serializable != null)
                    {
                        switch ((SerializationContextMode)x.Tag)
                        {
                            case SerializationContextMode.Serialization:
                                serializable.StartSerialization();
                                break;

                            case SerializationContextMode.Deserialization:
                                serializable.StartDeserialization();
                                break;

                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                },
                x =>
                {
                    var serializable = x.Instance.Model as ISerializable;
                    if (serializable != null)
                    {
                        switch ((SerializationContextMode)x.Tag)
                        {
                            case SerializationContextMode.Serialization:
                                serializable.FinishSerialization();
                                break;

                            case SerializationContextMode.Deserialization:
                                serializable.FinishDeserialization();
                                break;

                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    x.Instance.TypeStack.Pop();
                }, ContextMode);
        }
    }
}
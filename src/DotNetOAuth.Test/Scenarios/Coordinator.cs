﻿//-----------------------------------------------------------------------
// <copyright file="Coordinator.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOAuth.Test.Scenarios {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using DotNetOAuth.Messaging;
	using DotNetOAuth.ChannelElements;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	/// <summary>
	/// Runs a Consumer and Service Provider simultaneously so they can interact in a full simulation.
	/// </summary>
	internal class Coordinator {
		Actor consumerAction;
		Actor serviceProviderAction;

		/// <summary>Initializes a new instance of the <see cref="Coordinator"/> class.</summary>
		/// <param name="consumerAction">The code path of the Consumer.</param>
		/// <param name="serviceProviderAction">The code path of the Service Provider.</param>
		internal Coordinator(Actor consumerAction, Actor serviceProviderAction) {
			if (consumerAction == null) {
				throw new ArgumentNullException("consumerAction");
			}
			if (serviceProviderAction == null) {
				throw new ArgumentNullException("serviceProviderAction");
			}

			this.consumerAction = consumerAction;
			this.serviceProviderAction = serviceProviderAction;
		}

		/// <summary>
		/// Gets or sets the signing element the Consumer channel should use.
		/// </summary>
		/// <remarks>
		/// The Service Provider never signs a message, so no property is necessary for that.
		/// </remarks>
		internal SigningBindingElementBase SigningElement { get; set; }

		internal delegate void Actor(OAuthChannel channel);

		/// <summary>
		/// Starts the simulation.
		/// </summary>
		internal void Start() {
			if (SigningElement == null) {
				throw new InvalidOperationException("SigningElement must be set first.");
			}

			// Prepare channels that will pass messages directly back and forth.
			CoordinatingOAuthChannel consumerChannel = new CoordinatingOAuthChannel(SigningElement);
			CoordinatingOAuthChannel serviceProviderChannel = new CoordinatingOAuthChannel(SigningElement);
			consumerChannel.RemoteChannel = serviceProviderChannel;
			serviceProviderChannel.RemoteChannel = consumerChannel;

			Thread consumerThread = null, serviceProviderThread = null;
			Exception failingException = null;

			Action<Actor, OAuthChannel> safeWrapper = (actor, channel) => {
				try {
					actor(channel);
				} catch (Exception ex) {
					// We may be the second thread in an ThreadAbortException, so check the "flag"
					if (failingException == null) {
						failingException = ex;
						if (Thread.CurrentThread == consumerThread) {
							serviceProviderThread.Abort();
						} else {
							consumerThread.Abort();
						}
					}
				}
			};

			consumerThread = new Thread(() => { safeWrapper(consumerAction, consumerChannel); });
			serviceProviderThread = new Thread(() => { safeWrapper(serviceProviderAction, serviceProviderChannel); });
			consumerThread.Start();
			serviceProviderThread.Start();
			consumerThread.Join();
			serviceProviderThread.Join();

			if (failingException != null) {
				throw new AssertFailedException("Coordinator thread threw unhandled exception: " + failingException, failingException);
			}
		}
	}
}

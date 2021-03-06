﻿using CsWebChat.WpfClient.ChatModule.Events;
using CsWebChat.WpfClient.ChatModule.Models;
using CsWebChat.WpfClient.ChatModule.Views;
using CsWebChat.WpfClient.Regions;
using Microsoft.AspNetCore.SignalR.Client;
using Prism.Events;
using Prism.Logging;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace CsWebChat.WpfClient.ChatModule.ViewModels
{
    class ChatSplitViewModel : BindableBase, INavigationAware, IRegionMemberLifetime
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerFacade _logger;
        private readonly HubConnection _connection;

        private bool _tryConnecting = true;
        private TimeSpan _reconnectTime = TimeSpan.FromSeconds(10);

        public ChatSplitViewModel(IRegionManager regionManager, IEventAggregator eventAggregator,
            ILoggerFacade logger, HubConnection connection)
        {
            if (regionManager == null || eventAggregator == null
                || logger == null || connection == null)
                throw new ArgumentException();

            this._regionManager = regionManager;
            this._eventAggregator = eventAggregator;
            this._logger = logger;
            this._connection = connection;

            // Set as RegionContext in order to make the connection available to all 
            // other hostet child views.
            this._regionManager.Regions[MainWindowRegionNames.MAIN_REGION].Context = connection;

            this._connection.Closed += this.WebsocketConnectionLost;
            this._connection.On("PongAsync", this.HandleResponse);
            this._connection.On<Message>("ReceiveMessageAsync", this.HandleReceiveMessage);
        }

        private async Task WebsocketConnectionLost(Exception exception)
        {
            this._eventAggregator.GetEvent<WebsocketConnectionStateEvent>()
                .Publish(WebSocketState.Closed);
            await this.TryConnectingToWebsocket();
        }

        private void HandleResponse()
        {
            this._tryConnecting = false;
            this._eventAggregator.GetEvent<WebsocketConnectionStateEvent>().Publish(WebSocketState.Open);
        }

        private void HandleReceiveMessage(Message message)
        {
            this._eventAggregator.GetEvent<MessageReceivedEvent>()
                .Publish(message);
        }

        private async Task TryConnectingToWebsocket()
        {
            this._tryConnecting = true;

            while(this._tryConnecting)
            {
                try
                {
                    await this._connection.StartAsync()
                        .ContinueWith((t) => { this._connection.InvokeAsync("Ping"); });
                    await Task.Delay(this._reconnectTime);
                }
                catch
                {
                    this._eventAggregator.GetEvent<WebsocketConnectionStateEvent>()
                        .Publish(WebSocketState.Closed);
                }
            }
        }


        // IRegionMemberLifetime:
        public bool KeepAlive
        {
            get { return false; }
        }


        // INavigationAware:
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Task.Run(async () => { await this.TryConnectingToWebsocket(); });
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            // Always return false in order to force a new instantiation.
            // -> Injecting a new HubConnection is necessary since the address might
            // change.
            return false;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Task.Run(async () => 
            {
                await this._connection.StopAsync();
                this._connection.Closed -= this.WebsocketConnectionLost;
                this._tryConnecting = false;
            });
        }
    }
}

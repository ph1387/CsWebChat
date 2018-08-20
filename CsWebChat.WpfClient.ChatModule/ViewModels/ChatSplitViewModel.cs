﻿using CsWebChat.WpfClient.ChatModule.Views;
using CsWebChat.WpfClient.Events;
using CsWebChat.WpfClient.Regions;
using Prism.Events;
using Prism.Logging;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsWebChat.WpfClient.ChatModule.ViewModels
{
    class ChatSplitViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerFacade _logger;

        public ChatSplitViewModel(IRegionManager regionManager, IEventAggregator eventAggregator,
            ILoggerFacade logger)
        {
            if (regionManager == null || eventAggregator == null
                || logger == null)
                throw new ArgumentException();

            this._regionManager = regionManager;
            this._eventAggregator = eventAggregator;
            this._logger = logger;

            this._eventAggregator.GetEvent<LoginSuccessEvent>()
                .Subscribe(this.SwitchToTabView, ThreadOption.UIThread);
        }

        private void SwitchToTabView()
        {
            this._regionManager.RequestNavigate(MainWindowRegionNames.MAIN_REGION, 
                new Uri(nameof(ChatSplitView), UriKind.Relative));
        }
    }
}

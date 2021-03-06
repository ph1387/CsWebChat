﻿using CsWebChat.WpfClient.LoginModule.Events;
using CsWebChat.WpfClient.LoginModule.Models;
using CsWebChat.WpfClient.LoginModule.Services;
using CsWebChat.WpfClient.LoginModule.Views;
using CsWebChat.WpfClient.Regions;
using CsWebChat.WpfClient.WebLogicModule.Models;
using Microsoft.Practices.Unity;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Logging;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace CsWebChat.WpfClient.LoginModule.ViewModels
{
    class RegisterViewModel : BindableBase
    {
        // Header for TabControl display.
        private readonly string _header = "Register";
        public string Header
        {
            get { return _header; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
            set { SetProperty<string>(ref _name, value); }
        }

        private readonly int _minPasswordLength = 6;
        private readonly int _maxPasswordLength = 20;
        public string PasswordLengthError
        {
            get
            {
                return String.Format("Password must contain {0} to {1} characters.",
                    this._minPasswordLength,
                    this._maxPasswordLength);
            }
        }

        private bool _passwordLengthOk;
        public bool PasswordLengthOk
        {
            get { return _passwordLengthOk; }
            set { SetProperty<bool>(ref _passwordLengthOk, value); }
        }

        public SecureString Password { get; set; }
        public List<string> ServerAddresses { get { return this._addressStorage.Servers; } }

        private string _selectedServerAddress;
        public string SelectedServerAddress
        {
            get { return _selectedServerAddress; }
            set
            {
                SetProperty<string>(ref _selectedServerAddress, value);
                this._addressStorage.ServerAddress = value;
            }
        }

        private ObservableCollection<string> _errorMessages = new ObservableCollection<string>();
        public ObservableCollection<string> ErrorMessages
        {
            get { return _errorMessages; }
            set { SetProperty<ObservableCollection<string>>(ref _errorMessages, value); }
        }

        private ObservableCollection<string> _successMesssages = new ObservableCollection<string>();
        public ObservableCollection<string> SuccessMessages
        {
            get { return _successMesssages; }
            set { SetProperty<ObservableCollection<string>>(ref _successMesssages, value); }
        }

        private bool _enableProgressRing;
        public bool EnableProgressRing
        {
            get { return _enableProgressRing; }
            set { SetProperty<bool>(ref _enableProgressRing, value); }
        }

        public ICommand ButtonRegister { get; set; }
        public ICommand PasswordChangedCommand { get; set; }

        private readonly IUnityContainer _container;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerFacade _logger;
        private readonly IRegionManager _regionManager;
        private readonly AuthenticationService _authenticationService;
        private readonly AddressStorage _addressStorage;
        private readonly PasswordHashService _passwordHashService;

        public RegisterViewModel(IUnityContainer container, IEventAggregator eventAggregator,
            ILoggerFacade logger, IRegionManager regionManager,
            AuthenticationService authenticationService, AddressStorage addressStorage,
            PasswordHashService passwordHashService)
        {
            if (container == null || eventAggregator == null
                || logger == null || regionManager == null
                || authenticationService == null || addressStorage == null
                || passwordHashService == null)
                throw new ArgumentException();

            this._container = container;
            this._eventAggregator = eventAggregator;
            this._logger = logger;
            this._regionManager = regionManager;
            this._authenticationService = authenticationService;
            this._addressStorage = addressStorage;
            this._passwordHashService = passwordHashService;

            // Trigger the getter once in order to force the view to load 
            // any existing selected addresses.
            SelectedServerAddress = this._addressStorage.ServerAddress;

            // Ensure that the visible servers are updated.
            this._eventAggregator.GetEvent<ServerAddedEvent>()
                .Subscribe(() => { RaisePropertyChanged(nameof(ServerAddresses)); }, ThreadOption.UIThread);
            // Ensure that existing information are removed when logging in.
            this._eventAggregator.GetEvent<LoginEvent>()
                .Subscribe(this.RemoveLoginInformation, ThreadOption.UIThread, false, (b) => { return b; });

            ButtonRegister = new DelegateCommand(async () => { await this.ButtonRegisterClicked(); });
            PasswordChangedCommand = new DelegateCommand<PasswordBox>(async (box) => { await this.PasswordChangedFired(box); });
        }

        private void RemoveLoginInformation(bool result)
        {
            var view = this._regionManager.Regions[LoginModuleRegionNames.TAB_REGION]
                .Views
                .OfType<RegisterView>()
                .SingleOrDefault();
            var passwordBox = view?.PasswordBoxRegister;

            if(passwordBox != null)
            {
                passwordBox.SecurePassword.Dispose();
                passwordBox.Password = null;
            }

            Password?.Dispose();
            Password = null;
            Name = null;
        }

        private async Task ButtonRegisterClicked()
        {
            ErrorMessages.Clear();
            SuccessMessages.Clear();

            try
            {
                if (!String.IsNullOrEmpty(SelectedServerAddress) && Uri.IsWellFormedUriString(SelectedServerAddress, UriKind.Absolute))
                {
                    EnableProgressRing = true;

                    var user = new User()
                    {
                        Name = Name,
                        Password = this._passwordHashService.HashSecureString(Password)
                    };
                    var registerResult = await this._authenticationService.RegisterUser(user);

                    if (registerResult.Success)
                    {
                        SuccessMessages.Add("Registration successful.");

                        this.RemoveLoginInformation(true);
                    }
                    else
                    {
                        ErrorMessages.Add(String.Format("Registration unsuccessful. Reason: {0}", registerResult.StatusCode));

                        // Show the response of the server to the user.
                        if (registerResult.Response != null)
                        {
                            var outputMap = "Field: {0}, Value: {1}";

                            if (!String.IsNullOrEmpty(registerResult.Response.Name))
                                ErrorMessages.Add(String.Format(outputMap, nameof(Name), registerResult.Response.Name));
                            if (!String.IsNullOrEmpty(registerResult.Response.Password))
                                ErrorMessages.Add(String.Format(outputMap, nameof(Password), registerResult.Response.Password));
                        }
                    }
                }
                else
                {
                    ErrorMessages.Add("Please use a valid URL.");
                }
            }
            catch (HttpRequestException)
            {
                ErrorMessages.Add("Server could not be reached.");
            }
            finally
            {
                EnableProgressRing = false;
            }
        }

        private async Task PasswordChangedFired(PasswordBox box)
        {
            var passwordLength = box.SecurePassword.Length;

            Password = box.SecurePassword;
            PasswordLengthOk = passwordLength >= this._minPasswordLength && passwordLength <= this._maxPasswordLength;
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gauniv.Client.Pages;
using Gauniv.Client.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gauniv.Client.ViewModel
{
    public partial class MenuViewModel : ObservableObject
    {
        [RelayCommand]
        public void GoToProfile() => NavigationService.Instance.Navigate<Profile>([]);

        [ObservableProperty]
        private bool isConnected = NetworkService.Instance.Token != null;

        public MenuViewModel()
        {
            NetworkService.Instance.OnConnected += Instance_OnConnected;
        }

        private void Instance_OnConnected()
        {
            IsConnected = true;
        }
    }
}

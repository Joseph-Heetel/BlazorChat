using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using BlazorChat.Client;
using BlazorChat.Client.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using BlazorChat.Shared;

namespace BlazorChat.Client.Components
{

    public partial class Avatar : IDisposable
    {
        private string _imageUrl = "";
        private string _colorCode = "";
        private string _initials = "";
        private string _initialsStyle = "";
        private bool _onlineBadge = false;
        private string _size = "";
        private ItemId _avatarId;

        [Parameter]
        public AvatarParams Params { get; set; } = default;
        [Parameter]
        public Size Size { get; set; } = Size.Small;

        [Parameter]
        public string Style { get; set; } = "";


        protected override void OnParametersSet()
        {
            _colorCode = "";
            _initials = "?";
            _onlineBadge = false;
            if (!_avatarId.IsZero && Params.User?.Avatar?.Id != _avatarId)
            {
                MediaResolver.Unsubscribe(_avatarId, AvatarUrlChanged);
                _imageUrl = "";
                _avatarId = new ItemId();
            }

            if (!Params.UserId.IsZero)
            {
                if (Params.User != null)
                {
                    _onlineBadge = Params.User.Online && Params.DisplayOnlineState;
                    if (Params.User.Avatar != null && Params.User.Avatar.Id != _avatarId)
                    {
                        _avatarId = Params.User.Avatar.Id;
                        _imageUrl = MediaResolver.GetAndSubscribe(Params.UserId, Params.User.Avatar, AvatarUrlChanged);
                    }
                    if (!string.IsNullOrEmpty(Params.User.Name))
                    {
                        _initials = "";
                        string[] sections = Params.User.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < Math.Min(sections.Length, 2); i++)
                        {
                            _initials += sections[i].First();
                        }
                    }
                }
                byte[] colorBytes = new byte[3];
                for (int i = 0; i < Params.UserId.Value!.Length; i++)
                {
                    colorBytes[i % 3] = (byte)(colorBytes[i % 3] ^ Params.UserId.Value[i]!);
                }
                _colorCode = $"background-color: #{Convert.ToHexString(colorBytes)}";
                {
                    float r = (float)(colorBytes[0]) / 255f;
                    float g = (float)(colorBytes[1]) / 255f;
                    float b = (float)(colorBytes[2]) / 255f;
                    float brigthness = MathF.Sqrt(0.299f * r*r + 0.587f * g*g + 0.114f * b*b);
                    if (brigthness > 0.5)
                    {
                        _initialsStyle = "color: #111";
                    }
                    else
                    {
                        _initialsStyle = "color: #DDD";
                    }
                }
                switch (Size)
                {
                    case Size.Small:
                        _size = "small";
                        break;
                    case Size.Medium:
                        _size = "medium";
                        break;
                    case Size.Large:
                        _size = "large";
                        break;
                }
            }
        }
        private void AvatarUrlChanged(string url)
        {
            _imageUrl = url;
            this.StateHasChanged();
        }

        public void Dispose()
        {
            MediaResolver.Unsubscribe(_avatarId, AvatarUrlChanged);
        }

    }
}
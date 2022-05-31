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
using BlazorChat.Client.Services;
using BlazorChat.Shared;

namespace BlazorChat.Client.Components.Chat
{
    public partial class ImageViewDialog
    {
        [CascadingParameter]
        public MudDialogInstance? DialogInstance { get; set; }

        [Parameter]
        public ItemId DomainId { get; set; }
        [Parameter]
        public FileAttachment? Attachment { get; set; }

        private ItemId _fileId;
        private string _url = "";
        private string _title = "";
        private string _sizeInfo = "";

        protected override void OnInitialized()
        {
            _title = "";
            _sizeInfo = "? B";

            if (!_fileId.IsZero && Attachment?.Id != _fileId)
            {
                _mediaService.Unsubscribe(_fileId, ImageUrlChanged);
                _url = "";
                _fileId = new ItemId();
            }

            if (!DomainId.IsZero && Attachment != null && !Attachment.Id.IsZero && FileHelper.IsImageMime(Attachment.MimeType))
            {
                _fileId = Attachment.Id;
                _url = _mediaService.GetAndSubscribe(DomainId, Attachment, ImageUrlChanged);
            }

            if (Attachment != null)
            {
                _title = Attachment.FileName();
                _sizeInfo = FileHelper.MakeHumanReadableFileSize(Attachment.Size);
            }
        }

        private void ImageUrlChanged(string url)
        {
            _url = url;
            this.StateHasChanged();
        }
    }
}